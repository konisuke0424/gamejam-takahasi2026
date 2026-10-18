using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace KaiForest
{
    /// <summary>
    /// The Tripo-made monster (Resources/Monster100, from Tools/blender/convert_tripo_monster.py): a
    /// skinned model whose "idle" and "run" clips are mixed by speed (all run while chasing, half and
    /// half while walking), with the uncanny extras laid on top of the animation: the head held at a
    /// wrong angle and twitching, and the fold over the boy when it catches him.
    /// MonsterAI calls <see cref="Drive"/> every frame.
    /// </summary>
    public class MonsterBody : MonoBehaviour
    {
        public const string Resource = "Monster100/Monster100";
        /// <summary>Its URP material (made by MonsterModelPostprocessor); also set here in case the FBX isn't remapped yet.</summary>
        const string MaterialResource = "Monster100/Monster100_Mat";
        /// <summary>Ground the "run" clip covers per second at the model's own size (printed by the converter).</summary>
        const float RunGroundSpeed = 1.551f;
        /// <summary>Share of the run mixed into the idle while it walks (searching, investigating, retreating).</summary>
        const float WalkRunShare = 0.5f;
        /// <summary>
        /// Centre of the face (eyes to mouth) from the Head bone, in Head→HeadTop lengths, measured on
        /// Monster100's mesh. The head is bowed, so the face sits below the jutting forehead.
        /// Lower FaceUp = the face moves up in the capture close-up.
        /// </summary>
        const float FaceUp = 1.2f, FaceAhead = 0.7f;
        /// <summary>Capture close-up: how far the head leans to one side, and how much it twitches around that (degrees).</summary>
        const float ScareHeadTilt = 15f, ScareHeadTwitch = 1.5f;

        public CharacterFactory.Rig Rig { get; private set; }
        /// <summary>The way the face looks right now (follows the head's animation and twitches).</summary>
        public Vector3 FaceForward => (faceFront.position - Rig.head.position).normalized;

        Transform monster, head, armL, armR, faceFront;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable idlePlayable, runPlayable;
        float scale = 1f, runWeight, speed, scareLean;
        bool chasing;

        /// <summary>The modelled monster if it is in Resources, otherwise null (callers fall back).</summary>
        public static MonsterBody Build(Transform parent, float height)
        {
            var asset = Resources.Load<GameObject>(Resource);
            if (asset == null) return null;
            AnimationClip idle = null, run = null;
            foreach (var c in Resources.LoadAll<AnimationClip>(Resource))
            {
                if (c.name.StartsWith("__preview__")) continue;
                if (c.name == "idle" || c.name.EndsWith("|idle")) idle = c;
                else if (c.name == "run" || c.name.EndsWith("|run")) run = c;
            }
            if (run == null) return null;
            var model = Instantiate(asset, parent, false);
            model.name = "Model";
            var body = model.AddComponent<MonsterBody>();
            if (!body.Setup(parent, height, idle, run))
            {
                Destroy(model);
                return null;
            }
            return body;
        }

        bool Setup(Transform parent, float height, AnimationClip idle, AnimationClip run)
        {
            monster = parent;
            var skin = GetComponentInChildren<SkinnedMeshRenderer>();
            head = FindBone("Head");
            var top = FindBone("HeadTop_End");
            armL = FindBone("LeftArm");
            armR = FindBone("RightArm");
            if (skin == null || head == null || top == null || armL == null || armR == null) return false;
            // It runs and leans far out of its bind-pose box; a bigger box keeps it from being culled
            // while it is on screen (cheaper than updateWhenOffscreen, which skins it even when hidden).
            var lb = skin.localBounds;
            lb.Expand(lb.size.magnitude);
            skin.localBounds = lb;
            var mat = Resources.Load<Material>(MaterialResource);
            if (mat != null)
            {
                var mats = skin.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                skin.sharedMaterials = mats;
            }

            // Stand it on the ground at the game's height (the converter keeps Tripo's ~1 m).
            var b = BindPoseBounds(skin);
            scale = height / b.size.y;
            transform.localScale = Vector3.one * scale;
            transform.localPosition = new Vector3(0f, -b.min.y * scale, 0f);

            // What the capture camera looks at: the middle of the face. Tripo's head bones stop at the
            // chin; the face sits above and in front of them (FaceUp / FaceAhead, in head-bone lengths).
            var face = new GameObject("Face").transform;
            face.SetParent(head, false);
            var up = top.position - head.position;
            face.position = head.position + up * FaceUp + monster.forward * up.magnitude * FaceAhead;
            faceFront = new GameObject("FaceFront").transform;
            faceFront.SetParent(head, false);
            faceFront.position = face.position + monster.forward * 0.3f;
            Rig = new CharacterFactory.Rig { root = parent, body = transform, head = face, armL = armL, armR = armR };

            var animator = GetComponent<Animator>();
            if (animator == null) animator = gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            graph = PlayableGraph.Create("Monster");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            idlePlayable = AnimationClipPlayable.Create(graph, idle != null ? idle : run);
            if (idle == null) idlePlayable.SetSpeed(0); // one frame of the run as the standing pose
            runPlayable = AnimationClipPlayable.Create(graph, run);
            graph.Connect(idlePlayable, 0, mixer, 0);
            graph.Connect(runPlayable, 0, mixer, 1);
            mixer.SetInputWeight(0, 1f);
            mixer.SetInputWeight(1, 0f);
            AnimationPlayableOutput.Create(graph, "Animation", animator).SetSourcePlayable(mixer);
            graph.Play();
            return true;
        }

        Transform FindBone(string name)
        {
            foreach (var t in GetComponentsInChildren<Transform>())
                if (t.name == name || t.name.EndsWith(":" + name)) return t;
            return null;
        }

        /// <summary>The mesh's bind-pose box in this transform's space (before scaling).</summary>
        Bounds BindPoseBounds(SkinnedMeshRenderer skin)
        {
            var mb = skin.sharedMesh.bounds;
            var b = new Bounds(transform.InverseTransformPoint(skin.transform.TransformPoint(mb.center)), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                b.Encapsulate(transform.InverseTransformPoint(skin.transform.TransformPoint(corner)));
            }
            return b;
        }

        /// <param name="moveSpeed">How fast it is really moving (m/s).</param>
        /// <param name="pace">Leg speed: 1 = feet match the ground, higher = faster strides that slide a little.</param>
        public void Drive(float moveSpeed, bool isChasing, float lean, float pace)
        {
            speed = moveSpeed;
            chasing = isChasing;
            scareLean = lean;
            if (!graph.IsValid()) return;

            float target = lean > 0f ? 0f : isChasing ? 1f : Mathf.Clamp01(moveSpeed / 0.6f) * WalkRunShare;
            runWeight = Mathf.MoveTowards(runWeight, target, Time.deltaTime * 2.5f);
            mixer.SetInputWeight(0, 1f - runWeight);
            mixer.SetInputWeight(1, runWeight);
            // A half-mixed run takes shorter strides, so it has to cycle faster to cover the same ground.
            float stride = RunGroundSpeed * scale * Mathf.Max(runWeight, 0.35f);
            runPlayable.SetSpeed(Mathf.Clamp(moveSpeed / stride * pace, 0.5f, 1.8f));
            Wrap(idlePlayable);
            Wrap(runPlayable);
        }

        static void Wrap(AnimationClipPlayable p)
        {
            float len = p.GetAnimationClip().length;
            if (len > 0f && p.GetTime() > len) p.SetTime(p.GetTime() % len);
        }

        void LateUpdate()
        {
            // After the Animator has posed the bones for this frame.
            float t = Time.time;
            if (scareLean > 0f)
            {
                // It folds over him from the feet, so its face ends up right in front of his. The face
                // is far above the neck's pivot, so the twitch stays small and smooth to keep it readable.
                float j = (Mathf.PerlinNoise(t * 6f, 3.1f) - 0.5f) * 2f;
                transform.localRotation = Quaternion.Euler(scareLean, 0f, j * 0.5f);
                Vector3 r = transform.right, f = transform.forward;
                head.rotation = Quaternion.AngleAxis(-scareLean * 0.85f, r) * Quaternion.AngleAxis(ScareHeadTilt + j * ScareHeadTwitch, f) * head.rotation;
                armL.rotation = Quaternion.AngleAxis(-70f, r) * Quaternion.AngleAxis(25f, f) * armL.rotation;
                armR.rotation = Quaternion.AngleAxis(-70f, r) * Quaternion.AngleAxis(-25f, f) * armR.rotation;
                return;
            }
            transform.localRotation = Quaternion.identity;
            // The head sits at a wrong angle and twitches.
            float twitch = Mathf.PerlinNoise(t * (chasing ? 9f : 2f), 7.7f) > 0.78f ? Random.Range(-12f, 12f) : 0f;
            float look = Mathf.Sin(t * 0.5f) * (speed > 0.3f ? 8f : 20f);
            head.rotation = Quaternion.AngleAxis(look, monster.up)
                          * Quaternion.AngleAxis(24f + twitch, monster.forward)
                          * Quaternion.AngleAxis(6f + twitch * 0.3f, monster.right)
                          * head.rotation;
        }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
