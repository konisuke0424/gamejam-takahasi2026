using UnityEngine;

namespace KaiForest
{
    public enum MonsterState { Search, Investigate, Chase, Retreat }

    /// <summary>
    /// Imitates the mother's voice while hunting the boy.
    /// Tells for the player: it only knows a few of her phrases, always says them with exactly the same
    /// intonation, and the takes themselves warble / stutter / end wrong (baked by build_audio.py).
    /// Search: drifts around the boy's rough area. Investigate: heard footsteps, walks to the sound.
    /// Chase: saw the lantern, runs at him; loses him after a while out of sight, or gives up after
    /// chaseMaxDuration. Retreat: walks away ignoring the boy for retreatDuration, then searches again.
    /// </summary>
    public class MonsterAI : MonoBehaviour
    {
        public float searchSpeed = 1.5f;
        public float investigateSpeed = 2.1f;
        public float chaseSpeed = 2.6f;
        public float lungeSpeed = 3.4f;
        public float hearMovingDistance = 15f;
        public float hearStillDistance = 4.5f;
        public float sightDistance = 12f;
        public float catchDistance = 1.3f;
        public float loseSightTime = 4.5f;
        [Tooltip("これだけ追いかけたら諦めて退却する（秒）")]
        public float chaseMaxDuration = 10f;
        [Tooltip("退却中はカイを無視する（秒）。終わると探索に戻る")]
        public float retreatDuration = 8f;
        public float retreatDistance = 40f;
        public float retreatSpeed = 2.0f;
        public float searchAroundPlayerRadius = 35f;
        public Vector2 callGap = new Vector2(5.5f, 10f);
        public Vector2 chaseCallGap = new Vector2(1.5f, 3.5f);
        public float firstCallDelay = 7f;

        public bool Active { get; set; }
        public bool Frozen { get; set; }
        public MonsterState State { get; private set; } = MonsterState.Search;
        public float StateTime => Time.time - stateSince;
        public VoiceEmitter Voice { get; private set; }
        public CharacterFactory.Rig Rig { get; private set; }
        public float DistanceToPlayer { get; private set; } = 999f;
        public int ChaseCount { get; private set; }

        ForestMap forest;
        PlayerController player;
        AudioBank bank;
        NpcMotor motor;
        AudioSource breath, stepSource;
        Vector3 lastKnown;
        float stateSince, lastSeen, nextCall, nextPerceive, nextLunge, lungeUntil, stepTimer, lookAroundUntil;
        int last1 = -1, last2 = -1;

        public void Init(ForestMap f, PlayerController p, AudioBank b, MaterialLibrary mats)
        {
            forest = f; player = p; bank = b;
            var cc = gameObject.AddComponent<CharacterController>();
            cc.height = 2.4f; cc.radius = 0.32f; cc.center = new Vector3(0f, 1.2f, 0f);
            motor = gameObject.AddComponent<NpcMotor>();
            motor.turnSpeed = 300f;
            Voice = gameObject.AddComponent<VoiceEmitter>();
            Voice.audibleRange = 200f;
            Voice.loudness = 1f;
            Rig = CharacterFactory.BuildMonster(transform, mats);

            breath = Make3D(bank.monsterBreath, true, 20f);
            stepSource = Make3D(null, false, 25f);
        }

        AudioSource Make3D(AudioClip clip, bool loop, float maxDist)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = loop;
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 1.5f;
            s.maxDistance = maxDist;
            s.dopplerLevel = 0f;
            s.volume = 0f;
            return s;
        }

        public void Activate()
        {
            Active = true;
            nextCall = Time.time + firstCallDelay;
            Enter(MonsterState.Search);
            if (breath.clip != null) breath.Play();
        }

        public void Teleport(Vector3 p) => motor.Teleport(p);

        public void Halt()
        {
            Active = false;
            motor.Stop();
            Voice.Stop();
        }

