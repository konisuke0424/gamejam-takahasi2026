using System.Collections.Generic;
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
    /// chaseMaxDuration. Its calls bring the other monsters within alertRadius to where he is, so two
    /// can close in at once. Retreat: walks away ignoring the boy for retreatDuration, then searches again.
    /// A trap's monster has a territory (SetTerritory): it wanders inside it, only notices the boy
    /// while he is inside, and chases him no further than its edge.
    /// </summary>
    public class MonsterAI : MonoBehaviour
    {
        public float searchSpeed = 1.9f;
        public float investigateSpeed = 2.4f;
        public float chaseSpeed = 2.6f;
        public float lungeSpeed = 3.4f;
        public float hearMovingDistance = 22f;
        public float hearStillDistance = 6f;
        public float sightDistance = 16f;
        public float catchDistance = 1.3f;
        public float loseSightTime = 6f;
        [Tooltip("これだけ追いかけたら諦めて退却する（秒）")]
        public float chaseMaxDuration = 10f;
        [Tooltip("退却中はカイを無視する（秒）。終わると探索に戻る")]
        public float retreatDuration = 4f;
        public float retreatDistance = 20f;
        public float retreatSpeed = 2.0f;
        [Tooltip("探索の行き先は、この確率でカイのまわり（searchAroundPlayerRadius 以内）。残りはマップのどこか")]
        public float searchTowardPlayerChance = 0.9f;
        public float searchAroundPlayerRadius = 18f;
        [Tooltip("探索の行き先を選び直す間隔（秒）。短いほどカイの動きについてくる")]
        public float searchRetargetTime = 8f;
        [Tooltip("追っている化け物の声が届く距離（m）。この中のほかの化け物もカイの居場所へ寄ってくる。0 で無効")]
        public float alertRadius = 50f;
        [Tooltip("追っている間、ほかの化け物へ居場所を伝え直す間隔（秒）")]
        public float alertInterval = 2f;
        public Vector2 callGap = new Vector2(5.5f, 10f);
        public Vector2 chaseCallGap = new Vector2(1.5f, 3.5f);
        public float firstCallDelay = 7f;
        [Tooltip("モデルの背の高さ（m）。Tripo のモデル（Resources/Monster100）をこの高さに拡大する")]
        public float height = 2.6f;
        [Tooltip("走りアニメの脚の速さ。1 で足と地面が合う。上げると脚の回転が速くなる（少し滑る）")]
        public float runAnimPace = 1.25f;

        public bool Active { get; set; }
        public bool Frozen { get; set; }
        public MonsterState State { get; private set; } = MonsterState.Search;
        public float StateTime => Time.time - stateSince;
        public VoiceEmitter Voice { get; private set; }
        public CharacterFactory.Rig Rig { get; private set; }
        /// <summary>The animated Tripo model, or null when one of the older stand-ins is used.</summary>
        public MonsterBody Body { get; private set; }
        public float DistanceToPlayer { get; private set; } = 999f;
        public int ChaseCount { get; private set; }
        /// <summary>True for a trap's monster, which keeps to its area.</summary>
        public bool HasTerritory { get; private set; }

        Vector3 territoryCenter;
        float territoryRadius;

        ForestMap forest;
        PlayerController player;
        AudioBank bank;
        NpcMotor motor;
        AudioSource breath, stepSource;
        Vector3 lastKnown;
        float stateSince, lastSeen, nextCall, nextPerceive, nextLunge, lungeUntil, stepTimer, lookAroundUntil, nextAlert;
        int last1 = -1, last2 = -1;

        /// <summary>The active monster nearest the boy, or null when none is active.</summary>
        public static MonsterAI Nearest(IReadOnlyList<MonsterAI> monsters)
        {
            MonsterAI best = null;
            foreach (var m in monsters)
                if (m != null && m.Active && (best == null || m.DistanceToPlayer < best.DistanceToPlayer)) best = m;
            return best;
        }

        /// <summary>True while any active monster is chasing the boy.</summary>
        public static bool AnyChasing(IReadOnlyList<MonsterAI> monsters)
        {
            foreach (var m in monsters)
                if (m != null && m.Active && m.State == MonsterState.Chase) return true;
            return false;
        }

        public void Init(ForestMap f, PlayerController p, AudioBank b, MaterialLibrary mats)
        {
            forest = f; player = p; bank = b;
            var cc = gameObject.AddComponent<CharacterController>();
            cc.height = 2.4f; cc.radius = 0.32f; cc.center = new Vector3(0f, 1.2f, 0f);
            motor = gameObject.AddComponent<NpcMotor>();
            motor.turnSpeed = 300f;
            Voice = gameObject.AddComponent<VoiceEmitter>();
            Voice.kind = VoiceKind.Fake;
            // Tripo model with animations, else the Blender model, else the primitive one.
            Body = MonsterBody.Build(transform, height);
            Rig = Body != null ? Body.Rig
                : CharacterFactory.BuildMonsterFromModel(transform) ?? CharacterFactory.BuildMonster(transform, mats);

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

        /// <summary>Keeps this monster inside the circle (a monster trap's area).</summary>
        public void SetTerritory(Vector3 center, float radius)
        {
            HasTerritory = true;
            territoryCenter = new Vector3(center.x, 0f, center.z);
            territoryRadius = radius;
        }

        public bool InTerritory(Vector3 p)
        {
            if (!HasTerritory) return true;
            var d = p - territoryCenter; d.y = 0f;
            return d.sqrMagnitude <= territoryRadius * territoryRadius;
        }

        /// <summary>The point itself, or the nearest point on the territory's edge.</summary>
        Vector3 KeepInside(Vector3 p)
        {
            if (InTerritory(p)) return p;
            var d = p - territoryCenter; d.y = 0f;
            return territoryCenter + d.normalized * territoryRadius;
        }

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
                nextAlert = 0f;
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
            var target = KeepInside(transform.position + away.normalized * retreatDistance);
            motor.MoveTo(KeepInside(forest.FindOpenPoint(target, HasTerritory ? 4f : 10f)), retreatSpeed);
        }

        void PickSearchTarget()
        {
            if (HasTerritory)
            {
                motor.MoveTo(forest.FindOpenPoint(territoryCenter, territoryRadius * 0.8f), searchSpeed);
                return;
            }
            // It does not know where the boy is, but it is drawn to him.
            Vector3 center;
            if (Random.value < searchTowardPlayerChance)
            {
                Vector2 o = Random.insideUnitCircle * searchAroundPlayerRadius;
                center = player.transform.position + new Vector3(o.x, 0f, o.y);
            }
            else
                center = new Vector3(Random.Range(-forest.halfSize, forest.halfSize), 0f, Random.Range(-forest.halfSize, forest.halfSize));
            motor.MoveTo(forest.FindOpenPoint(center, 12f), searchSpeed);
        }

        /// <summary>Calls the other monsters nearby over to where the boy is.</summary>
        void AlertOthers(Vector3 at)
        {
            if (alertRadius <= 0f || GameManager.I == null) return;
            foreach (var m in GameManager.I.Monsters)
            {
                if (m == this || m == null || !m.Active || m.Frozen) continue;
                if (m.State == MonsterState.Chase || m.State == MonsterState.Retreat) continue;
                if ((m.transform.position - transform.position).sqrMagnitude > alertRadius * alertRadius) continue;
                if (!m.InTerritory(at)) continue; // a trap's monster doesn't leave its area
                m.lastKnown = at;
                m.lookAroundUntil = 0f;
                m.Enter(MonsterState.Investigate);
            }
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
            bool inside = InTerritory(pp);
            if (Time.time >= nextPerceive || State == MonsterState.Chase)
            {
                nextPerceive = Time.time + 0.2f;
                sees = inside && d < sightDistance && LineOfSight();
                hears = inside && d < (player.IsMoving ? hearMovingDistance : hearStillDistance);
            }

            switch (State)
            {
                case MonsterState.Search:
                    if (sees) Enter(MonsterState.Chase);
                    else if (hears) { lastKnown = pp; Enter(MonsterState.Investigate); }
                    else if (!motor.HasTarget || Time.time - motor.TargetSetTime > searchRetargetTime) PickSearchTarget();
                    break;

                case MonsterState.Investigate:
                    if (sees) { Enter(MonsterState.Chase); break; }
                    if (hears) lastKnown = pp;
                    if (Time.time < lookAroundUntil) { motor.Stop(); transform.Rotate(0f, 70f * Time.deltaTime, 0f); }
                    else
                    {
                        lastKnown = KeepInside(lastKnown);
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
                    // A trap's monster follows him along its edge when he steps out, then loses him.
                    if (sees || d < 4f) { lastSeen = Time.time; lastKnown = KeepInside(pp); }
                    if (Time.time >= nextAlert) { nextAlert = Time.time + alertInterval; AlertOthers(lastKnown); }
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
            float breathTarget = (chasing ? 0.9f : (Active ? 0.15f : 0f)) * Hearing.Volume;
            breath.volume = Mathf.MoveTowards(breath.volume, breathTarget, Time.deltaTime * 1.5f);
            float v = motor != null ? motor.Velocity.magnitude : 0f;
            if (v > 0.3f && bank.monsterSteps != null && bank.monsterSteps.Length > 0)
            {
                stepTimer -= Time.deltaTime * v;
                if (stepTimer <= 0f)
                {
                    stepTimer = 1.25f;
                    stepSource.volume = (chasing ? 0.9f : 0.35f) * Hearing.Volume;
                    stepSource.pitch = Random.Range(0.8f, 1.0f);
                    stepSource.PlayOneShot(bank.monsterSteps[Random.Range(0, bank.monsterSteps.Length)]);
                }
            }
        }

        /// <summary>Degrees the whole body bends forward over the boy during the capture.</summary>
        public float ScareLean { get; set; }

        /// <summary>Which way its face looks (the capture close-up sits in front of it).</summary>
        public Vector3 FaceForward(Vector3 viewer) =>
            Body != null ? Body.FaceForward : (viewer - Rig.head.position).normalized;

        void Animate()
        {
            bool chasing = State == MonsterState.Chase && Active;
            if (Body != null)
            {
                Body.Drive(motor != null ? motor.Velocity.magnitude : 0f, chasing, ScareLean, runAnimPace);
                return;
            }
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
