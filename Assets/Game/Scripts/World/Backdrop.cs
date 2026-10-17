using UnityEngine;
using UnityEngine.Rendering;

namespace KaiForest
{
    /// <summary>
    /// The far backdrop: a band of sphere around the camera carrying BackdropSettings' image. It moves
    /// with the camera (so it never gets closer, like real mountains), is drawn behind everything and
    /// ignores the fog, so it shows wherever nothing stands in front of it within the view distance.
    /// </summary>
    public class Backdrop : MonoBehaviour
    {
        /// <summary>Radius of the band: inside the camera's 40 m view distance at night.</summary>
        const float Radius = 30f;
        const int Segments = 96, Rings = 24;

        /// <summary>0 = night, 1 = the debug daylight look (set by GameManager): fades into the (day) fog colour.</summary>
        public float Daylight { get; set; }

        Transform follow;
        BackdropSettings settings;
        Material material;
        MeshRenderer meshRenderer;
        Mesh mesh;
        int builtCopies;
        float builtBottom, builtTop;

        /// <summary>Makes the backdrop around <paramref name="camera"/>, or returns null if it has no image or shader.</summary>
        public static Backdrop Create(Transform camera)
        {
            var settings = BackdropSettings.Load();
            var shader = settings.shader != null ? settings.shader : Shader.Find(BackdropSettings.ShaderName);
            if (shader == null || settings.texture == null) return null;

            var go = new GameObject("Backdrop");
            var b = go.AddComponent<Backdrop>();
            b.follow = camera;
            b.settings = settings;
            b.material = new Material(shader);
            b.mesh = new Mesh { name = "Backdrop" };
            go.AddComponent<MeshFilter>().sharedMesh = b.mesh;
            b.meshRenderer = go.AddComponent<MeshRenderer>();
            b.meshRenderer.sharedMaterial = b.material;
            b.meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            b.meshRenderer.receiveShadows = false;
            b.meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            b.meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            b.LateUpdate();
            return b;
        }

        void LateUpdate()
        {
            if (follow == null) return;
            transform.SetPositionAndRotation(follow.position, Quaternion.Euler(0f, settings.yaw, 0f));
            meshRenderer.enabled = settings.visible && settings.texture != null;
            if (!meshRenderer.enabled) return;

            float bottom = Mathf.Min(settings.bottomAngle, settings.topAngle - 1f);
            if (settings.copies != builtCopies || bottom != builtBottom || settings.topAngle != builtTop)
                Build(Mathf.Max(1, settings.copies), bottom, settings.topAngle);

            material.SetTexture("_BaseMap", settings.texture);
            material.SetFloat("_Brightness", settings.brightness);
            material.SetColor("_MixColor", RenderSettings.fogColor);
            material.SetFloat("_Mix", Mathf.Max(settings.fogMix, Daylight));
        }

        /// <summary>Band from bottom to top (degrees above eye level); the image's height spans it evenly by angle.</summary>
        void Build(int copies, float bottom, float top)
        {
            builtCopies = copies;
            builtBottom = bottom;
            builtTop = top;
            var verts = new Vector3[(Segments + 1) * (Rings + 1)];
            var uvs = new Vector2[verts.Length];
            var tris = new int[Segments * Rings * 6];
            for (int r = 0; r <= Rings; r++)
            {
                float v = (float)r / Rings;
                float e = Mathf.Lerp(bottom, top, v) * Mathf.Deg2Rad;
                for (int s = 0; s <= Segments; s++)
                {
                    float u = (float)s / Segments;
                    float a = u * Mathf.PI * 2f; // clockwise from +Z seen from above: the image reads left to right
                    int i = r * (Segments + 1) + s;
                    verts[i] = new Vector3(Mathf.Cos(e) * Mathf.Sin(a), Mathf.Sin(e), Mathf.Cos(e) * Mathf.Cos(a)) * Radius;
                    uvs[i] = new Vector2(u * copies, v);
                }
            }
            int k = 0;
            for (int r = 0; r < Rings; r++)
            {
                for (int s = 0; s < Segments; s++)
                {
                    int i = r * (Segments + 1) + s;
                    tris[k++] = i; tris[k++] = i + Segments + 1; tris[k++] = i + 1;
                    tris[k++] = i + 1; tris[k++] = i + Segments + 1; tris[k++] = i + Segments + 2;
                }
            }
            mesh.Clear();
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * Radius * 2f);
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
            if (mesh != null) Destroy(mesh);
        }
    }
}
