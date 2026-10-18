using UnityEngine;

namespace KaiForest
{
    /// <summary>
    /// Plays a VoiceLine as positional audio. WebGL has no realtime audio filters, so distance is
    /// faked by cross-fading the dry "near" take into a pre-filtered "far" take, and attenuation is
    /// computed here rather than by the engine (engine rolloff is effectively disabled).
    /// How it fades with distance comes from VoiceSettings (by kind: mother's voice or a fake's).
    /// </summary>
    public class VoiceEmitter : MonoBehaviour
    {
        [Tooltip("お母さんの声か偽物の声か（VoiceSettings のどちらの範囲・音量を使うか）")]
        public VoiceKind kind = VoiceKind.Mother;

        AudioSource near, far;
        Transform listener;
        VoiceSettings settings;
        public bool IsPlaying => near != null && near.isPlaying;
        public int PlayCount { get; private set; }
        public string LastLineId { get; private set; }
        public float LastPlayTime { get; private set; } = -999f;

        void Awake()
        {
            settings = VoiceSettings.Load();
            near = MakeSource();
            far = MakeSource();
        }

        AudioSource MakeSource()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 5000f;
            s.maxDistance = 5001f;
            s.dopplerLevel = 0f;
            s.spread = 0f;
            s.volume = 0f;
            return s;
        }

        /// <summary>Starts the line and returns its duration in seconds.</summary>
        public float Play(VoiceLine line, float pitch = 1f)
        {
            if (line == null || line.near == null) return 0f;
            near.Stop(); far.Stop();
            near.clip = line.near;
            far.clip = line.far != null ? line.far : line.near;
            near.pitch = far.pitch = pitch;
            UpdateMix();
            near.Play();
            far.Play();
            PlayCount++;
            LastLineId = line.id;
            LastPlayTime = Time.time;
            return line.near.length / Mathf.Max(0.1f, pitch);
        }

        public void Stop()
        {
            near.Stop(); far.Stop();
        }

        /// <summary>0..1 gain this emitter would have at the given distance.</summary>
        public float GainAt(float d) => settings.GainAt(kind, d);

        void LateUpdate()
        {
            if (IsPlaying) UpdateMix();
        }

        void UpdateMix()
        {
            if (listener == null)
            {
                var l = FindAnyObjectByType<AudioListener>();
                if (l == null) return;
                listener = l.transform;
            }
            float d = Vector3.Distance(listener.position, transform.position);
            // Hearing: an out-of-breath boy hears voices quieter and muffled (pushed toward the far take).
            float gain = GainAt(d) * settings.Loudness(kind) * Hearing.Volume;
            float mix = Mathf.Max(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(settings.nearDistance, settings.farDistance, d)), Hearing.Muffle);
            near.volume = gain * (1f - mix);
            far.volume = gain * mix;
        }
    }
}
