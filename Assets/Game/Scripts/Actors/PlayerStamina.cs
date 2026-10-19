using System.Collections.Generic;
using UnityEngine;

namespace KaiForest
{
    /// <summary>
    /// The boy's stamina: drains only while a monster is chasing him and recovers afterwards.
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
        IReadOnlyList<MonsterAI> monsters;
        AudioSource pant;
        float recoverAt, nextBreath;
        int breath;

        /// <summary>The panting comes one breath at a time with a pause between (StaminaSettings.pantGap).</summary>
        bool ByBreath => settings.pantBreaths != null && settings.pantBreaths.Length > 0;

        public void Init(IReadOnlyList<MonsterAI> m)
        {
            settings = StaminaSettings.Load();
            monsters = m;
            pant = gameObject.AddComponent<AudioSource>();
            pant.playOnAwake = false;
            pant.spatialBlend = 0f;
            pant.volume = 0f;
            if (ByBreath) breath = Random.Range(0, settings.pantBreaths.Length);
            else
            {
                // Fallback: the whole take looped, as it was recorded.
                pant.clip = settings.pantClip;
                pant.loop = true;
                if (pant.clip != null) pant.Play();
            }
            Hearing.Reset();
        }

        /// <summary>The next breath (in the recorded order) once the last one and the pause after it are over.</summary>
        void Breathe(float e)
        {
            if (e <= 0.01f || pant.isPlaying || Time.time < nextBreath) return;
            var clip = settings.pantBreaths[breath];
            breath = (breath + 1) % settings.pantBreaths.Length;
            if (clip == null) return;
            pant.clip = clip;
            pant.Play();
            nextBreath = Time.time + clip.length / pant.pitch + settings.pantGap * Random.Range(0.85f, 1.15f);
        }

        void Update()
        {
            if (settings == null) return;
            float dt = Time.deltaTime;
            var gm = GameManager.I;
            bool playing = gm != null && gm.State == GameState.Playing;
            bool chased = playing && MonsterAI.AnyChasing(monsters);

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
            if (ByBreath) Breathe(e);
            Hearing.Volume = 1f - settings.maxHearingLoss * e;
            Hearing.Muffle = settings.maxMuffle * e;
        }

        void OnDestroy() => Hearing.Reset();
    }
}
