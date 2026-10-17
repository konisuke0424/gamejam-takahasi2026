using UnityEngine;

namespace KaiForest
{
    /// <summary>
    /// Points at the moving parts of an imported character model so the procedural animation in
    /// MonsterAI can drive them. Filled in by KaiModelImporter; each part's origin is its joint.
    /// </summary>
    public class ModelRig : MonoBehaviour
    {
        public Transform body;
        public Transform head;
        public Transform armL;
        public Transform armR;

        public bool IsComplete => body != null && head != null && armL != null && armR != null;
    }
}
