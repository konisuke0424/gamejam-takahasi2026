using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KaiForest
{
    /// <summary>Builds hierarchies out of Unity's built-in primitive meshes, without their colliders.</summary>
    public static class Prims
    {
        static readonly Dictionary<PrimitiveType, Mesh> cache = new Dictionary<PrimitiveType, Mesh>();

        public static Mesh Mesh(PrimitiveType type)
        {
            if (!cache.TryGetValue(type, out var mesh) || mesh == null)
            {
                var tmp = GameObject.CreatePrimitive(type);
                mesh = tmp.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(tmp);
                cache[type] = mesh;
            }
            return mesh;
        }

        /// <summary>Cube/Sphere are 1 unit; Cylinder/Capsule are 1 wide and 2 tall (scale.y = half height).</summary>
        public static Transform Part(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 localScale, Vector3 localEuler, Material mat, bool castShadows = true)
        {
            var go = new GameObject(type.ToString());
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            t.localRotation = Quaternion.Euler(localEuler);
            t.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = Mesh(type);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return t;
        }

        public static Transform Group(Transform parent, string name, Vector3 pos, float yaw)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return t;
        }

        public static void Box(Transform t, Vector3 center, Vector3 size)
        {
            var c = t.gameObject.AddComponent<BoxCollider>();
            c.center = center;
            c.size = size;
        }

        public static void Capsule(Transform t, Vector3 center, float radius, float height, int direction = 1)
        {
            var c = t.gameObject.AddComponent<CapsuleCollider>();
            c.center = center;
            c.radius = radius;
            c.height = height;
            c.direction = direction;
        }
    }
}
