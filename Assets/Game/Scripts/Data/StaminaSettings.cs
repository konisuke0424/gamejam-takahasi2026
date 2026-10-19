using UnityEngine;

namespace KaiForest
{
    /// <summary>
    /// Tuning for the boy's stamina. Lives at Assets/Game/Resources/StaminaSettings.asset
    /// (menu: KaiForest > Stamina Settings). Read every frame, so edits during Play apply at once.
    /// Stamina only drains while the monster is chasing; running low never slows the boy down,
    /// it only makes him hard of hearing (panting, a pounding heart, muffled surroundings).
    /// </summary>
    [CreateAssetMenu(menuName = "KaiForest/Stamina Settings")]
    public class StaminaSettings : ScriptableObject
    {
        public const string ResourceName = "StaminaSettings";

        [Header("スタミナ")]
        [Tooltip("化け物に追われている間、満タンから空になるまでの秒数")]
        [Min(0.5f)] public float drainSeconds = 10f;

        [Tooltip("追跡が終わってから回復し始めるまでの秒数")]
        [Min(0f)] public float recoverDelay = 1.5f;

        [Tooltip("空から満タンまで回復するのにかかる秒数")]
        [Min(0.5f)] public float recoverSeconds = 12f;

        [Header("スタミナが減ったときの影響（移動の速さは変わらない）")]
        [Tooltip("スタミナがこの割合を下回ると影響が出始め、0で最大になる")]
        [Range(0f, 1f)] public float effectStart = 0.7f;

        [Tooltip("息切れの最大音量")]
        [Range(0f, 1f)] public float pantMaxVolume = 0.8f;

        [Tooltip("息と息のあいだの間（秒）。毎回 ±15% ゆらぐ。0 にすると元の素材と同じ速さ")]
        [Min(0f)] public float pantGap = 0.35f;

        [Tooltip("スタミナ0のとき、周りの音（声・虫・風・化け物の足音と息）をどれだけ小さくするか")]
        [Range(0f, 1f)] public float maxHearingLoss = 0.6f;

        [Tooltip("スタミナ0のとき、声をどれだけこもらせるか（遠くの声のように聞こえる）")]
        [Range(0f, 1f)] public float maxMuffle = 0.7f;

        [Header("音素材（自動で設定される）")]
        [Tooltip("Pant Breaths が空のときだけ使う、続けて鳴らす息切れ")]
        public AudioClip pantClip;
        [Tooltip("息切れを1息ずつに切ったもの（Assets/Game/Audio/Sfx/Pant。Tools/audio/slice_pant.py で作る）。順番に、間をあけて鳴らす")]
        public AudioClip[] pantBreaths = new AudioClip[0];

        /// <summary>The project's settings asset, or built-in defaults if it hasn't been created yet.</summary>
        public static StaminaSettings Load()
        {
            var s = Resources.Load<StaminaSettings>(ResourceName);
            return s != null ? s : CreateInstance<StaminaSettings>();
        }
    }
}
