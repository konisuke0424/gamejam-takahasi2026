using UnityEngine;

namespace KaiForest
{
    /// <summary>Scene-view drawing for the placed map objects (gizmos never show in builds).</summary>
    public static class GizmoShapes
    {
        public static void Circle(Vector3 center, float radius, int segments = 48)
        {
            for (int i = 0; i < segments; i++)
            {
                float a0 = i / (float)segments * Mathf.PI * 2f, a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
                Gizmos.DrawLine(center + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius,
                                center + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius);
            }
        }

        public static void Label(Vector3 position, string text)
        {
#if UNITY_EDITOR
            UnityEditor.Handles.Label(position, text);
#endif
        }
    }
}
