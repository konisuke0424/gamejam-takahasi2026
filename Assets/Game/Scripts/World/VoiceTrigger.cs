using UnityEngine;

namespace KaiForest
{
    /// <summary>
    /// A spot in the forest (placed in Forest.unity) where the boy says a line once when he walks
    /// into it, e.g. at the first fork: "the voice is coming from the right". Only in the chosen stage
    /// (or any stage). Invisible in play; the radius shows in the Scene view.
    /// </summary>
    public class VoiceTrigger : MonoBehaviour
    {
        [Tooltip("この半径（m）に入ると一度だけ言う")]
        public float radius = 4f;
        [Tooltip("言うセリフ。空なら Boy Voice Settings の Fork Line")]
        public AudioClip line;
        [Tooltip("オンなら段階に関係なく言う。オフなら下の段階のときだけ")]
        public bool anyStage;
        [Tooltip("この段階のときだけ言う（Any Stage がオフのとき）")]
        public Stage stage = Stage.Handkerchief;

        public bool Fired { get; private set; }

        void Update()
        {
            var gm = GameManager.I;
            if (Fired || gm == null || gm.State != GameState.Playing || gm.Player == null || gm.Director == null) return;
            if (!anyStage && gm.Director.Stage != stage) return;
            var d = gm.Player.transform.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > radius * radius || BoyVoice.Instance == null) return;
            Fired = true;
            BoyVoice.Instance.Say(line != null ? line : BoyVoiceSettings.Load().forkLine);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.5f);
            GizmoShapes.Circle(transform.position + Vector3.up * 0.05f, radius);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 1f, 0.2f);
            string clip = line != null ? line.name : "Fork Line";
            GizmoShapes.Label(transform.position + Vector3.up * 1.6f, $"セリフ（{clip}）{(Application.isPlaying && Fired ? " 済" : "")}");
        }
    }
}
