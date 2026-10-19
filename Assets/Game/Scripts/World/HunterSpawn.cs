using UnityEngine;

namespace KaiForest
{
    /// <summary>
    /// Where a hunting monster appears once the kanzashi is found (placed in Forest.unity; menu
    /// KaiForest > Map > Add Hunter Spawn). With several, each monster takes a different one at random;
    /// with none (or more monsters than markers), they appear at random spots between the boy and his
    /// mother (GameManager.PickHunterSpawn). Seen only in the editor.
    /// </summary>
    public class HunterSpawn : MonoBehaviour
    {
        public Vector3 Position => new Vector3(transform.position.x, 0f, transform.position.z);

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.25f, 0.2f);
            Gizmos.DrawWireSphere(Position + Vector3.up * 1f, 1f);
            GizmoShapes.Circle(Position + Vector3.up * 0.05f, 1.5f);
            GizmoShapes.Label(transform.position + Vector3.up * 2.4f, "追ってくる化け物（3つ目）");
        }
    }
}
