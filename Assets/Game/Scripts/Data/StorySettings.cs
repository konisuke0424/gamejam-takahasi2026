using UnityEngine;

namespace KaiForest
{
    /// <summary>
    /// The diary pictures that open and close the game, and how long their dissolves take. Lives at
    /// Assets/Game/Resources/StorySettings.asset (menu: KaiForest > Story Settings); the pictures are
    /// filled in automatically from Assets/Denim/nikki01.png / nikki02.png.
    /// </summary>
    [CreateAssetMenu(menuName = "KaiForest/Story Settings")]
    public class StorySettings : ScriptableObject
    {
        public const string ResourceName = "StorySettings";

        [Header("はじまり（タイトル → 日記 → 本編）")]
        [Tooltip("タイトルのあとに出す日記の絵")]
        public Texture2D introImage;
        [Tooltip("タイトルから日記へのディゾルブ（秒）")]
        [Min(0f)] public float titleToIntroSeconds = 1f;
        [Tooltip("日記を見せる時間（秒）。クリックでとばせる")]
        [Min(0f)] public float introSeconds = 3f;
        [Tooltip("日記から本編へのディゾルブ（秒）")]
        [Min(0f)] public float introToGameSeconds = 1.5f;

        [Header("おわり（お母さんに会えたとき）")]
        [Tooltip("クリアのときに出す日記の絵。クリックでタイトルへ戻る")]
        public Texture2D clearImage;
        [Tooltip("お母さんに会えて顔を見たあと（約1.5秒後）から、絵へ切り替わり始めるまで（秒）")]
        [Min(0f)] public float clearDelaySeconds = 2f;
        [Tooltip("本編から日記の絵へのクロスフェード（秒）")]
        [Min(0f)] public float clearFadeSeconds = 2f;

        [Header("リザルト（おわりの絵の上の「ゲームクリア」とクリア時間）")]
        [Tooltip("「ゲームクリア」の見出し画像（Tools/gen_textures.py が作る UI_GameClear.png）")]
        public Texture2D clearTitleImage;
        [Tooltip("リザルトを出す位置。絵の左上の角からの割合（x: 右へ、y: 下へ）")]
        public Vector2 resultPosition = new Vector2(0.015f, 0.01f);
        [Tooltip("リザルト全体の大きさ（倍率）")]
        [Min(0.1f)] public float resultScale = 1f;

        /// <summary>The project's settings asset, or built-in defaults if it hasn't been created yet.</summary>
        public static StorySettings Load()
        {
            var s = Resources.Load<StorySettings>(ResourceName);
            return s != null ? s : CreateInstance<StorySettings>();
        }
    }
}
