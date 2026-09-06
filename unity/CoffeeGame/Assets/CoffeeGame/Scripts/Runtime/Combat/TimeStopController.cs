using System;
using System.Collections.Generic;
using CoffeeGame.Actors;
using CoffeeGame.Presentation;
using UnityEngine;

namespace CoffeeGame.Combat
{
    public enum TimeStopEndReason
    {
        Completed,
        Cancelled,
        CasterLost,
        Disabled
    }

    [DisallowMultipleComponent]
    public sealed class TimeStopController : MonoBehaviour
    {
        private sealed class DeferredTarget
        {
            public readonly HashSet<long> AttackIds = new HashSet<long>();
            public int Amount;
            public GameObject Source;
            public Vector3 HitPoint;
            public Vector3 Knockback;
            public long LastAttackId;
        }

        private readonly struct RigidbodyState
        {
            public RigidbodyState(Rigidbody body)
            {
                IsKinematic = body.isKinematic;
                UseGravity = body.useGravity;
                Constraints = body.constraints;
                Velocity = body.linearVelocity;
                AngularVelocity = body.angularVelocity;
            }

            public bool IsKinematic { get; }
            public bool UseGravity { get; }
            public RigidbodyConstraints Constraints { get; }
            public Vector3 Velocity { get; }
            public Vector3 AngularVelocity { get; }
        }

        private readonly struct ParticleState
        {
            public ParticleState(ParticleSystem particle)
            {
                WasPlaying = particle.isPlaying;
                WasPaused = particle.isPaused;
            }

            public bool WasPlaying { get; }
            public bool WasPaused { get; }
        }

        private readonly struct TrailState
        {
            public TrailState(TrailRenderer trail)
            {
                Emitting = trail.emitting;
                Time = trail.time;
            }

            public bool Emitting { get; }
            public float Time { get; }
        }

        private readonly Dictionary<Health, DeferredTarget> deferred = new Dictionary<Health, DeferredTarget>();
        private readonly List<Health> deferredOrder = new List<Health>();
        private readonly Dictionary<Health, HashSet<long>> observedAttacks = new Dictionary<Health, HashSet<long>>();
        private readonly Dictionary<Rigidbody, RigidbodyState> rigidbodies = new Dictionary<Rigidbody, RigidbodyState>();
        private readonly Dictionary<Animator, float> animators = new Dictionary<Animator, float>();
        private readonly Dictionary<ParticleSystem, ParticleState> particles = new Dictionary<ParticleSystem, ParticleState>();
        private readonly Dictionary<AudioSource, bool> audioSources = new Dictionary<AudioSource, bool>();
        private readonly Dictionary<TrailRenderer, TrailState> trails = new Dictionary<TrailRenderer, TrailState>();
        private readonly HashSet<GameObject> independentClockOwners = new HashSet<GameObject>();

        private GameObject caster;
        private float remaining;
        private float activeStartUnityTime;
        private float frozenWorldTime;
        private float worldTimeOffset;
        private bool paused;
        private bool finishing;
        private TimeStopWorldEffect worldEffect;

        public static TimeStopController Instance { get; private set; }
        public bool IsActive { get; private set; }
        public bool IsPaused => paused;
        public float Remaining => IsActive ? Mathf.Max(0f, remaining) : 0f;
        public GameObject Caster => caster;

        public event Action<TimeStopController> Started;
        public event Action<TimeStopController, TimeStopEndReason> Ended;

        public static TimeStopController EnsureExists()
        {
            if (Instance != null)
            {
                return Instance;
            }

            var host = new GameObject("Time Stop Controller");
            return host.AddComponent<TimeStopController>();
        }

        public void InitializeWorldVisual(Camera worldCamera)
        {
            if (worldCamera == null)
            {
                return;
            }

            worldEffect = worldCamera.GetComponent<TimeStopWorldEffect>();
            if (worldEffect == null)
            {
                worldEffect = worldCamera.gameObject.AddComponent<TimeStopWorldEffect>();
            }

            worldEffect.Initialize(worldCamera);
            worldEffect.SetActive(IsActive);
        }

        public bool TryBegin(GameObject abilityCaster, float durationSeconds = 10f)
        {
            if (IsActive || abilityCaster == null || durationSeconds <= 0f)
            {
                return false;
            }

            caster = CombatOwnership.Resolve(abilityCaster);
            if (caster == null)
            {
                return false;
            }

            independentClockOwners.Add(caster);
            remaining = durationSeconds;
            activeStartUnityTime = UnityEngine.Time.time;
            frozenWorldTime = activeStartUnityTime - worldTimeOffset;
            IsActive = true;
            ClearDeferred();
            CaptureNewFrozenState();
            if (worldEffect == null && Camera.main != null)
            {
                InitializeWorldVisual(Camera.main);
            }
            worldEffect?.SetActive(true);
            UpdateShaderClock();
            Started?.Invoke(this);
            return true;
        }

