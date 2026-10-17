using UnityEngine;

namespace KaiForest
{
    /// <summary>
    /// The boy's stamina: drains only while the monster is chasing him and recovers afterwards.
    /// It never affects movement. As it runs out he pants, his heart pounds (AmbienceController reads
    /// Exhaustion) and the world gets quieter and muffled (Hearing), so the voices are harder to judge.
    /// </summary>
    public class PlayerStamina : MonoBehaviour
    {
        /// <summary>1 = full, 0 = empty.</summary>
        public float Stamina { get; private set; } = 1f;

        /// <summary>0..1 strength of the out-of-breath effects (smoothed).</summary>
        public float Exhaustion { get; private set; }

        StaminaSettings settings;
        MonsterAI monster;
        AudioSource pant;
        float recoverAt;

        public void Init(MonsterAI m)
        {
            settings = StaminaSettings.Load();
            monster = m;
            pant = gameObject.AddComponent<AudioSource>();
            pant.clip = settings.pantClip;
            pant.loop = true;
            pant.playOnAwake = false;
            pant.spatialBlend = 0f;
            pant.volume = 0f;
            if (pant.clip != null) pant.Play();
            Hearing.Reset();
        }

        void Update()
        {
            if (settings == null) return;
            float dt = Time.deltaTime;
            var gm = GameManager.I;
            bool playing = gm != null && gm.State == GameState.Playing;
            bool chased = playing && monster != null && monster.Active && monster.State == MonsterState.Chase;

            if (chased)
            {
                Stamina = Mathf.Max(0f, Stamina - dt / settings.drainSeconds);
                recoverAt = Time.time + settings.recoverDelay;
            }
            else if (Time.time >= recoverAt)
                Stamina = Mathf.Min(1f, Stamina + dt / settings.recoverSeconds);

            // Effects ramp in below effectStart and peak at empty; they fade out once the run is over.
            float target = playing ? Mathf.InverseLerp(settings.effectStart, 0f, Stamina) : 0f;
            Exhaustion = Mathf.MoveTowards(Exhaustion, target, dt * (playing ? 2f : 0.8f));
            float e = Mathf.SmoothStep(0f, 1f, Exhaustion);

            pant.volume = settings.pantMaxVolume * e;
            pant.pitch = Mathf.Lerp(0.95f, 1.2f, e);
            Hearing.Volume = 1f - settings.maxHearingLoss * e;
            Hearing.Muffle = settings.maxMuffle * e;
        }

        void OnDestroy() => Hearing.Reset();
    }
}
