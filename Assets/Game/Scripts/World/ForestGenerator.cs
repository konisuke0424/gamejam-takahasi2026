using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KaiForest
{
    public enum LandmarkType { Jizo, StoneLanterns, Hokora, SacredTree, FallenLog, Well, Graves, Stumps }

    /// <summary>
    /// Builds the whole flat cedar forest at runtime from a fixed seed, so the layout is the same
    /// every run. Only the exit (where the mother waits) is picked per run from three candidates;
    /// the two unused candidates get broken torii as false exits.
    /// </summary>
    public class ForestGenerator : MonoBehaviour
    {
        public int seed = 20261017;
        public float halfSize = 120f;
        public float treeSpacing = 3.7f;
        public float chunkSize = 24f;
        public int landmarkCount = 12;

        public static readonly Vector3[] ExitCandidates =
        {
            new Vector3(-62f, 0f, 98f), new Vector3(0f, 0f, 104f), new Vector3(62f, 0f, 98f),
        };

        public Vector3 StartPosition { get; private set; }
        public Vector3 ExitPosition { get; private set; }
        public int TreeCount { get; private set; }
        public readonly List<KeyValuePair<Vector3, LandmarkType>> Landmarks = new List<KeyValuePair<Vector3, LandmarkType>>();

        public const int ObstacleMask = 1 << 0; // Default layer: trees, landmarks, ground, walls.

        struct Clearing { public Vector2 c; public float r; }

        MaterialLibrary mats;
        System.Random rng;
        Transform root;
        readonly List<Clearing> clearings = new List<Clearing>();
        readonly Dictionary<(int, int, Material), MeshBuilder> builders = new Dictionary<(int, int, Material), MeshBuilder>();
        readonly Dictionary<(int, int), Transform> colliderHosts = new Dictionary<(int, int), Transform>();

        public void Generate(MaterialLibrary materials, int exitIndex)
        {
            mats = materials;
            rng = new System.Random(seed);
            root = new GameObject("Forest").transform;
            StartPosition = new Vector3(0f, 0f, -100f);
            ExitPosition = ExitCandidates[Mathf.Clamp(exitIndex, 0, ExitCandidates.Length - 1)];

            clearings.Add(new Clearing { c = Flat(StartPosition), r = 5f });
            foreach (var e in ExitCandidates) clearings.Add(new Clearing { c = Flat(e), r = 11f });
            PlaceLandmarkSpots();

            BuildGround();
            BuildWalls();
            BuildTrees();
            BuildUndergrowth();
            FlushBuilders();

            foreach (var lm in Landmarks) BuildLandmark(lm.Key, lm.Value);
            for (int i = 0; i < ExitCandidates.Length; i++)
            {
                if (ExitCandidates[i] == ExitPosition) BuildExit(ExitCandidates[i]);
                else BuildBrokenTorii(ExitCandidates[i]);
            }
            Physics.SyncTransforms();
        }

        // ------------------------------------------------------------------ queries

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

        // ------------------------------------------------------------------ layout

        static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        bool InClearing(Vector2 p, float scale = 1f)
        {
            foreach (var c in clearings)
                if ((p - c.c).sqrMagnitude < c.r * c.r * scale * scale) return true;
            return false;
        }

        void PlaceLandmarkSpots()
        {
            var types = (LandmarkType[])System.Enum.GetValues(typeof(LandmarkType));
            var spots = new List<Vector2>();
            int tries = 0;
            while (spots.Count < landmarkCount && tries++ < 4000)
            {
                var p = new Vector2(R(-halfSize + 15f, halfSize - 15f), R(-halfSize + 15f, halfSize - 20f));
                bool ok = true;
                foreach (var s in spots) if ((s - p).sqrMagnitude < 32f * 32f) { ok = false; break; }
                foreach (var c in clearings) if ((c.c - p).sqrMagnitude < (c.r + 22f) * (c.r + 22f)) { ok = false; break; }
                if (!ok) continue;
                spots.Add(p);
            }
            for (int i = 0; i < spots.Count; i++)
            {
                var type = types[i % types.Length];
                Landmarks.Add(new KeyValuePair<Vector3, LandmarkType>(new Vector3(spots[i].x, 0f, spots[i].y), type));
                clearings.Add(new Clearing { c = spots[i], r = type == LandmarkType.SacredTree ? 7f : 5f });
            }
        }

        List<Vector2> Poisson(Vector2 min, Vector2 max, float r)
        {
            float cell = r / Mathf.Sqrt(2f);
            int gw = Mathf.CeilToInt((max.x - min.x) / cell), gh = Mathf.CeilToInt((max.y - min.y) / cell);
            var grid = new int[gw * gh];
            for (int i = 0; i < grid.Length; i++) grid[i] = -1;
            var pts = new List<Vector2>();
            var active = new List<int>();

            void Add(Vector2 p)
            {
                pts.Add(p);
                active.Add(pts.Count - 1);
                int gx = (int)((p.x - min.x) / cell), gy = (int)((p.y - min.y) / cell);
                grid[gy * gw + gx] = pts.Count - 1;
            }

            Add(new Vector2(R(min.x, max.x), R(min.y, max.y)));
            while (active.Count > 0)
            {
                int ai = rng.Next(active.Count);
                var p = pts[active[ai]];
                bool found = false;
                for (int k = 0; k < 24; k++)
                {
                    float ang = R(0f, Mathf.PI * 2f), rad = r * R(1f, 1.6f);
                    var q = p + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;
                    if (q.x < min.x || q.y < min.y || q.x >= max.x || q.y >= max.y) continue;
                    int gx = (int)((q.x - min.x) / cell), gy = (int)((q.y - min.y) / cell);
                    bool ok = true;
                    for (int y = Mathf.Max(0, gy - 2); y <= Mathf.Min(gh - 1, gy + 2) && ok; y++)
                        for (int x = Mathf.Max(0, gx - 2); x <= Mathf.Min(gw - 1, gx + 2); x++)
                        {
                            int idx = grid[y * gw + x];
                            if (idx >= 0 && (pts[idx] - q).sqrMagnitude < r * r) { ok = false; break; }
                        }
                    if (!ok) continue;
                    Add(q);
                    found = true;
                    break;
                }
                if (!found) active.RemoveAt(ai);
            }
            return pts;
        }

        // ------------------------------------------------------------------ chunked meshes

        MeshBuilder B(Vector3 p, Material m)
        {
            var key = (Mathf.FloorToInt(p.x / chunkSize), Mathf.FloorToInt(p.z / chunkSize), m);
            if (!builders.TryGetValue(key, out var b)) builders[key] = b = new MeshBuilder();
            return b;
        }

        Transform ColliderHost(Vector3 p)
        {
            var key = (Mathf.FloorToInt(p.x / chunkSize), Mathf.FloorToInt(p.z / chunkSize));
            if (!colliderHosts.TryGetValue(key, out var t))
            {
                t = new GameObject($"Colliders_{key.Item1}_{key.Item2}").transform;
                t.SetParent(root, false);
                colliderHosts[key] = t;
            }
            return t;
        }

        void FlushBuilders()
        {
            var chunks = new GameObject("Chunks").transform;
            chunks.SetParent(root, false);
            foreach (var kv in builders)
            {
                var go = new GameObject($"Chunk_{kv.Key.Item1}_{kv.Key.Item2}_{kv.Key.Item3.name}");
                go.transform.SetParent(chunks, false);
                go.AddComponent<MeshFilter>().sharedMesh = kv.Value.ToMesh(go.name);
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = kv.Key.Item3;
                bool canopy = kv.Key.Item3 == mats.foliage;
                mr.shadowCastingMode = canopy ? ShadowCastingMode.Off : ShadowCastingMode.On;
            }
            builders.Clear();
        }

        // ------------------------------------------------------------------ ground & bounds

        void BuildGround()
        {
            float s = halfSize + 40f;
            var b = new MeshBuilder();
            // Subdivide so per-vertex fog/light interpolation stays smooth.
            int n = 32;
            float step = s * 2f / n;
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    float x0 = -s + x * step, z0 = -s + z * step, x1 = x0 + step, z1 = z0 + step;
                    b.AddQuad(new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), new Vector3(x0, 0, z1),
                        new Vector2(x0, z0) / 4f, new Vector2(x1, z1) / 4f);
                }
            var go = new GameObject("Ground");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = b.ToMesh("Ground");
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mats.ground;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, -0.5f, 0f);
            col.size = new Vector3(s * 2f, 1f, s * 2f);
        }

        void BuildWalls()
        {
            var w = new GameObject("Walls").transform;
            w.SetParent(root, false);
            float h = halfSize + 0.5f, len = halfSize * 2f + 4f;
            Prims.Box(w, new Vector3(0, 5, h), new Vector3(len, 10, 1));
            Prims.Box(w, new Vector3(0, 5, -h), new Vector3(len, 10, 1));
            Prims.Box(w, new Vector3(h, 5, 0), new Vector3(1, 10, len));
            Prims.Box(w, new Vector3(-h, 5, 0), new Vector3(1, 10, len));
        }

        // ------------------------------------------------------------------ trees

        void BuildTrees()
        {
            float outer = halfSize + 14f;
            var pts = Poisson(new Vector2(-outer, -outer), new Vector2(outer, outer), treeSpacing);
            foreach (var p in pts)
            {
                if (InClearing(p)) continue;
                bool inside = Mathf.Abs(p.x) < halfSize + 1f && Mathf.Abs(p.y) < halfSize + 1f;
                AddTree(p, inside, 1f);
            }
            // Dense ring outside the walls so the edge reads as impassable forest.
            for (float t = -outer; t < outer; t += 1.6f)
            {
                for (int side = 0; side < 4; side++)
                {
                    float depth = R(1.5f, 9f);
                    float along = t + R(-0.6f, 0.6f);
                    float off = halfSize + depth;
                    Vector2 p = side switch
                    {
                        0 => new Vector2(along, off),
                        1 => new Vector2(along, -off),
                        2 => new Vector2(off, along),
                        _ => new Vector2(-off, along),
                    };
                    AddTree(p, false, 1f);
                }
            }
        }

        void AddTree(Vector2 p, bool collider, float scale)
        {
            TreeCount++;
            var origin = new Vector3(p.x, 0f, p.y);
            float r0 = Mathf.Lerp(0.2f, 0.42f, Mathf.Pow((float)rng.NextDouble(), 1.5f)) * scale;
            float h = R(13f, 21f) * scale;
            var lean = Quaternion.Euler(R(-2.5f, 2.5f), R(0f, 360f), R(-2.5f, 2.5f));

            var bark = B(origin, mats.bark);
            bark.AddTube(origin, lean,
                new[] { -0.2f, 0.35f, 1.2f, h * 0.5f, h },
                new[] { r0 * 1.5f, r0 * 1.12f, r0, r0 * 0.8f, r0 * 0.3f }, 7, 2f, 0.25f, R(-0.05f, 0.05f));

            // Dead branch stubs on the lower trunk: they catch the lantern light.
            int stubs = rng.Next(1, 5);
            for (int i = 0; i < stubs; i++)
            {
                float y = R(1.8f, Mathf.Min(7f, h * 0.45f));
                float yaw = R(0f, 360f);
                var dir = Quaternion.Euler(R(-65f, -35f), yaw, 0f);
                var at = origin + lean * new Vector3(0f, y, 0f);
                bark.AddTube(at, lean * dir * Quaternion.Euler(90f, 0f, 0f), new[] { 0f, R(0.6f, 1.6f) }, new[] { r0 * 0.22f, 0.015f }, 4, 1f, 0.5f);
            }

            var leaf = B(origin, mats.foliage);
            int layers = 4;
            for (int i = 0; i < layers; i++)
            {
                float k = i / (float)(layers - 1);
                float y = Mathf.Lerp(h * 0.42f, h * 0.88f, k);
                float rad = Mathf.Lerp(2.5f, 0.9f, k) * scale * R(0.85f, 1.15f);
                leaf.AddCone(origin + lean * new Vector3(0f, y, 0f), lean * Quaternion.Euler(0f, R(0f, 360f), 0f), rad, R(3.0f, 4.0f) * scale, 8);
            }

            if (collider)
                Prims.Capsule(ColliderHost(origin), new Vector3(p.x, h * 0.5f, p.y), r0 * 1.05f + 0.05f, h);
        }

        // ------------------------------------------------------------------ undergrowth

        void BuildUndergrowth()
        {
            float h = halfSize + 6f;
            for (int i = 0; i < 1500; i++)
            {
                var p = new Vector2(R(-h, h), R(-h, h));
                if (InClearing(p, 0.8f)) continue;
                float r = R(0.35f, 0.95f);
                var c = new Vector3(p.x, r * 0.35f, p.y);
                B(c, mats.bush).AddBlob(c, new Vector3(r, r * 0.65f, r * R(0.8f, 1.2f)), Quaternion.Euler(0f, R(0f, 360f), 0f), 7, 4, 0.28f, rng);
            }
            for (int i = 0; i < 380; i++)
            {
                var p = new Vector2(R(-h, h), R(-h, h));
                if (InClearing(p, 0.6f)) continue;
                float r = R(0.15f, 0.7f);
                var c = new Vector3(p.x, r * 0.15f, p.y);
                B(c, mats.stone).AddBlob(c, new Vector3(r, r * R(0.4f, 0.7f), r * R(0.7f, 1.1f)), Quaternion.Euler(R(-10, 10), R(0f, 360f), R(-10, 10)), 6, 4, 0.3f, rng);
            }
            for (int i = 0; i < 1600; i++)
            {
                var p = new Vector2(R(-h, h), R(-h, h));
                if (InClearing(p, 0.5f)) continue;
                var c = new Vector3(p.x, 0f, p.y);
                var mb = B(c, mats.moss);
                int blades = rng.Next(3, 6);
                for (int k = 0; k < blades; k++)
                {
                    var o = c + new Vector3(R(-0.15f, 0.15f), 0f, R(-0.15f, 0.15f));
                    mb.AddCone(o, Quaternion.Euler(R(-25f, 25f), R(0f, 360f), R(-25f, 25f)), R(0.03f, 0.06f), R(0.3f, 0.75f), 3);
                }
            }
            for (int i = 0; i < 420; i++)
            {
                var p = new Vector2(R(-h, h), R(-h, h));
                if (InClearing(p, 0.7f)) continue;
                var c = new Vector3(p.x, 0.05f, p.y);
                float len = R(0.8f, 3.2f);
                B(c, mats.bark).AddTube(c, Quaternion.Euler(0f, R(0f, 360f), 0f) * Quaternion.Euler(0f, 0f, 90f - R(-6f, 6f)),
                    new[] { -len * 0.5f, len * 0.5f }, new[] { R(0.03f, 0.09f), 0.02f }, 5, 1f, 0.5f);
            }
        }

        // ------------------------------------------------------------------ landmarks

        void BuildLandmark(Vector3 pos, LandmarkType type)
        {
            float yaw = R(0f, 360f);
            var g = Prims.Group(root, "Landmark_" + type, pos, yaw);
            switch (type)
            {
                case LandmarkType.Jizo:
                    for (int i = -1; i <= 1; i++) Jizo(g, new Vector3(i * 0.75f, 0f, 0f), R(-8f, 8f));
                    Prims.Part(g, PrimitiveType.Cube, new Vector3(0f, 0.06f, 0f), new Vector3(2.6f, 0.12f, 0.8f), Vector3.zero, mats.stone);
                    Prims.Box(g, new Vector3(0f, 0.5f, 0f), new Vector3(2.6f, 1f, 0.8f));
                    break;
                case LandmarkType.StoneLanterns:
                    StoneLantern(g, new Vector3(-1.4f, 0f, 0f), false);
                    StoneLantern(g, new Vector3(1.4f, 0f, 0f), false);
                    break;
                case LandmarkType.Hokora:
                    Hokora(g);
                    break;
                case LandmarkType.SacredTree:
                    SacredTree(g);
                    break;
                case LandmarkType.FallenLog:
                    Prims.Part(g, PrimitiveType.Cylinder, new Vector3(0f, 0.4f, 0f), new Vector3(0.8f, 3.6f, 0.8f), new Vector3(0f, 0f, 90f), mats.bark);
                    Prims.Part(g, PrimitiveType.Cylinder, new Vector3(3.7f, 0.5f, 0f), new Vector3(1.4f, 0.5f, 1.4f), new Vector3(0f, 0f, 90f), mats.bark);
                    Prims.Part(g, PrimitiveType.Sphere, new Vector3(-1.2f, 0.75f, 0.1f), new Vector3(0.7f, 0.25f, 0.6f), Vector3.zero, mats.moss);
                    Prims.Capsule(g, new Vector3(0.2f, 0.45f, 0f), 0.5f, 8.2f, 0);
                    break;
                case LandmarkType.Well:
                    Prims.Part(g, PrimitiveType.Cylinder, new Vector3(0f, 0.4f, 0f), new Vector3(1.7f, 0.4f, 1.7f), Vector3.zero, mats.stone);
                    Prims.Part(g, PrimitiveType.Cylinder, new Vector3(0f, 0.42f, 0f), new Vector3(1.35f, 0.4f, 1.35f), Vector3.zero, mats.monsterBody, false);
                    Prims.Part(g, PrimitiveType.Cube, new Vector3(-1.0f, 1.1f, 0f), new Vector3(0.12f, 2.2f, 0.12f), Vector3.zero, mats.wood);
                    Prims.Part(g, PrimitiveType.Cube, new Vector3(1.0f, 1.1f, 0f), new Vector3(0.12f, 2.2f, 0.12f), Vector3.zero, mats.wood);
                    Prims.Part(g, PrimitiveType.Cube, new Vector3(0f, 2.15f, 0f), new Vector3(2.3f, 0.12f, 0.14f), Vector3.zero, mats.wood);
                    Prims.Part(g, PrimitiveType.Cube, new Vector3(0f, 1.55f, 0f), new Vector3(0.02f, 1.1f, 0.02f), Vector3.zero, mats.straw, false);
                    Prims.Capsule(g, new Vector3(0f, 1f, 0f), 0.9f, 2f);
                    break;
                case LandmarkType.Graves:
                    for (int i = 0; i < 6; i++)
                    {
                        var p = new Vector3((i % 3 - 1) * 1.1f + R(-0.15f, 0.15f), 0f, (i / 3) * 1.3f - 0.65f);
                        float hh = R(0.6f, 1.0f);
                        Prims.Part(g, PrimitiveType.Cube, p + new Vector3(0f, hh * 0.5f, 0f), new Vector3(0.3f, hh, 0.22f), new Vector3(R(-8f, 8f), R(-10f, 10f), R(-8f, 8f)), mats.stone);
                        Prims.Part(g, PrimitiveType.Cube, p + new Vector3(0f, 0.05f, 0f), new Vector3(0.5f, 0.1f, 0.4f), Vector3.zero, mats.stone);
                    }
                    Prims.Box(g, new Vector3(0f, 0.5f, 0f), new Vector3(3.2f, 1f, 2.6f));
                    break;
                case LandmarkType.Stumps:
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i / 5f * Mathf.PI * 2f, d = R(1.0f, 2.2f);
                        float sr = R(0.35f, 0.6f), sh = R(0.3f, 0.7f);
                        var p = new Vector3(Mathf.Cos(a) * d, sh * 0.5f, Mathf.Sin(a) * d);
                        Prims.Part(g, PrimitiveType.Cylinder, p, new Vector3(sr * 2f, sh * 0.5f, sr * 2f), Vector3.zero, mats.bark);
                        Prims.Capsule(g, new Vector3(p.x, 0.5f, p.z), sr, 1f);
                    }
                    break;
            }
        }

        void Jizo(Transform g, Vector3 p, float yaw)
        {
            var j = Prims.Group(g, "Jizo", p, yaw);
            Prims.Part(j, PrimitiveType.Capsule, new Vector3(0f, 0.42f, 0f), new Vector3(0.36f, 0.3f, 0.3f), Vector3.zero, mats.stone);
            Prims.Part(j, PrimitiveType.Sphere, new Vector3(0f, 0.86f, 0f), new Vector3(0.27f, 0.29f, 0.27f), Vector3.zero, mats.stone);
            Prims.Part(j, PrimitiveType.Cube, new Vector3(0f, 0.55f, 0.11f), new Vector3(0.3f, 0.24f, 0.12f), new Vector3(-12f, 0f, 0f), mats.torii);
            Prims.Part(j, PrimitiveType.Sphere, new Vector3(0f, 1.0f, 0f), new Vector3(0.18f, 0.08f, 0.18f), Vector3.zero, mats.moss);
        }

        void StoneLantern(Transform g, Vector3 p, bool toppled)
        {
            var l = Prims.Group(g, "Toro", p, R(-5f, 5f));
            if (toppled) { l.localRotation = Quaternion.Euler(0f, R(0f, 360f), 0f) * Quaternion.Euler(78f, 0f, 0f); l.localPosition += Vector3.up * 0.3f; }
            Prims.Part(l, PrimitiveType.Cylinder, new Vector3(0f, 0.08f, 0f), new Vector3(0.6f, 0.08f, 0.6f), Vector3.zero, mats.stone);
            Prims.Part(l, PrimitiveType.Cylinder, new Vector3(0f, 0.6f, 0f), new Vector3(0.24f, 0.45f, 0.24f), Vector3.zero, mats.stone);
            Prims.Part(l, PrimitiveType.Cube, new Vector3(0f, 1.2f, 0f), new Vector3(0.44f, 0.34f, 0.44f), Vector3.zero, mats.stone);
            Prims.Part(l, PrimitiveType.Cube, new Vector3(0f, 1.2f, 0f), new Vector3(0.46f, 0.16f, 0.2f), Vector3.zero, mats.monsterBody, false);
            Prims.Part(l, PrimitiveType.Cube, new Vector3(0f, 1.44f, 0f), new Vector3(0.75f, 0.13f, 0.75f), new Vector3(0f, 45f, 0f), mats.stone);
            Prims.Part(l, PrimitiveType.Sphere, new Vector3(0f, 1.58f, 0f), new Vector3(0.16f, 0.18f, 0.16f), Vector3.zero, mats.stone);
            if (!toppled) Prims.Capsule(l, new Vector3(0f, 0.8f, 0f), 0.35f, 1.6f);
        }

        void Hokora(Transform g)
        {
            Prims.Part(g, PrimitiveType.Cube, new Vector3(0f, 0.2f, 0f), new Vector3(1.2f, 0.4f, 1.0f), Vector3.zero, mats.stone);
            Prims.Part(g, PrimitiveType.Cube, new Vector3(0f, 0.8f, 0f), new Vector3(0.8f, 0.8f, 0.65f), Vector3.zero, mats.wood);
            Prims.Part(g, PrimitiveType.Cube, new Vector3(0f, 0.75f, 0.33f), new Vector3(0.5f, 0.55f, 0.02f), Vector3.zero, mats.monsterBody, false);
            Prims.Part(g, PrimitiveType.Cube, new Vector3(0.3f, 1.35f, 0f), new Vector3(0.75f, 0.06f, 1.05f), new Vector3(0f, 0f, -32f), mats.wood);
            Prims.Part(g, PrimitiveType.Cube, new Vector3(-0.3f, 1.35f, 0f), new Vector3(0.75f, 0.06f, 1.05f), new Vector3(0f, 0f, 32f), mats.wood);
            Prims.Part(g, PrimitiveType.Cube, new Vector3(0f, 0.95f, 0.36f), new Vector3(0.7f, 0.04f, 0.04f), Vector3.zero, mats.straw);
            for (int i = 0; i < 3; i++)
                Prims.Part(g, PrimitiveType.Cube, new Vector3(-0.22f + i * 0.22f, 0.82f, 0.37f), new Vector3(0.05f, 0.2f, 0.01f), new Vector3(0f, 0f, 10f), mats.paper, false);
            Prims.Box(g, new Vector3(0f, 0.75f, 0f), new Vector3(1.2f, 1.5f, 1.0f));
        }

        void SacredTree(Transform g)
        {
            float r = 1.15f, h = 30f;
            var bark = new MeshBuilder();
            bark.AddTube(Vector3.zero, Quaternion.identity, new[] { -0.2f, 0.6f, 2.0f, h * 0.5f, h }, new[] { r * 1.6f, r * 1.15f, r, r * 0.8f, r * 0.3f }, 10, 3f, 0.25f);
            var leaf = new MeshBuilder();
            for (int i = 0; i < 5; i++)
            {
                float k = i / 4f;
                leaf.AddCone(new Vector3(0f, Mathf.Lerp(h * 0.4f, h * 0.88f, k), 0f), Quaternion.Euler(0f, i * 37f, 0f), Mathf.Lerp(4.5f, 1.4f, k), 5f, 9);
            }
            AttachMesh(g, "Trunk", bark.ToMesh("SacredTrunk"), mats.bark, true);
            AttachMesh(g, "Canopy", leaf.ToMesh("SacredCanopy"), mats.foliage, false);
            // Shimenawa rope and shide paper.
            Prims.Part(g, PrimitiveType.Cylinder, new Vector3(0f, 2.3f, 0f), new Vector3(r * 2.25f, 0.13f, r * 2.25f), Vector3.zero, mats.straw);
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * 360f;
                var q = Quaternion.Euler(0f, a, 0f);
                Prims.Part(g, PrimitiveType.Cube, q * new Vector3(0f, 2.0f, r * 1.14f), new Vector3(0.12f, 0.42f, 0.01f), new Vector3(0f, a, 8f), mats.paper, false);
            }
            Prims.Capsule(g, new Vector3(0f, h * 0.5f, 0f), r * 1.1f, h);
        }

        void AttachMesh(Transform parent, string name, Mesh mesh, Material mat, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ exits

        static float FacingCenterYaw(Vector3 p)
        {
            // Torii front faces the map centre (the way the boy arrives from).
            var d = -p; d.y = 0f;
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        void Torii(Transform g, bool broken)
        {
            if (!broken)
            {
                Prims.Part(g, PrimitiveType.Cylinder, new Vector3(-1.9f, 2.1f, 0f), new Vector3(0.42f, 2.1f, 0.42f), Vector3.zero, mats.torii);
                Prims.Part(g, PrimitiveType.Cylinder, new Vector3(1.9f, 2.1f, 0f), new Vector3(0.42f, 2.1f, 0.42f), Vector3.zero, mats.torii);
                Prims.Part(g, PrimitiveType.Cube, new Vector3(0f, 4.35f, 0f), new Vector3(5.6f, 0.28f, 0.5f), Vector3.zero, mats.torii);
                Prims.Part(g, PrimitiveType.Cube, new Vector3(-2.75f, 4.48f, 0f), new Vector3(0.8f, 0.24f, 0.5f), new Vector3(0f, 0f, -9f), mats.torii);
                Prims.Part(g, PrimitiveType.Cube, new Vector3(2.75f, 4.48f, 0f), new Vector3(0.8f, 0.24f, 0.5f), new Vector3(0f, 0f, 9f), mats.torii);
                Prims.Part(g, PrimitiveType.Cube, new Vector3(0f, 4.15f, 0f), new Vector3(5.0f, 0.14f, 0.42f), Vector3.zero, mats.monsterBody);
                Prims.Part(g, PrimitiveType.Cube, new Vector3(0f, 3.5f, 0f), new Vector3(4.7f, 0.22f, 0.26f), Vector3.zero, mats.torii);
                Prims.Part(g, PrimitiveType.Cube, new Vector3(0f, 3.8f, 0f), new Vector3(0.22f, 0.55f, 0.2f), Vector3.zero, mats.torii);
                Prims.Part(g, PrimitiveType.Cylinder, new Vector3(-1.9f, 0.15f, 0f), new Vector3(0.52f, 0.15f, 0.52f), Vector3.zero, mats.monsterBody);
                Prims.Part(g, PrimitiveType.Cylinder, new Vector3(1.9f, 0.15f, 0f), new Vector3(0.52f, 0.15f, 0.52f), Vector3.zero, mats.monsterBody);
                Prims.Capsule(g, new Vector3(-1.9f, 2.1f, 0f), 0.25f, 4.2f);
                Prims.Capsule(g, new Vector3(1.9f, 2.1f, 0f), 0.25f, 4.2f);
            }
            else
            {
                Prims.Part(g, PrimitiveType.Cylinder, new Vector3(-1.9f, 1.4f, 0f), new Vector3(0.42f, 1.4f, 0.42f), new Vector3(0f, 0f, 4f), mats.torii);
                Prims.Part(g, PrimitiveType.Cylinder, new Vector3(2.4f, 0.22f, 1.2f), new Vector3(0.42f, 2.0f, 0.42f), new Vector3(84f, 30f, 0f), mats.torii);
                Prims.Part(g, PrimitiveType.Cube, new Vector3(0.4f, 0.9f, -0.6f), new Vector3(5.6f, 0.28f, 0.5f), new Vector3(8f, 12f, -20f), mats.torii);
                Prims.Part(g, PrimitiveType.Cube, new Vector3(0.6f, 0.12f, 0.8f), new Vector3(4.7f, 0.22f, 0.26f), new Vector3(0f, -25f, 0f), mats.torii);
                Prims.Capsule(g, new Vector3(-1.9f, 1.4f, 0f), 0.25f, 2.8f);
                Prims.Box(g, new Vector3(1.2f, 0.5f, 0.2f), new Vector3(4.5f, 1f, 2f));
            }
        }

        void BuildExit(Vector3 pos)
        {
            var g = Prims.Group(root, "Exit_Torii", pos, FacingCenterYaw(pos));
            Torii(g, false);
            StoneLantern(g, new Vector3(-3.6f, 0f, 3.2f), false);
            StoneLantern(g, new Vector3(3.6f, 0f, 3.2f), false);
            for (int i = 0; i < 9; i++)
                Prims.Part(g, PrimitiveType.Cube, new Vector3((i % 2 - 0.5f) * 0.3f, 0.03f, 1.5f + i * 1.1f), new Vector3(0.9f, 0.06f, 0.7f), new Vector3(0f, (i * 23) % 17 - 8f, 0f), mats.stone, false);
            // A hanging paper lantern under the gate: the only fixed light in the forest.
            var lamp = Prims.Part(g, PrimitiveType.Sphere, new Vector3(0f, 2.9f, 0f), new Vector3(0.42f, 0.55f, 0.42f), Vector3.zero, mats.lanternGlow, false);
            Prims.Part(g, PrimitiveType.Cube, new Vector3(0f, 3.25f, 0f), new Vector3(0.02f, 0.3f, 0.02f), Vector3.zero, mats.lanternFrame, false);
            var lg = new GameObject("GateLight").AddComponent<Light>();
            lg.transform.SetParent(lamp, false);
            lg.type = LightType.Point;
            lg.color = new Color(1f, 0.62f, 0.32f);
            lg.range = 9f;
            lg.intensity = 6f;
            lg.shadows = LightShadows.None;
        }

        void BuildBrokenTorii(Vector3 pos)
        {
            var g = Prims.Group(root, "Broken_Torii", pos, FacingCenterYaw(pos) + R(-20f, 20f));
            Torii(g, true);
            StoneLantern(g, new Vector3(-3.6f, 0f, 3.2f), true);
            StoneLantern(g, new Vector3(3.6f, 0f, 3.2f), false);
        }
    }
}