        public void Cancel()
        {
            Finish(false, TimeStopEndReason.Cancelled);
        }

        public void SetPaused(bool value)
        {
            paused = value;
        }

        public bool IsFrozen(GameObject owner)
        {
            if (!IsActive)
            {
                return false;
            }

            GameObject resolved = CombatOwnership.Resolve(owner);
            if (resolved == null)
            {
                return true;
            }

            if (resolved == caster)
            {
                return false;
            }

            Transform resolvedTransform = resolved.transform;
            Transform casterTransform = caster != null ? caster.transform : null;
            return casterTransform == null ||
                (!resolvedTransform.IsChildOf(casterTransform) && !casterTransform.IsChildOf(resolvedTransform));
        }

        public float GetClockTime(GameObject owner)
        {
            GameObject resolved = CombatOwnership.Resolve(owner);
            if (resolved != null && UsesIndependentClock(resolved))
            {
                return UnityEngine.Time.time;
            }

            return IsActive ? frozenWorldTime : UnityEngine.Time.time - worldTimeOffset;
        }

        public void Advance(float deltaTime)
        {
            if (!IsActive)
            {
                UpdateShaderClock();
                return;
            }

            if (caster == null)
            {
                Finish(false, TimeStopEndReason.CasterLost);
                return;
            }

            Health casterHealth = caster.GetComponentInParent<Health>();
            if (casterHealth != null && !casterHealth.IsAlive)
            {
                Finish(false, TimeStopEndReason.CasterLost);
                return;
            }

            CaptureNewFrozenState();
            if (paused || UnityEngine.Time.timeScale <= 0f)
            {
                UpdateShaderClock();
                return;
            }

            remaining = Mathf.Max(0f, remaining - Mathf.Max(0f, deltaTime));
            UpdateShaderClock();
            if (remaining <= 0f)
            {
                Finish(true, TimeStopEndReason.Completed);
            }
        }

        internal bool TryQueueDamage(Health target, DamageInfo damage)
        {
            if (!IsActive || target == null || !target.IsAlive || !IsFrozen(target.gameObject))
            {
                return false;
            }

            if (!deferred.TryGetValue(target, out DeferredTarget pending))
            {
                pending = new DeferredTarget();
                deferred.Add(target, pending);
                deferredOrder.Add(target);
            }

            if (!pending.AttackIds.Add(damage.AttackId))
            {
                return false;
            }

            pending.Amount = (int)Math.Min(int.MaxValue, (long)pending.Amount + damage.Amount);
            pending.Source = damage.Source;
            pending.HitPoint = damage.HitPoint;
            pending.Knockback += damage.Knockback;
            pending.LastAttackId = damage.AttackId;
            return true;
        }

        internal bool TryClaimDamage(Health target, long attackId)
        {
            if (!IsActive || target == null || !IsFrozen(target.gameObject))
            {
                return false;
            }

            if (!observedAttacks.TryGetValue(target, out HashSet<long> attacks))
            {
                attacks = new HashSet<long>();
                observedAttacks.Add(target, attacks);
            }

            return attacks.Add(attackId);
        }

        private bool UsesIndependentClock(GameObject owner)
        {
            foreach (GameObject independentOwner in independentClockOwners)
            {
                if (independentOwner == null)
                {
                    continue;
                }

                if (owner == independentOwner || owner.transform.IsChildOf(independentOwner.transform))
                {
                    return true;
                }
            }

            return false;
        }

        private void Finish(bool resolveDamage, TimeStopEndReason reason)
        {
            if (!IsActive || finishing)
            {
                return;
            }

            finishing = true;
            worldTimeOffset += Mathf.Max(0f, UnityEngine.Time.time - activeStartUnityTime);
            if (resolveDamage)
            {
                ResolveDeferredDamage();
            }
            else
            {
                ClearDeferred();
            }

            RestoreFrozenState();
            IsActive = false;
            remaining = 0f;
            caster = null;
            worldEffect?.SetActive(false);
            UpdateShaderClock();
            finishing = false;
            Ended?.Invoke(this, reason);
        }

        private void ResolveDeferredDamage()
        {
            for (int index = 0; index < deferredOrder.Count; index++)
            {
                Health target = deferredOrder[index];
                if (target == null || !target.IsAlive || !deferred.TryGetValue(target, out DeferredTarget pending))
                {
                    continue;
                }

                var combined = new DamageInfo(
                    pending.Amount,
                    pending.Source,
                    pending.HitPoint,
                    pending.Knockback,
                    pending.LastAttackId);
                target.ApplyResolvedDamage(combined);
            }

            ClearDeferred();
        }

        private void ClearDeferred()
        {
            deferred.Clear();
            deferredOrder.Clear();
            observedAttacks.Clear();
        }

