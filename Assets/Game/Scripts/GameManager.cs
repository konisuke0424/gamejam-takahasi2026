using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KaiForest
{
    public enum GameState { Title, Intro, Playing, Clear, Caught }

    /// <summary>
    /// Loads the map (Forest.unity, placed by hand in the editor), spawns the boy, mother and monsters
    /// on its markers, builds the UI, and drives the title → intro → play → ending flow.
    /// Main.unity only holds this component and the post-process volume.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public const string SceneName = "Main";
        const string ClearedPlayerPrefsKey = "KaiForest.GameCleared";
        static Texture2D goldMedalTexture;
        static Texture2D grayMedalTexture;

        public static GameManager I { get; private set; }
        /// <summary>Set before reloading the scene so a retry skips the title screen.</summary>
        public static bool SkipTitle;
        /// <summary>Set by AutoTest so a stray real click can't skip screens or move the boy mid-test.</summary>
        public static bool IgnoreMouse;

        [Header("Assets (wired by KaiForestSetup)")]
        public MaterialLibrary mats;
        public AudioBank audioBank;
        public Font uiFont;
        public Texture2D titleTexture;
        public Texture2D clearTexture;
        public Texture2D gameOverTexture;
        public Volume volume;

        /// <summary>Night fog: about half the scene is gone at 10 m, nearly all of it at 20 m.</summary>
        public const float FogDensity = 0.085f;

        [Header("Monsters")]
        [Tooltip("かんざしを拾ったあとに現れる、森じゅうでカイを追ってくる化け物の数（トラップの化け物は別）")]
        public int monsterCount = 1;
        [Tooltip("化け物どうしが最初にこれ以上離れて現れる（m）")]
        public float monsterSpacing = 25f;
        [Tooltip("化け物はカイ（かんざしを拾った場所）からこれ以上離れて現れる（m）。小さいほど早く出会う")]
        public float monsterMinFromStart = 35f;
        [Tooltip("化け物はお母さんの待つ場所からこれ以上離れて現れる（m）")]
        public float monsterMinFromMother = 25f;
        [Tooltip("カイからお母さんへの道すじの左右この幅（m）に化け物を出す。0 にするとマップ全体から選ぶ")]
        public float monsterRouteWidth = 35f;
        [Tooltip("化け物ごとに最初の呼び声をずらす（秒）。同じ声が一斉に聞こえないように")]
        public float monsterCallStagger = 4.5f;

        [Header("Capture scare")]
        [Tooltip("捕まったとき、顔のドアップを見せる時間（秒）。このあと暗転する")]
        public float scareDuration = 1.6f;
        [Tooltip("ドアップの視野角（度）。小さいほど顔が大きく映る（通常は 68）")]
        public float scareFov = 30f;
        [Tooltip("ドアップのときのカメラと顔の距離（m）")]
        public float scareFaceDistance = 0.55f;
        [Tooltip("ドアップのカメラ位置のずらし（m）。カイから見て x: 右＋／左－、y: 上＋／下－。顔は常に画面の中心に来る")]
        public Vector2 scareViewOffset = new Vector2(-0.18f, -0.15f);
        [Tooltip("画面の中心に来る点のずらし（m）。x: 右＋／左－、y: 上＋／下－、z: 奥＋／手前－。顔の中心からずれて見えるときに直す")]
        public Vector3 scareAimOffset = new Vector3(0.03f, -0.03f, 0f);
        [Tooltip("ドアップ中のランタンの明るさ（通常を 1 とした倍率）。ランタンは画面の外へ落ち、顔を下から照らす")]
        public float scareLanternLight = 0.5f;

        [Header("Night look (applied at runtime)")]
        public Color fogColor = new Color(0.012f, 0.016f, 0.024f);
        public Color ambientColor = new Color(0.05f, 0.055f, 0.075f);

        public GameState State { get; private set; } = GameState.Title;
        /// <summary>True once the map is loaded and the actors exist.</summary>
        public bool Ready { get; private set; }
        public ForestMap Map { get; private set; }
        public PlayerController Player { get; private set; }
        public MotherAI Mother { get; private set; }
        /// <summary>Every monster: the traps' (from the kanzashi stage) and the hunters (mother stage).</summary>
        public List<MonsterAI> Monsters { get; } = new List<MonsterAI>();
        /// <summary>The first hunter, else the first monster (AutoTest follows this one).</summary>
        public MonsterAI Monster => Monsters.Find(m => !m.HasTerritory) ?? (Monsters.Count > 0 ? Monsters[0] : null);
        public StageDirector Director { get; private set; }
        public AmbienceController Ambience { get; private set; }
        public PlayerStamina Stamina { get; private set; }
        public float PlayTime { get; private set; }
        public bool CanRestart { get; private set; }

        Canvas canvas;
        Image fade;
        CanvasGroup titleGroup, endGroup, diaryGroup, hintGroup, messageGroup, deerGroup;
        Text endSub, endHint, titleHint, debugText, messageText;
        RawImage endImage, deerImage, diaryImage;
        AspectRatioFitter diaryFit;
        RectTransform resultBlock;
        RawImage resultTitle;
        Text resultTitleText, resultTime, resultHint;
        CanvasGroup resultTitleGroup, resultTimeGroup;
        /// <summary>Width of the "ゲームクリア" headline on the ending page (canvas units, before Result Scale).</summary>
        const float ResultTitleWidth = 760f;
        float clearStartedAt;
        StorySettings storySettings;
        Coroutine messageRoutine, deerRoutine;
        Vignette vignette;
        ChromaticAberration chroma;
        float baseVignette, baseChroma;
        bool debugHud, openingLine;
        Coroutine introRoutine;
        LanternSettings viewSettings;

        void Awake()
        {
            I = this;
            viewSettings = LanternSettings.Load();
            storySettings = StorySettings.Load();
            ApplyRenderSettings();
            BuildMoon();
            BuildUI();
            if (volume != null)
            {
                var p = volume.profile; // runtime copy, safe to animate
                if (p.TryGet(out vignette)) baseVignette = vignette.intensity.value;
                if (p.TryGet(out chroma)) baseChroma = chroma.intensity.value;
            }

            debugHud = HasArg("-debughud");
            if (HasArg("-autotest") && AutoTest.Instance == null)
                new GameObject("AutoTest").AddComponent<AutoTest>();
        }

        IEnumerator Start()
        {
            // The map lives in its own scene; it is already loaded when Forest.unity was the one played.
            if (ForestMap.Instance == null)
            {
                var op = SceneManager.LoadSceneAsync(ForestMap.SceneName, LoadSceneMode.Additive);
                while (op != null && !op.isDone) yield return null;
            }
            Map = ForestMap.Instance;
            if (Map == null)
            {
                Debug.LogError("[KaiForest] Forest scene not found. Add Assets/Scenes/Forest.unity to the build (KaiForest > Setup Project).");
                yield break;
            }
            Physics.SyncTransforms();
            SpawnActors();
            Ready = true;

            if (SkipTitle) introRoutine = StartCoroutine(IntroRoutine(true));
            else ShowTitle();
        }

        void SpawnActors()
        {
            var pgo = new GameObject("Player") { layer = 2 };
            Player = pgo.AddComponent<PlayerController>();
            Player.Init(mats, audioBank.steps);
            Player.Teleport(Map.StartPosition, Map.StartYaw);

            // Mother waits at her marker (Forest.unity), or in front of the real torii if there is none.
            var spawn = Map.MotherSpawn;
            var home = spawn != null ? spawn.Home : Map.exit != null ? Map.exit.MotherHome : Map.ExitPosition;
            var mgo = new GameObject("Mother") { layer = 2 };
            Mother = mgo.AddComponent<MotherAI>();
            Mother.Init(Map, Player, audioBank, mats, home);
            // She stands exactly on her marker unless it is set to wander.
            Mother.stayPut = spawn == null || !spawn.wander;
            if (spawn != null) Mother.wanderRadius = spawn.wanderRadius;
            else if (Map.exit != null) Mother.wanderRadius = Map.exit.wanderRadius;
            Mother.Teleport(Mother.stayPut ? home : Map.FindOpenPoint(home, 3f));

            // Monsters come later, stage by stage (StageDirector).
            Stamina = pgo.AddComponent<PlayerStamina>();
            Stamina.Init(Monsters);
            pgo.AddComponent<BoyVoice>().Init(Map); // his own lines (start, outer wall, fork, voices heard)

            Ambience = gameObject.AddComponent<AmbienceController>();
            Ambience.Init(audioBank, Player, Monsters, Stamina);

            backdrop = Backdrop.Create(Player.Cam.transform);

            Director = gameObject.AddComponent<StageDirector>();
            Director.Init(this);
        }

        /// <summary>Creates a monster (not yet active) at the given spot and adds it to Monsters.</summary>
        public MonsterAI SpawnMonster(string name, Vector3 position)
        {
            var go = new GameObject(name) { layer = 2 };
            var monster = go.AddComponent<MonsterAI>();
            monster.firstCallDelay += Monsters.Count * monsterCallStagger;
            monster.Init(Map, Player, audioBank, mats);
            monster.Teleport(position);
            Monsters.Add(monster);
            return monster;
        }

        /// <summary>Where a hunter appears: between the boy and his mother, away from both.</summary>
        public Vector3 PickHunterSpawn() => PickMonsterSpawn(Player.transform.position, Mother.Home);

        public static bool HasArg(string a)
        {
            foreach (var s in System.Environment.GetCommandLineArgs())
                if (s == a) return true;
            return false;
        }

        // ------------------------------------------------------------------ setup

        static readonly Color MoonColor = new Color(0.55f, 0.65f, 1f);
        static readonly Color SunColor = new Color(1f, 0.96f, 0.88f);
        static readonly Color DayFogColor = new Color(0.62f, 0.68f, 0.74f);
        static readonly Color DayAmbientColor = new Color(0.55f, 0.57f, 0.6f);
        Light moon;
        Backdrop backdrop;

        void ApplyRenderSettings()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.skybox = null;
            RenderSettings.reflectionIntensity = 0f;
            ApplyLook();
        }

        /// <summary>
        /// The night look, blended toward daylight by LanternSettings.daylight (editor-only debug).
        /// Runs every frame so fog density and the daylight slider can be tuned during Play.
        /// </summary>
        void ApplyLook()
        {
            float day = viewSettings.EffectiveDaylight;
            RenderSettings.fogDensity = Mathf.Lerp(FogDensity, 0.004f, day);
            RenderSettings.fogColor = Color.Lerp(fogColor, DayFogColor, day);
            RenderSettings.ambientLight = Color.Lerp(ambientColor, DayAmbientColor, day);
            if (moon != null)
            {
                moon.intensity = Mathf.Lerp(0.06f, 1.3f, day);
                moon.color = Color.Lerp(MoonColor, SunColor, day);
            }
            if (Player != null && Player.Cam != null)
            {
                Player.Cam.backgroundColor = RenderSettings.fogColor;
                Player.Cam.farClipPlane = Mathf.Lerp(40f, 300f, day);
            }
            if (backdrop != null) backdrop.Daylight = day;
        }

        void BuildMoon()
        {
            moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = MoonColor;
            moon.intensity = 0.06f;
            moon.shadows = LightShadows.None;
            moon.transform.rotation = Quaternion.Euler(55f, -40f, 0f);
        }

        Vector3 PickMonsterSpawn(Vector3 from, Vector3 motherHome)
        {
            // On the way from the boy to his mother (within monsterRouteWidth of that line), away from
            // both ends and from the other monsters. If there's no room, the spacing rule is dropped
            // first, then the route rule.
            var start = new Vector3(from.x, 0f, from.z);
            var route = motherHome - start;
            var side = Vector3.Cross(Vector3.up, route.normalized);
            float h = Map.halfSize - 25f;
            for (int i = 0; i < 600; i++)
            {
                var p = monsterRouteWidth > 0f && i < 450
                    ? start + route * Random.value + side * Random.Range(-monsterRouteWidth, monsterRouteWidth)
                    : new Vector3(Random.Range(-h, h), 0f, Random.Range(-h, h));
                if (Mathf.Abs(p.x) > h || Mathf.Abs(p.z) > h) continue;
                if (Vector3.Distance(p, start) < monsterMinFromStart) continue;
                if (Vector3.Distance(p, motherHome) < monsterMinFromMother) continue;
                if (i < 300 && Monsters.Exists(m => Vector3.Distance(p, m.transform.position) < monsterSpacing)) continue;
                if (!Map.IsOpen(p)) continue;
                return p;
            }
            return Map.FindOpenPoint((start + motherHome) * 0.5f, 20f);
        }

        // ------------------------------------------------------------------ flow

        void ShowTitle()
        {
            State = GameState.Title;
            fade.color = new Color(0f, 0f, 0f, 0.72f);
            titleGroup.alpha = 1f;
        }

        public void BeginGame()
        {
            if (State != GameState.Title) return;
            openingLine = true; // "I have to find mother" once play starts (not on retries)
            introRoutine = StartCoroutine(IntroRoutine(false));
        }

        IEnumerator IntroRoutine(bool retry)
        {
            State = GameState.Intro;
            if (retry)
            {
                // Retry: no story, straight back into the forest.
                titleGroup.alpha = 0f;
                fade.color = Color.black;
                StartPlaying();
                yield return FadeImage(0f, 0.8f);
                yield break;
            }
            // The title dissolves into the diary page, which stays a few seconds and then dissolves
            // straight into the forest.
            var s = storySettings;
            if (s.introImage != null)
            {
                ShowDiary(s.introImage);
                yield return CrossFade(titleGroup, diaryGroup, s.titleToIntroSeconds);
                fade.color = new Color(0f, 0f, 0f, 0f); // hidden behind the page now
                yield return new WaitForSeconds(s.introSeconds);
            }
            else yield return Fade(titleGroup, 0f, 0.6f);
            StartPlaying();
            yield return IntoPlay(s.introToGameSeconds);
        }

        /// <summary>Dissolves whatever covers the forest (the diary page, the dark title veil) away, then shows the controls hint.</summary>
        IEnumerator IntoPlay(float seconds)
        {
            float veil = fade.color.a, page = diaryGroup.alpha;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                fade.color = new Color(0f, 0f, 0f, Mathf.Lerp(veil, 0f, k));
                diaryGroup.alpha = Mathf.Lerp(page, 0f, k);
                yield return null;
            }
            fade.color = new Color(0f, 0f, 0f, 0f);
            diaryGroup.alpha = 0f;
            hintGroup.alpha = 1f;
            yield return new WaitForSeconds(14f);
            yield return Fade(hintGroup, 0f, 2f);
        }

        /// <summary>A click during the opening diary page jumps straight into play.</summary>
        void SkipStory()
        {
            if (introRoutine != null) StopCoroutine(introRoutine);
            titleGroup.alpha = 0f;
            StartPlaying();
            introRoutine = StartCoroutine(IntoPlay(0.6f));
        }

        /// <summary>Puts a diary page up (alpha is left to the caller); withResult adds the clear result on it.</summary>
        void ShowDiary(Texture2D page, bool withResult = false)
        {
            diaryImage.texture = page;
            diaryImage.enabled = page != null;
            diaryFit.aspectRatio = page != null ? (float)page.width / page.height : 16f / 9f;
            resultBlock.gameObject.SetActive(withResult);
            if (!withResult) return;
            var at = storySettings.resultPosition;
            resultBlock.anchorMin = resultBlock.anchorMax = new Vector2(at.x, 1f - at.y);
            resultBlock.anchoredPosition = Vector2.zero;
            resultBlock.localScale = Vector3.one * storySettings.resultScale;
            // Headline sized to its picture; the time sits just under it, the click hint under that.
            var head = storySettings.clearTitleImage;
            resultTitle.texture = head;
            resultTitle.enabled = head != null;
            resultTitleText.enabled = head == null;
            float th = head != null ? ResultTitleWidth * head.height / head.width : 160f;
            var tr = resultTitle.rectTransform;
            tr.pivot = new Vector2(0.5f, 0.5f);
            tr.sizeDelta = new Vector2(ResultTitleWidth, th);
            tr.anchoredPosition = new Vector2(ResultTitleWidth * 0.5f, -th * 0.5f);
            tr.localScale = Vector3.one;
            resultTitle.color = Color.white;
            resultTime.rectTransform.anchoredPosition = new Vector2(ResultTitleWidth * 0.04f, -th + 12f);
            resultHint.rectTransform.anchoredPosition = new Vector2(ResultTitleWidth * 0.04f, -th - 52f);
            int sec = Mathf.FloorToInt(PlayTime);
            resultTime.text = $"クリア時間　{sec / 60}分{sec % 60:00}秒";
            resultTitleGroup.alpha = 0f;
            resultTimeGroup.alpha = 0f;
            resultHint.enabled = false;
        }

        /// <summary>
        /// The headline bursts in (scaling down from big, with a chime) partway through the page's
        /// fade, then the clear time fades in under it.
        /// </summary>
        IEnumerator RevealResult(float delay)
        {
            yield return new WaitForSeconds(delay);
            Ambience.PlayOneShot(audioBank.clearChime, 0.6f);
            var tr = resultTitle.rectTransform;
            const float pop = 0.8f;
            for (float t = 0f; t < pop; t += Time.deltaTime)
            {
                float k = 1f - Mathf.Pow(1f - t / pop, 3f);
                tr.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, k);
                resultTitleGroup.alpha = Mathf.Clamp01(t / (pop * 0.5f));
                yield return null;
            }
            tr.localScale = Vector3.one;
            resultTitleGroup.alpha = 1f;
            yield return new WaitForSeconds(0.25f);
            yield return Fade(resultTimeGroup, 1f, 0.6f);
        }

        IEnumerator CrossFade(CanvasGroup from, CanvasGroup to, float seconds)
        {
            float a = from.alpha, b = to.alpha;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                from.alpha = Mathf.Lerp(a, 0f, t / seconds);
                to.alpha = Mathf.Lerp(b, 1f, t / seconds);
                yield return null;
            }
            from.alpha = 0f;
            to.alpha = 1f;
        }

        void StartPlaying()
        {
            if (State == GameState.Playing) return;
            State = GameState.Playing;
            PlayTime = StageDirector.SavedPlayTime; // a retry from a checkpoint keeps counting
            Player.ControlEnabled = true;
            LockCursor(true);
            Director.Begin(); // wakes the current stage: its voice, traps, monsters, mother
            if (openingLine && BoyVoice.Instance != null) StartCoroutine(OpeningLineRoutine());
            openingLine = false;
        }

        /// <summary>"I have to find mother": he stays where he is, unable to move or look round, until he has said it.</summary>
        IEnumerator OpeningLineRoutine()
        {
            Player.ControlEnabled = false;
            float until = Time.time + BoyVoice.Instance.SayStart() + 0.2f;
            while (Time.time < until && State == GameState.Playing) yield return null;
            if (State == GameState.Playing) Player.ControlEnabled = true;
        }

        /// <summary>
        /// Locks and hides the cursor so mouse movement turns the view without clicking.
        /// Browsers only grant the lock on a user click (Unity applies it at the next one),
        /// and Esc releases it; a click during play locks it again (see Update).
        /// </summary>
        static void LockCursor(bool on)
        {
            if (IgnoreMouse) return; // AutoTest: don't grab the desktop cursor
            Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !on;
        }

        /// <summary>AutoTest/dev shortcut: jump straight into play from the title or intro.</summary>
        public void SkipIntro()
        {
            if (introRoutine != null) StopCoroutine(introRoutine);
            titleGroup.alpha = 0f;
            diaryGroup.alpha = 0f;
            fade.color = new Color(0f, 0f, 0f, 0f);
            openingLine = false; // no waiting on the opening line here
            StartPlaying();
        }

        public void OnReunite()
        {
            if (State != GameState.Playing) return;
            State = GameState.Clear;
            clearStartedAt = Time.time;
            PlayerPrefs.SetInt(ClearedPlayerPrefsKey, 1);
            PlayerPrefs.Save();
            Player.ControlEnabled = false;
            LockCursor(false);
            foreach (var m in Monsters) m.Halt();
            Director.StopAll();
            StageDirector.ClearCheckpoint(); // the next game starts from the beginning
            if (BoyVoice.Instance != null) BoyVoice.Instance.Stop();
            Mother.SayFound();
            Ambience.FadeTo(0.3f, 3f);
            Ambience.PlayOneShot(audioBank.clearChime, 0.8f);
            StartCoroutine(ClearRoutine());
        }

        IEnumerator ClearRoutine()
        {
            for (float t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                var face = Mother.Rig.head.position;
                Mother.FaceTowards(Player.transform.position);
                Player.SetLookOverride(Quaternion.LookRotation(face - Player.HeadPosition), Mathf.SmoothStep(0f, 1f, t / 1.2f));
                Player.LanternScale = Mathf.Lerp(1f, 0.12f, t / 0.8f);
                yield return null;
            }
            // Then the forest cross-fades into the diary page of the two of them, with the result in its
            // dark upper-left corner: that page is the ending. A click on it goes back to the title.
            var s = storySettings;
            yield return new WaitForSeconds(s.clearDelaySeconds);
            ShowDiary(s.clearImage, true);
            var reveal = StartCoroutine(RevealResult(s.clearFadeSeconds * 0.6f));
            yield return Fade(diaryGroup, 1f, s.clearFadeSeconds);
            yield return reveal;
            FinishEnding();
        }

        /// <summary>Seconds from meeting mother until the ending page and its result are fully up.</summary>
        float EndingLength => 1.5f + storySettings.clearDelaySeconds + Mathf.Max(storySettings.clearFadeSeconds, storySettings.clearFadeSeconds * 0.6f + 1.65f);

        /// <summary>The ending page and result are up: from now a click returns to the title.</summary>
        void FinishEnding()
        {
            if (!resultBlock.gameObject.activeSelf) ShowDiary(storySettings.clearImage, true);
            diaryGroup.alpha = 1f;
            resultTitleGroup.alpha = 1f;
            resultTimeGroup.alpha = 1f;
            resultTitle.rectTransform.localScale = Vector3.one;
            resultHint.enabled = true;
            CanRestart = true;
        }

        /// <summary>After the ending: back to the title screen, for a new game from the start.</summary>
        public void ReturnToTitle()
        {
            SkipTitle = false;
            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);
        }

        public void OnCaught(MonsterAI who)
        {
            if (State != GameState.Playing) return;
            State = GameState.Caught;
            Player.ControlEnabled = false;
            LockCursor(false);
            Mother.Active = false;
            Mother.Voice.Stop();
            foreach (var m in Monsters) m.Halt();
            Director.StopAll();
            if (BoyVoice.Instance != null) BoyVoice.Instance.Stop();
            Ambience.FadeTo(0f, 0.4f);
            Ambience.PlayOneShot(audioBank.catchLine.near, 1f);
            Ambience.PlayOneShot(audioBank.gameOverSting, 0.85f);
            StartCoroutine(CaughtRoutine(who));
        }

        IEnumerator CaughtRoutine(MonsterAI who)
        {
            // It stands over the boy and folds down to him, and the view is dragged into its face until
            // the face fills the screen. The lantern drops out of his hand and out of view; from down
            // there, dimmed, it lights the face from below instead of washing it out.
            var fwd = Player.transform.forward;
            var toM = who.transform.position - Player.transform.position; toM.y = 0f;
            if (toM.sqrMagnitude > 0.01f) fwd = toM.normalized;
            who.Teleport(Player.transform.position + fwd * 2.55f);
            who.transform.rotation = Quaternion.LookRotation(-fwd);
            var eye = Player.HeadPosition;
            for (float t = 0f; t < scareDuration; t += Time.deltaTime)
            {
                who.ScareLean = Mathf.Lerp(0f, 54f, Mathf.SmoothStep(0f, 1f, t / 0.3f));
                Player.LanternDrop = Mathf.Clamp01(t / 0.35f);
                Player.LanternScale = Mathf.Lerp(1f, scareLanternLight, Mathf.Clamp01(t / 0.25f));

                // Slow, then all at once: the view rushes in and stays right in front of the face, shaking.
                float push = Mathf.Clamp01((t - 0.06f) / 0.32f);
                push *= push;
                // Offsets are in the view's terms: looking back along the face's own forward.
                var view = -who.FaceForward(eye);
                var right = Vector3.Cross(Vector3.up, view);
                right = right.sqrMagnitude > 1e-4f ? right.normalized : Player.transform.right;
                var up = Vector3.Cross(view, right);
                var aim = who.Rig.head.position + right * scareAimOffset.x + up * scareAimOffset.y + view * scareAimOffset.z;
                var close = aim - view * scareFaceDistance + right * scareViewOffset.x + up * scareViewOffset.y;
                var shake = Random.insideUnitSphere * Mathf.Lerp(0.004f, 0.01f, push);
                var camPos = Vector3.Lerp(eye, close, push) + shake;
                var roll = Quaternion.Euler(0f, 0f, push * (7f + Mathf.Sin(t * 41f) * 2f));
                Player.SetLookOverride(Quaternion.LookRotation(aim - camPos) * roll, Mathf.Clamp01(t / 0.08f));
                Player.SetViewOverride(camPos, push, scareFov);
                if (vignette != null) vignette.intensity.value = Mathf.Lerp(baseVignette, 0.55f, push);
                if (chroma != null) chroma.intensity.value = 1f;
                yield return null;
            }
            fade.color = Color.black;
            CanRestart = true; // the scare has played; a click retries right away
            endImage.texture = gameOverTexture;
            endImage.SetNativeSize();
            endImage.rectTransform.sizeDelta *= 0.62f;
            endSub.text = "あれは、おかあさんじゃ　なかった。";
            endHint.text = StageDirector.HasCheckpoint ? "クリックで　つづきから" : "クリックで　もういちど";
            endHint.enabled = true;
            yield return new WaitForSeconds(0.3f);
            yield return Fade(endGroup, 1f, 0.8f);
        }

        public void Restart()
        {
            SkipTitle = true;
            // Single-mode load drops the map too; the new GameManager loads it again.
            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);
        }

        void Update()
        {
            ApplyLook(); // fog / daylight are live-tunable in LanternSettings
            if (!Ready) return;
            var mouse = IgnoreMouse ? null : Mouse.current;
            bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;
            switch (State)
            {
                case GameState.Title:
                    if (click) BeginGame();
                    if (titleHint != null) titleHint.color = new Color(0.85f, 0.8f, 0.75f, 0.45f + 0.45f * Mathf.Sin(Time.time * 2.4f));
                    break;
                case GameState.Intro:
                    if (click) SkipStory();
                    break;
                case GameState.Playing:
                    PlayTime += Time.deltaTime;
                    // Esc releases the lock; the next click takes it back.
                    if (click && Cursor.lockState != CursorLockMode.Locked) LockCursor(true);
                    break;
                case GameState.Clear:
                    // Also by the clock, in case the ending coroutine was cut short (e.g. a recompile during Play).
                    if (!CanRestart && Time.time >= clearStartedAt + EndingLength + 0.5f) FinishEnding();
                    if (CanRestart)
                    {
                        resultHint.color = new Color(0.85f, 0.8f, 0.74f, 0.45f + 0.45f * Mathf.Sin(Time.time * 2.4f));
                        float glow = 0.9f + 0.1f * Mathf.Sin(Time.time * 1.6f); // the headline breathes softly
                        resultTitle.color = new Color(glow, glow, glow, 1f);
                    }
                    if (click && CanRestart) ReturnToTitle();
                    break;
                case GameState.Caught:
                    if (click && CanRestart) Restart();
                    break;
            }

            if (State == GameState.Playing)
            {
                bool chase = MonsterAI.AnyChasing(Monsters);
                var nearest = MonsterAI.Nearest(Monsters);
                float k = Mathf.Clamp01(Mathf.InverseLerp(20f, 3f, nearest != null ? nearest.DistanceToPlayer : 999f));
                if (vignette != null) vignette.intensity.value = Mathf.MoveTowards(vignette.intensity.value, baseVignette + (chase ? 0.12f : 0f) + k * 0.08f, Time.deltaTime * 0.5f);
                if (chroma != null) chroma.intensity.value = Mathf.MoveTowards(chroma.intensity.value, baseChroma + (chase ? 0.45f : 0f) + k * 0.3f, Time.deltaTime);
            }

            var kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame) debugHud = !debugHud;
            // Dev only: F2 skips to the next stage (not a checkpoint).
            if (kb != null && kb.f2Key.wasPressedThisFrame && State == GameState.Playing && (Application.isEditor || Debug.isDebugBuild))
                Director.DebugAdvance();
            // Dev only: N goes straight to the ending's doorstep (mother stage, 4 m in front of her).
            if (kb != null && kb.nKey.wasPressedThisFrame && State == GameState.Playing && (Application.isEditor || Debug.isDebugBuild))
                DebugMeetMother();
            debugText.enabled = debugHud;
            if (debugHud) debugText.text = DebugString();
        }

        /// <summary>Dev: puts the boy 4 m in front of mother in the mother stage; one step and it's the ending.</summary>
        public void DebugMeetMother()
        {
            var home = Mother.Home;
            var away = Player.transform.position - home;
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.back;
            var spot = home + away * 4f;
            // Teleport first so the hunter appears far from where he now stands.
            Player.Teleport(spot, Mathf.Atan2(-away.x, -away.z) * Mathf.Rad2Deg);
            Director.DebugJumpTo(Stage.Mother);
        }

        public string DebugString()
        {
            float dm = Vector3.Distance(Player.transform.position, Mother.transform.position);
            var pp = Player.transform.position;
            var monsters = new System.Text.StringBuilder();
            for (int i = 0; i < Monsters.Count; i++)
            {
                var m = Monsters[i];
                monsters.Append($"monster{i + 1} {m.DistanceToPlayer:0.0}m  {m.State}  calls {m.Voice.PlayCount}  last {m.Voice.LastLineId}\n");
            }
            var item = Director.Current;
            string target = item != null ? $"{item.displayName} {Vector3.Distance(pp, item.transform.position):0.0}m" : "mother";
            return $"state {State}  t {PlayTime:0.0}s  fps {1f / Mathf.Max(Time.smoothDeltaTime, 0.0001f):0}  kai ({pp.x:0.0}, {pp.z:0.0}) yaw {Player.Yaw:0}\n" +
                   $"stage {Director.Stage}  find {target}  checkpoint {StageDirector.HasCheckpoint}\n" +
                   $"deer trap {Director.NearestDeerTrap(pp)}\n" +
                   $"mother {dm:0.0}m  calls {Mother.Voice.PlayCount}  last {Mother.Voice.LastLineId}  noticed {Mother.Noticed}\n" +
                   monsters +
                   $"stamina {Stamina.Stamina * 100f:0}%  exhaustion {Stamina.Exhaustion:0.00}  hearing {Hearing.Volume:0.00}\n" +
                   $"exit {Map.ExitPosition}  trees {Map.TreeCount}";
        }

        // ------------------------------------------------------------------ UI

        /// <summary>A line of text that fades in, stays a while and fades out.</summary>
        public void ShowMessage(string text, float hold = 3.5f)
        {
            if (messageRoutine != null) StopCoroutine(messageRoutine);
            messageRoutine = StartCoroutine(MessageRoutine(text, hold));
        }

        IEnumerator MessageRoutine(string text, float hold)
        {
            messageText.text = text;
            yield return Fade(messageGroup, 1f, 0.6f);
            yield return new WaitForSeconds(hold);
            yield return Fade(messageGroup, 0f, 1.2f);
        }

        /// <summary>A checkpoint item was picked up (StageDirector has already moved on).</summary>
        public void OnPickedUp(CheckpointItem item)
        {
            Ambience.PlayOneShot(audioBank.clearChime, 0.4f);
            if (!string.IsNullOrEmpty(item.message)) ShowMessage(item.message);
        }

        /// <summary>Deer trap: a deer leaps at the screen with a shriek. Only a scare; play goes on.</summary>
        public void PlayDeerScare(Texture2D image)
        {
            if (image == null) return;
            if (deerRoutine != null) StopCoroutine(deerRoutine);
            deerRoutine = StartCoroutine(DeerScareRoutine(image));
        }

        IEnumerator DeerScareRoutine(Texture2D image)
        {
            const float length = 1.3f, fadeFrom = 0.85f;
            deerImage.texture = image;
            var rt = deerImage.rectTransform;
            Ambience.PlayOneShot(audioBank.gameOverSting, 1f);
            for (float t = 0f; t < length; t += Time.deltaTime)
            {
                // Bursts out of the dark in a tenth of a second, then shudders and fades.
                float pop = Mathf.Clamp01(t / 0.1f);
                float calm = 1f - t / length;
                rt.localScale = Vector3.one * (Mathf.Lerp(0.3f, 1.15f, 1f - (1f - pop) * (1f - pop)) + Mathf.Sin(t * 47f) * 0.02f * calm);
                rt.anchoredPosition = Random.insideUnitCircle * 18f * calm;
                deerGroup.alpha = t < fadeFrom ? 1f : Mathf.InverseLerp(length, fadeFrom, t);
                if (chroma != null) chroma.intensity.value = 1f;
                yield return null;
            }
            deerGroup.alpha = 0f;
        }

        IEnumerator Fade(CanvasGroup g, float target, float seconds)
        {
            float start = g.alpha;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                g.alpha = Mathf.Lerp(start, target, t / seconds);
                yield return null;
            }
            g.alpha = target;
        }

        IEnumerator FadeImage(float targetAlpha, float seconds)
        {
            var c = fade.color;
            float start = c.a;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                c.a = Mathf.Lerp(start, targetAlpha, t / seconds);
                fade.color = c;
                yield return null;
            }
            c.a = targetAlpha;
            fade.color = c;
        }

        void BuildUI()
        {
            var go = new GameObject("UI");
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            fade = MakeRect(go.transform, "Fade", Vector2.zero, Vector2.zero, true).gameObject.AddComponent<Image>();
            fade.raycastTarget = false;
            fade.color = Color.black;

            titleGroup = MakeGroup(go.transform, "Title");
            var title = MakeRect(titleGroup.transform, "Logo", new Vector2(0f, 160f), Vector2.zero, false).gameObject.AddComponent<RawImage>();
            title.texture = titleTexture;
            title.raycastTarget = false;
            title.SetNativeSize();
            title.rectTransform.sizeDelta *= 0.9f;
            MakeText(titleGroup.transform, "―　本物の声を、聞き分けて　―", 40, new Color(0.75f, 0.7f, 0.65f), new Vector2(0f, 0f));
            MakeText(titleGroup.transform, "ヘッドホン推奨", 60, new Color(0.5f, 0.45f, 0.42f), new Vector2(0f, -150f));
            titleHint = MakeText(titleGroup.transform, "クリックで　はじめる", 60, Color.white, new Vector2(0f, -240f));
            var medal = MakeRect(titleGroup.transform, "ClearMedal", Vector2.zero, new Vector2(142f, 178f), false).gameObject.AddComponent<RawImage>();
            medal.texture = GetMedalTexture(PlayerPrefs.GetInt(ClearedPlayerPrefsKey, 0) != 0);
            medal.raycastTarget = false;
            var medalRect = medal.rectTransform;
            medalRect.anchorMin = medalRect.anchorMax = medalRect.pivot = new Vector2(1f, 1f);
            medalRect.anchoredPosition = new Vector2(-72f, -62f);
            titleGroup.alpha = 0f;

            // Diary pages (StorySettings): the opening and the ending, fitted to the screen on black.
            diaryGroup = MakeGroup(go.transform, "Diary");
            var black = MakeRect(diaryGroup.transform, "Black", Vector2.zero, Vector2.zero, true).gameObject.AddComponent<Image>();
            black.color = Color.black;
            black.raycastTarget = false;
            var page = MakeRect(diaryGroup.transform, "Page", Vector2.zero, Vector2.zero, true);
            diaryImage = page.gameObject.AddComponent<RawImage>();
            diaryImage.raycastTarget = false;
            diaryFit = page.gameObject.AddComponent<AspectRatioFitter>();
            diaryFit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            // The result, on the ending page's dark upper-left corner (placed by StorySettings.resultPosition).
            resultBlock = MakeRect(page, "Result", Vector2.zero, new Vector2(ResultTitleWidth, 320f), false);
            resultBlock.pivot = new Vector2(0f, 1f);
            // The gold brush headline (StorySettings.clearTitleImage); pivot in its middle so it can pop in.
            var titleRect = MakeRect(resultBlock, "Title", Vector2.zero, new Vector2(ResultTitleWidth, 195f), false);
            titleRect.anchorMin = titleRect.anchorMax = new Vector2(0f, 1f);
            resultTitle = titleRect.gameObject.AddComponent<RawImage>();
            resultTitle.raycastTarget = false;
            resultTitleGroup = titleRect.gameObject.AddComponent<CanvasGroup>();
            resultTitleText = MakeText(titleRect, "ゲームクリア", 110, new Color(1f, 0.86f, 0.55f), Vector2.zero);
            resultTitleText.gameObject.AddComponent<Outline>().effectColor = new Color(0.2f, 0.09f, 0.03f, 1f);
            resultTime = ResultLine(resultBlock, "", 40, new Color(0.98f, 0.94f, 0.86f), 0f);
            var outline = resultTime.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.08f, 0.04f, 0.02f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);
            resultTimeGroup = resultTime.gameObject.AddComponent<CanvasGroup>();
            resultHint = ResultLine(resultBlock, "クリックで　タイトルへ", 26, new Color(0.85f, 0.8f, 0.74f), 0f);
            resultBlock.gameObject.SetActive(false);
            diaryGroup.alpha = 0f;

            hintGroup = MakeGroup(go.transform, "Hint");
            MakeText(hintGroup.transform, "マウス：見回す　　左長押し：前へ　　右長押し：うしろへ　　Esc：カーソルを出す（クリックで戻る）",
                22, new Color(0.7f, 0.68f, 0.64f, 0.8f), new Vector2(0f, 480f));
            hintGroup.alpha = 0f;

            messageGroup = MakeGroup(go.transform, "Message");
            messageText = MakeText(messageGroup.transform, "", 34, new Color(0.88f, 0.84f, 0.78f), new Vector2(0f, -200f));
            messageText.lineSpacing = 1.15f;
            messageGroup.alpha = 0f;

            // The deer that leaps at the screen (deer trap): over the game, under the end screens.
            deerGroup = MakeGroup(go.transform, "DeerScare");
            deerImage = MakeRect(deerGroup.transform, "Deer", Vector2.zero, new Vector2(1080f, 1080f), false).gameObject.AddComponent<RawImage>();
            deerImage.raycastTarget = false;
            deerGroup.alpha = 0f;

            endGroup = MakeGroup(go.transform, "End");
            endImage = MakeRect(endGroup.transform, "EndImage", new Vector2(0f, 110f), Vector2.zero, false).gameObject.AddComponent<RawImage>();
            endImage.raycastTarget = false;
            endSub = MakeText(endGroup.transform, "", 34, new Color(0.82f, 0.78f, 0.74f), new Vector2(0f, -70f));
            endHint = MakeText(endGroup.transform, "", 30, Color.white, new Vector2(0f, -250f));
            endGroup.alpha = 0f;

            debugText = MakeText(go.transform, "", 18, new Color(0.6f, 1f, 0.6f), Vector2.zero);
            var dr = debugText.rectTransform;
            dr.anchorMin = dr.anchorMax = dr.pivot = new Vector2(0f, 1f);
            dr.anchoredPosition = new Vector2(16f, -12f);
            debugText.alignment = TextAnchor.UpperLeft;
            debugText.enabled = false;
        }

        /// <summary>A left-aligned line of the result block, y down from its top-left corner.</summary>
        Text ResultLine(RectTransform block, string text, int size, Color color, float y)
        {
            var t = MakeText(block, text, size, color, Vector2.zero);
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(640f, size * 1.5f);
            t.alignment = TextAnchor.UpperLeft;
            return t;
        }

        static RectTransform MakeRect(Transform parent, string name, Vector2 pos, Vector2 size, bool stretch)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            if (stretch)
            {
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
            }
            else
            {
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = pos;
                rt.sizeDelta = size;
            }
            return rt;
        }

        static Texture2D GetMedalTexture(bool cleared)
        {
            if (cleared)
            {
                if (goldMedalTexture == null) goldMedalTexture = CreateMedalTexture(true);
                return goldMedalTexture;
            }

            if (grayMedalTexture == null) grayMedalTexture = CreateMedalTexture(false);
            return grayMedalTexture;
        }

        static Texture2D CreateMedalTexture(bool gold)
        {
            const int width = 256;
            const int height = 320;
            var outer = gold ? new Color32(104, 61, 18, 255) : new Color32(48, 52, 58, 255);
            var rim = gold ? new Color32(255, 225, 138, 255) : new Color32(184, 188, 191, 255);
            var ring = gold ? new Color32(168, 108, 28, 255) : new Color32(79, 84, 91, 255);
            var face = gold ? new Color32(224, 163, 49, 255) : new Color32(126, 132, 139, 255);
            var ribbon = gold ? new Color32(174, 103, 27, 255) : new Color32(91, 97, 105, 255);
            var ribbonLight = gold ? new Color32(247, 195, 80, 255) : new Color32(156, 161, 165, 255);
            var starColor = gold ? new Color32(255, 245, 195, 255) : new Color32(235, 238, 240, 255);
            var shine = gold ? new Color32(255, 251, 221, 255) : new Color32(250, 251, 252, 255);

            Vector2[] leftRibbon = { new Vector2(84, 161), new Vector2(122, 170), new Vector2(111, 28), new Vector2(96, 50), new Vector2(73, 35) };
            Vector2[] rightRibbon = { new Vector2(134, 170), new Vector2(172, 161), new Vector2(183, 35), new Vector2(160, 50), new Vector2(145, 28) };
            Vector2[] leftStripe = { new Vector2(91, 143), new Vector2(100, 147), new Vector2(96, 72), new Vector2(87, 67) };
            Vector2[] rightStripe = { new Vector2(156, 147), new Vector2(165, 143), new Vector2(169, 67), new Vector2(160, 72) };
            var center = new Vector2(128f, 210f);
            var star = BuildMedalStar(center, 38f, 17f);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var point = new Vector2(x + 0.5f, y + 0.5f);
                var color = new Color32(0, 0, 0, 0);
                if (PointInPolygon(point, leftRibbon) || PointInPolygon(point, rightRibbon)) color = ribbon;
                if (PointInPolygon(point, leftStripe) || PointInPolygon(point, rightStripe)) color = ribbonLight;

                float distance = Vector2.Distance(point, center);
                if (distance <= 84f) color = outer;
                if (distance <= 76f) color = rim;
                if (distance <= 68f) color = ring;
                if (distance <= 59f) color = face;
                if (PointInPolygon(point, star)) color = starColor;
                if (Vector2.Distance(point, new Vector2(101f, 234f)) <= 6f) color = shine;
                pixels[y * width + x] = color;
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = gold ? "ClearMedal_Gold" : "ClearMedal_Gray",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static Vector2[] BuildMedalStar(Vector2 center, float outerRadius, float innerRadius)
        {
            var points = new Vector2[10];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = -Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float radius = i % 2 == 0 ? outerRadius : innerRadius;
                points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            return points;
        }

        static bool PointInPolygon(Vector2 point, Vector2[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                var a = polygon[i];
                var b = polygon[j];
                if ((a.y > point.y) != (b.y > point.y) &&
                    point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        static CanvasGroup MakeGroup(Transform parent, string name)
        {
            var rt = MakeRect(parent, name, Vector2.zero, Vector2.zero, true);
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            g.blocksRaycasts = false;
            g.interactable = false;
            return g;
        }

        Text MakeText(Transform parent, string text, int size, Color color, Vector2 pos)
        {
            var rt = MakeRect(parent, "Text", pos, new Vector2(1800f, 200f), false);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = uiFont;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var sh = rt.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.8f);
            sh.effectDistance = new Vector2(2f, -2f);
            return t;
        }
    }
}
