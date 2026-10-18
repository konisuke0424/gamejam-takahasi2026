using UnityEngine;

namespace KaiForest
{
    /// <summary>
    /// Keeps a flat picture (a Quad, which shows its -Z side) turned towards the camera around the
    /// vertical axis only, like a standing cut-out that always faces the boy.
    /// </summary>
    public class Billboard : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var away = transform.position - cam.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(away);
        }
    }
}