        private void CaptureNewFrozenState()
        {
            CaptureRigidbodies();
            CaptureAnimators();
            CaptureParticles();
            CaptureAudio();
            CaptureTrails();
        }

        private bool ShouldFreeze(Component component)
        {
            return component != null &&
                component.GetComponentInParent<Canvas>() == null &&
                IsFrozen(component.gameObject);
        }

        private void CaptureRigidbodies()
        {
            foreach (Rigidbody body in FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude))
            {
                if (!ShouldFreeze(body) || rigidbodies.ContainsKey(body))
                {
                    continue;
                }

                rigidbodies.Add(body, new RigidbodyState(body));
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.isKinematic = true;
            }
        }

        private void CaptureAnimators()
        {
            foreach (Animator animator in FindObjectsByType<Animator>(FindObjectsInactive.Exclude))
            {
                if (!ShouldFreeze(animator) || animators.ContainsKey(animator))
                {
                    continue;
                }

                animators.Add(animator, animator.speed);
                animator.speed = 0f;
            }
        }

        private void CaptureParticles()
        {
            foreach (ParticleSystem particle in FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude))
            {
                if (!ShouldFreeze(particle) || particles.ContainsKey(particle))
                {
                    continue;
                }

                particles.Add(particle, new ParticleState(particle));
                particle.Pause(true);
            }
        }

        private void CaptureAudio()
        {
            foreach (AudioSource source in FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude))
            {
                if (!ShouldFreeze(source) || audioSources.ContainsKey(source))
                {
                    continue;
                }

                audioSources.Add(source, source.isPlaying);
                source.Pause();
            }
        }

        private void CaptureTrails()
        {
            foreach (TrailRenderer trail in FindObjectsByType<TrailRenderer>(FindObjectsInactive.Exclude))
            {
                if (!ShouldFreeze(trail) || trails.ContainsKey(trail))
                {
                    continue;
                }

                trails.Add(trail, new TrailState(trail));
                trail.emitting = false;
                trail.time = float.MaxValue;
            }
        }

        private void RestoreFrozenState()
        {
            foreach (KeyValuePair<Rigidbody, RigidbodyState> entry in rigidbodies)
            {
                if (entry.Key == null)
                {
                    continue;
                }

                Rigidbody body = entry.Key;
                RigidbodyState state = entry.Value;
                body.isKinematic = state.IsKinematic;
                body.useGravity = state.UseGravity;
                body.constraints = state.Constraints;
                if (!state.IsKinematic)
                {
                    body.linearVelocity = state.Velocity;
                    body.angularVelocity = state.AngularVelocity;
                }
            }

            foreach (KeyValuePair<Animator, float> entry in animators)
            {
                if (entry.Key != null)
                {
                    entry.Key.speed = entry.Value;
                }
            }

            foreach (KeyValuePair<ParticleSystem, ParticleState> entry in particles)
            {
                if (entry.Key == null)
                {
                    continue;
                }

                if (entry.Value.WasPlaying)
                {
                    entry.Key.Play(true);
                }
                else if (entry.Value.WasPaused)
                {
                    entry.Key.Pause(true);
                }
                else
                {
                    entry.Key.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            foreach (KeyValuePair<AudioSource, bool> entry in audioSources)
            {
                if (entry.Key != null && entry.Value)
                {
                    entry.Key.UnPause();
                }
            }

            foreach (KeyValuePair<TrailRenderer, TrailState> entry in trails)
            {
                if (entry.Key != null)
                {
                    entry.Key.time = entry.Value.Time;
                    entry.Key.emitting = entry.Value.Emitting;
                }
            }

            rigidbodies.Clear();
            animators.Clear();
            particles.Clear();
            audioSources.Clear();
            trails.Clear();
        }

        private void UpdateShaderClock()
        {
            Shader.SetGlobalFloat("_CoffeeGameWorldTime", GetClockTime(null));
            Shader.SetGlobalFloat("_CoffeeGameTimeStopActive", IsActive ? 1f : 0f);
            Shader.SetGlobalFloat("_CoffeeGameCombatClockReady", 1f);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            UpdateShaderClock();
        }

        private void Update()
        {
            Advance(UnityEngine.Time.deltaTime);
        }

        private void OnDisable()
        {
            if (IsActive)
            {
                Finish(false, TimeStopEndReason.Disabled);
            }
        }

        private void OnDestroy()
        {
            if (IsActive)
            {
                Finish(false, TimeStopEndReason.Disabled);
            }

            if (Instance == this)
            {
                Instance = null;
                Shader.SetGlobalFloat("_CoffeeGameTimeStopActive", 0f);
                Shader.SetGlobalFloat("_CoffeeGameCombatClockReady", 0f);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            Shader.SetGlobalFloat("_CoffeeGameTimeStopActive", 0f);
            Shader.SetGlobalFloat("_CoffeeGameCombatClockReady", 0f);
        }
    }
}
