using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace KaiForest
{
    /// <summary>
    /// Mouse-only first person control.
    /// Hold left = walk forward, hold right = walk backward; moving the mouse while holding turns.
    /// Hold the wheel button and drag = look around without moving.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public float walkSpeed = 2.9f;
        public float backSpeed = 1.5f;
        public float acceleration = 7f;
        public float lookSensitivity = 0.15f;
        public float eyeHeight = 1.25f;
        public float stepDistance = 0.78f;

        public Camera Cam { get; private set; }
        public Light LanternLight { get; private set; }
        /// <summary>
        /// When control is handed over, a mouse button still held from the click that started play
        /// (title / story skip) is ignored until it is released, so the boy doesn't lurch forward.
        /// </summary>
        public bool ControlEnabled
        {
            get => controlEnabled;
            set
            {
                if (value && !controlEnabled) waitForRelease = true;
                controlEnabled = value;
            }
        }
        public bool IsMoving => Mathf.Abs(speed) > 0.35f;
        public float Speed => speed;
        public float Yaw => yaw;
        public Vector3 HeadPosition => Cam.transform.position;

        /// <summary>Multiplier on the lantern light (lowered during the reunion so mother isn't blown out).</summary>
        public float LanternScale { get; set; } = 1f;

        /// <summary>AutoTest override: -1..1 drives the walk input without a mouse.</summary>
        public float DebugWalk { get; set; }

        CharacterController cc;
        Transform lantern;
        AudioSource stepSource;
        AudioClip[] steps;
        LanternSettings lanternSettings;
        bool controlEnabled, waitForRelease;
        float yaw, pitch, speed, vy, stepAccum, bobPhase, lastYaw;
        Vector3 lanternBasePos, lightLocalPos;
        Quaternion lookOverride;
        float lookOverrideWeight;

        public void Init(MaterialLibrary mats, AudioClip[] stepClips)
        {
            cc = GetComponent<CharacterController>();
            cc.height = 1.4f;
            cc.radius = 0.28f;
            cc.center = new Vector3(0f, 0.7f, 0f);
            cc.stepOffset = 0.3f;
            cc.slopeLimit = 50f;

            var camGo = new GameObject("PlayerCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(transform, false);
            camGo.transform.localPosition = new Vector3(0f, eyeHeight, 0f);
            Cam = camGo.AddComponent<Camera>();
            Cam.nearClipPlane = 0.05f;
            Cam.farClipPlane = 40f; // fog hides everything past ~25 m; fewer trees to draw
            Cam.fieldOfView = 68f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = RenderSettings.fogColor;
            camGo.AddComponent<AudioListener>();
            var data = Cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            lanternSettings = LanternSettings.Load();
            var rig = CharacterFactory.BuildHandLantern(camGo.transform, mats, lanternSettings.range, lanternSettings.intensity);
            lantern = rig.lantern;
            LanternLight = rig.light;
            lanternBasePos = lantern.localPosition;
            lightLocalPos = LanternLight.transform.localPosition;

            steps = stepClips;
            stepSource = gameObject.AddComponent<AudioSource>();
            stepSource.playOnAwake = false;
            stepSource.spatialBlend = 0f;
            stepSource.volume = 0.32f;
        }

        public void Teleport(Vector3 pos, float yawDeg)
        {
            cc.enabled = false;
            transform.position = pos;
            cc.enabled = true;
            yaw = lastYaw = yawDeg;
            pitch = 0f;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Cam.transform.localRotation = Quaternion.identity;
        }

        public void FaceTowards(Vector3 worldPoint)
        {
            var d = worldPoint - HeadPosition;
            yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Cam.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        /// <summary>Blends the camera towards a forced world rotation (used for the reunion / jump scare).</summary>
        public void SetLookOverride(Quaternion worldRot, float weight)
        {
            lookOverride = worldRot;
            lookOverrideWeight = Mathf.Clamp01(weight);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float input = 0f;
            var mouse = GameManager.IgnoreMouse ? null : Mouse.current;
            if (ControlEnabled && mouse != null)
            {
                bool l = mouse.leftButton.isPressed, r = mouse.rightButton.isPressed, m = mouse.middleButton.isPressed;
                if (waitForRelease)
                {
                    if (!l && !r && !m) waitForRelease = false;
                    l = r = m = false;
                }
                if (l || r || m)
                {
                    Vector2 d = mouse.delta.ReadValue();
                    yaw += d.x * lookSensitivity;
                    pitch = Mathf.Clamp(pitch - d.y * lookSensitivity, -70f, 70f);
                }
                if (l && !r) input = 1f;
                else if (r && !l) input = -1f;
            }
            if (ControlEnabled && DebugWalk != 0f) input = DebugWalk;

            float target = input > 0f ? walkSpeed * input : backSpeed * input;
            speed = Mathf.MoveTowards(speed, target, acceleration * dt);

            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var camRot = Quaternion.Euler(pitch, 0f, 0f);
            if (lookOverrideWeight > 0f)
                Cam.transform.rotation = Quaternion.Slerp(transform.rotation * camRot, lookOverride, lookOverrideWeight);
            else
                Cam.transform.localRotation = camRot;

            vy = cc.isGrounded ? -1f : vy - 9.8f * dt;
            var before = transform.position;
            cc.Move((transform.forward * speed + Vector3.up * vy) * dt);
            var moved = transform.position - before; moved.y = 0f;
            float dist = moved.magnitude;

            stepAccum += dist;
            if (stepAccum >= stepDistance)
            {
                stepAccum = 0f;
                if (steps != null && steps.Length > 0)
                {
                    stepSource.pitch = Random.Range(0.88f, 1.08f);
                    stepSource.PlayOneShot(steps[Random.Range(0, steps.Length)], Random.Range(0.7f, 1f));
                }
            }

            // Head bob and lantern sway follow the walk.
            bobPhase += dist / stepDistance * Mathf.PI;
            float moveK = Mathf.Clamp01(Mathf.Abs(speed) / walkSpeed);
            Cam.transform.localPosition = new Vector3(Mathf.Cos(bobPhase) * 0.012f * moveK, eyeHeight + Mathf.Sin(bobPhase * 2f) * 0.025f * moveK, 0f);

            float yawVel = Mathf.DeltaAngle(lastYaw, yaw) / Mathf.Max(dt, 0.0001f);
            lastYaw = yaw;
            lantern.localPosition = lanternBasePos + new Vector3(Mathf.Sin(bobPhase) * 0.012f, Mathf.Abs(Mathf.Cos(bobPhase)) * 0.02f * moveK, 0f);
            lantern.localRotation = Quaternion.Euler(Mathf.Sin(bobPhase) * 3f * moveK, 0f, Mathf.Clamp(yawVel * 0.03f, -12f, 12f));

            // Read the settings every frame so Inspector edits during Play apply immediately.
            // Reach pulls the light back behind the eye: a farther source flattens the 1/d² falloff,
            // and EffectiveIntensity compensates so the brightness 2 m ahead stays the same.
            var s = lanternSettings;
            LanternLight.transform.localPosition = lightLocalPos;
            LanternLight.transform.position -= Cam.transform.forward * s.reach;
            float flicker = Mathf.PerlinNoise(Time.time * 5.5f, 0.37f) * 0.14f + Mathf.PerlinNoise(Time.time * 17f, 0.91f) * 0.05f;
            LanternLight.range = s.EffectiveRange;
            LanternLight.intensity = s.EffectiveIntensity * LanternScale * (0.9f + flicker);
        }
    }
}
