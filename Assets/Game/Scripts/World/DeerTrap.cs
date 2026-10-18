using UnityEngine;

namespace KaiForest
{
    /// <summary>
    /// A fake's trap (placed in Forest.unity): from the kanzashi stage on it calls with the monsters'
    /// imitation of mother's voice. When the boy comes within triggerRadius a deer leaps at the screen
    /// (GameManager.PlayDeerScare). Only a scare, not a capture; it then falls silent for good.
    /// Invisible in play; the radius shows in the Scene view.
    /// </summary>
    public class DeerTrap : MonoBehaviour
    {
        [Tooltip("この半径（m）にカイが入ると鹿が飛び出す")]
        public float triggerRadius = 1f;
        [Tooltip("飛び出す鹿の絵")]
        public Texture2D scareImage;
        [Tooltip("偽物の声を出す間隔（秒）")]
        public Vector2 callGap = new Vector2(6f, 11f);

        public bool Armed { get; private set; }
        public bool Sprung { get; private set; }
        public VoiceEmitter Voice { get; private set; }

        AudioBank bank;
        PlayerController player;
        float nextCall;
        int last = -1;

        public void Init(AudioBank b, PlayerController p)
        {
            bank = b;
            player = p;
            if (scareImage == null) scareImage = Resources.Load<Texture2D>(CheckpointLayout.DeerImageResource);
            Voice = gameObject.AddComponent<VoiceEmitter>();
            Voice.kind = VoiceKind.Fake;
        }

        public void Arm(float firstDelay)
        {
            if (Sprung) return;
            Armed = true;
            nextCall = Time.time + firstDelay;
        }

        public void Disarm()
        {
            Armed = false;
            if (Voice != null) Voice.Stop();
        }

        void Update()
        {
            if (!Armed) return;
            var d = player.transform.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude <= triggerRadius * triggerRadius)
            {
                Sprung = true;
                Disarm();
                GameManager.I.PlayDeerScare(scareImage);
                return;
            }
            if (Voice.IsPlaying || Time.time < nextCall) return;
            var line = AudioBank.PickWeighted(bank.monsterLines, last, -1, out last);
            // The fakes always say it exactly the same way.
            float len = Voice.Play(line, 1f);
            nextCall = Time.time + len + Random.Range(callGap.x, callGap.y);
        }

        /// <summary>For the debug display: dormant until the handkerchief is picked up, then armed, then sprung.</summary>
        public string StateLabel => Sprung ? "発動済み" : Armed ? "有効" : "待機中（ハンカチを拾うと有効）";

        void OnDrawGizmos()
        {
            // During play the colour shows the state: red = armed, dark = dormant, grey = sprung.
            bool playing = Application.isPlaying;
            Gizmos.color = !playing || Armed ? new Color(1f, 0.25f, 0.2f)
                : Sprung ? new Color(0.5f, 0.5f, 0.5f) : new Color(0.45f, 0.12f, 0.1f);
            GizmoShapes.Circle(transform.position + Vector3.up * 0.05f, triggerRadius);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.6f, 0.25f);
            GizmoShapes.Label(transform.position + Vector3.up * 1.4f, playing ? $"鹿トラップ（{StateLabel}）" : "鹿トラップ");
        }
    }
}
