using UnityEditor;
using UnityEngine;

namespace KaiForest.EditorTools
{
    /// <summary>
    /// Creates the tuning assets in Assets/Game/Resources with default values the first time the
    /// scripts load (no Setup run needed), and adds KaiForest menu items to jump to them.
    /// </summary>
    [InitializeOnLoad]
    public static class SettingsAssets
    {
        const string Dir = "Assets/Game/Resources";
        const string PantClipPath = "Assets/Game/Audio/Sfx/player_pant.wav";
        const string BackdropImagePath = "Assets/Denim/back.png";
        const string MotherImagePath = "Assets/Denim/mother.png";

        static SettingsAssets() => EditorApplication.delayCall += EnsureAll;

        public static void EnsureAll()
        {
            Lantern();
            Stamina();
            Backdrop();
            Voice();
            MotherBillboard();
        }

        /// <summary>
        /// Mother's picture material (Resources/MotherBillboard.mat): URP Lit cut out along the picture's
        /// transparency, both faces, no shine. Made once when missing; tweak it freely afterwards.
        /// A saved asset (rather than one made at runtime) keeps the alpha-clip shader variant in builds.
        /// </summary>
        public static Material MotherBillboard()
        {
            string path = $"{Dir}/{CharacterFactory.MotherBillboardResource}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(MotherImagePath);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (tex == null || shader == null) return null;
            m = new Material(shader);
            m.SetTexture("_BaseMap", tex);
            m.SetTexture("_MainTex", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            m.SetFloat("_Cull", 0f);
            m.doubleSidedGI = true;
            m.SetFloat("_Smoothness", 0f);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_SpecularHighlights", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            AssetDatabase.CreateAsset(m, path);
            AssetDatabase.SaveAssets();
            return m;
        }

        public static LanternSettings Lantern() => Ensure<LanternSettings>(LanternSettings.ResourceName);

        public static VoiceSettings Voice() => Ensure<VoiceSettings>(VoiceSettings.ResourceName);

        public static StaminaSettings Stamina()
        {
            var s = Ensure<StaminaSettings>(StaminaSettings.ResourceName);
            if (s.pantClip == null)
            {
                s.pantClip = AssetDatabase.LoadAssetAtPath<AudioClip>(PantClipPath);
                if (s.pantClip != null)
                {
                    EditorUtility.SetDirty(s);
                    AssetDatabase.SaveAssets();
                }
            }
            return s;
        }

        /// <summary>Also fills in the image (Assets/Denim/back.png) and the shader if they are empty.</summary>
        public static BackdropSettings Backdrop()
        {
            var s = Ensure<BackdropSettings>(BackdropSettings.ResourceName);
            bool dirty = false;
            if (s.texture == null)
            {
                s.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(BackdropImagePath);
                dirty |= s.texture != null;
            }
            if (s.shader == null)
            {
                s.shader = Shader.Find(BackdropSettings.ShaderName);
                dirty |= s.shader != null;
            }
            if (dirty)
            {
                EditorUtility.SetDirty(s);
                AssetDatabase.SaveAssets();
            }
            return s;
        }

        static T Ensure<T>(string name) where T : ScriptableObject
        {
            string path = $"{Dir}/{name}.asset";
            var s = AssetDatabase.LoadAssetAtPath<T>(path);
            if (s != null) return s;
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Game", "Resources");
            s = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(s, path);
            AssetDatabase.SaveAssets();
            return s;
        }

        [MenuItem("KaiForest/Lantern Settings")]
        static void SelectLantern() => Select(Lantern());

        [MenuItem("KaiForest/Stamina Settings")]
        static void SelectStamina() => Select(Stamina());

        [MenuItem("KaiForest/Backdrop Settings")]
        static void SelectBackdrop() => Select(Backdrop());

        [MenuItem("KaiForest/Voice Settings")]
        static void SelectVoice() => Select(Voice());

        [MenuItem("KaiForest/Mother Picture Material")]
        static void SelectMotherBillboard() => Select(MotherBillboard());

        static void Select(Object o)
        {
            if (o == null) return;
            Selection.activeObject = o;
            EditorGUIUtility.PingObject(o);
        }
    }
}
