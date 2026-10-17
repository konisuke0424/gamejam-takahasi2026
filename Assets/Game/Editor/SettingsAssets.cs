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

        static SettingsAssets() => EditorApplication.delayCall += EnsureAll;

        public static void EnsureAll()
        {
            Lantern();
            Stamina();
        }

        public static LanternSettings Lantern() => Ensure<LanternSettings>(LanternSettings.ResourceName);

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

        static void Select(Object o)
        {
            Selection.activeObject = o;
            EditorGUIUtility.PingObject(o);
        }
    }
}
