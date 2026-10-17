using UnityEngine;

namespace KaiForest
{
    /// <summary>Walks a CharacterController towards a target, side-stepping trunks with sphere casts.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class NpcMotor : MonoBehaviour
    {
        public float turnSpeed = 220f;
        public float probeHeight = 0.9f;
        public float probeRadius = 0.35f;
        public float probeLength = 1.8f;

        public bool HasTarget { get; private set; }
        public Vector3 Target { get; private set; }
        public float Speed { get; private set; }
        public Vector3 Velocity { get; private set; }
        public float TargetSetTime { get; private set; }
        public int StuckCount { get; private set; }

        CharacterController cc;
        float vy, stuckTimer, detourUntil, detourAngle;

        void Awake() => cc = GetComponent<CharacterController>();

        public void MoveTo(Vector3 target, float speed)
        {
            target.y = 0f;
            if (!HasTarget || (target - Target).sqrMagnitude > 0.25f) TargetSetTime = Time.time;
            Target = target;
            Speed = speed;
            HasTarget = true;
        }

        public void Stop() => HasTarget = false;

        public void FaceTowards(Vector3 p, float degPerSec)
        {
            var d = p - transform.position; d.y = 0f;
            if (d.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), degPerSec * Time.deltaTime);
        }

        public void Teleport(Vector3 p)
        {
            cc.enabled = false;
            transform.position = p;
            cc.enabled = true;
        }

        bool Blocked(Vector3 dir)
        {
            return Physics.SphereCast(transform.position + Vector3.up * probeHeight, probeRadius, dir, out _, probeLength,
                ForestMap.ObstacleMask, QueryTriggerInteraction.Ignore);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 desired = Vector3.zero;
            if (HasTarget)
            {
                var to = Target - transform.position; to.y = 0f;
                if (to.magnitude < 0.5f) HasTarget = false;
                else
                {
                    var dir = to.normalized;
                    if (Time.time < detourUntil) dir = Quaternion.Euler(0f, detourAngle, 0f) * dir;
                    else if (Blocked(dir))
                    {
                        float side = Random.value < 0.5f ? 1f : -1f;
                        foreach (float a in new[] { 35f, 70f, 105f })
                        {
                            if (!Blocked(Quaternion.Euler(0f, a * side, 0f) * dir)) { detourAngle = a * side; break; }
                            if (!Blocked(Quaternion.Euler(0f, -a * side, 0f) * dir)) { detourAngle = -a * side; break; }
                            detourAngle = a * side;
                        }
                        detourUntil = Time.time + 0.6f;
                        dir = Quaternion.Euler(0f, detourAngle, 0f) * dir;
                    }
                    desired = dir * Speed;
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), turnSpeed * dt);
                }
            }

            vy = cc.isGrounded ? -1f : vy - 9.8f * dt;
            var before = transform.position;
            cc.Move((desired + Vector3.up * vy) * dt);
            var moved = transform.position - before; moved.y = 0f;
            Velocity = moved / dt;

            if (HasTarget && Velocity.magnitude < Speed * 0.25f)
            {
                stuckTimer += dt;
                if (stuckTimer > 1.2f)
                {
                    stuckTimer = 0f;
                    StuckCount++;
                    detourAngle = Random.value < 0.5f ? 100f : -100f;
                    detourUntil = Time.time + 1.0f;
                }
            }
            else stuckTimer = 0f;
        }
    }
}
