using UnityEngine;

namespace KaiForest
{
    /// <summary>
    /// A fake's trap (placed in Forest.unity): from the kanzashi stage on, a monster lives inside
    /// areaRadius, calling with mother's voice. It goes for the boy only while he is inside the area and
    /// chases him no further than its edge (MonsterAI territory). Its capture is a real game over.
    /// Invisible in play; the area shows in the Scene view.
    /// </summary>
    public class MonsterTrap : MonoBehaviour
    {
        [Tooltip("化け物の縄張りの半径（m）。この中に入ったカイだけを追い、外へは出てこない")]
        public float areaRadius = 20f;

        public Vector3 Center => new Vector3(transform.position.x, 0f, transform.position.z);

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.55f, 0.1f);
            GizmoShapes.Circle(Center + Vector3.up * 0.1f, areaRadius, 96);
            Gizmos.DrawWireSphere(Center + Vector3.up * 1.2f, 0.5f);
            GizmoShapes.Label(transform.position + Vector3.up * 3f, "化け物トラップ");
        }
    }
}
