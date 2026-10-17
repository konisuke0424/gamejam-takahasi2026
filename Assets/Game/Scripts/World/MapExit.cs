using UnityEngine;

namespace KaiForest
{
    /// <summary>The real exit (the torii the mother waits at). Its front (+Z) faces the way the boy arrives.</summary>
    public class MapExit : MonoBehaviour
    {
        [Tooltip("Mother's wander centre, this far in front of the gate.")]
        public float homeOffset = 4f;
        [Tooltip("How far she wanders from that centre while calling.")]
        public float wanderRadius = 20f;

        public Vector3 MotherHome
        {
            get
            {
                var p = transform.position + transform.forward * homeOffset;
                return new Vector3(p.x, 0f, p.z);
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            var c = MotherHome + Vector3.up * 0.1f;
            Gizmos.DrawWireSphere(c, 0.8f);
            const int n = 48;
            for (int i = 0; i < n; i++)
            {
                float a0 = i / (float)n * Mathf.PI * 2f, a1 = (i + 1) / (float)n * Mathf.PI * 2f;
                Gizmos.DrawLine(c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * wanderRadius,
                                c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * wanderRadius);
            }
        }
    }
}