        void Enter(MonsterState s)
        {
            if (s == MonsterState.Chase && State != MonsterState.Chase)
            {
                ChaseCount++;
                nextCall = Time.time + 0.2f;
                nextLunge = Time.time + 1.5f;
            }
            State = s;
            stateSince = Time.time;
            if (s == MonsterState.Search) PickSearchTarget();
            if (s == MonsterState.Retreat) PickRetreatTarget();
        }

        void PickRetreatTarget()
        {
            // Walk off away from the boy so it doesn't spot him again right away.
            var away = transform.position - player.transform.position; away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere;
            away.y = 0f;
            motor.MoveTo(forest.FindOpenPoint(transform.position + away.normalized * retreatDistance, 10f), retreatSpeed);
        }

        void PickSearchTarget()
        {
            // It does not know where the boy is, but it is drawn to him.
            Vector3 center;
            if (Random.value < 0.7f)
            {
                Vector2 o = Random.insideUnitCircle * searchAroundPlayerRadius;
                center = player.transform.position + new Vector3(o.x, 0f, o.y);
            }
            else
                center = new Vector3(Random.Range(-forest.halfSize, forest.halfSize), 0f, Random.Range(-forest.halfSize, forest.halfSize));
            motor.MoveTo(forest.FindOpenPoint(center, 12f), searchSpeed);
        }

        bool LineOfSight()
        {
            var eye = transform.position + Vector3.up * 2.4f;
            return !Physics.Linecast(eye, player.HeadPosition, ForestMap.ObstacleMask, QueryTriggerInteraction.Ignore);
        }

        void Update()
        {
            Animate();
            UpdateSounds();
            if (!Active) return;

            var pp = player.transform.position;
            var flat = pp - transform.position; flat.y = 0f;
            float d = flat.magnitude;
            DistanceToPlayer = d;
            if (Frozen) { motor.Stop(); return; }

            if (d < catchDistance)
            {
                GameManager.I.OnCaught(this);
                return;
            }

            bool sees = false, hears = false;
            if (Time.time >= nextPerceive || State == MonsterState.Chase)
            {
                nextPerceive = Time.time + 0.2f;
                sees = d < sightDistance && LineOfSight();
                hears = d < (player.IsMoving ? hearMovingDistance : hearStillDistance);
            }

            switch (State)
            {
                case MonsterState.Search:
                    if (sees) Enter(MonsterState.Chase);
                    else if (hears) { lastKnown = pp; Enter(MonsterState.Investigate); }
                    else if (!motor.HasTarget || Time.time - motor.TargetSetTime > 20f) PickSearchTarget();
                    break;

                case MonsterState.Investigate:
                    if (sees) { Enter(MonsterState.Chase); break; }
                    if (hears) lastKnown = pp;
                    if (Time.time < lookAroundUntil) { motor.Stop(); transform.Rotate(0f, 70f * Time.deltaTime, 0f); }
                    else
                    {
                        motor.MoveTo(lastKnown, investigateSpeed);
                        var toLast = lastKnown - transform.position; toLast.y = 0f;
                        if (toLast.magnitude < 1.2f)
                        {
                            if (lookAroundUntil < stateSince) lookAroundUntil = Time.time + 2.5f;
                            else Enter(MonsterState.Search);
                        }
                    }
                    if (StateTime > 25f) Enter(MonsterState.Search);
                    break;

                case MonsterState.Chase:
                    if (sees || d < 4f) { lastSeen = Time.time; lastKnown = pp; }
                    float speed = chaseSpeed;
                    if (Time.time < lungeUntil) speed = lungeSpeed;
                    else if (d < 6f && Time.time > nextLunge) { lungeUntil = Time.time + 1.2f; nextLunge = Time.time + 4.5f; }
                    motor.MoveTo(lastKnown, speed);
                    if (Time.time - lastSeen > loseSightTime)
                    {
                        lookAroundUntil = 0f;
                        Enter(MonsterState.Investigate);
                    }
                    else if (StateTime > chaseMaxDuration)
                        Enter(MonsterState.Retreat);
                    break;

                case MonsterState.Retreat:
                    // Ignores sight and hearing; can still catch him if he walks right into it.
                    if (StateTime > retreatDuration) Enter(MonsterState.Search);
                    break;
            }

            if (Time.time >= nextCall && !Voice.IsPlaying)
            {
                VoiceLine line;
                float gap;
                if (State == MonsterState.Chase)
                {
                    line = AudioBank.PickWeighted(bank.chaseLines, -1, -1, out _);
                    gap = Random.Range(chaseCallGap.x, chaseCallGap.y);
                }
                else
                {
                    line = AudioBank.PickWeighted(bank.monsterLines, last1, -1, out int idx);
                    last2 = last1; last1 = idx;
                    gap = Random.Range(callGap.x, callGap.y);
                    if (State == MonsterState.Investigate) gap *= 0.6f;
                }
                // Always the exact same pitch: mechanical repetition is one of the tells.
                float len = Voice.Play(line, 1f);
                nextCall = Time.time + len + gap;
            }
        }

