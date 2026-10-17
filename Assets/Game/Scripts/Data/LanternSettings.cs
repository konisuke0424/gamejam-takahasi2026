using UnityEngine;

namespace KaiForest
{
    /// <summary>
    /// Tuning for the boy's hand lantern. Lives at Assets/Game/Resources/LanternSettings.asset
    /// (menu: KaiForest > Lantern Settings). Read every frame, so edits during Play show up at once
    /// and are kept after Play stops.
    /// </summary>
    [CreateAssetMenu(menuName = "KaiForest/Lantern Settings")]
    public class LanternSettings : ScriptableObject
    {
        public const string ResourceName = "LanternSettings";

        [Tooltip("光が届く距離（m）。この距離で光はゼロになる")]
        [Min(1f)] public float range = 11f;

        [Tooltip("明るさ。点光源は距離の2乗で暗くなるので、遠くまで照らしたいときは range と一緒に上げる。上げすぎると近くの幹が白飛びする")]
        [Min(0f)] public float intensity = 22f;

        /// <summary>The project's settings asset, or built-in defaults if it hasn't been created yet.</summary>
        public static LanternSettings Load()
        {
            var s = Resources.Load<LanternSettings>(ResourceName);
            return s != null ? s : CreateInstance<LanternSettings>();
        }
    }
}
