using UnityEngine;

namespace KaiForest
{
    /// <summary>
    /// Night forest bed (insects, wind) plus tension layers driven by how close the monster is.
    /// The heartbeat also follows the boy's exhaustion; the forest bed follows Hearing.
    /// </summary>
    public class AmbienceController : MonoBehaviour
    {
        public float insectsVolume = 0.12f;
        public float windVolume = 0.2f;
        public float droneMaxVolume = 0.5f;
        public float heartbeatRange = 16f;

        AudioSource insects, wind, drone, heart, oneShot;
        AudioBank bank;
        PlayerController player;
        MonsterAI monster;
        PlayerStamina stamina;
        float master = 1f, masterTarget = 1f, masterRate = 1f, nextBeat;

        public void Init(AudioBank b, PlayerController p, MonsterAI m, PlayerStamina s)
        {
            bank = b; player = p; monster = m; stamina = s;
            insects = Loop(b.insects);
            wind = Loop(b.wind);
            drone = Loop(b.drone);
            heart = Make2D();
            oneShot = Make2D();
            drone.volume = 0f;
        }

        AudioSource Make2D()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            return s;
        }

        AudioSource Loop(AudioClip c)
        {
            var s = Make2D();
            s.clip = c;
            s.loop = true;
            s.volume = 0f;
            if (c != null) s.Play();
            return s;
        }

        public void FadeTo(float target, float seconds)
        {
            masterTarget = target;
            masterRate = Mathf.Abs(target - master) / Mathf.Max(0.01f, seconds);
        }

        public void PlayOneShot(AudioClip c, float vol)
        {
            if (c != null) oneShot.PlayOneShot(c, vol);
        }

        void Update()
        {
            master = Mathf.MoveTowards(master, masterTarget, masterRate * Time.deltaTime);
            float d = monster != null && monster.Active ? monster.DistanceToPlayer : 999f;
            bool chase = monster != null && monster.Active && monster.State == MonsterState.Chase;

            insects.volume = insectsVolume * master * Hearing.Volume;
            wind.volume = windVolume * master * Hearing.Volume;
            float dk = Mathf.Clamp01(Mathf.InverseLerp(45f, 6f, d));
            float droneTarget = (dk * 0.7f + (chase ? 0.3f : 0f)) * droneMaxVolume * master;
            drone.volume = Mathf.MoveTowards(drone.volume, droneTarget, Time.deltaTime * 0.4f);

            float hk = Mathf.Clamp01(Mathf.InverseLerp(heartbeatRange, 2f, d));
            if (chase) hk = Mathf.Max(hk, 0.45f);
            // Out of breath: the heart pounds louder and faster whatever the monster is doing.
            if (stamina != null) hk = Mathf.Max(hk, stamina.Exhaustion);
            if (hk > 0.01f && Time.time >= nextBeat && bank.heartbeat != null)
            {
                heart.PlayOneShot(bank.heartbeat, Mathf.Lerp(0.25f, 1f, hk) * master);
                nextBeat = Time.time + Mathf.Lerp(1.05f, 0.42f, hk);
            }
        }
    }
}
