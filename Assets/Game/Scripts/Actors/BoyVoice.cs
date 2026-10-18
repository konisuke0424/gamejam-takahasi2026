using UnityEngine;

namespace KaiForest
{
    /// <summary>
    /// The boy's own lines, heard as his thoughts (no direction, not muffled by exhaustion):
    /// a line when the game starts from the title, one when he walks into the outer wall (there is no
    /// way on), one from VoiceTrigger spots (the first fork), and a reaction after hearing a fake's or
    /// mother's voice. The reactions are kept rare by their cooldowns and by BoyVoiceSettings.minGap
    /// between any two lines. Never talks over himself; only during play. Lives on the Player
    /// (its CharacterController reports the wall bumps).
    /// </summary>
    public class BoyVoice : MonoBehaviour
    {
        public static BoyVoice Instance { get; private set; }

        BoyVoiceSettings settings;
        AudioSource source;
        AudioClip pending;
        Transform walls;
        float pendingAt, nextAny, nextFake, nextMother, nextWall;

        public bool IsSpeaking => source != null && source.isPlaying;

        void Awake()
        {
            Instance = this;
            settings = BoyVoiceSettings.Load();
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            VoiceEmitter.Played += OnVoicePlayed;
        }

        void OnDestroy()
        {
            VoiceEmitter.Played -= OnVoicePlayed;
            if (Instance == this) Instance = null;
        }

        /// <summary>The map's outer walls (ForestMap/Walls); bumping into them gets the wall line.</summary>
        public void Init(ForestMap map) => walls = map != null ? map.transform.Find("Walls") : null;

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (walls == null || !hit.collider.transform.IsChildOf(walls)) return;
            if (!Playing || Time.time < nextWall || pending != null || IsSpeaking) return;
            nextWall = Time.time + settings.wallCooldown;
            Say(settings.wallLine);
        }

        static bool Playing => GameManager.I != null && GameManager.I.State == GameState.Playing;

        /// <summary>Says the line after delay seconds (a line already waiting is replaced). For lines that must be said.</summary>
        public void Say(AudioClip clip, float delay = 0f)
        {
            if (clip == null) return;
            pending = clip;
            pendingAt = Time.time + delay;
        }

        /// <summary>The line for starting the game (from the title); returns the seconds until it has been said.</summary>
        public float SayStart()
        {
            Say(settings.startLine, settings.startDelay);
            return settings.startLine != null ? settings.startDelay + settings.startLine.length : 0f;
        }

        public void Stop()
        {
            pending = null;
            source.Stop();
        }

        void OnVoicePlayed(VoiceEmitter voice, float heard, float length)
        {
            if (!Playing || pending != null || IsSpeaking || Time.time < nextAny) return;
            bool fake = voice.kind == VoiceKind.Fake;
            var clip = fake ? settings.fakeHeardLine : settings.motherHeardLine;
            if (clip == null || heard < (fake ? settings.fakeHeardMinVolume : settings.motherHeardMinVolume)) return;
            if (Time.time < (fake ? nextFake : nextMother)) return;
            if (fake) nextFake = Time.time + settings.fakeHeardCooldown;
            else nextMother = Time.time + settings.motherHeardCooldown;
            Say(clip, length + settings.afterHeardDelay);
        }

        void Update()
        {
            if (!Playing)
            {
                pending = null;
                return;
            }
            source.volume = settings.volume;
            if (pending == null || Time.time < pendingAt || IsSpeaking) return;
            source.clip = pending;
            source.Play();
            pending = null;
            nextAny = Time.time + source.clip.length + settings.minGap;
        }
    }
}
