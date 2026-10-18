using UnityEngine;

namespace KaiForest
{
    /// <summary>Primitive-built placeholder bodies for the mother, the monster and the boy's lantern.</summary>
    public static class CharacterFactory
    {
        public class Rig
        {
            public Transform root, body, head, armL, armR, lantern;
            public Light light;
        }

        public static Rig BuildMother(Transform parent, MaterialLibrary m)
        {
            var r = new Rig { root = parent };
            r.body = Prims.Group(parent, "Body", Vector3.zero, 0f);
            Prims.Part(r.body, PrimitiveType.Cylinder, new Vector3(0f, 0.42f, 0f), new Vector3(0.5f, 0.42f, 0.42f), Vector3.zero, m.cloth);
            Prims.Part(r.body, PrimitiveType.Capsule, new Vector3(0f, 0.98f, 0f), new Vector3(0.42f, 0.36f, 0.32f), Vector3.zero, m.cloth);
            Prims.Part(r.body, PrimitiveType.Cylinder, new Vector3(0f, 0.92f, 0f), new Vector3(0.44f, 0.07f, 0.34f), Vector3.zero, m.obi);
            r.head = Prims.Group(r.body, "Head", new Vector3(0f, 1.45f, 0f), 0f);
            Prims.Part(r.head, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.2f, 0.24f, 0.21f), Vector3.zero, m.skin);
            Prims.Part(r.head, PrimitiveType.Sphere, new Vector3(0f, 0.03f, -0.025f), new Vector3(0.23f, 0.25f, 0.22f), Vector3.zero, m.hair);
            Prims.Part(r.head, PrimitiveType.Sphere, new Vector3(0f, 0.1f, -0.13f), new Vector3(0.13f, 0.12f, 0.12f), Vector3.zero, m.hair);
            r.armL = Prims.Group(r.body, "ArmL", new Vector3(-0.22f, 1.25f, 0f), 0f);
            Prims.Part(r.armL, PrimitiveType.Capsule, new Vector3(0f, -0.28f, 0f), new Vector3(0.11f, 0.28f, 0.11f), Vector3.zero, m.cloth);
            r.armR = Prims.Group(r.body, "ArmR", new Vector3(0.22f, 1.25f, 0f), 0f);
            r.armR.localRotation = Quaternion.Euler(-55f, 0f, 0f);
            Prims.Part(r.armR, PrimitiveType.Capsule, new Vector3(0f, -0.28f, 0f), new Vector3(0.11f, 0.28f, 0.11f), Vector3.zero, m.cloth);

            // Paper lantern hanging from a short stick held forward.
            r.lantern = Prims.Group(r.armR, "Chochin", new Vector3(0f, -0.56f, 0.05f), 0f);
            Prims.Part(r.lantern, PrimitiveType.Cube, new Vector3(0f, -0.1f, 0f), new Vector3(0.015f, 0.2f, 0.015f), Vector3.zero, m.lanternFrame, false);
            BuildChochin(r.lantern, new Vector3(0f, -0.38f, 0f), 0.85f, m);
            // The light sits a metre ahead of her rather than inside the paper: URP point lights blow out
            // anything within a hand's width, and her body would turn white.
            r.light = AddLanternLight(r.body, new Vector3(0.15f, 0.55f, 1.0f), 9f, 6f, false);
            return r;
        }

        /// <summary>Resources name of the modelled monster (Tools/blender/build_monster.py -> KaiModelImporter).</summary>
        public const string MonsterModelResource = "MonsterModel";

        /// <summary>
        /// The Blender-made monster if it has been imported, otherwise null (callers fall back to
        /// <see cref="BuildMonster"/>). Its parts keep the same joints, so the animation is unchanged.
        /// </summary>
        public static Rig BuildMonsterFromModel(Transform parent)
        {
            var asset = Resources.Load<GameObject>(MonsterModelResource);
            if (asset == null) return null;
            var model = Object.Instantiate(asset, parent, false);
            model.name = "Model";
            var parts = model.GetComponent<ModelRig>();
            if (parts == null || !parts.IsComplete)
            {
                Object.Destroy(model);
                return null;
            }
            return new Rig { root = parent, body = parts.body, head = parts.head, armL = parts.armL, armR = parts.armR };
        }