        void UpdateSounds()
        {
            bool chasing = Active && State == MonsterState.Chase;
            breath.volume = Mathf.MoveTowards(breath.volume, chasing ? 0.9f : (Active ? 0.15f : 0f), Time.deltaTime * 1.5f);
            float v = motor != null ? motor.Velocity.magnitude : 0f;
            if (v > 0.3f && bank.monsterSteps != null && bank.monsterSteps.Length > 0)
            {
                stepTimer -= Time.deltaTime * v;
                if (stepTimer <= 0f)
                {
                    stepTimer = 1.25f;
                    stepSource.volume = chasing ? 0.9f : 0.35f;
                    stepSource.pitch = Random.Range(0.8f, 1.0f);
                    stepSource.PlayOneShot(bank.monsterSteps[Random.Range(0, bank.monsterSteps.Length)]);
                }
            }
        }

        /// <summary>Degrees the whole body bends forward over the boy during the capture.</summary>
        public float ScareLean { get; set; }

        void Animate()
        {
            float t = Time.time;
            if (ScareLean > 0f)
            {
                float j = Random.Range(-1f, 1f);
                Rig.body.localPosition = Vector3.zero;
                Rig.body.localRotation = Quaternion.Euler(ScareLean, 0f, j * 2f);
                Rig.head.localRotation = Quaternion.Euler(-ScareLean * 0.85f, j * 3f, 28f + j * 6f);
                Rig.armL.localRotation = Quaternion.Euler(-70f, 0f, 25f);
                Rig.armR.localRotation = Quaternion.Euler(-70f, 0f, -25f);
                return;
            }
            bool chasing = State == MonsterState.Chase && Active;
            float v = motor != null ? Mathf.Clamp01(motor.Velocity.magnitude / chaseSpeed) : 0f;
            Rig.body.localRotation = Quaternion.Euler((chasing ? 14f : 3f) * v + Mathf.Sin(t * 0.9f) * 2f, 0f, Mathf.Sin(t * 1.3f) * 3f);
            Rig.body.localPosition = new Vector3(0f, Mathf.Sin(t * 1.1f) * 0.04f, 0f);
            float swing = (chasing ? 35f : 10f) * v;
            Rig.armL.localRotation = Quaternion.Euler(Mathf.Sin(t * 5f) * swing, 0f, 3f + Mathf.Sin(t * 0.8f) * 2f);
            Rig.armR.localRotation = Quaternion.Euler(-Mathf.Sin(t * 5f) * swing, 0f, -3f - Mathf.Sin(t * 0.7f) * 2f);
            // The head tilts at a wrong angle and twitches.
            float twitch = Mathf.PerlinNoise(t * (chasing ? 9f : 2f), 7.7f) > 0.78f ? Random.Range(-12f, 12f) : 0f;
            Rig.head.localRotation = Quaternion.Euler(6f + twitch * 0.3f, Mathf.Sin(t * 0.5f) * 20f, 24f + twitch);
        }
    }
}
