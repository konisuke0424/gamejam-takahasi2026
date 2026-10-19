using UnityEngine;

namespace KaiForest
{
    /// <summary>Who a voice belongs to: mother's real voice (her, her handkerchief and kanzashi) or a fake (monsters, deer traps).</summary>
    public enum VoiceKind { Mother, Fake }

    /// <summary>One voice file's own volume (the file itself is left as it is).</summary>
    [System.Serializable]
    public class ClipVolume
    {
        [Tooltip("ファイル名（表示用。自動で入る）")]
        public string name;
        public AudioClip clip;
        [Tooltip("1 で元の音量。0.8 で2割小さく。1 より上げても、すでに最大音量で聞こえているところ（すぐ近く）ではそれ以上大きくならない")]
        [Range(0f, 2f)] public float volume = 1f;
    }

    /// <summary>
    /// How voices fade with distance. Lives at Assets/Game/Resources/VoiceSettings.asset
    /// (menu: KaiForest > Voice Settings). Read every frame, so edits during Play are heard at once
    /// and are kept after Play stops.
    /// </summary>
    [CreateAssetMenu(menuName = "KaiForest/Voice Settings")]
    public class VoiceSettings : ScriptableObject
    {
        public const string ResourceName = "VoiceSettings";

        [Header("減衰")]
        [Tooltip("距離による小さくなり方。大きいほど早く小さくなる（1 で距離2倍ごとに約-6dB、1.3 で約-8dB）")]
        [Min(0f)] public float falloff = 1.3f;

        [Tooltip("減衰の基準の距離（m）。これより近いとほぼ最大の音量")]
        [Min(0.1f)] public float referenceDistance = 6f;

        [Header("聞こえる範囲（この距離の75%から小さくなり始め、ここで無音）")]
        [Tooltip("お母さんの声（お母さん・ハンカチ・かんざし）が聞こえる距離（m）")]
        [Min(1f)] public float motherRange = 120f;

        [Tooltip("偽物の声（化け物・鹿トラップ）が聞こえる距離（m）")]
        [Min(1f)] public float fakeRange = 90f;

        [Header("音量")]
        [Tooltip("お母さんの声の音量（倍率）")]
        [Min(0f)] public float motherLoudness = 1f;

        [Tooltip("偽物の声の音量（倍率）")]
        [Min(0f)] public float fakeLoudness = 1f;

        [Header("遠くの声のこもり方")]
        [Tooltip("この距離（m）までは、はっきりした声")]
        [Min(0f)] public float nearDistance = 6f;

        [Tooltip("この距離（m）より先は、こもった遠くの声")]
        [Min(0f)] public float farDistance = 45f;

        [Header("セリフごとの音量（素材はそのまま。ゲームで使っている声が自動で並ぶ）")]
        [Tooltip("お母さん・化け物・カイの声それぞれの音量。遠くの声（_far）にも同じ倍率がかかる")]
        public ClipVolume[] lineVolumes = new ClipVolume[0];

        public float Range(VoiceKind k) => k == VoiceKind.Mother ? motherRange : fakeRange;
        public float Loudness(VoiceKind k) => k == VoiceKind.Mother ? motherLoudness : fakeLoudness;

        /// <summary>The voice file's own volume from lineVolumes (1 if it isn't listed).</summary>
        public float VolumeOf(AudioClip clip)
        {
            if (clip == null || lineVolumes == null) return 1f;
            foreach (var v in lineVolumes)
                if (v != null && v.clip == clip) return v.volume;
            return 1f;
        }

        /// <summary>0..1 gain of a voice of this kind heard from distance d.</summary>
        public float GainAt(VoiceKind k, float d)
        {
            float g = Mathf.Pow(referenceDistance / (referenceDistance + d), falloff);
            float range = Range(k);
            float edge = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(range * 0.75f, range, d));
            return Mathf.Min(1f, g * 1.35f) * edge;
        }

        /// <summary>The project's settings asset, or built-in defaults if it hasn't been created yet.</summary>
        public static VoiceSettings Load()
        {
            var s = Resources.Load<VoiceSettings>(ResourceName);
            return s != null ? s : CreateInstance<VoiceSettings>();
        }
    }
}
