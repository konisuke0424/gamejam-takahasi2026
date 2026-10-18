using UnityEngine;
using UnityEngine.InputSystem;

namespace KaiForest
{
    /// <summary>The three stages, in order.</summary>
    public enum Stage
    {
        /// <summary>Only mother's voice, coming from her handkerchief. No monsters, no fakes.</summary>
        Handkerchief,
        /// <summary>The fakes' traps wake up (deer traps, trap monsters); her voice comes from her kanzashi.</summary>
        Kanzashi,
        /// <summary>Mother appears and calls; monsterCount monsters hunt the whole forest. Reaching her clears.</summary>
        Mother,
    }

    /// <summary>
    /// Runs the stages and checkpoints. Picking up an item (click within its reach) moves to the next
    /// stage and is a checkpoint: after a capture, the retry starts there, where it was picked up.
    /// Reaching mother clears the checkpoint. If nothing at all is placed in Forest.unity, the default
    /// set is put at the default spots (CheckpointLayout); if only some is, a stage whose item is
    /// missing is skipped.
    /// </summary>
    public class StageDirector : MonoBehaviour
    {
        class Checkpoint
        {
            public Stage stage;
            public Vector3 position;
            public float yaw, playTime;
        }

        /// <summary>Kept across the scene reload of a retry.</summary>
        static Checkpoint saved;

        // Static fields survive entering Play mode again when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => saved = null;

        public static bool HasCheckpoint => saved != null;
        public static float SavedPlayTime => saved != null ? saved.playTime : 0f;
        public static void ClearCheckpoint() => saved = null;

        public Stage Stage { get; private set; }

        /// <summary>The item to find now (null in the mother stage).</summary>
        public CheckpointItem Current => Stage < Stage.Mother ? ItemFor(Stage) : null;

        GameManager gm;
        CheckpointItem[] items;
        DeerTrap[] deerTraps;
        MonsterTrap[] monsterTraps;
        bool trapsArmed, huntersOut;

        public void Init(GameManager g)
        {
            gm = g;
            items = g.Map.Items;
            deerTraps = g.Map.DeerTraps;
            monsterTraps = g.Map.MonsterTraps;
            if (items.Length == 0 && deerTraps.Length == 0 && monsterTraps.Length == 0)
            {
                // Nothing placed in Forest.unity yet: play with the default set at the default spots.
                var root = CheckpointLayout.PlaceDefaults(g.Map, g.mats);
                items = root.GetComponentsInChildren<CheckpointItem>();
                deerTraps = root.GetComponentsInChildren<DeerTrap>();
                monsterTraps = root.GetComponentsInChildren<MonsterTrap>();
                if (Application.isEditor)
                    Debug.Log("[KaiForest] No checkpoints / traps placed in Forest.unity: using the default spots. " +
                              "KaiForest > Map > Place Checkpoints and Traps puts them in the scene so they can be moved.");
            }
            foreach (var it in items) it.Init(g.audioBank);
            foreach (var t in deerTraps) t.Init(g.audioBank, g.Player);

            Stage = SkipMissing(saved != null ? saved.stage : Stage.Handkerchief);
            if (saved != null) g.Player.Teleport(saved.position, saved.yaw);
            ShowStageObjects();
        }

        CheckpointItem ItemFor(Stage s)
        {
            if (s >= Stage.Mother) return null;
            var kind = s == Stage.Handkerchief ? ItemKind.Handkerchief : ItemKind.Kanzashi;
            foreach (var it in items)
                if (it.kind == kind) return it;
            return null;
        }

        Stage SkipMissing(Stage s)
        {
            while (s < Stage.Mother && ItemFor(s) == null) s++;
            return s;
        }

        /// <summary>Only the current item is in the forest; mother only in her stage.</summary>
        void ShowStageObjects()
        {
            var current = Current;
            foreach (var it in items) it.SetShown(it == current && !it.Taken);
            gm.Mother.gameObject.SetActive(Stage == Stage.Mother);
        }

        /// <summary>Play starts (or restarts at the checkpoint).</summary>
        public void Begin() => BeginStage();

        void BeginStage()
        {
            ShowStageObjects();
            var item = Current;
            if (item != null) item.StartCalling();
            if (Stage >= Stage.Kanzashi) ArmTraps();
            if (Stage == Stage.Mother)
            {
                ReleaseHunters();
                gm.Mother.Activate();
            }
        }

        void ArmTraps()
        {
            if (trapsArmed) return;
            trapsArmed = true;
            for (int i = 0; i < deerTraps.Length; i++) deerTraps[i].Arm(3f + i * 2.5f);
            for (int i = 0; i < monsterTraps.Length; i++)
            {
                var t = monsterTraps[i];
                var m = gm.SpawnMonster($"TrapMonster{i + 1}", gm.Map.FindOpenPoint(t.Center, t.areaRadius * 0.5f));
                m.SetTerritory(t.Center, t.areaRadius);
                m.Activate();
            }
        }

        void ReleaseHunters()
        {
            if (huntersOut) return;
            huntersOut = true;
            for (int i = 0; i < gm.monsterCount; i++)
                gm.SpawnMonster($"Monster{i + 1}", gm.PickHunterSpawn()).Activate();
        }

        void Update()
        {
            if (gm == null) return;
            var item = gm.State == GameState.Playing ? Current : null;
            bool near = item != null && item.InReach(gm.Player.transform.position);
            gm.ShowPickupHint(near ? $"クリックで　{item.displayName}を　拾う" : null);
            if (near && Clicked()) PickUp(item);
        }

        static bool Clicked()
        {
            var mouse = GameManager.IgnoreMouse ? null : Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
        }

        void PickUp(CheckpointItem item)
        {
            item.Take();
            gm.Player.HoldUntilRelease(); // the click shouldn't also walk him forward
            gm.OnPickedUp(item);
            Stage = SkipMissing(Stage + 1);
            saved = new Checkpoint
            {
                stage = Stage,
                position = gm.Player.transform.position,
                yaw = gm.Player.Yaw,
                playTime = gm.PlayTime,
            };
            BeginStage();
        }

        /// <summary>Caught or reunited: the items and traps fall silent.</summary>
        public void StopAll()
        {
            foreach (var it in items) it.StopCalling();
            foreach (var t in deerTraps) t.Disarm();
        }

        /// <summary>Debug display: the nearest deer trap from p, its distance and state.</summary>
        public string NearestDeerTrap(Vector3 p)
        {
            DeerTrap best = null;
            float bestD = float.MaxValue;
            foreach (var t in deerTraps)
            {
                var d = t.transform.position - p;
                d.y = 0f;
                if (d.magnitude < bestD) { bestD = d.magnitude; best = t; }
            }
            return best == null ? "none" : $"{bestD:0.0}m (r {best.triggerRadius:0.#}m) {best.StateLabel}";
        }

        /// <summary>Dev (F2) / AutoTest: moves on without picking anything up (not a checkpoint).</summary>
        public void DebugJumpTo(Stage s)
        {
            while (Stage < s)
            {
                var it = Current;
                if (it != null) it.Take();
                Stage = SkipMissing(Stage + 1);
            }
            BeginStage();
        }

        public void DebugAdvance()
        {
            if (Stage < Stage.Mother) DebugJumpTo(Stage + 1);
        }
    }
}
