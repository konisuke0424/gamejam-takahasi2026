using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KaiForest.EditorTools
{
    /// <summary>
    /// Prefabs for the checkpoints (mother's handkerchief and kanzashi, mother's spot) and the fakes'
    /// traps (deer, monster), and a menu that places one set of them in Forest.unity along the way from
    /// the start to the exit (CheckpointLayout's default spots). Move them around by hand afterwards;
    /// the menu only adds what is missing. Until something is placed, the game puts the same set at
    /// the same spots by itself.
    /// </summary>
    public static class CheckpointBuilder
    {
        const string Dir = "Assets/Game/Map/Prefabs/Checkpoints";
        const string DeerImagePath = "Assets/Game/Resources/" + CheckpointLayout.DeerImageResource + ".png";
        const string RootName = "Checkpoints";

        static string PrefabPath(string name) => $"{Dir}/{name}.prefab";

        static MaterialLibrary LoadMats() => AssetDatabase.LoadAssetAtPath<MaterialLibrary>("Assets/Game/Data/MaterialLibrary.asset");

        // ------------------------------------------------------------------ menu

        /// <summary>Forest.unity (opened if it isn't) and its ForestMap; false if either can't be had.</summary>
        static bool OpenForest(out Scene scene, out ForestMap map)
        {
            map = null;
            scene = SceneManager.GetSceneByPath(ForestBaker.ScenePath);
            if (!scene.isLoaded)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
                scene = EditorSceneManager.OpenScene(ForestBaker.ScenePath, OpenSceneMode.Single);
            }
            foreach (var go in scene.GetRootGameObjects())
                if ((map = go.GetComponentInChildren<ForestMap>()) != null) break;
            if (map == null) Debug.LogError("[KaiForest] ForestMap not found in Forest.unity");
            return map != null;
        }

        /// <summary>ForestMap/Checkpoints, made if missing.</summary>
        static Transform CheckpointsRoot(ForestMap map)
        {
            var root = map.transform.Find(RootName);
            if (root != null) return root;
            root = new GameObject(RootName).transform;
            root.SetParent(map.transform, false);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Place Checkpoints");
            return root;
        }

        [MenuItem("KaiForest/Map/Add Hunter Spawn")]
        static void MenuAddHunterSpawn()
        {
            if (!OpenForest(out var scene, out var map)) return;
            // Where the Scene view is looking, else halfway between the kanzashi and mother.
            var view = SceneView.lastActiveSceneView;
            Vector3 at;
            if (view != null) at = view.pivot;
            else
            {
                var spots = CheckpointLayout.DefaultSpots(map);
                at = (spots.kanzashi + spots.mother) * 0.5f;
            }
            at = map.ClampToMap(at);
            var go = new GameObject("HunterSpawn", typeof(HunterSpawn));
            go.transform.SetParent(CheckpointsRoot(map), false);
            go.transform.position = at;
            Undo.RegisterCreatedObjectUndo(go, "Add Hunter Spawn");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            Debug.Log("[KaiForest] Added a hunter spawn under ForestMap/Checkpoints (red circle). Move it to where the chasing monster should appear and save Forest.unity.");
        }

        [MenuItem("KaiForest/Map/Place Checkpoints and Traps")]
        static void MenuPlace()
        {
            if (!OpenForest(out var scene, out var map)) return;

            EnsurePrefabs(LoadMats(), false);
            Physics.SyncTransforms();
            int added = Place(map, scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            var root = map.transform.Find(RootName);
            if (root != null)
            {
                Selection.activeGameObject = root.gameObject;
                EditorGUIUtility.PingObject(root.gameObject);
            }
            Debug.Log(added > 0
                ? $"[KaiForest] Placed {added} checkpoint / trap objects under ForestMap/{RootName}. Move them where you like; run Rebake Undergrowth if bushes cover them."
                : "[KaiForest] Every checkpoint and trap is already placed; nothing added.");
        }

        [MenuItem("KaiForest/Map/Regenerate Checkpoint Prefabs (overwrites)")]
        static void MenuRegenerate()
        {
            if (!EditorUtility.DisplayDialog("チェックポイントのプレハブを作り直す",
                    "ハンカチ・かんざし・お母さん・鹿トラップ・化け物トラップのプレハブを仮の見た目で作り直します。\n手で差し替えた見た目や変えた値は失われます。", "作り直す", "やめる")) return;
            EnsurePrefabs(LoadMats(), true);
        }

        /// <summary>Adds whatever is missing at the default spots.</summary>
        static int Place(ForestMap map, Scene scene)
        {
            var root = CheckpointsRoot(map);

            int added = 0;
            void Add(string prefab, Vector3 pos, float yaw)
            {
                var t = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(prefab)), scene)).transform;
                t.SetParent(root, false);
                t.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
                Undo.RegisterCreatedObjectUndo(t.gameObject, "Place Checkpoints");
                added++;
            }

            var spots = CheckpointLayout.DefaultSpots(map);
            bool hasHandkerchief = false, hasKanzashi = false;
            foreach (var it in map.GetComponentsInChildren<CheckpointItem>(true))
            {
                hasHandkerchief |= it.kind == ItemKind.Handkerchief;
                hasKanzashi |= it.kind == ItemKind.Kanzashi;
            }
            if (!hasHandkerchief) Add("Handkerchief", spots.handkerchief, 30f);
            if (!hasKanzashi) Add("Kanzashi", spots.kanzashi, 200f);
            if (map.GetComponentInChildren<MotherSpawn>(true) == null) Add("Mother", spots.mother, spots.motherYaw);
            if (map.GetComponentsInChildren<DeerTrap>(true).Length == 0)
                foreach (var p in spots.deer) Add("DeerTrap", p, 0f);
            if (map.GetComponentsInChildren<MonsterTrap>(true).Length == 0)
                foreach (var p in spots.monster) Add("MonsterTrap", p, 0f);
            if (map.GetComponentsInChildren<VoiceTrigger>(true).Length == 0) Add("VoiceTrigger_Fork", spots.fork, 0f);
            return added;
        }

        // ------------------------------------------------------------------ prefabs

        /// <summary>Creates any missing prefab (or all of them when overwrite is set).</summary>
        public static void EnsurePrefabs(MaterialLibrary m, bool overwrite)
        {
            Folder(Dir);
            if (overwrite || Missing("Handkerchief")) Save(CheckpointLayout.Handkerchief(m), "Handkerchief");
            if (overwrite || Missing("Kanzashi")) Save(CheckpointLayout.Kanzashi(m), "Kanzashi");
            if (overwrite || Missing("Mother")) Save(BuildMother(m), "Mother");
            if (overwrite || Missing("DeerTrap")) Save(CheckpointLayout.DeerTrap(AssetDatabase.LoadAssetAtPath<Texture2D>(DeerImagePath)), "DeerTrap");
            if (overwrite || Missing("MonsterTrap")) Save(CheckpointLayout.MonsterTrap(), "MonsterTrap");
            if (overwrite || Missing("VoiceTrigger_Fork")) Save(CheckpointLayout.ForkVoiceTrigger(), "VoiceTrigger_Fork");
            AssetDatabase.SaveAssets();
        }

        static bool Missing(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(name)) == null;

        static void Save(GameObject go, string name)
        {
            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath(name));
            Object.DestroyImmediate(go);
        }

        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        [MenuItem("KaiForest/Map/Regenerate Mother Prefab (overwrites)")]
        static void MenuRegenerateMother()
        {
            Folder(Dir);
            Save(BuildMother(LoadMats()), "Mother");
            AssetDatabase.SaveAssets();
            Debug.Log("[KaiForest] Mother prefab rebuilt (its editor preview now shows mother's picture).");
        }

        static GameObject BuildMother(MaterialLibrary m)
        {
            var go = CheckpointLayout.MotherMarker();
            // Stand-in for editing only (the real mother is built at runtime): her picture, facing the
            // way the marker faces (+Z), or the primitive figure if the picture material is missing.
            var preview = new GameObject("Preview") { tag = "EditorOnly" };
            preview.transform.SetParent(go.transform, false);
            var picture = SettingsAssets.MotherBillboard();
            if (picture != null)
                CharacterFactory.BuildMotherPicture(preview.transform, picture).localRotation = Quaternion.Euler(0f, 180f, 0f);
            else
            {
                CharacterFactory.BuildMother(preview.transform, m);
                foreach (var light in preview.GetComponentsInChildren<Light>()) Object.DestroyImmediate(light.gameObject);
            }
            return go;
        }
    }
}
