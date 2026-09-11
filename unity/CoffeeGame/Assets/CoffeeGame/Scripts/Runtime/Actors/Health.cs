using System;
using CoffeeGame.Combat;
using UnityEngine;

namespace CoffeeGame.Actors
{
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1)] private int maxHealth = 1;
        [SerializeField, Min(0f)] private float invulnerabilitySeconds;
        [SerializeField] private DamageTeam team;

        private float invulnerableUntil;
        private float dodgeInvulnerableUntil;

        public event Action<Health, DamageInfo> Damaged;
        public event Action<Health, DamageInfo> Died;
        public event Action AttackAvoided;
        public event Action<DamageInfo> DodgeAvoided;

        public int Current { get; private set; }
        public int Maximum => maxHealth;
        public DamageTeam Team => team;
        public bool IsAlive => Current > 0;
        public float Normalized => maxHealth <= 0 ? 0f : (float)Current / maxHealth;
        public float IncomingDamageMultiplier { get; set; } = 1f;
        public float AbilityDefenseMultiplier { get; set; } = 1f;

        public void Heal(int amount)
        {
            if (IsAlive && amount > 0) Current = Mathf.Min(maxHealth, Current + amount);
        }
        public float EvasionChance { get; set; }

        public void Initialize(int maximum, float invulnerability = 0f)
        {
            maxHealth = Mathf.Max(1, maximum);
            invulnerabilitySeconds = Mathf.Max(0f, invulnerability);
            Current = maxHealth;
            invulnerableUntil = 0f;
            dodgeInvulnerableUntil = 0f;
        }

        public void RestoreFull()
        {
            Current = maxHealth;
            invulnerableUntil = 0f;
            dodgeInvulnerableUntil = 0f;
        }

        public void SetTeam(DamageTeam value)
        {
            team = value;
        }

        // Hydration must restore the saved current value without granting the
        // difference when a newer build derives a larger maximum.
        public void SetCurrentAndMaximum(int current, int maximum)
        {
            maxHealth = Mathf.Max(1, maximum);
            Current = Mathf.Clamp(current, 0, maxHealth);
        }

        public bool IsDodgeInvulnerable => CombatClock.Time(gameObject) < dodgeInvulnerableUntil;

        public void BeginDodgeInvulnerability(float seconds)
        {
            float until = CombatClock.Time(gameObject) + Mathf.Max(0f, seconds);
            dodgeInvulnerableUntil = until;
            if (until > invulnerableUntil)
            {
                invulnerableUntil = until;
            }
        }

        public void EndDodgeInvulnerability()
        {
            if (invulnerableUntil <= dodgeInvulnerableUntil)
            {
                invulnerableUntil = CombatClock.Time(gameObject);
            }

            dodgeInvulnerableUntil = 0f;
        }

        public bool ApplyDamage(DamageInfo damage)
        {
            if (!IsAlive || damage.Amount <= 0 || CombatClock.IsPaused || !DamageFaction.CanDamage(damage.Source, this))
            {
                return false;
            }

            TimeStopController timeStop = TimeStopController.Instance;
            if (damage.Source != null && DragonGateCaptivity.IsCaptured(damage.Source)) return false;
            if (timeStop != null && timeStop.IsActive && damage.Source != null && timeStop.IsFrozen(damage.Source)) return false;
            bool deferredHit = timeStop != null && timeStop.IsActive && timeStop.IsFrozen(gameObject);
            if (deferredHit)
            {
                if (damage.Source == null || timeStop.IsFrozen(damage.Source) ||
                    !timeStop.TryClaimDamage(this, damage.AttackId))
                {
                    return false;
                }
            }

            float now = CombatClock.Time(gameObject);
            if (now < invulnerableUntil)
            {
                if (now < dodgeInvulnerableUntil)
                {
                    AttackAvoided?.Invoke();
                    DodgeAvoided?.Invoke(damage);
                }

                return false;
            }

            if (EvasionChance > 0f && UnityEngine.Random.value < Mathf.Clamp01(EvasionChance))
            {
                return false;
            }

            bool counter = GetComponent<IEnemyAttack>()?.IsAttacking == true &&
                damage.Source != null && damage.Source.GetComponentInParent<Health>()?.Team == DamageTeam.Party;
            int adjustedAmount = Mathf.Max(
                1,
                Mathf.RoundToInt(damage.Amount * (counter ? 2f : 1f) * Mathf.Clamp(IncomingDamageMultiplier, 0.05f, 10f) / Mathf.Max(1f, AbilityDefenseMultiplier)));
            var appliedDamage = new DamageInfo(
                adjustedAmount,
                damage.Source,
                damage.HitPoint,
                damage.Knockback,
                damage.AttackId, damage.IsGuarded, counter);

            var defense = GetComponent<PlayerDefense>();
            if (defense != null && defense.TryGuard(damage, adjustedAmount, out DamageInfo guarded))
            {
                if (guarded.Amount == 0) return false;
                appliedDamage = guarded;
            }

            if (deferredHit)
            {
                bool queued = timeStop.TryQueueDamage(this, appliedDamage);
                if (queued && counter) damage.Source.GetComponent<PlayerDefense>()?.ShowCounter(damage.HitPoint);
                return queued;
            }

            bool accepted = ApplyResolvedDamage(appliedDamage);
            if (accepted && counter) damage.Source.GetComponent<PlayerDefense>()?.ShowCounter(damage.HitPoint);
            return accepted;
        }

        internal bool ApplyResolvedDamage(DamageInfo appliedDamage)
        {
            if (!IsAlive || appliedDamage.Amount <= 0)
            {
                return false;
            }

            int adjustedAmount = appliedDamage.Amount;
            Current = Mathf.Max(0, Current - adjustedAmount);
            invulnerableUntil = CombatClock.Time(gameObject) + invulnerabilitySeconds;
            Damaged?.Invoke(this, appliedDamage);

            if (Current == 0)
            {
                Died?.Invoke(this, appliedDamage);
            }
            return true;
        }

        public void IncreaseMaximum(int amount, bool healAddedAmount)
        {
            int delta = Mathf.Max(0, amount);
            maxHealth += delta;
            if (healAddedAmount)
            {
                Current = Mathf.Min(maxHealth, Current + delta);
            }
        }

        private void Awake()
        {
            if (Current <= 0)
            {
                Current = Mathf.Max(1, maxHealth);
            }
        }
    }
}
