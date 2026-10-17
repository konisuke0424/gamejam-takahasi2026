using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace KaiForest.EditorTools
{
    /// <summary>
    /// Turns the procedural forest into real assets and scene objects:
    /// cedar variant prefabs, landmark / torii prefabs, and Forest.unity with every tree and
    /// landmark placed as a prefab instance. After the first bake the scene is meant to be edited
    /// by hand; "Rebake Undergrowth" is the only step that is safe to re-run on an edited map.
    /// </summary>
    public static class ForestBaker
    {
        public const string ScenePath = "Assets/Scenes/Forest.unity";
        const string MapDir = "Assets/Game/Map";
        const string PrefabDir = MapDir + "/Prefabs";
        const string GenDir = MapDir + "/Generated";
        const string CedarMeshPath = GenDir + "/CedarMeshes.asset";
        const string SacredMeshPath = GenDir + "/SacredTreeMeshes.asset";
        const string UndergrowthPath = GenDir + "/Undergrowth.asset";

        const int Seed = 20261017;
        const float HalfSize = 120f;
        const float TreeSpacing = 3.7f;
        const int CedarVariants = 8;
        const int LandmarkCount = 12;
        const float UndergrowthChunk = 40f;

        static readonly Vector3 StartPos = new Vector3(0f, 0f, -100f);
        static readonly Vector3[] ExitSpots = { new Vector3(-62f, 0f, 98f), new Vector3(0f, 0f, 104f), new Vector3(62f, 0f, 98f) };
        const int RealExit = 2; // the other spots get broken torii

        static readonly LandmarkType[] LandmarkCycle =
        {
            LandmarkType.Jizo, LandmarkType.StoneLanterns, LandmarkType.Hokora, LandmarkType.SacredTree,
            LandmarkType.FallenLog, LandmarkType.Well, LandmarkType.Graves, LandmarkType.Stumps,
        };

        static System.Random rng;
        static MaterialLibrary mats;

        static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

        // ------------------------------------------------------------------ menu

        [MenuItem("KaiForest/Map/Rebuild Forest Scene From Seed (overwrites hand edits)")]
        static void MenuRebuild()
        {
            if (!EditorUtility.DisplayDialog("Forest を作り直す",
                    "Forest.unity をシードから作り直します。\n手で動かした・追加した木や目印はすべて失われます。", "作り直す", "やめる")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildForestScene(LoadMats());
        }

        [MenuItem("KaiForest/Map/Rebake Undergrowth")]
        static void MenuRebakeUndergrowth()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            ForestMap map = null;
            foreach (var go in scene.GetRootGameObjects())
                if ((map = go.GetComponentInChildren<ForestMap>()) != null) break;
            if (map == null) { Debug.LogError("[KaiForest] ForestMap not found in Forest.unity"); return; }
            mats = LoadMats();
            BakeUndergrowth(map, scene);
            EditorSceneManager.SaveScene(scene);
        }

        [MenuItem("KaiForest/Map/Regenerate Placeholder Prefabs (overwrites)")]
        static void MenuRegeneratePrefabs()
        {
            if (!EditorUtility.DisplayDialog("プレハブを作り直す",
                    "木・目印・鳥居のプレハブを仮の見た目で作り直します。\nプレハブを手で差し替えていた場合、その変更は失われます。", "作り直す", "やめる")) return;
            EnsurePrefabs(LoadMats(), true);
        }

        static MaterialLibrary LoadMats() => AssetDatabase.LoadAssetAtPath<MaterialLibrary>("Assets/Game/Data/MaterialLibrary.asset");

        // ------------------------------------------------------------------ prefabs

        static string CedarPath(int i) => $"{PrefabDir}/Trees/Cedar_{i + 1:00}.prefab";
        static string LandmarkPath(LandmarkType t) => $"{PrefabDir}/Landmarks/{t}.prefab";
        static string ExitPath => $"{PrefabDir}/Landmarks/Torii_Exit.prefab";

        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            Folder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }

        /// <summary>Creates any missing prefab (or all of them when overwrite is set).</summary>
        public static void EnsurePrefabs(MaterialLibrary m, bool overwrite = false)
        {
            mats = m;
            Folder(PrefabDir + "/Trees");
            Folder(PrefabDir + "/Landmarks");
            Folder(GenDir);

            bool cedars = overwrite;
            for (int i = 0; i < CedarVariants; i++) cedars |= AssetDatabase.LoadAssetAtPath<GameObject>(CedarPath(i)) == null;
            if (cedars) BuildCedarPrefabs();

            foreach (LandmarkType t in System.Enum.GetValues(typeof(LandmarkType)))
                if (overwrite || AssetDatabase.LoadAssetAtPath<GameObject>(LandmarkPath(t)) == null) BuildLandmarkPrefab(t);
            if (overwrite || AssetDatabase.LoadAssetAtPath<GameObject>(ExitPath) == null) BuildExitPrefab();
            AssetDatabase.SaveAssets();
        }

        static void SaveMeshes(string path, List<Mesh> meshes)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(meshes[0], path);
            for (int i = 1; i < meshes.Count; i++) AssetDatabase.AddObjectToAsset(meshes[i], path);
            AssetDatabase.SaveAssets();
        }

        static GameObject SavePrefab(GameObject go, string path)
        {
            var p = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return p;
        }

        static Transform AttachMesh(Transform parent, string name, Mesh mesh, Material mat, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go.transform;
        }

        static void BuildCedarPrefabs()
        {
            rng = new System.Random(Seed + 7);
            var meshes = new List<Mesh>();
            var dims = new List<Vector2>();
            for (int v = 0; v < CedarVariants; v++)
            {
                // Spread the variants over thin/thick and short/tall so random picks still look varied.
                float r0 = Mathf.Lerp(0.2f, 0.42f, (v % 4) / 3f) * R(0.92f, 1.08f);
                float h = Mathf.Lerp(13f, 21f, (v / 4 + (v % 2) * 0.5f) / 1.5f) * R(0.95f, 1.05f);
                dims.Add(new Vector2(r0, h));

                var bark = new MeshBuilder();
                bark.AddTube(Vector3.zero, Quaternion.identity,
                    new[] { -0.2f, 0.35f, 1.2f, h * 0.5f, h },
                    new[] { r0 * 1.5f, r0 * 1.12f, r0, r0 * 0.8f, r0 * 0.3f }, 7, 2f, 0.25f, R(-0.05f, 0.05f));
                int stubs = 1 + v % 4;
                for (int i = 0; i < stubs; i++)
                {
                    float y = R(1.8f, Mathf.Min(7f, h * 0.45f));
                    var dir = Quaternion.Euler(R(-65f, -35f), R(0f, 360f), 0f);
                    bark.AddTube(new Vector3(0f, y, 0f), dir * Quaternion.Euler(90f, 0f, 0f), new[] { 0f, R(0.6f, 1.6f) }, new[] { r0 * 0.22f, 0.015f }, 4, 1f, 0.5f);
                }
                var leaf = new MeshBuilder();
                for (int i = 0; i < 4; i++)
                {
                    float k = i / 3f;
                    leaf.AddCone(new Vector3(0f, Mathf.Lerp(h * 0.42f, h * 0.88f, k), 0f), Quaternion.Euler(0f, R(0f, 360f), 0f),
                        Mathf.Lerp(2.5f, 0.9f, k) * R(0.85f, 1.15f), R(3.0f, 4.0f), 8);
                }
                meshes.Add(bark.ToMesh($"Cedar_{v + 1:00}_Trunk"));
                meshes.Add(leaf.ToMesh($"Cedar_{v + 1:00}_Canopy"));
            }
            SaveMeshes(CedarMeshPath, meshes);

            for (int v = 0; v < CedarVariants; v++)
            {
                var go = new GameObject($"Cedar_{v + 1:00}");
                go.AddComponent<ForestTree>();
                AttachMesh(go.transform, "Trunk", meshes[v * 2], mats.bark, true);
                AttachMesh(go.transform, "Canopy", meshes[v * 2 + 1], mats.foliage, false);
                Prims.Capsule(go.transform, new Vector3(0f, dims[v].y * 0.5f, 0f), dims[v].x * 1.05f + 0.05f, dims[v].y);
                SavePrefab(go, CedarPath(v));
            }
        }

        static void BuildLandmarkPrefab(LandmarkType type)
        {
            rng = new System.Random(Seed + 101 * ((int)type + 1));
            var g = new GameObject(type.ToString()).transform;
            g.gameObject.AddComponent<Landmark>().type = type;
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
                case LandmarkType.BrokenTorii:
                    Torii(g, true);
                    StoneLantern(g, new Vector3(-3.6f, 0f, 3.2f), true);
                    StoneLantern(g, new Vector3(3.6f, 0f, 3.2f), false);
                    break;
            }
            SavePrefab(g.gameObject, LandmarkPath(type));
        }

        static void BuildExitPrefab()
        {
            rng = new System.Random(Seed + 5000);
            var g = new GameObject("Torii_Exit").transform;
            g.gameObject.AddComponent<MapExit>();
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
            SavePrefab(g.gameObject, ExitPath);
        }

        static void Jizo(Transform g, Vector3 p, float yaw)
        {
            var j = Prims.Group(g, "Jizo", p, yaw);
            Prims.Part(j, PrimitiveType.Capsule, new Vector3(0f, 0.42f, 0f), new Vector3(0.36f, 0.3f, 0.3f), Vector3.zero, mats.stone);
            Prims.Part(j, PrimitiveType.Sphere, new Vector3(0f, 0.86f, 0f), new Vector3(0.27f, 0.29f, 0.27f), Vector3.zero, mats.stone);
            Prims.Part(j, PrimitiveType.Cube, new Vector3(0f, 0.55f, 0.11f), new Vector3(0.3f, 0.24f, 0.12f), new Vector3(-12f, 0f, 0f), mats.torii);
            Prims.Part(j, PrimitiveType.Sphere, new Vector3(0f, 1.0f, 0f), new Vector3(0.18f, 0.08f, 0.18f), Vector3.zero, mats.moss);
        }

        static void StoneLantern(Transform g, Vector3 p, bool toppled)
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

        static void Hokora(Transform g)
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

        static void SacredTree(Transform g)
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
            var trunk = bark.ToMesh("SacredTree_Trunk");
            var canopy = leaf.ToMesh("SacredTree_Canopy");
            SaveMeshes(SacredMeshPath, new List<Mesh> { trunk, canopy });
            AttachMesh(g, "Trunk", trunk, mats.bark, true);
            AttachMesh(g, "Canopy", canopy, mats.foliage, false);
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

        static void Torii(Transform g, bool broken)
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

        // ------------------------------------------------------------------ scene

        /// <summary>Creates Forest.unity from the seed (overwrites it). Leaves the scene open.</summary>
        public static void BuildForestScene(MaterialLibrary m)
        {
            EnsurePrefabs(m);
            mats = m;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Bright, fog-free lighting for editing. At runtime Main.unity's GameManager applies the night look.
            RenderSettings.fog = false;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.34f, 0.38f);

            var root = new GameObject("ForestMap").transform;
            var map = root.gameObject.AddComponent<ForestMap>();
            map.halfSize = HalfSize;

            var preview = new GameObject("EditorPreviewLight") { tag = "EditorOnly" };
            preview.transform.SetParent(root, false);
            preview.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var pl = preview.AddComponent<Light>();
            pl.type = LightType.Directional;
            pl.intensity = 0.9f;
            pl.shadows = LightShadows.None;
            preview.AddComponent<EditorPreviewLight>();

            var ground = Prims.Part(root, PrimitiveType.Plane, Vector3.zero, new Vector3((HalfSize + 40f) / 5f, 1f, (HalfSize + 40f) / 5f), Vector3.zero, mats.ground, false);
            ground.name = "Ground";
            Prims.Box(ground, new Vector3(0f, -0.5f, 0f), new Vector3(10f, 1f, 10f));

            var walls = new GameObject("Walls").transform;
            walls.SetParent(root, false);
            float hw = HalfSize + 0.5f, len = HalfSize * 2f + 4f;
            Prims.Box(walls, new Vector3(0, 5, hw), new Vector3(len, 10, 1));
            Prims.Box(walls, new Vector3(0, 5, -hw), new Vector3(len, 10, 1));
            Prims.Box(walls, new Vector3(hw, 5, 0), new Vector3(1, 10, len));
            Prims.Box(walls, new Vector3(-hw, 5, 0), new Vector3(1, 10, len));

            var start = new GameObject("PlayerStart").transform;
            start.SetParent(root, false);
            start.position = StartPos;
            map.playerStart = start;

            // Same layout as the old runtime generator: landmark spots first, then the Poisson trees.
            rng = new System.Random(Seed);
            var clearings = new List<KeyValuePair<Vector2, float>> { new KeyValuePair<Vector2, float>(Flat(StartPos), 5f) };
            foreach (var e in ExitSpots) clearings.Add(new KeyValuePair<Vector2, float>(Flat(e), 11f));
            var spots = PlaceLandmarkSpots(clearings);

            var cedarPrefabs = new GameObject[CedarVariants];
            for (int i = 0; i < CedarVariants; i++) cedarPrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(CedarPath(i));
            var treesRoot = new GameObject("Trees").transform;
            treesRoot.SetParent(root, false);
            var areas = new Dictionary<(int, int), Transform>();

            void PlaceTree(Vector2 p)
            {
                var key = (Mathf.FloorToInt(p.x / UndergrowthChunk), Mathf.FloorToInt(p.y / UndergrowthChunk));
                if (!areas.TryGetValue(key, out var area))
                {
                    area = new GameObject($"Area_{key.Item1}_{key.Item2}").transform;
                    area.SetParent(treesRoot, false);
                    areas[key] = area;
                }
                var t = ((GameObject)PrefabUtility.InstantiatePrefab(cedarPrefabs[rng.Next(CedarVariants)], scene)).transform;
                t.SetParent(area, false);
                t.SetPositionAndRotation(new Vector3(p.x, 0f, p.y), Quaternion.Euler(R(-2.5f, 2.5f), R(0f, 360f), R(-2.5f, 2.5f)));
                t.localScale = Vector3.one * R(0.9f, 1.1f);
            }

            float outer = HalfSize + 14f;
            foreach (var p in Poisson(new Vector2(-outer, -outer), new Vector2(outer, outer), TreeSpacing))
                if (!InClearing(clearings, p, 1f)) PlaceTree(p);
            // Dense ring outside the walls so the edge reads as impassable forest.
            for (float t = -outer; t < outer; t += 1.6f)
            {
                for (int side = 0; side < 4; side++)
                {
                    float depth = R(1.5f, 9f), along = t + R(-0.6f, 0.6f), off = HalfSize + depth;
                    PlaceTree(side switch
                    {
                        0 => new Vector2(along, off),
                        1 => new Vector2(along, -off),
                        2 => new Vector2(off, along),
                        _ => new Vector2(-off, along),
                    });
                }
            }

            var lmRoot = new GameObject("Landmarks").transform;
            lmRoot.SetParent(root, false);
            for (int i = 0; i < spots.Count; i++)
            {
                var type = LandmarkCycle[i % LandmarkCycle.Length];
                Place(LandmarkPath(type), lmRoot, new Vector3(spots[i].x, 0f, spots[i].y), R(0f, 360f), scene);
            }
            for (int i = 0; i < ExitSpots.Length; i++)
            {
                float yaw = FacingCenterYaw(ExitSpots[i]);
                if (i == RealExit) map.exit = Place(ExitPath, lmRoot, ExitSpots[i], yaw, scene).GetComponent<MapExit>();
                else Place(LandmarkPath(LandmarkType.BrokenTorii), lmRoot, ExitSpots[i], yaw + R(-20f, 20f), scene);
            }

            BakeUndergrowth(map, scene);
            Folder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[KaiForest] Forest scene baked: {treesRoot.GetComponentsInChildren<ForestTree>().Length} trees");
        }

        static Transform Place(string prefabPath, Transform parent, Vector3 pos, float yaw, Scene scene)
        {
            var t = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), scene)).transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            return t;
        }

        static float FacingCenterYaw(Vector3 p) => Mathf.Atan2(-p.x, -p.z) * Mathf.Rad2Deg;

        static bool InClearing(List<KeyValuePair<Vector2, float>> clearings, Vector2 p, float scale)
        {
            foreach (var c in clearings)
                if ((p - c.Key).sqrMagnitude < c.Value * c.Value * scale * scale) return true;
            return false;
        }

        static List<Vector2> PlaceLandmarkSpots(List<KeyValuePair<Vector2, float>> clearings)
        {
            var spots = new List<Vector2>();
            int tries = 0;
            while (spots.Count < LandmarkCount && tries++ < 4000)
            {
                var p = new Vector2(R(-HalfSize + 15f, HalfSize - 15f), R(-HalfSize + 15f, HalfSize - 20f));
                bool ok = true;
                foreach (var s in spots) if ((s - p).sqrMagnitude < 32f * 32f) { ok = false; break; }
                foreach (var c in clearings) if ((c.Key - p).sqrMagnitude < (c.Value + 22f) * (c.Value + 22f)) { ok = false; break; }
                if (ok) spots.Add(p);
            }
            for (int i = 0; i < spots.Count; i++)
                clearings.Add(new KeyValuePair<Vector2, float>(spots[i], LandmarkCycle[i % LandmarkCycle.Length] == LandmarkType.SacredTree ? 7f : 5f));
            return spots;
        }

        static List<Vector2> Poisson(Vector2 min, Vector2 max, float r)
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
                grid[(int)((p.y - min.y) / cell) * gw + (int)((p.x - min.x) / cell)] = pts.Count - 1;
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

        // ------------------------------------------------------------------ undergrowth

        /// <summary>
        /// Scatters bushes, rocks, grass and fallen branches into one combined mesh per 40 m area and
        /// material, keeping clear of the start, the exit and every placed landmark (wherever they are now).
        /// </summary>
        static void BakeUndergrowth(ForestMap map, Scene scene)
        {
            var clearings = new List<KeyValuePair<Vector2, float>>();
            if (map.playerStart != null) clearings.Add(new KeyValuePair<Vector2, float>(Flat(map.playerStart.position), 5f));
            foreach (var go in scene.GetRootGameObjects())
            {
                foreach (var e in go.GetComponentsInChildren<MapExit>()) clearings.Add(new KeyValuePair<Vector2, float>(Flat(e.transform.position), 11f));
                foreach (var l in go.GetComponentsInChildren<Landmark>()) clearings.Add(new KeyValuePair<Vector2, float>(Flat(l.transform.position), l.ClearingRadius));
            }

            rng = new System.Random(Seed + 99);
            var builders = new Dictionary<(int, int, Material), MeshBuilder>();
            MeshBuilder B(Vector3 p, Material mat)
            {
                var key = (Mathf.FloorToInt(p.x / UndergrowthChunk), Mathf.FloorToInt(p.z / UndergrowthChunk), mat);
                if (!builders.TryGetValue(key, out var b)) builders[key] = b = new MeshBuilder();
                return b;
            }

            float h = map.halfSize + 6f;
            for (int i = 0; i < 1500; i++)
            {
                var p = new Vector2(R(-h, h), R(-h, h));
                if (InClearing(clearings, p, 0.8f)) continue;
                float r = R(0.35f, 0.95f);
                var c = new Vector3(p.x, r * 0.35f, p.y);
                B(c, mats.bush).AddBlob(c, new Vector3(r, r * 0.65f, r * R(0.8f, 1.2f)), Quaternion.Euler(0f, R(0f, 360f), 0f), 7, 4, 0.28f, rng);
            }
            for (int i = 0; i < 380; i++)
            {
                var p = new Vector2(R(-h, h), R(-h, h));
                if (InClearing(clearings, p, 0.6f)) continue;
                float r = R(0.15f, 0.7f);
                var c = new Vector3(p.x, r * 0.15f, p.y);
                B(c, mats.stone).AddBlob(c, new Vector3(r, r * R(0.4f, 0.7f), r * R(0.7f, 1.1f)), Quaternion.Euler(R(-10, 10), R(0f, 360f), R(-10, 10)), 6, 4, 0.3f, rng);
            }
            for (int i = 0; i < 1600; i++)
            {
                var p = new Vector2(R(-h, h), R(-h, h));
                if (InClearing(clearings, p, 0.5f)) continue;
                var c = new Vector3(p.x, 0f, p.y);
                var mb = B(c, mats.moss);
                int blades = rng.Next(3, 7);
                for (int k = 0; k < blades; k++)
                    mb.AddBlade(c + new Vector3(R(-0.15f, 0.15f), 0f, R(-0.15f, 0.15f)), Quaternion.Euler(R(-25f, 25f), R(0f, 360f), R(-25f, 25f)), R(0.05f, 0.1f), R(0.3f, 0.75f));
            }
            for (int i = 0; i < 420; i++)
            {
                var p = new Vector2(R(-h, h), R(-h, h));
                if (InClearing(clearings, p, 0.7f)) continue;
                var c = new Vector3(p.x, 0.05f, p.y);
                float len = R(0.8f, 3.2f);
                B(c, mats.bark).AddTube(c, Quaternion.Euler(0f, R(0f, 360f), 0f) * Quaternion.Euler(0f, 0f, 90f - R(-6f, 6f)),
                    new[] { -len * 0.5f, len * 0.5f }, new[] { R(0.03f, 0.09f), 0.02f }, 5, 1f, 0.5f);
            }

            var old = map.transform.Find("Undergrowth");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var ugRoot = new GameObject("Undergrowth").transform;
            ugRoot.SetParent(map.transform, false);

            var meshes = new List<Mesh>();
            var owners = new List<(Mesh, Material)>();
            foreach (var kv in builders)
            {
                var mesh = kv.Value.ToMesh($"UG_{kv.Key.Item1}_{kv.Key.Item2}_{kv.Key.Item3.name}");
                meshes.Add(mesh);
                owners.Add((mesh, kv.Key.Item3));
            }
            Folder(GenDir);
            SaveMeshes(UndergrowthPath, meshes);
            foreach (var (mesh, mat) in owners)
                AttachMesh(ugRoot, mesh.name, mesh, mat, true);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[KaiForest] Undergrowth baked: {meshes.Count} meshes");
        }
    }
}
