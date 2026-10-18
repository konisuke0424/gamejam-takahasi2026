using System.Collections;
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
    /// Loads the map (Forest.unity, placed by hand in the editor), spawns the boy, mother and monster
    /// on its markers, builds the UI, and drives the title → intro → play → ending flow.
    /// Main.unity only holds this component and the post-process volume.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public const string SceneName = "Main";
        const string ClearRecordKey = "KaiForest.HasCleared";

        public static GameManager I { get; private set; }
        /// <summary>Set before reloading the scene so a retry skips the title screen.</summary>
        public static bool SkipTitle;
        /// <summary>Set by AutoTest so a stray real click can't skip screens or move the boy mid-test.</summary>
        public static bool IgnoreMouse;

        [Header("Assets (wired by KaiForestSetup)")]
        public MaterialLibrary mats;
        public AudioBank audioBank;
        public Font uiFont;
        public Font titleFont;
        public Texture2D titleTexture;
        public Texture2D clearTexture;
        public Texture2D gameOverTexture;
        public Volume volume;

        /// <summary>Night fog: about half the scene is gone at 10 m, nearly all of it at 20 m.</summary>
        public const float FogDensity = 0.085f;

        [Header("Night look (applied at runtime)")]
        public Color fogColor = new Color(0.012f, 0.016f, 0.024f);
        public Color ambientColor = new Color(0.05f, 0.055f, 0.075f);

        public GameState State { get; private set; } = GameState.Title;
        /// <summary>True once the map is loaded and the actors exist.</summary>
        public bool Ready { get; private set; }
        public ForestMap Map { get; private set; }
        public PlayerController Player { get; private set; }
        public MotherAI Mother { get; private set; }
        public MonsterAI Monster { get; private set; }
        public AmbienceController Ambience { get; private set; }
        public float PlayTime { get; private set; }
        public bool CanRestart { get; private set; }

        Canvas canvas;
        Image fade;
        CanvasGroup titleGroup, endGroup, storyGroup, hintGroup;
        Text storyText, endSub, endHint, titleHint, titleMedalStar, debugText;
        Image titleMedal;
        Image[] titleMedalRibbons;
        RawImage endImage;
        Vignette vignette;
        ChromaticAberration chroma;
        float baseVignette, baseChroma;
        bool debugHud;
        Coroutine introRoutine;
        LanternSettings viewSettings;

        void Awake()
        {
            I = this;
            uiFont = Resources.Load<Font>("KaiForest/Fonts/HGRGY") ?? uiFont;
            titleFont = Resources.Load<Font>("KaiForest/Fonts/aoyagireisyosimo_ttf") ?? uiFont;
            viewSettings = LanternSettings.Load();
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

            var home = Map.exit != null ? Map.exit.MotherHome : Map.ExitPosition;
            var mgo = new GameObject("Mother") { layer = 2 };
            Mother = mgo.AddComponent<MotherAI>();
            Mother.Init(Map, Player, audioBank, mats, home);
            if (Map.exit != null) Mother.wanderRadius = Map.exit.wanderRadius;
            Mother.Teleport(Map.FindOpenPoint(home, 3f));

            var xgo = new GameObject("Monster") { layer = 2 };
            Monster = xgo.AddComponent<MonsterAI>();
            Monster.Init(Map, Player, audioBank, mats);
            Monster.Teleport(PickMonsterSpawn(home));

            Ambience = gameObject.AddComponent<AmbienceController>();
            Ambience.Init(audioBank, Player, Monster);
        }

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

        Vector3 PickMonsterSpawn(Vector3 motherHome)
        {
            // Somewhere between the boy and his mother, well away from both.
            float h = Map.halfSize - 25f;
            for (int i = 0; i < 300; i++)
            {
                var p = new Vector3(Random.Range(-h, h), 0f, Random.Range(-h, h));
                if (Vector3.Distance(p, Map.StartPosition) < 75f) continue;
                if (Vector3.Distance(p, motherHome) < 55f) continue;
                if (!Map.IsOpen(p)) continue;
                return p;
            }
            return Map.FindOpenPoint((Map.StartPosition + motherHome) * 0.5f, 20f);
        }

        // ------------------------------------------------------------------ flow

        void ShowTitle()
        {
            State = GameState.Title;
            fade.color = new Color(0f, 0f, 0f, 0.72f);
            bool cleared = PlayerPrefs.GetInt(ClearRecordKey, 0) == 1;
            titleMedal.color = cleared ? new Color(0.93f, 0.68f, 0.2f) : new Color(0.48f, 0.48f, 0.48f);
            Color ribbonColor = cleared ? new Color(0.68f, 0.38f, 0.08f) : new Color(0.3f, 0.3f, 0.3f);
            foreach (var ribbon in titleMedalRibbons) ribbon.color = ribbonColor;
            titleMedalStar.color = cleared ? new Color(0.35f, 0.2f, 0.05f) : new Color(0.2f, 0.2f, 0.2f);
            titleGroup.alpha = 1f;
        }

        public void BeginGame()
        {
            if (State != GameState.Title) return;
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
            yield return Fade(titleGroup, 0f, 0.6f);
            fade.color = Color.black;
            yield return ShowStory("森で、迷子に　なってしまった。", 2.6f);
            yield return ShowStory("おかあさんの声が　きこえる……\n\nでも、この森には\n人の声を　まねる　化け物が　いるらしい。", 4.2f);
            StartPlaying();
            yield return FadeIntoPlay(2.5f);
        }

        IEnumerator FadeIntoPlay(float seconds)
        {
            yield return FadeImage(0f, seconds);
            hintGroup.alpha = 1f;
            yield return new WaitForSeconds(14f);
            yield return Fade(hintGroup, 0f, 2f);
        }

        /// <summary>A click during the opening story jumps straight into play.</summary>
        void SkipStory()
        {
            if (introRoutine != null) StopCoroutine(introRoutine);
            titleGroup.alpha = 0f;
            storyGroup.alpha = 0f;
            StartPlaying();
            introRoutine = StartCoroutine(FadeIntoPlay(0.6f));
        }

        IEnumerator ShowStory(string text, float hold)
        {
            storyText.text = text;
            yield return Fade(storyGroup, 1f, 0.9f);
            yield return new WaitForSeconds(hold);
            yield return Fade(storyGroup, 0f, 0.7f);
        }

        void StartPlaying()
        {
            if (State == GameState.Playing) return;
            State = GameState.Playing;
            PlayTime = 0f;
            Player.ControlEnabled = true;
            LockCursor(true);
            Mother.Activate();
            Monster.Activate();
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
            storyGroup.alpha = 0f;
            fade.color = new Color(0f, 0f, 0f, 0f);
            StartPlaying();
        }

        public void OnReunite()
        {
            if (State != GameState.Playing) return;
            State = GameState.Clear;
            PlayerPrefs.SetInt(ClearRecordKey, 1);
            PlayerPrefs.Save();
            Player.ControlEnabled = false;
            LockCursor(false);
            Monster.Halt();
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
            CanRestart = true; // a click from here on retries; the clear screen keeps playing out otherwise
            yield return new WaitForSeconds(1.6f);
            fade.color = new Color(0.06f, 0.035f, 0.02f, 0f);
            yield return FadeImage(0.88f, 2f);
            endImage.texture = clearTexture;
            endImage.SetNativeSize();
            endImage.rectTransform.sizeDelta *= 0.62f;
            int sec = Mathf.FloorToInt(PlayTime);
            endSub.text = $"森を　ぬけるまで　{sec / 60}分{sec % 60:00}秒";
            endHint.text = "クリックで　もういちど";
            endHint.enabled = false;
            yield return Fade(endGroup, 1f, 1.5f);
            endHint.enabled = true;
        }

        public void OnCaught(MonsterAI who)
        {
            if (State != GameState.Playing) return;
            State = GameState.Caught;
            Player.ControlEnabled = false;
            LockCursor(false);
            Mother.Active = false;
            Mother.Voice.Stop();
            who.Halt();
            Ambience.FadeTo(0f, 0.4f);
            Ambience.PlayOneShot(audioBank.catchLine.near, 1f);
            Ambience.PlayOneShot(audioBank.gameOverSting, 0.85f);
            StartCoroutine(CaughtRoutine(who));
        }

        IEnumerator CaughtRoutine(MonsterAI who)
        {
            // It stands over the boy and folds down until its face is right in front of his.
            var fwd = Player.transform.forward;
            var toM = who.transform.position - Player.transform.position; toM.y = 0f;
            if (toM.sqrMagnitude > 0.01f) fwd = toM.normalized;
            who.Teleport(Player.transform.position + fwd * 2.55f);
            who.transform.rotation = Quaternion.LookRotation(-fwd);
            for (float t = 0f; t < 1.1f; t += Time.deltaTime)
            {
                who.ScareLean = Mathf.Lerp(0f, 54f, Mathf.SmoothStep(0f, 1f, t / 0.3f));
                var face = who.Rig.head.position + Random.insideUnitSphere * 0.02f * (1f - t);
                Player.SetLookOverride(Quaternion.LookRotation(face - Player.HeadPosition), Mathf.Clamp01(t / 0.15f));
                if (vignette != null) vignette.intensity.value = Mathf.Lerp(baseVignette, 0.65f, t);
                if (chroma != null) chroma.intensity.value = 1f;
                yield return null;
            }
            fade.color = Color.black;
            CanRestart = true; // the scare has played; a click retries right away
            endImage.texture = gameOverTexture;
            endImage.SetNativeSize();
            endImage.rectTransform.sizeDelta *= 0.62f;
            endSub.text = "あれは、おかあさんじゃ　なかった。";
            endHint.text = "クリックで　もういちど";
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
                case GameState.Caught:
                    if (click && CanRestart) Restart();
                    break;
            }

            if (State == GameState.Playing)
            {
                bool chase = Monster.State == MonsterState.Chase;
                float k = Mathf.Clamp01(Mathf.InverseLerp(20f, 3f, Monster.DistanceToPlayer));
                if (vignette != null) vignette.intensity.value = Mathf.MoveTowards(vignette.intensity.value, baseVignette + (chase ? 0.12f : 0f) + k * 0.08f, Time.deltaTime * 0.5f);
                if (chroma != null) chroma.intensity.value = Mathf.MoveTowards(chroma.intensity.value, baseChroma + (chase ? 0.45f : 0f) + k * 0.3f, Time.deltaTime);
            }

            var kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame) debugHud = !debugHud;
            debugText.enabled = debugHud;
            if (debugHud) debugText.text = DebugString();
        }

        public string DebugString()
        {
            float dm = Vector3.Distance(Player.transform.position, Mother.transform.position);
            var pp = Player.transform.position;
            return $"state {State}  t {PlayTime:0.0}s  fps {1f / Mathf.Max(Time.smoothDeltaTime, 0.0001f):0}  kai ({pp.x:0.0}, {pp.z:0.0}) yaw {Player.Yaw:0}\n" +
                   $"mother {dm:0.0}m  calls {Mother.Voice.PlayCount}  last {Mother.Voice.LastLineId}  noticed {Mother.Noticed}\n" +
                   $"monster {Monster.DistanceToPlayer:0.0}m  {Monster.State}  calls {Monster.Voice.PlayCount}  last {Monster.Voice.LastLineId}\n" +
                   $"exit {Map.ExitPosition}  trees {Map.TreeCount}";
        }

        // ------------------------------------------------------------------ UI

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
            title.rectTransform.sizeDelta *= 1.2f;
            MakeText(titleGroup.transform, "―　本物の声を、聞き分けて　―", 60, new Color(0.75f, 0.7f, 0.65f), new Vector2(0f, 0f), titleFont);
            MakeText(titleGroup.transform, "ヘッドホン推奨", 50, new Color(0.5f, 0.45f, 0.42f), new Vector2(0f, -300f));
            titleHint = MakeText(titleGroup.transform, "クリックで　はじめる", 40, Color.white, new Vector2(0f, -390f));
            var medalRoot = MakeRect(titleGroup.transform, "ClearMedal", new Vector2(-134f, -42f), new Vector2(86f, 96f), false);
            medalRoot.anchorMin = medalRoot.anchorMax = medalRoot.pivot = new Vector2(1f, 1f);
            titleMedalRibbons = new Image[2];
            for (int i = 0; i < titleMedalRibbons.Length; i++)
            {
                var ribbonRect = MakeRect(medalRoot, "Ribbon", new Vector2(i == 0 ? -13f : 13f, -29f), new Vector2(19f, 42f), false);
                ribbonRect.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? -12f : 12f);
                titleMedalRibbons[i] = ribbonRect.gameObject.AddComponent<Image>();
                titleMedalRibbons[i].raycastTarget = false;
            }
            var medalCircle = MakeRect(medalRoot, "MedalFace", new Vector2(0f, 10f), new Vector2(68f, 68f), false).gameObject.AddComponent<Image>();
            medalCircle.sprite = CreateCircleSprite();
            medalCircle.raycastTarget = false;
            titleMedal = medalCircle;
            titleMedalStar = MakeText(medalRoot, "★", 42, Color.black, new Vector2(0f, 10f));
            titleMedalStar.rectTransform.sizeDelta = new Vector2(68f, 68f);
            titleGroup.alpha = 0f;

            storyGroup = MakeGroup(go.transform, "Story");
            storyText = MakeText(storyGroup.transform, "", 40, new Color(0.85f, 0.82f, 0.78f), new Vector2(0f, 20f));
            storyText.lineSpacing = 1.15f;
            MakeText(storyGroup.transform, "クリックで　とばす", 22, new Color(0.55f, 0.52f, 0.48f, 0.8f), new Vector2(0f, -470f));
            storyGroup.alpha = 0f;

            hintGroup = MakeGroup(go.transform, "Hint");
            MakeText(hintGroup.transform, "マウス：見回す　　左長押し：前へ　　右長押し：うしろへ　　Esc：カーソルを出す（クリックで戻る）",
                22, new Color(0.7f, 0.68f, 0.64f, 0.8f), new Vector2(0f, 480f));
            hintGroup.alpha = 0f;

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

        static CanvasGroup MakeGroup(Transform parent, string name)
        {
            var rt = MakeRect(parent, name, Vector2.zero, Vector2.zero, true);
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            g.blocksRaycasts = false;
            g.interactable = false;
            return g;
        }

        Text MakeText(Transform parent, string text, int size, Color color, Vector2 pos, Font fontOverride = null)
        {
            var rt = MakeRect(parent, "Text", pos, new Vector2(1800f, 200f), false);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = fontOverride != null ? fontOverride : uiFont;
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

        static Sprite CreateCircleSprite()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "ClearMedalCircle";
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(center - distance + 1f) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
