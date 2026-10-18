using UnityEngine;

namespace KaiForest
{
    public enum ItemKind { Handkerchief, Kanzashi }

    /// <summary>
    /// Something of mother's lying in the forest (placed in Forest.unity). While it is the one to find,
    /// her real voice calls from it; clicking within pickupRadius picks it up, which is a checkpoint
    /// (see StageDirector). Hidden until its stage comes.
    /// </summary>
    public class CheckpointItem : MonoBehaviour
    {
        public ItemKind kind;
        [Tooltip("拾える距離（m）。この中でクリックすると拾う")]
        public float pickupRadius = 1.8f;
        [Tooltip("「クリックで　〇〇を　拾う」に出る名前")]
        public string displayName = "ハンカチ";
        [Tooltip("拾ったときに画面に出る文")]
        [TextArea] public string message = "";
        [Tooltip("お母さんの声がここから聞こえる間隔（秒）")]
        public Vector2 callGap = new Vector2(4f, 7.5f);

        public bool Taken { get; private set; }
        public VoiceEmitter Voice { get; private set; }
        public Vector3 Position => new Vector3(transform.position.x, 0f, transform.position.z);

        AudioBank bank;
        bool calling;
        float nextCall;
        int last1 = -1, last2 = -1;

        public void Init(AudioBank b)
        {
            bank = b;
            Voice = gameObject.AddComponent<VoiceEmitter>();
            Voice.kind = VoiceKind.Mother;
        }

        public void StartCalling(float firstDelay = 2.5f)
        {
            calling = true;
            nextCall = Time.time + firstDelay;
        }

        public void StopCalling()
        {
            calling = false;
            if (Voice != null) Voice.Stop();
        }

        public void Take()
        {
            Taken = true;
            StopCalling();
            gameObject.SetActive(false);
        }

        public bool InReach(Vector3 p)
        {
            var d = p - transform.position;
            d.y = 0f;
            return d.sqrMagnitude <= pickupRadius * pickupRadius;
        }

        void Update()
        {
            if (!calling || Voice.IsPlaying || Time.time < nextCall) return;
            var line = AudioBank.PickWeighted(bank.motherLines, last1, last2, out int idx);
            last2 = last1; last1 = idx;
            // Her real voice: never the same intonation twice.
            float len = Voice.Play(line, Random.Range(0.96f, 1.04f));
            nextCall = Time.time + len + Random.Range(callGap.x, callGap.y);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.9f, 0.3f);
            GizmoShapes.Circle(transform.position + Vector3.up * 0.05f, pickupRadius);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.15f, 0.15f);
            GizmoShapes.Label(transform.position + Vector3.up * 1.2f, kind == ItemKind.Handkerchief ? "ハンカチ（1つ目）" : "かんざし（2つ目）");
        }
    }
}
