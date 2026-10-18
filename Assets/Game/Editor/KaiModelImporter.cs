using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace KaiForest.EditorTools
{
    /// <summary>
    /// Imports *.kfmodel files written by Tools/blender/build_monster.py: a hierarchy of parts, each
    /// with its pivot (joint) position, mesh data already in Unity space and material slot names.
    /// The result is a model asset (like an FBX) with a ModelRig component pointing at the parts.
    /// Re-running the Blender script re-imports it automatically.
    /// </summary>
    [ScriptedImporter(1, "kfmodel")]
    public class KaiModelImporter : ScriptedImporter
    {
        const string MaterialLibraryPath = "Assets/Game/Data/MaterialLibrary.asset";

        [Serializable] class ModelData { public int version; public PartData[] parts; }

        [Serializable]
        class PartData
        {
            public string name;
            public string parent;
            public float[] pivot;
            public float[] vertices;
            public float[] normals;
            public SubmeshData[] submeshes;
        }

        [Serializable] class SubmeshData { public string material; public int[] indices; }

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var data = JsonUtility.FromJson<ModelData>(File.ReadAllText(ctx.assetPath));
            ctx.DependsOnArtifact(MaterialLibraryPath);
            var lib = AssetDatabase.LoadAssetAtPath<MaterialLibrary>(MaterialLibraryPath);

            var root = new GameObject(Path.GetFileNameWithoutExtension(ctx.assetPath));
            var nodes = new Dictionary<string, Transform>();
            var pivots = new Dictionary<string, Vector3>();

            foreach (var p in data.parts)
            {
                var go = new GameObject(p.name);
                var pivot = ToVec(p.pivot, 0);
                bool hasParent = !string.IsNullOrEmpty(p.parent) && nodes.ContainsKey(p.parent);
                go.transform.SetParent(hasParent ? nodes[p.parent] : root.transform, false);
                go.transform.localPosition = hasParent ? pivot - pivots[p.parent] : pivot;

                var mesh = new Mesh { name = p.name };
                var verts = new Vector3[p.vertices.Length / 3];
                var norms = new Vector3[verts.Length];
                for (int i = 0; i < verts.Length; i++)
                {
                    verts[i] = ToVec(p.vertices, i * 3);
                    norms[i] = ToVec(p.normals, i * 3);
                }
                if (verts.Length > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.vertices = verts;
                mesh.normals = norms;
                mesh.subMeshCount = p.submeshes.Length;
                var materials = new Material[p.submeshes.Length];
                for (int s = 0; s < p.submeshes.Length; s++)
                {
                    mesh.SetTriangles(p.submeshes[s].indices, s);
                    materials[s] = MaterialFor(lib, p.submeshes[s].material);
                }
                mesh.RecalculateBounds();
                ctx.AddObjectToAsset(p.name + "_mesh", mesh);

                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = materials;
                nodes[p.name] = go.transform;
                pivots[p.name] = pivot;
            }

            var rig = root.AddComponent<ModelRig>();
            nodes.TryGetValue("Body", out rig.body);
            nodes.TryGetValue("Head", out rig.head);
            nodes.TryGetValue("ArmL", out rig.armL);
            nodes.TryGetValue("ArmR", out rig.armR);
            if (!rig.IsComplete) ctx.LogImportWarning("Model is missing one of Body / Head / ArmL / ArmR");

            ctx.AddObjectToAsset("root", root);
            ctx.SetMainObject(root);
        }

        static Vector3 ToVec(float[] a, int i) => new Vector3(a[i], a[i + 1], a[i + 2]);

        /// <summary>Slot names from Blender -> the game's materials (shared look, emission, tuning).</summary>
        static Material MaterialFor(MaterialLibrary lib, string slot)
        {
            if (lib == null) return null;
            switch (slot)
            {
                case "Hair": return lib.hair;
                case "MonsterFace": return lib.monsterFace;
                case "MonsterSkin": return lib.monsterFace;
                case "MonsterEye": return lib.monsterEye;
                default: return lib.monsterBody;
            }
        }
    }
}
