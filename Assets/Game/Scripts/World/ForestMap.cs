using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KaiForest
{
    /// <summary>
    /// Root of Forest.unity. Holds the map bounds and the start / exit markers placed in the scene,
    /// and answers "is this spot walkable?" for the AI. Everything else in the map is ordinary
    /// scene objects (tree and landmark prefabs, baked undergrowth) that can be edited by hand.
    /// </summary>
    public class ForestMap : MonoBehaviour
    {
        public const string SceneName = "Forest";
        public const int ObstacleMask = 1 << 0; // Default layer: trees, landmarks, ground, walls.

        public static ForestMap Instance { get; private set; }

        [Tooltip("The walls stand at ±halfSize; AI destinations stay inside.")]
        public float halfSize = 120f;
        public Transform playerStart;
        public MapExit exit;

        public Vector3 StartPosition => playerStart != null ? Flat(playerStart.position) : Vector3.zero;
        public float StartYaw => playerStart != null ? playerStart.eulerAngles.y : 0f;
        public Vector3 ExitPosition => exit != null ? Flat(exit.transform.position) : Vector3.zero;
        public int TreeCount { get; private set; }
        public Landmark[] Landmarks { get; private set; }
        /// <summary>The checkpoints and traps placed in the scene (KaiForest > Map > Place Checkpoints and Traps).</summary>
        public CheckpointItem[] Items { get; private set; }
        public DeerTrap[] DeerTraps { get; private set; }
        public MonsterTrap[] MonsterTraps { get; private set; }
        public MotherSpawn MotherSpawn { get; private set; }
        /// <summary>Where the hunting monsters appear (KaiForest > Map > Add Hunter Spawn); empty = at random.</summary>
        public HunterSpawn[] HunterSpawns { get; private set; }

        void Awake()
        {
            Instance = this;
            var trees = new List<ForestTree>();
            var landmarks = new List<Landmark>();
            var items = new List<CheckpointItem>();
            var deer = new List<DeerTrap>();
            var traps = new List<MonsterTrap>();
            var hunters = new List<HunterSpawn>();
            foreach (var go in gameObject.scene.GetRootGameObjects())
            {
                trees.AddRange(go.GetComponentsInChildren<ForestTree>());
                landmarks.AddRange(go.GetComponentsInChildren<Landmark>());
                items.AddRange(go.GetComponentsInChildren<CheckpointItem>(true));
                deer.AddRange(go.GetComponentsInChildren<DeerTrap>(true));
                traps.AddRange(go.GetComponentsInChildren<MonsterTrap>(true));
                hunters.AddRange(go.GetComponentsInChildren<HunterSpawn>());
                if (MotherSpawn == null) MotherSpawn = go.GetComponentInChildren<MotherSpawn>(true);
            }
            TreeCount = trees.Count;
            Landmarks = landmarks.ToArray();
            Items = items.ToArray();
            DeerTraps = deer.ToArray();
            MonsterTraps = traps.ToArray();
            HunterSpawns = hunters.ToArray();

            // Forest.unity opened on its own and played: bring in the game scene too.
            if (FindAnyObjectByType<GameManager>() == null && Application.CanStreamedLevelBeLoaded(GameManager.SceneName))
                SceneManager.LoadScene(GameManager.SceneName, LoadSceneMode.Additive);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        public bool IsOpen(Vector3 p, float radius = 0.6f)
        {
            return !Physics.CheckCapsule(p + Vector3.up * (0.2f + radius), p + Vector3.up * 1.8f, radius, ObstacleMask, QueryTriggerInteraction.Ignore);
        }

        public Vector3 ClampToMap(Vector3 p, float margin = 3f)
        {
            float h = halfSize - margin;
            return new Vector3(Mathf.Clamp(p.x, -h, h), 0f, Mathf.Clamp(p.z, -h, h));
        }

        /// <summary>Random walkable point within radius of center (falls back to center).</summary>
        public Vector3 FindOpenPoint(Vector3 center, float radius)
        {
            for (int i = 0; i < 40; i++)
            {
                Vector2 o = Random.insideUnitCircle * radius;
                var p = ClampToMap(new Vector3(center.x + o.x, 0f, center.z + o.y));
                if (IsOpen(p)) return p;
            }
            return ClampToMap(center);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(new Vector3(0f, 2f, 0f), new Vector3(halfSize * 2f, 4f, halfSize * 2f));
            if (playerStart != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(playerStart.position + Vector3.up * 0.7f, 0.7f);
                Gizmos.DrawRay(playerStart.position + Vector3.up * 0.7f, playerStart.forward * 3f);
            }
        }
    }
}
