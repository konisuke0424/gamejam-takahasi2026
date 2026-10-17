using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KaiForest.EditorTools
{
    /// <summary>
    /// One-click (or -executeMethod) project setup: materials, audio bank, post-process profile,
    /// the Main scene, player/quality settings, and the WebGL / Windows builds.
    /// Safe to re-run: assets are updated in place.
    /// </summary>
    public static class KaiForestSetup
    {
        const string Root = "Assets/Game";
        const string ScenePath = "Assets/Scenes/Main.unity";

        /// <summary>
        /// Recreates materials, audio bank, post-process profile and Main.unity, and creates any missing
        /// map prefab. Forest.unity is only created when it doesn't exist yet: it holds hand edits.
        /// </summary>
        [MenuItem("KaiForest/Setup Project")]
        public static void Setup()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (var d in new[] { "Materials", "Data", "Settings" })
                if (!AssetDatabase.IsValidFolder($"{Root}/{d}")) AssetDatabase.CreateFolder(Root, d);
            AssetDatabase.Refresh();

            var mats = CreateMaterials();
            var bank = CreateAudioBank();
            var profile = CreateVolumeProfile();
            ConfigurePipeline();
            ConfigurePlayer();
            LanternSettingsAsset.Ensure();
            ForestBaker.EnsurePrefabs(mats);
            if (!File.Exists(ForestBaker.ScenePath)) ForestBaker.BuildForestScene(mats);
            CreateScene(mats, bank, profile);
            AssetDatabase.SaveAssets();
            Debug.Log("[KaiForest] Setup done");
        }

        [MenuItem("KaiForest/Build WebGL")]
        public static void BuildWebGL() => Build(BuildTarget.WebGL, "Builds/WebGL");

        [MenuItem("KaiForest/Build Windows")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/KaiForest.exe");

        // -executeMethod entry points
        public static void CI_SetupOnly() => Setup();
        public static void CI_SetupBuildWindows() { Setup(); BuildWindows(); }
        public static void CI_SetupBuildWebGL() { Setup(); BuildWebGL(); }

        static void Build(BuildTarget target, string path)
        {
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath, ForestBaker.ScenePath },
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            Debug.Log($"[KaiForest] Build {target}: {s.result} size={s.totalSize / (1024 * 1024)}MB time={s.totalTime} errors={s.totalErrors}");
            if (Application.isBatchMode && s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        // ------------------------------------------------------------------ materials

        static Material Lit(string name, Color color, float smooth = 0.12f, Texture2D tex = null, Color? emission = null, float tiling = 1f)
        {
            string path = $"{Root}/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", Vector2.one * tiling);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                // Must not be None: URP's material validation (run whenever the editor loads the
                // material) treats None as "emission off" and strips _EMISSION.
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else m.DisableKeyword("_EMISSION");
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static MaterialLibrary CreateMaterials()
        {
            var ground = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Textures/T_Ground.png");
            var bark = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Textures/T_Bark.png");
            var lib = LoadOrCreate<MaterialLibrary>($"{Root}/Data/MaterialLibrary.asset");
            // Colours are sRGB; the project is linear, so these read much darker than they look here.
            // The ground is a 320 m plane: tile the texture every 4 m.
            lib.ground = Lit("M_Ground", Color.white, 0.08f, ground, null, 80f);
            lib.bark = Lit("M_Bark", Color.white, 0.1f, bark);
            lib.foliage = Lit("M_Foliage", new Color(0.12f, 0.18f, 0.12f), 0.05f);
            lib.bush = Lit("M_Bush", new Color(0.2f, 0.3f, 0.16f), 0.1f);
            lib.stone = Lit("M_Stone", new Color(0.52f, 0.53f, 0.5f), 0.15f);
            lib.moss = Lit("M_Moss", new Color(0.3f, 0.38f, 0.18f), 0.05f);
            lib.torii = Lit("M_Torii", new Color(0.62f, 0.1f, 0.05f), 0.25f);
            lib.wood = Lit("M_Wood", new Color(0.3f, 0.21f, 0.14f), 0.15f);
            lib.straw = Lit("M_Straw", new Color(0.62f, 0.52f, 0.32f), 0.1f);
            lib.paper = Lit("M_Paper", new Color(0.9f, 0.88f, 0.84f), 0.1f);
            lib.cloth = Lit("M_Kimono", new Color(0.16f, 0.2f, 0.36f), 0.2f);
            lib.obi = Lit("M_Obi", new Color(0.5f, 0.3f, 0.2f), 0.25f);
            lib.skin = Lit("M_Skin", new Color(0.66f, 0.52f, 0.45f), 0.3f);
            lib.hair = Lit("M_Hair", new Color(0.02f, 0.02f, 0.025f), 0.35f);
            lib.monsterBody = Lit("M_MonsterBody", new Color(0.01f, 0.01f, 0.012f), 0.05f);
            // Faint self-glow so the pale face floats in the dark a little beyond the lantern.
            lib.monsterFace = Lit("M_MonsterFace", new Color(0.88f, 0.87f, 0.82f), 0.35f, null, new Color(0.16f, 0.16f, 0.15f));
            lib.monsterEye = Lit("M_MonsterEye", Color.black, 0.5f, null, new Color(1.2f, 0.9f, 0.7f));
            lib.lanternGlow = Lit("M_LanternGlow", new Color(1f, 0.85f, 0.7f), 0.1f, null, new Color(2.2f, 1.1f, 0.45f));
            lib.lanternFrame = Lit("M_LanternFrame", new Color(0.03f, 0.03f, 0.03f), 0.3f);
            EditorUtility.SetDirty(lib);
            return lib;
        }

        // ------------------------------------------------------------------ audio

        static VoiceLine Line(string id, float weight = 1f)
        {
            var near = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/Audio/Voices/{id}.wav");
            var far = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/Audio/Voices/{id}_far.wav");
            if (near == null) Debug.LogWarning("[KaiForest] missing voice " + id);
            return new VoiceLine { id = id, near = near, far = far, weight = weight };
        }

        static AudioClip Sfx(string id) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/Audio/Sfx/{id}.wav");

        static AudioBank CreateAudioBank()
        {
            var b = LoadOrCreate<AudioBank>($"{Root}/Data/AudioBank.asset");
            var mother = new List<VoiceLine>();
            for (int i = 1; i <= 10; i++) mother.Add(Line($"mother_{i:00}"));
            b.motherLines = mother.ToArray();
            b.motherFound = Line("mother_found");
            // The pure imitations are the most common; the broken ones are rarer and creepier.
            b.monsterLines = new[]
            {
                Line("monster_01", 3f), Line("monster_02", 3f), Line("monster_03", 3f),
                Line("monster_04", 1f), Line("monster_05", 1f), Line("monster_06", 1f),
                Line("monster_07", 0.5f), Line("monster_08", 1f),
            };
            b.chaseLines = new[] { Line("chase_01", 2f), Line("chase_02", 1f) };
            b.catchLine = Line("catch_01");
            b.insects = Sfx("amb_insects");
            b.wind = Sfx("amb_wind");
            b.drone = Sfx("amb_drone");
            b.heartbeat = Sfx("heartbeat");
            b.monsterBreath = Sfx("monster_breath");
            b.clearChime = Sfx("clear_chime");
            b.gameOverSting = Sfx("gameover_sting");
            b.steps = new[] { Sfx("step_01"), Sfx("step_02"), Sfx("step_03"), Sfx("step_04"), Sfx("step_05") };
            b.monsterSteps = new[] { Sfx("monster_step_01"), Sfx("monster_step_02"), Sfx("monster_step_03") };
            EditorUtility.SetDirty(b);
            return b;
        }

        // ------------------------------------------------------------------ post processing

        static T AddOverride<T>(VolumeProfile p) where T : VolumeComponent
        {
            var c = p.Add<T>(true);
            c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(c, p);
            return c;
        }

        static VolumeProfile CreateVolumeProfile()
        {
            string path = $"{Root}/Settings/KaiForest_Volume.asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) != null) AssetDatabase.DeleteAsset(path);
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, path);

            var tm = AddOverride<Tonemapping>(p);
            tm.mode.Override(TonemappingMode.ACES);
            var bloom = AddOverride<Bloom>(p);
            bloom.threshold.Override(0.8f);
            bloom.intensity.Override(0.8f);
            bloom.scatter.Override(0.7f);
            bloom.tint.Override(new Color(1f, 0.75f, 0.55f));
            var ca = AddOverride<ColorAdjustments>(p);
            ca.postExposure.Override(0.35f);
            ca.contrast.Override(14f);
            ca.saturation.Override(-28f);
            var smh = AddOverride<ShadowsMidtonesHighlights>(p);
            smh.shadows.Override(new Vector4(0.85f, 0.95f, 1.1f, 0f));
            var vig = AddOverride<Vignette>(p);
            vig.color.Override(Color.black);
            vig.intensity.Override(0.42f);
            vig.smoothness.Override(0.55f);
            var grain = AddOverride<FilmGrain>(p);
            grain.type.Override(FilmGrainLookup.Medium4);
            grain.intensity.Override(0.45f);
            grain.response.Override(0.75f);
            var chroma = AddOverride<ChromaticAberration>(p);
            chroma.intensity.Override(0.1f);

            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
            return p;
        }

        // ------------------------------------------------------------------ settings

        static void ConfigurePipeline()
        {
            // Every platform uses the lighter "Mobile" quality level, so the Windows test build looks like WebGL.
            QualitySettings.SetQualityLevel(0, true);
            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            if (rp == null) { Debug.LogWarning("[KaiForest] Mobile_RPAsset not found"); return; }
            var so = new SerializedObject(rp);
            void B(string n, bool v) { var p = so.FindProperty(n); if (p != null) p.boolValue = v; else Debug.LogWarning("missing " + n); }
            void F(string n, float v) { var p = so.FindProperty(n); if (p != null) p.floatValue = v; else Debug.LogWarning("missing " + n); }
            void I(string n, int v) { var p = so.FindProperty(n); if (p != null) p.intValue = v; else Debug.LogWarning("missing " + n); }
            B("m_SupportsHDR", true);
            F("m_RenderScale", 1f);
            B("m_MainLightShadowsSupported", false);
            B("m_AdditionalLightShadowsSupported", true);
            I("m_AdditionalLightsShadowmapResolution", 2048);
            B("m_SoftShadowsSupported", true);
            I("m_AdditionalLightsPerObjectLimit", 4);
            F("m_ShadowDistance", 25f);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rp);
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.WebGL.template = "PROJECT:KaiForest";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        }

        // ------------------------------------------------------------------ scene

        static void CreateScene(MaterialLibrary mats, AudioBank bank, VolumeProfile profile)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var vol = new GameObject("PostProcess").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;

            var gm = new GameObject("GameManager").AddComponent<GameManager>();
            gm.mats = mats;
            gm.audioBank = bank;
            gm.uiFont = AssetDatabase.LoadAssetAtPath<Font>($"{Root}/Fonts/NotoSansJP-Regular.ttf");
            gm.titleTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/UI/UI_Title.png");
            gm.clearTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/UI/UI_Clear.png");
            gm.gameOverTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/UI/UI_GameOver.png");
            gm.volume = vol;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = GameManager.FogDensity;
            RenderSettings.fogColor = gm.fogColor;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = gm.ambientColor;
            RenderSettings.skybox = null;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(ForestBaker.ScenePath, true),
            };
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }
    }
}
