using UnityEngine;

namespace KaiForest
{
    /// <summary>One spoken line: a dry "near" take and a muffled, reverberant "far" take of the same length.</summary>
    [System.Serializable]
    public class VoiceLine
    {
        public string id;
        public AudioClip near;
        public AudioClip far;
        [Tooltip("Relative pick weight when choosing a random line.")]
        public float weight = 1f;
    }

    /// <summary>All clips. Created and filled by KaiForestSetup from Assets/Game/Audio.</summary>
    [CreateAssetMenu(menuName = "KaiForest/Audio Bank")]
    public class AudioBank : ScriptableObject
    {
        public VoiceLine[] motherLines;
        public VoiceLine motherFound;
        public VoiceLine[] monsterLines;
        public VoiceLine[] chaseLines;
        public VoiceLine catchLine;

        public AudioClip insects;
        public AudioClip wind;
        public AudioClip drone;
        public AudioClip heartbeat;
        public AudioClip monsterBreath;
        public AudioClip clearChime;
        public AudioClip gameOverSting;
        public AudioClip[] steps;
        public AudioClip[] monsterSteps;

        public static VoiceLine PickWeighted(VoiceLine[] lines, int avoidA, int avoidB, out int index)
        {
            float total = 0f;
            for (int i = 0; i < lines.Length; i++)
                if (i != avoidA && i != avoidB) total += lines[i].weight;
            float r = Random.value * total;
            for (int i = 0; i < lines.Length; i++)
            {
                if (i == avoidA || i == avoidB) continue;
                r -= lines[i].weight;
                if (r <= 0f) { index = i; return lines[i]; }
            }
            index = lines.Length - 1;
            return lines[index];
        }
    }
}
