using UnityEngine;

namespace KaiForest
{
    /// <summary>
    /// Wanders around the exit calling for Kai with many different, natural lines.
    /// Walks to the boy once she can see his lantern; touching her clears the game.
    /// </summary>
    public class MotherAI : MonoBehaviour
    {
        public float wanderRadius = 20f;
        public float walkSpeed = 1.0f;
        public float approachSpeed = 1.9f;
        public float noticeDistance = 11f;
        public float reunionDistance = 2.0f;
        public Vector2 callGap = new Vector2(4.0f, 7.5f);
        public float firstCallDelay = 2.5f;

        public bool Active { get; set; }
        public Vector3 Home { get; private set; }
        public bool Noticed { get; private set; }
        public VoiceEmitter Voice { get; private set; }
        public CharacterFactory.Rig Rig { get; private set; }

        ForestMap forest;
        PlayerController player;
        AudioBank bank;
        NpcMotor motor;
        float nextCall, waitUntil, activatedAt, lanternBase;
        int last1 = -1, last2 = -1;

        public void Init(ForestMap f, PlayerController p, AudioBank b, MaterialLibrary mats, Vector3 home)
        {
            forest = f; player = p; bank = b; Home = home;
            var cc = gameObject.AddComponent<CharacterController>();
            cc.height = 1.6f; cc.radius = 0.3f; cc.center = new Vector3(0f, 0.8f, 0f);
            motor = gameObject.AddComponent<NpcMotor>();
            Voice = gameObject.AddComponent<VoiceEmitter>();
            Voice.kind = VoiceKind.Mother;
            Rig = CharacterFactory.BuildMother(transform, mats);
            lanternBase = Rig.light.intensity;
        }

        public void Activate()
        {
            Active = true;
            activatedAt = Time.time;
            nextCall = Time.time + firstCallDelay;
        }

        float FlatDist(Vector3 a, Vector3 b) { a.y = b.y = 0f; return Vector3.Distance(a, b); }

        bool SeesPlayer(float d)
        {
            if (d > noticeDistance) return false;
            var eye = transform.position + Vector3.up * 1.45f;
            return !Physics.Linecast(eye, player.HeadPosition, ForestMap.ObstacleMask, QueryTriggerInteraction.Ignore);
        }

        void Update()
        {
            Animate();
            if (!Active) return;

            float d = FlatDist(transform.position, player.transform.position);
            if (d < reunionDistance)
            {
                GameManager.I.OnReunite();
                return;
            }

            if (SeesPlayer(d))
            {
                if (!Noticed) { Noticed = true; nextCall = Mathf.Min(nextCall, Time.time + 0.4f); }
                motor.MoveTo(player.transform.position, approachSpeed);
            }
            else
            {
                if (Noticed && d > noticeDistance * 1.6f) Noticed = false;
                if (!motor.HasTarget || Time.time - motor.TargetSetTime > 18f)
                {
                    if (waitUntil < 0f) waitUntil = Time.time + Random.Range(1.5f, 4f);
                    else if (Time.time > waitUntil)
                    {
                        waitUntil = -1f;
                        motor.MoveTo(forest.FindOpenPoint(Home, wanderRadius), walkSpeed);
                    }
                }
            }

            if (Time.time >= nextCall && !Voice.IsPlaying)
            {
                var line = AudioBank.PickWeighted(bank.motherLines, last1, last2, out int idx);
                last2 = last1; last1 = idx;
                // A real voice never says a line exactly the same way twice.
                float len = Voice.Play(line, Random.Range(0.96f, 1.04f));
                float gap = Random.Range(callGap.x, callGap.y);
                if (Noticed) gap *= 0.5f;
                nextCall = Time.time + len + gap;
            }
        }

        public void SayFound()
        {
            Active = false;
            motor.Stop();
            Voice.Play(bank.motherFound, 1f);
        }

        public void FaceTowards(Vector3 p) => motor.FaceTowards(p, 360f);

        public void Teleport(Vector3 p) => motor.Teleport(p);

        void Animate()
        {
            float t = Time.time;
            float walk = Mathf.Clamp01(motor != null ? motor.Velocity.magnitude / approachSpeed : 0f);
            Rig.body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(t * 6f)) * 0.03f * walk, 0f);
            Rig.armL.localRotation = Quaternion.Euler(Mathf.Sin(t * 6f) * 18f * walk, 0f, 4f);
            Rig.armR.localRotation = Quaternion.Euler(-55f + Mathf.Sin(t * 6f + 1f) * 4f * walk, 0f, -4f);
            // Lantern swings a little and flickers.
            Rig.lantern.localRotation = Quaternion.Euler(55f + Mathf.Sin(t * 2.3f) * 6f, 0f, Mathf.Sin(t * 1.7f) * 5f);
            Rig.light.intensity = lanternBase * (0.88f + Mathf.PerlinNoise(t * 6f, 3.1f) * 0.2f);
            if (Voice != null && Voice.IsPlaying)
                Rig.head.localRotation = Quaternion.Euler(-8f + Mathf.Sin(t * 1.3f) * 4f, Mathf.Sin(t * 0.7f) * 25f, 0f);
            else
                Rig.head.localRotation = Quaternion.Slerp(Rig.head.localRotation, Quaternion.identity, Time.deltaTime * 2f);
        }
    }
}
