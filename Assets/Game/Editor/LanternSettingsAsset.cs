using UnityEditor;
using UnityEngine;

namespace KaiForest.EditorTools
{
    /// <summary>
    /// Creates Assets/Game/Resources/LanternSettings.asset with default values the first time the
    /// scripts load (no Setup run needed), and adds KaiForest > Lantern Settings to jump to it.
    /// </summary>
    [InitializeOnLoad]
    public static class LanternSettingsAsset
    {
        public const string AssetPath = "Assets/Game/Resources/" + LanternSettings.ResourceName + ".asset";

        static LanternSettingsAsset() => EditorApplication.delayCall += () => Ensure();

        public static LanternSettings Ensure()
        {
            var s = AssetDatabase.LoadAssetAtPath<LanternSettings>(AssetPath);
            if (s != null) return s;
            if (!AssetDatabase.IsValidFolder("Assets/Game/Resources")) AssetDatabase.CreateFolder("Assets/Game", "Resources");
            s = ScriptableObject.CreateInstance<LanternSettings>();
            AssetDatabase.CreateAsset(s, AssetPath);
            AssetDatabase.SaveAssets();
            return s;
        }

        [MenuItem("KaiForest/Lantern Settings")]
        static void SelectAsset()
        {
            var s = Ensure();
            Selection.activeObject = s;
            EditorGUIUtility.PingObject(s);
        }
    }
}
