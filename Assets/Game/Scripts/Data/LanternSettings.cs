using UnityEngine;

namespace KaiForest
{
    /// <summary>
    /// Tuning for the boy's hand lantern, plus a debug daylight slider. Lives at
    /// Assets/Game/Resources/LanternSettings.asset (menu: KaiForest > Lantern Settings).
    /// Read every frame, so edits during Play show up at once and are kept after Play stops.
    /// </summary>
    [CreateAssetMenu(menuName = "KaiForest/Lantern Settings")]
    public class LanternSettings : ScriptableObject
    {
        public const string ResourceName = "LanternSettings";

        [Header("ランタン")]
        [Tooltip("光が届く距離（m）。この距離で光はゼロになる")]
        [Min(1f)] public float range = 11f;

        [Tooltip("明るさ。点光源は距離の2乗で暗くなるので、上げすぎると近くの幹が白飛びする")]
        [Min(0f)] public float intensity = 22f;

        [Header("デバッグ（エディタで再生したときだけ効く）")]
        [Tooltip("0=夜（本番の見た目）、1=昼。霧・空・環境光・月明かり・描画距離をまとめて昼に寄せる。マップ確認用で、ビルドでは常に夜")]
        [Range(0f, 1f)] public float daylight = 0f;

        /// <summary>Daylight actually applied: the slider in the editor, always night in builds.</summary>
        public float EffectiveDaylight => Application.isEditor ? daylight : 0f;

        /// <summary>The project's settings asset, or built-in defaults if it hasn't been created yet.</summary>
        public static LanternSettings Load()
        {
            var s = Resources.Load<LanternSettings>(ResourceName);
            return s != null ? s : CreateInstance<LanternSettings>();
        }
    }
}
