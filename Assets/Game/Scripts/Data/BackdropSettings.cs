using UnityEngine;

namespace KaiForest
{
    /// <summary>
    /// The far backdrop (mountains and moon behind the forest). Lives at
    /// Assets/Game/Resources/BackdropSettings.asset (menu: KaiForest > Backdrop Settings); the image
    /// and shader are filled in automatically. Read every frame, so edits during Play show up at once.
    /// </summary>
    [CreateAssetMenu(menuName = "KaiForest/Backdrop Settings")]
    public class BackdropSettings : ScriptableObject
    {
        public const string ResourceName = "BackdropSettings";
        public const string ShaderName = "KaiForest/Backdrop";

        [Tooltip("遠景を出す")]
        public bool visible = true;

        [Tooltip("遠景の画像（横長の一枚絵）。初期値は Assets/Denim/back.png")]
        public Texture2D texture;

        [Tooltip("遠景用のシェーダー（自動で入る。霧の影響を受けない）")]
        public Shader shader;

        [Header("並べ方")]
        [Tooltip("一周に並べる枚数。1枚おきに左右反転するので、偶数なら継ぎ目が出ない")]
        [Min(1)] public int copies = 2;

        [Tooltip("画像の下端の高さ（度）。0 が目の高さ、マイナスが下")]
        [Range(-89f, 89f)] public float bottomAngle = -60f;

        [Tooltip("画像の上端の高さ（度）。上端－下端 を 360÷枚数×(縦÷横) にすると絵が縦横につぶれない（2枚・3:2 の絵なら 120）")]
        [Range(-89f, 89f)] public float topAngle = 60f;

        [Tooltip("画像を横に回す角度（度）。0 で月が月明かりの来る方角（だいたい南東）に来る")]
        public float yaw = 0f;

        [Header("色")]
        [Tooltip("明るさ")]
        [Min(0f)] public float brightness = 1f;

        [Tooltip("霧の色を混ぜる割合。上げると遠景が霧に沈んで目立たなくなる")]
        [Range(0f, 1f)] public float fogMix = 0f;

        /// <summary>The project's settings asset, or built-in defaults if it hasn't been created yet.</summary>
        public static BackdropSettings Load()
        {
            var s = Resources.Load<BackdropSettings>(ResourceName);
            return s != null ? s : CreateInstance<BackdropSettings>();
        }
    }
}
