using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace KaiForest
{
    /// <summary>
    /// Mouse-only first person control.
    /// Moving the mouse looks around (the cursor is locked during play, see GameManager).
    /// Hold left = walk forward, hold right = walk backward.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public float walkSpeed = 2.9f;
        public float backSpeed = 1.5f;
        public float acceleration = 7f;
        [Tooltip("視点の感度（度/ピクセル）。ゆっくり動かしたときはこの値。プレイヤーが設定画面のマウス感度でこれに倍率をかける")]
        public float lookSensitivity = 0.225f;
        [Tooltip("マウスを速く振るほど左右に大きく回る。速く振ったときの感度は lookSensitivity ×（1 + この値）")]
        public float lookAcceleration = 0.5f;
        [Tooltip("この速さ（ピクセル/秒）から加速が始まり、lookAccelerationFullSpeed で最大になる")]
        public float lookAccelerationStartSpeed = 400f;
        public float lookAccelerationFullSpeed = 2500f;
        [Tooltip("上下の視点の限界（度）。上にも下にもこの角度まで")]
        public float pitchLimit = 10f;
        [Tooltip("マウスの速さの上限（ピクセル/秒）。ブラウザでときどき来る跳ねで視点が飛ぶのを防ぐ（フレームレートに関係なく効く）")]
        public float maxLookSpeed = 12000f;
        [Tooltip("カーソルをロックした直後、この時間（秒）はマウスの動きを無視する。ロック直後の跳ねで視点が飛ぶのを防ぐ")]
        public float lookMuteAfterLock = 0.1f;
        [Header("スマホ（タッチ）")]
        [Tooltip("スワイプで回る角度（度）。指を画面の高さぶん動かすと、これだけ回る。設定画面の感度で倍率がかかる")]
        public float touchLookDegrees = 150f;
        [Tooltip("押してからこの秒数、指がほとんど動かなければ前へ歩き出す（その前に動かすとスワイプ＝見回すだけ）")]
        public float touchWalkDelay = 0.18f;
        [Tooltip("指がこれより動いたらスワイプとみなす（画面の高さに対する割合）")]
        public float touchSwipeThreshold = 0.03f;
        public float eyeHeight = 1.25f;
        public float stepDistance = 0.78f;
        public float fieldOfView = 68f;

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

        /// <summary>0..1: the lantern falls out of his hand, down past the lower right of the view (the capture).</summary>
        public float LanternDrop { get; set; }
        static readonly Vector3 LanternDropOffset = new Vector3(0.35f, -0.9f, -0.35f);

        /// <summary>AutoTest override: -1..1 drives the walk input without a mouse.</summary>
        public float DebugWalk { get; set; }

        CharacterController cc;
        Transform lantern;
        AudioSource stepSource;
        AudioClip[] steps;
        LanternSettings lanternSettings;
        bool controlEnabled, waitForRelease, wasLocked;
        float yaw, pitch, speed, vy, stepAccum, bobPhase, lastYaw, lookMuteUntil;

        /// <summary>What each finger on the screen does, decided soon after it comes down.</summary>
        enum TouchRole { Undecided, Look, Walk, Ignore }
        struct TouchState { public TouchRole role; public Vector2 start; public float since; }
        readonly System.Collections.Generic.Dictionary<int, TouchState> touches = new System.Collections.Generic.Dictionary<int, TouchState>();
        Vector3 lanternBasePos;
        Quaternion lookOverride;
        float lookOverrideWeight;
        Vector3 viewOverridePos;
        float viewOverrideWeight, viewOverrideFov;

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
            Cam.fieldOfView = fieldOfView;
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

        /// <summary>Ignores the mouse buttons until they are released (a click that picked something up shouldn't also walk).</summary>
        public void HoldUntilRelease() => waitForRelease = true;

        /// <summary>Pulls the camera towards a world position and narrows the view to fov (the jump scare). Weight 0 = normal.</summary>
        public void SetViewOverride(Vector3 worldPos, float weight, float fov)
        {
            viewOverridePos = worldPos;
            viewOverrideWeight = Mathf.Clamp01(weight);
            viewOverrideFov = fov;
        }

        /// <summary>
        /// Phones: a finger held still walks forward (moving it while walking steers); one that moves
        /// off at once is a swipe and only looks round. The view follows the finger like the mouse.
        /// A finger put down while he can't move, or on the settings, is ignored until lifted.
        /// Returns true while some finger is walking.
        /// </summary>
        bool ReadTouch()
        {
            bool walk = false;
            int down = 0;
            float h = Mathf.Max(1f, Screen.height);
            foreach (var t in GameInput.Touches)
            {
                if (!t.press.isPressed) continue;
                down++;
                int id = t.touchId.ReadValue();
                var pos = t.position.ReadValue();
                if (!touches.TryGetValue(id, out var s))
                {
                    bool ui = GameManager.I != null && GameManager.I.UiOwnsTouch(pos);
                    s = new TouchState { role = ControlEnabled && !ui ? TouchRole.Undecided : TouchRole.Ignore, start = pos, since = Time.unscaledTime };
                }
                if (s.role == TouchRole.Ignore || !ControlEnabled || Time.timeScale == 0f) // paused: the settings window is up
                {
                    touches[id] = s;
                    continue;
                }
                if (s.role == TouchRole.Undecided)
                {
                    if ((pos - s.start).magnitude > touchSwipeThreshold * h) s.role = TouchRole.Look;
                    else if (Time.unscaledTime - s.since >= touchWalkDelay) s.role = TouchRole.Walk;
                }
                touches[id] = s;
                var d = t.delta.ReadValue() / h * touchLookDegrees * GameOptions.LookScale;
                yaw += d.x;
                pitch = Mathf.Clamp(pitch - d.y, -pitchLimit, pitchLimit);
                walk |= s.role == TouchRole.Walk;
            }
            if (down == 0) touches.Clear(); // every finger is up: forget them all
            return walk;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float input = 0f;
            var mouse = GameInput.Mouse; // none while a finger is on the screen (phones)
            // On the settings gear or window the mouse neither looks nor walks; a button pressed there
            // must be let go before it walks again.
            if (mouse != null && GameManager.I != null && GameManager.I.UiHasMouse)
            {
                waitForRelease = true;
                mouse = null;
            }
            if (ControlEnabled && mouse != null)
            {
                bool l = mouse.leftButton.isPressed, r = mouse.rightButton.isPressed;
                if (waitForRelease)
                {
                    if (!l && !r) waitForRelease = false;
                    l = r = false;
                }
                // Looking needs no button: the mouse always turns the view.
                bool locked = Cursor.lockState == CursorLockMode.Locked;
                if (locked && !wasLocked) lookMuteUntil = Time.unscaledTime + lookMuteAfterLock;
                wasLocked = locked;
                Vector2 d = mouse.delta.ReadValue();
                if (Time.unscaledTime < lookMuteUntil) d = Vector2.zero;
                d = Vector2.ClampMagnitude(d, maxLookSpeed * Mathf.Max(dt, 1f / 120f));
                // A quick swing turns further, so a flick of the wrist can turn him round.
                float swing = Mathf.InverseLerp(lookAccelerationStartSpeed, lookAccelerationFullSpeed, Mathf.Abs(d.x) / Mathf.Max(dt, 0.0001f));
                float sens = lookSensitivity * GameOptions.LookScale; // the player's own setting (settings window)
                yaw += d.x * sens * (1f + lookAcceleration * swing);
                pitch = Mathf.Clamp(pitch - d.y * sens, -pitchLimit, pitchLimit);
                if (l && !r) input = 1f;
                else if (r && !l) input = -1f;
            }
            if (ReadTouch()) input = 1f;
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
            if (viewOverrideWeight > 0f)
                Cam.transform.position = Vector3.Lerp(Cam.transform.position, viewOverridePos, viewOverrideWeight);
            Cam.fieldOfView = Mathf.Lerp(fieldOfView, viewOverrideFov, viewOverrideWeight);

            float yawVel = Mathf.DeltaAngle(lastYaw, yaw) / Mathf.Max(dt, 0.0001f);
            lastYaw = yaw;
            float drop = LanternDrop * LanternDrop; // falling: slow to start, then gone
            lantern.localPosition = lanternBasePos + new Vector3(Mathf.Sin(bobPhase) * 0.012f, Mathf.Abs(Mathf.Cos(bobPhase)) * 0.02f * moveK, 0f)
                                  + LanternDropOffset * drop;
            lantern.localRotation = Quaternion.Euler(Mathf.Sin(bobPhase) * 3f * moveK - 40f * drop, 0f, Mathf.Clamp(yawVel * 0.03f, -12f, 12f) - 30f * drop);

            // Read the settings every frame so Inspector edits during Play apply immediately.
            float flicker = Mathf.PerlinNoise(Time.time * 5.5f, 0.37f) * 0.14f + Mathf.PerlinNoise(Time.time * 17f, 0.91f) * 0.05f;
            LanternLight.range = lanternSettings.range;
            LanternLight.intensity = lanternSettings.intensity * LanternScale * (0.9f + flicker);
        }
    }
}
