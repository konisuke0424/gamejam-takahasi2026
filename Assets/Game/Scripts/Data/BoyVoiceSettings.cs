using UnityEngine;

namespace KaiForest
{
    /// <summary>
    /// The boy's own lines (his thoughts, heard without direction). Lives at
    /// Assets/Game/Resources/BoyVoiceSettings.asset (menu: KaiForest > Boy Voice Settings); the clips
    /// are filled in automatically from Assets/Game/Audio/Voices/takashi_06..09.wav.
    /// Read every frame, so edits during Play apply at once.
    /// </summary>
    [CreateAssetMenu(menuName = "KaiForest/Boy Voice Settings")]
    public class BoyVoiceSettings : ScriptableObject
    {
        public const string ResourceName = "BoyVoiceSettings";

        [Tooltip("セリフの音量")]
        [Range(0f, 1f)] public float volume = 0.9f;
        [Tooltip("どのセリフどうしも、最低これだけあける（秒）")]
        [Min(0f)] public float minGap = 12f;

        [Header("ゲーム開始（タイトルから始めたとき一度だけ）")]
        [Tooltip("「お母さんを探さなきゃ」")]
        public AudioClip startLine;
        [Tooltip("本編が始まってから言うまで（秒）")]
        [Min(0f)] public float startDelay = 1.5f;

        [Header("外周の壁にぶつかったとき（これ以上進めないことを伝える）")]
        public AudioClip wallLine;
        [Tooltip("一度言ったら、これだけは言わない（秒）")]
        [Min(0f)] public float wallCooldown = 20f;

        [Header("分かれ道（VoiceTrigger に入ったとき）")]
        [Tooltip("VoiceTrigger にセリフが入っていないときに使う。「右側から声が聞こえる」")]
        public AudioClip forkLine;

        [Header("偽物の声を聞いたとき")]
        public AudioClip fakeHeardLine;
        [Tooltip("一度言ったら、これだけは言わない（秒）")]
        [Min(0f)] public float fakeHeardCooldown = 60f;
        [Tooltip("偽物の声がこの大きさ（0〜1）以上で聞こえたときだけ言う")]
        [Range(0f, 1f)] public float fakeHeardMinVolume = 0.1f;

        [Header("お母さんの声を聞いたとき")]
        public AudioClip motherHeardLine;
        [Tooltip("一度言ったら、これだけは言わない（秒）")]
        [Min(0f)] public float motherHeardCooldown = 90f;
        [Tooltip("お母さんの声がこの大きさ（0〜1）以上で聞こえたときだけ言う")]
        [Range(0f, 1f)] public float motherHeardMinVolume = 0.1f;

        [Tooltip("聞こえた声が終わってから言うまで（秒）")]
        [Min(0f)] public float afterHeardDelay = 0.6f;

        /// <summary>The project's settings asset, or built-in defaults if it hasn't been created yet.</summary>
        public static BoyVoiceSettings Load()
        {
            var s = Resources.Load<BoyVoiceSettings>(ResourceName);
            return s != null ? s : CreateInstance<BoyVoiceSettings>();
        }
    }
}
