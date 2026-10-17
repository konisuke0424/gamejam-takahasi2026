using UnityEngine;

namespace KaiForest
{
    /// <summary>
    /// A light that only exists so the Forest scene is visible while editing it.
    /// Tagged EditorOnly (stripped from builds) and switched off when play starts in the editor.
    /// </summary>
    public class EditorPreviewLight : MonoBehaviour
    {
        void Awake() => gameObject.SetActive(false);
    }
}
