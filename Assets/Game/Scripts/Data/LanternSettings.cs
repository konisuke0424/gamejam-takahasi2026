using UnityEngine;

namespace KaiForest
{
    /// <summary>
    /// Tuning for the boy's hand lantern and how far the night lets him see. Lives at
    /// Assets/Game/Resources/LanternSettings.asset (menu: KaiForest > Lantern Settings).
    /// Read every frame, so edits during Play show up at once and are kept after Play stops.
    /// </summary>
    [CreateAssetMenu(menuName = "KaiForest/Lantern Settings")]
    public class LanternSettings : ScriptableObject
    {
        public const string ResourceName = "LanternSettings";

        /// <summary>Distance from the lantern whose brightness Reach keeps unchanged.</summary>
        public const float ReferenceDistance = 2f;

        [Header("ランタン")]
        [Tooltip("光が届く距離（m）。この距離で光はゼロになる")]
        [Min(1f)] public float range = 11f;

        [Tooltip("ランタンから2m先の明るさ。上げすぎると近くの幹が白飛びする")]
        [Min(0f)] public float intensity = 22f;

        [Tooltip("奥行き（m）。上げるほど、2m先の明るさを保ったまま奥まで光が届き、手前はまぶしくなくなる。" +
                 "光の出どころをカイの後ろへこの距離だけ下げて実現しているので、上げると幹の影は出にくくなる。0で従来どおり")]
        [Range(0f, 8f)] public float reach = 0f;

        [Header("霧（見える距離）")]
        [Tooltip("霧の濃さ。下げるほど遠くまで見える。0.085で約10m先が半分かすむ")]
        [Range(0.01f, 0.2f)] public float fogDensity = 0.085f;

        /// <summary>Light intensity to use so the brightness at ReferenceDistance matches <see cref="intensity"/>.</summary>
        public float EffectiveIntensity
        {
            get
            {
                float k = (ReferenceDistance + reach) / ReferenceDistance;
                return intensity * k * k;
            }
        }

        /// <summary>Light range measured from the pulled-back light position.</summary>
        public float EffectiveRange => range + reach;

        /// <summary>The project's settings asset, or built-in defaults if it hasn't been created yet.</summary>
        public static LanternSettings Load()
        {
            var s = Resources.Load<LanternSettings>(ResourceName);
            return s != null ? s : CreateInstance<LanternSettings>();
        }
    }
}
