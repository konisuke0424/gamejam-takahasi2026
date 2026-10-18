using System.Collections.Generic;
using UnityEngine;

namespace KaiForest
{
    public enum ItemKind { Handkerchief, Kanzashi }

    /// <summary>
    /// Something of mother's lying in the forest (placed in Forest.unity). While it is the one to find,
    /// her real voice calls from it; walking within pickupRadius picks it up, which is a checkpoint
    /// (see StageDirector). Hidden until its stage comes, and gone once picked up, together with its
    /// markers (e.g. a sparkle particle placed next to it).
    /// </summary>
    public class CheckpointItem : MonoBehaviour
    {
        public ItemKind kind;
        [Tooltip("拾える距離（m）。この中に入ると拾う（クリック不要）")]
        public float pickupRadius = 3f;
        [Tooltip("呼び名（F1 のデバッグ表示に出る）")]
        public string displayName = "ハンカチ";
        [Tooltip("拾ったときに画面に出る文")]
        [TextArea] public string message = "";
        [Tooltip("お母さんの声がここから聞こえる間隔（秒）")]
        public Vector2 callGap = new Vector2(4f, 7.5f);
        [Tooltip("この物と一緒に出て、拾ったら一緒に消える目印（パーティクルなど）。空なら、近くに単独で置いたパーティクルを自動で使う")]
        public GameObject[] markers;
        [Tooltip("markers が空のとき、この距離（m）以内に単独で置いたパーティクルを目印とみなす")]
        public float markerSearchRadius = 4f;

        public bool Taken { get; private set; }
        public VoiceEmitter Voice { get; private set; }
        public Vector3 Position => new Vector3(transform.position.x, 0f, transform.position.z);

        AudioBank bank;
        bool calling;
        float nextCall;
        int last1 = -1, last2 = -1;
        readonly List<GameObject> shownWith = new List<GameObject>();

        public void Init(AudioBank b)
        {
            bank = b;
            Voice = gameObject.AddComponent<VoiceEmitter>();
            Voice.kind = VoiceKind.Mother;
            FindMarkers();
        }

        /// <summary>
        /// The markers set in the Inspector, or else every particle system standing on its own (not part
        /// of a landmark) within markerSearchRadius.
        /// </summary>
        void FindMarkers()
        {
            shownWith.Clear();
            if (markers != null)
                foreach (var m in markers)
                    if (m != null) shownWith.Add(m);
            if (shownWith.Count > 0) return;
            foreach (var ps in FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var t = ps.transform;
                if (t.parent != null || ps.gameObject.scene != gameObject.scene) continue;
                var d = t.position - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude <= markerSearchRadius * markerSearchRadius) shownWith.Add(ps.gameObject);
            }
        }

        /// <summary>Shows or hides the item together with its markers.</summary>
        public void SetShown(bool shown)
        {
            gameObject.SetActive(shown);
            foreach (var m in shownWith)
                if (m != null) m.SetActive(shown);
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
            SetShown(false);
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
            if (markers != null)
                foreach (var m in markers)
                    if (m != null) Gizmos.DrawLine(transform.position + Vector3.up * 0.15f, m.transform.position);
        }
    }
}
