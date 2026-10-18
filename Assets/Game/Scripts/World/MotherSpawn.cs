using UnityEngine;

namespace KaiForest
{
    /// <summary>
    /// Where mother appears once the kanzashi is found (placed in Forest.unity). She wanders within
    /// wanderRadius while calling. The child "Preview" is only a stand-in model for editing: tagged
    /// EditorOnly (stripped from builds) and switched off when play starts.
    /// </summary>
    public class MotherSpawn : MonoBehaviour
    {
        [Tooltip("お母さんが呼びながら歩き回る範囲（m）")]
        public float wanderRadius = 20f;

        public Vector3 Home => new Vector3(transform.position.x, 0f, transform.position.z);

        void Awake()
        {
            var preview = transform.Find("Preview");
            if (preview != null) preview.gameObject.SetActive(false);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(Home + Vector3.up * 0.1f, 0.8f);
            GizmoShapes.Circle(Home + Vector3.up * 0.1f, wanderRadius);
            GizmoShapes.Label(transform.position + Vector3.up * 2.2f, "お母さん（3つ目）");
        }
    }
}
