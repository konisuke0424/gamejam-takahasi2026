using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace KaiForest
{
    /// <summary>
    /// Scripted smoke test for a player build: run with -autotest [-autotestOut dir].
    /// Takes screenshots of the key views, checks the core rules (walk, AI movement, calls,
    /// chase, reunion, capture) and writes results.json, then quits.
    /// </summary>
    public class AutoTest : MonoBehaviour
    {
        public static AutoTest Instance { get; private set; }

        string outDir;
        readonly List<string> results = new List<string>();
        int failures;

        void Awake()
        {
            Instance = this;
            GameManager.IgnoreMouse = true;
            DontDestroyOnLoad(gameObject);
            outDir = ArgValue("-autotestOut") ?? Path.Combine(Application.persistentDataPath, "autotest");
            Directory.CreateDirectory(outDir);
            StartCoroutine(Run());
        }

        static string ArgValue(string key)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == key) return args[i + 1];
            return null;
        }

        void Check(string name, bool ok, string detail = "")
        {
            if (!ok) failures++;
            string line = $"{(ok ? "PASS" : "FAIL")} {name} {detail}";
            results.Add(line);
            Debug.Log("[AutoTest] " + line);
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(outDir, name + ".png"));
            yield return null;
            yield return null;
        }

        static GameManager GM => GameManager.I;

        /// <summary>
        /// The checks below are for the mother stage (hunter + mother), so jump there; they follow one
        /// hunter, and every other monster (traps' too) is stopped so it can't interfere.
        /// </summary>
        static void HaltExtraMonsters()
        {
            GM.Director.DebugJumpTo(Stage.Mother);
            foreach (var m in GM.Monsters)
                if (m != GM.Monster) m.Halt();
        }

        static IEnumerator WaitReady()
        {
            float t = 0f;
            while ((GM == null || !GM.Ready) && t < 20f) { t += Time.unscaledDeltaTime; yield return null; }
        }

        IEnumerator Run()
        {
            yield return WaitReady();
            yield return new WaitForSeconds(1.0f);
            Check("forest scene loaded", GM.Map.TreeCount > 1500 && GM.Map.exit != null, $"trees={GM.Map.TreeCount} exit={GM.Map.exit != null}");
            yield return Shot("01_title");

            GM.BeginGame();
            yield return new WaitForSeconds(2.2f);
            yield return Shot("02_intro");
            GM.SkipIntro();
            HaltExtraMonsters();
            yield return new WaitForSeconds(1.0f);
            yield return Shot("03_start");

            // Frame time at the start view.
            float t0 = Time.realtimeSinceStartup; int frames = 0;
            while (Time.realtimeSinceStartup - t0 < 3f) { frames++; yield return null; }
            float fps = frames / (Time.realtimeSinceStartup - t0);
            Check("fps at start", fps > 30f, $"fps={fps:0.0}");

            // Walk forward with the debug input.
            var p0 = GM.Player.transform.position;
            GM.Player.DebugWalk = 1f;
            yield return new WaitForSeconds(3f);
            GM.Player.DebugWalk = 0f;
            float walked = Vector3.Distance(p0, GM.Player.transform.position);
            Check("player walks", walked > 3f, $"dist={walked:0.0}");
            yield return Shot("04_walk");

            // Look at a landmark.
            if (GM.Map.Landmarks.Length > 0)
            {
                var lm = GM.Map.Landmarks[0];
                var lp = lm.transform.position;
                var from = GM.Map.FindOpenPoint(lp + new Vector3(0f, 0f, -4.5f), 1.5f);
                GM.Player.Teleport(from, 0f);
                GM.Player.FaceTowards(lp + Vector3.up * 0.6f);
                yield return new WaitForSeconds(0.5f);
                yield return Shot("05_landmark_" + lm.type);
            }

            // AI movement and voices over a few seconds.
            var mp = GM.Mother.transform.position;
            var xp = GM.Monster.transform.position;
            GM.Player.Teleport(GM.Map.StartPosition, 0f);
            yield return new WaitForSeconds(8f);
            Check("mother moves", Vector3.Distance(mp, GM.Mother.transform.position) > 1f, $"d={Vector3.Distance(mp, GM.Mother.transform.position):0.0}");
            Check("monster moves", Vector3.Distance(xp, GM.Monster.transform.position) > 1f, $"d={Vector3.Distance(xp, GM.Monster.transform.position):0.0}");
            Check("mother calls", GM.Mother.Voice.PlayCount > 0, $"calls={GM.Mother.Voice.PlayCount}");
            Check("monster calls", GM.Monster.Voice.PlayCount > 0, $"calls={GM.Monster.Voice.PlayCount}");

            // Meet the monster: screenshot, then it should start chasing.
            GM.Monster.Frozen = true;
            var mPos = GM.Monster.transform.position;
            var dir = (GM.Map.StartPosition - mPos); dir.y = 0f; dir.Normalize();
            var near = mPos + dir * 6f;
            GM.Player.Teleport(near, 0f);
            GM.Player.FaceTowards(GM.Monster.Rig.head.position - Vector3.up * 0.8f);
            GM.Monster.transform.rotation = Quaternion.LookRotation(dir);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("06_monster");
            GM.Player.Teleport(mPos + dir * 4f, Mathf.Atan2(-dir.x, -dir.z) * Mathf.Rad2Deg);
            GM.Monster.Frozen = false;
            float chaseWait = 0f;
            while (GM.Monster.State != MonsterState.Chase && chaseWait < 3f) { chaseWait += Time.deltaTime; yield return null; }
            Check("monster chases when close", GM.Monster.State == MonsterState.Chase, $"state={GM.Monster.State}");

            // A frozen monster stays in Chase, so stamina should keep draining (10 s from full to empty).
            GM.Monster.Frozen = true;
            float st0 = GM.Stamina.Stamina;
            yield return new WaitForSeconds(2f);
            Check("stamina drains while chased", GM.Stamina.Stamina < st0 - 0.1f, $"stamina {st0:0.00} -> {GM.Stamina.Stamina:0.00}");

            // Get away and find the mother.
            GM.Monster.Teleport(GM.Map.StartPosition + new Vector3(60f, 0f, 0f));
            var mom = GM.Mother.transform.position;
            var away = GM.Map.FindOpenPoint(mom + (GM.Map.StartPosition - mom).normalized * 6f, 1f);
            GM.Player.Teleport(away, 0f);
            GM.Player.FaceTowards(GM.Mother.Rig.head.position);
            GM.Mother.Active = false;
            yield return new WaitForSeconds(0.6f);
            yield return Shot("07_mother");
            GM.Mother.Active = true;

            GM.Player.Teleport(GM.Mother.transform.position + (away - GM.Mother.transform.position).normalized * 1.5f, 0f);
            yield return new WaitForSeconds(0.5f);
            Check("reunion clears", GM.State == GameState.Clear, $"state={GM.State}");
            yield return new WaitForSeconds(1.0f);
            yield return Shot("08_reunion");
            float waitEnd = 0f;
            while (!GM.CanRestart && waitEnd < 12f) { waitEnd += Time.deltaTime; yield return null; }
            yield return Shot("09_clear");

            // Retry, then get caught.
            GM.Restart();
            yield return new WaitForSeconds(0.3f);
            yield return WaitReady();
            yield return new WaitForSeconds(0.5f);
            Check("retry goes straight to play", GM.State == GameState.Playing, $"state={GM.State}");
            GM.SkipIntro();
            HaltExtraMonsters();
            yield return new WaitForSeconds(0.5f);
            var mp2 = GM.Monster.transform.position;
            GM.Player.Teleport(mp2 + new Vector3(0f, 0f, 1.0f), 180f);
            yield return new WaitForSeconds(0.35f);
            Check("capture is game over", GM.State == GameState.Caught, $"state={GM.State}");
            yield return Shot("10_jumpscare");
            waitEnd = 0f;
            while (!GM.CanRestart && waitEnd < 8f) { waitEnd += Time.deltaTime; yield return null; }
            yield return Shot("11_gameover");

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"failures\": {failures},");
            sb.AppendLine("  \"results\": [");
            for (int i = 0; i < results.Count; i++)
                sb.AppendLine($"    \"{results[i].Replace("\"", "'")}\"{(i < results.Count - 1 ? "," : "")}");
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            File.WriteAllText(Path.Combine(outDir, "results.json"), sb.ToString());
            Debug.Log($"[AutoTest] done, failures={failures}");
            yield return new WaitForSeconds(0.5f);
            Application.Quit(failures == 0 ? 0 : 1);
        }
    }
}