        /// <summary>Primitive placeholder monster, used when the modelled one isn't available.</summary>
        public static Rig BuildMonster(Transform parent, MaterialLibrary m)
        {
            var r = new Rig { root = parent };
            r.body = Prims.Group(parent, "Body", Vector3.zero, 0f);
            // Too tall, too thin, arms too long: it only pretends to be a person.
            Prims.Part(r.body, PrimitiveType.Cylinder, new Vector3(0f, 0.75f, 0f), new Vector3(0.6f, 0.75f, 0.45f), Vector3.zero, m.monsterBody);
            Prims.Part(r.body, PrimitiveType.Capsule, new Vector3(0f, 1.65f, 0f), new Vector3(0.42f, 0.55f, 0.3f), Vector3.zero, m.monsterBody);
            Prims.Part(r.body, PrimitiveType.Cylinder, new Vector3(0f, 2.25f, 0f), new Vector3(0.09f, 0.18f, 0.09f), Vector3.zero, m.monsterBody);
            r.head = Prims.Group(r.body, "Head", new Vector3(0f, 2.5f, 0.04f), 0f);
            Prims.Part(r.head, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.3f, 0.4f, 0.3f), Vector3.zero, m.hair);
            // Pale, too-long mask face with hollow eyes and a gaping mouth.
            Prims.Part(r.head, PrimitiveType.Sphere, new Vector3(0f, -0.04f, 0.1f), new Vector3(0.22f, 0.36f, 0.12f), Vector3.zero, m.monsterFace);
            Prims.Part(r.head, PrimitiveType.Sphere, new Vector3(-0.058f, 0.04f, 0.152f), new Vector3(0.075f, 0.045f, 0.03f), new Vector3(0f, 0f, -18f), m.monsterBody, false);
            Prims.Part(r.head, PrimitiveType.Sphere, new Vector3(0.058f, 0.04f, 0.152f), new Vector3(0.075f, 0.045f, 0.03f), new Vector3(0f, 0f, 18f), m.monsterBody, false);
            Prims.Part(r.head, PrimitiveType.Sphere, new Vector3(-0.058f, 0.038f, 0.168f), new Vector3(0.01f, 0.01f, 0.006f), Vector3.zero, m.monsterEye, false);
            Prims.Part(r.head, PrimitiveType.Sphere, new Vector3(0.058f, 0.038f, 0.168f), new Vector3(0.01f, 0.01f, 0.006f), Vector3.zero, m.monsterEye, false);
            Prims.Part(r.head, PrimitiveType.Sphere, new Vector3(0f, -0.16f, 0.15f), new Vector3(0.07f, 0.13f, 0.04f), new Vector3(0f, 0f, 6f), m.monsterBody, false);
            for (int i = 0; i < 5; i++)
                Prims.Part(r.head, PrimitiveType.Cube, new Vector3(-0.09f + i * 0.045f, -0.05f - (i % 2) * 0.06f, 0.175f + (i % 3) * 0.004f),
                    new Vector3(0.012f, 0.32f + (i % 3) * 0.08f, 0.008f), new Vector3(0f, 0f, (i - 2) * 4f), m.hair, false);
            // Long hair curtain down the back and over the sides of the face.
            Prims.Part(r.head, PrimitiveType.Cube, new Vector3(0f, -0.5f, -0.1f), new Vector3(0.36f, 1.1f, 0.06f), new Vector3(-6f, 0f, 0f), m.hair);
            Prims.Part(r.head, PrimitiveType.Cube, new Vector3(-0.15f, -0.35f, 0.05f), new Vector3(0.05f, 0.8f, 0.2f), Vector3.zero, m.hair);
            Prims.Part(r.head, PrimitiveType.Cube, new Vector3(0.15f, -0.35f, 0.05f), new Vector3(0.05f, 0.8f, 0.2f), Vector3.zero, m.hair);
            r.armL = Prims.Group(r.body, "ArmL", new Vector3(-0.24f, 2.05f, 0f), 0f);
            Prims.Part(r.armL, PrimitiveType.Capsule, new Vector3(0f, -0.72f, 0f), new Vector3(0.09f, 0.72f, 0.09f), Vector3.zero, m.monsterBody);
            Prims.Part(r.armL, PrimitiveType.Sphere, new Vector3(0f, -1.48f, 0.02f), new Vector3(0.09f, 0.2f, 0.06f), Vector3.zero, m.monsterFace);
            r.armR = Prims.Group(r.body, "ArmR", new Vector3(0.24f, 2.05f, 0f), 0f);
            Prims.Part(r.armR, PrimitiveType.Capsule, new Vector3(0f, -0.72f, 0f), new Vector3(0.09f, 0.72f, 0.09f), Vector3.zero, m.monsterBody);
            Prims.Part(r.armR, PrimitiveType.Sphere, new Vector3(0f, -1.48f, 0.02f), new Vector3(0.09f, 0.2f, 0.06f), Vector3.zero, m.monsterFace);
            return r;
        }

        /// <summary>The boy's own lantern, parented to the camera so it sits at the lower right of the view.</summary>
        public static Rig BuildHandLantern(Transform cam, MaterialLibrary m, float range, float intensity)
        {
            var r = new Rig { root = cam };
            r.lantern = Prims.Group(cam, "HandLantern", new Vector3(0.4f, -0.44f, 0.82f), 0f);
            Prims.Part(r.lantern, PrimitiveType.Cube, new Vector3(0f, 0.17f, 0f), new Vector3(0.01f, 0.2f, 0.01f), Vector3.zero, m.lanternFrame, false);
            BuildChochin(r.lantern, Vector3.zero, 0.42f, m);
            r.light = AddLanternLight(r.lantern, new Vector3(0f, 0.05f, 0f), range, intensity, true);
            return r;
        }

        static void BuildChochin(Transform parent, Vector3 pos, float size, MaterialLibrary m)
        {
            var c = Prims.Group(parent, "Paper", pos, 0f);
            c.localScale = Vector3.one * size;
            Prims.Part(c, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.24f, 0.3f, 0.24f), Vector3.zero, m.lanternGlow, false);
            Prims.Part(c, PrimitiveType.Cylinder, new Vector3(0f, 0.15f, 0f), new Vector3(0.14f, 0.025f, 0.14f), Vector3.zero, m.lanternFrame, false);
            Prims.Part(c, PrimitiveType.Cylinder, new Vector3(0f, -0.15f, 0f), new Vector3(0.14f, 0.025f, 0.14f), Vector3.zero, m.lanternFrame, false);
        }

        static Light AddLanternLight(Transform parent, Vector3 pos, float range, float intensity, bool shadows)
        {
            var go = new GameObject("LanternLight");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.66f, 0.36f);
            l.range = range;
            l.intensity = intensity;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            l.shadowStrength = 0.85f;
            l.shadowNearPlane = 0.2f;
            return l;
        }
    }
}
