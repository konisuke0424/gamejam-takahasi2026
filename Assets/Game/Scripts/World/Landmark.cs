using UnityEngine;

namespace KaiForest
{
    public enum LandmarkType { Jizo, StoneLanterns, Hokora, SacredTree, FallenLog, Well, Graves, Stumps, BrokenTorii }

    /// <summary>Tags a placed landmark prefab. Undergrowth baking keeps a clearing around it.</summary>
    public class Landmark : MonoBehaviour
    {
        public LandmarkType type;

        public float ClearingRadius => type switch
        {
            LandmarkType.SacredTree => 7f,
            LandmarkType.BrokenTorii => 10f,
            _ => 5f,
        };
    }
}
