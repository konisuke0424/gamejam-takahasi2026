using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KaiForest
{
    /// <summary>Accumulates simple procedural shapes into one mesh (one material).</summary>
    public class MeshBuilder
    {
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> tris = new List<int>();

        public int VertexCount => verts.Count;

        /// <summary>Open tube made of stacked rings. Rings are (height, radius) pairs in local space.</summary>
        public void AddTube(Vector3 origin, Quaternion rot, float[] heights, float[] radii, int sides, float uRepeat = 1f, float vScale = 0.5f, float twist = 0f)
        {
            int start = verts.Count;
            for (int r = 0; r < heights.Length; r++)
            {
                for (int s = 0; s <= sides; s++)
                {
                    float a = (s / (float)sides) * Mathf.PI * 2f + twist * r;
                    var p = new Vector3(Mathf.Cos(a) * radii[r], heights[r], Mathf.Sin(a) * radii[r]);
                    verts.Add(origin + rot * p);
                    uvs.Add(new Vector2(s / (float)sides * uRepeat, heights[r] * vScale));
                }
            }
            int row = sides + 1;
            for (int r = 0; r < heights.Length - 1; r++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int a = start + r * row + s, b = a + 1, c = a + row, d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }
        }

        /// <summary>Cone with a closed underside (we look up at canopies).</summary>
        public void AddCone(Vector3 origin, Quaternion rot, float radius, float height, int sides)
        {
            AddTube(origin, rot, new[] { 0f, height * 0.55f, height }, new[] { radius, radius * 0.5f, 0.001f }, sides, 1f, 0.3f);
            int center = verts.Count;
            verts.Add(origin); uvs.Add(new Vector2(0.5f, 0.5f));
            for (int s = 0; s <= sides; s++)
            {
                float a = (s / (float)sides) * Mathf.PI * 2f;
                verts.Add(origin + rot * new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
                uvs.Add(new Vector2(Mathf.Cos(a) * 0.5f + 0.5f, Mathf.Sin(a) * 0.5f + 0.5f));
            }
            for (int s = 0; s < sides; s++)
            {
                tris.Add(center); tris.Add(center + 1 + s); tris.Add(center + 2 + s);
            }
        }

        /// <summary>Low-poly ellipsoid; jitter > 0 roughens it (rocks, bushes).</summary>
        public void AddBlob(Vector3 center, Vector3 radii, Quaternion rot, int lon, int lat, float jitter, System.Random rng)
        {
            int start = verts.Count;
            for (int y = 0; y <= lat; y++)
            {
                float v = y / (float)lat;
                float phi = v * Mathf.PI;
                for (int x = 0; x <= lon; x++)
                {
                    float u = x / (float)lon;
                    float th = u * Mathf.PI * 2f;
                    var n = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                    float k = 1f;
                    if (jitter > 0f && x < lon && y > 0 && y < lat) k += ((float)rng.NextDouble() * 2f - 1f) * jitter;
                    verts.Add(center + rot * Vector3.Scale(n * k, radii));
                    uvs.Add(new Vector2(u * 2f, v));
                }
            }
            // Close the seam so jittered vertices match.
            for (int y = 0; y <= lat; y++)
                verts[start + y * (lon + 1) + lon] = verts[start + y * (lon + 1)];
            int row = lon + 1;
            for (int y = 0; y < lat; y++)
            {
                for (int x = 0; x < lon; x++)
                {
                    int a = start + y * row + x, b = a + 1, c = a + row, d = c + 1;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(d); tris.Add(c);
                }
            }
        }

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uvA, Vector2 uvC)
        {
            int s = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            uvs.Add(uvA); uvs.Add(new Vector2(uvC.x, uvA.y)); uvs.Add(uvC); uvs.Add(new Vector2(uvA.x, uvC.y));
            tris.Add(s); tris.Add(s + 3); tris.Add(s + 2);
            tris.Add(s); tris.Add(s + 2); tris.Add(s + 1);
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (verts.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
