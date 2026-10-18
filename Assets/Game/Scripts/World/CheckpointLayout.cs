using UnityEngine;

namespace KaiForest
{
    /// <summary>
    /// The checkpoint and trap objects and where they go by default: shared by the editor menu
    /// (KaiForest > Map > Place Checkpoints and Traps, which makes prefabs and places them in
    /// Forest.unity) and by the game, which places the same set itself when the scene has none.
    /// Item models are placeholders built from primitives.
    /// </summary>
    public static class CheckpointLayout
    {
        public const string DeerImageResource = "UI_DeerScare";

        /// <summary>Default spots, spread along the way from the start to the exit (always the same).</summary>
        public struct Spots
        {
            public Vector3 handkerchief, kanzashi, mother;
            public float motherYaw;
            public Vector3[] deer, monster;
            /// <summary>The boy's line at the first fork: a little way along from the start.</summary>
            public Vector3 fork;
        }

        public static Spots DefaultSpots(ForestMap map)
        {
            var saved = Random.state;
            Random.InitState(20261018);
            var start = map.StartPosition;
            var end = map.exit != null ? map.exit.MotherHome : Vector3.zero;
            var route = end - start;
            var side = Vector3.Cross(Vector3.up, route.normalized);
            Vector3 Along(float t, float off) => map.FindOpenPoint(start + route * t + side * off, 4f);
            var face = start - end;
            var s = new Spots
            {
                handkerchief = Along(0.3f, 10f),
                kanzashi = Along(0.62f, -14f),
                mother = end,
                motherYaw = Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg,
                deer = new[] { Along(0.4f, -8f), Along(0.55f, 9f), Along(0.78f, -6f) },
                monster = new[] { Along(0.5f, 32f), Along(0.85f, -28f) },
                fork = Along(0.12f, 0f),
            };
            Random.state = saved;
            return s;
        }

        // ------------------------------------------------------------------ objects

        public static GameObject Handkerchief(MaterialLibrary m)
        {
            var go = new GameObject("Handkerchief");
            var item = go.AddComponent<CheckpointItem>();
            item.kind = ItemKind.Handkerchief;
            item.displayName = "ハンカチ";
            item.message = "おかあさんの　ハンカチだ。\n……ほかにも、だれかが　呼んでいる。";
            // A crumpled white cloth with a red flower, dropped on the ground.
            var cloth = Prims.Group(go.transform, "Model", Vector3.zero, 0f);
            Prims.Part(cloth, PrimitiveType.Cube, new Vector3(0f, 0.012f, 0f), new Vector3(0.34f, 0.012f, 0.3f), new Vector3(2f, 0f, -3f), m.paper, false);
            Prims.Part(cloth, PrimitiveType.Cube, new Vector3(0.07f, 0.03f, 0.06f), new Vector3(0.2f, 0.016f, 0.16f), new Vector3(-8f, 25f, 12f), m.paper, false);
            Prims.Part(cloth, PrimitiveType.Cube, new Vector3(-0.09f, 0.026f, -0.07f), new Vector3(0.14f, 0.014f, 0.12f), new Vector3(10f, -30f, -6f), m.paper, false);
            Prims.Part(cloth, PrimitiveType.Sphere, new Vector3(-0.1f, 0.022f, 0.08f), new Vector3(0.06f, 0.01f, 0.06f), Vector3.zero, m.torii, false);
            Glint(go.transform, 0.5f);
            return go;
        }

        public static GameObject Kanzashi(MaterialLibrary m)
        {
            var go = new GameObject("Kanzashi");
            var item = go.AddComponent<CheckpointItem>();
            item.kind = ItemKind.Kanzashi;
            item.displayName = "かんざし";
            item.message = "おかあさんの　かんざしだ。\n……なにかが、こっちに　気づいた。";
            // A black lacquered pin with a red bead and paper dangles, lying on the ground (1.5x so it can be found).
            var pin = Prims.Group(go.transform, "Model", new Vector3(0f, 0.02f, 0f), 0f);
            pin.localScale = Vector3.one * 1.5f;
            Prims.Part(pin, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.012f, 0.1f, 0.012f), new Vector3(90f, 0f, 0f), m.lanternFrame, false);
            Prims.Part(pin, PrimitiveType.Sphere, new Vector3(0f, 0.012f, 0.1f), new Vector3(0.05f, 0.05f, 0.05f), Vector3.zero, m.torii, false);
            for (int i = 0; i < 3; i++)
                Prims.Part(pin, PrimitiveType.Cube, new Vector3(-0.025f + i * 0.025f, 0.004f, 0.15f + i % 2 * 0.012f), new Vector3(0.012f, 0.004f, 0.05f), new Vector3(0f, (i - 1) * 15f, 0f), m.paper, false);
            Glint(go.transform, 0.5f);
            return go;
        }

        public static GameObject MotherMarker()
        {
            var go = new GameObject("Mother");
            go.AddComponent<MotherSpawn>();
            return go;
        }

        public static GameObject DeerTrap(Texture2D image)
        {
            var go = new GameObject("DeerTrap");
            go.AddComponent<DeerTrap>().scareImage = image;
            return go;
        }

        public static GameObject MonsterTrap()
        {
            var go = new GameObject("MonsterTrap");
            go.AddComponent<MonsterTrap>();
            return go;
        }

        /// <summary>The first fork's line (its clip comes from Boy Voice Settings' Fork Line).</summary>
        public static GameObject ForkVoiceTrigger()
        {
            var go = new GameObject("VoiceTrigger_Fork");
            go.AddComponent<VoiceTrigger>();
            return go;
        }

        /// <summary>A faint warm glint so a small thing on the forest floor can be spotted up close.</summary>
        static void Glint(Transform parent, float height)
        {
            var l = new GameObject("Glint").AddComponent<Light>();
            l.transform.SetParent(parent, false);
            l.transform.localPosition = new Vector3(0f, height, 0f);
            l.type = LightType.Point;
            l.color = new Color(1f, 0.85f, 0.65f);
            l.range = 2.2f;
            l.intensity = 0.8f;
            l.shadows = LightShadows.None;
        }

        // ------------------------------------------------------------------ runtime fallback

        /// <summary>
        /// Nothing placed in Forest.unity: put the default set under the map (handkerchief, kanzashi,
        /// traps; mother falls back to the torii) so the stages still play. Returns the new root.
        /// </summary>
        public static Transform PlaceDefaults(ForestMap map, MaterialLibrary m)
        {
            var root = new GameObject("Checkpoints (default)").transform;
            root.SetParent(map.transform, false);
            var s = DefaultSpots(map);
            void Put(GameObject go, Vector3 p, float yaw)
            {
                go.transform.SetParent(root, false);
                go.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
            }
            Put(Handkerchief(m), s.handkerchief, 30f);
            Put(Kanzashi(m), s.kanzashi, 200f);
            var image = Resources.Load<Texture2D>(DeerImageResource);
            foreach (var p in s.deer) Put(DeerTrap(image), p, 0f);
            foreach (var p in s.monster) Put(MonsterTrap(), p, 0f);
            Put(ForkVoiceTrigger(), s.fork, 0f);
            return root;
        }
    }
}
