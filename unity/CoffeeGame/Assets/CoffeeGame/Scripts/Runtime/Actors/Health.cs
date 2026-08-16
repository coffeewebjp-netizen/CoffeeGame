using System;
using UnityEngine;

namespace CoffeeGame.Actors
{
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1)] private int maxHealth = 1;
        [SerializeField, Min(0f)] private float invulnerabilitySeconds;

        private float invulnerableUntil;

        public event Action<Health, DamageInfo> Damaged;
        public event Action<Health, DamageInfo> Died;

        public int Current { get; private set; }
        public int Maximum => maxHealth;
        public bool IsAlive => Current > 0;
        public float Normalized => maxHealth <= 0 ? 0f : (float)Current / maxHealth;
        public float IncomingDamageMultiplier { get; set; } = 1f;
        public float EvasionChance { get; set; }

        public void Initialize(int maximum, float invulnerability = 0f)
        {
            maxHealth = Mathf.Max(1, maximum);
            invulnerabilitySeconds = Mathf.Max(0f, invulnerability);
            Current = maxHealth;
            invulnerableUntil = 0f;
        }

        public void RestoreFull()
        {
            Current = maxHealth;
            invulnerableUntil = 0f;
        }

        public bool ApplyDamage(DamageInfo damage)
        {
            if (!IsAlive || damage.Amount <= 0 || Time.time < invulnerableUntil)
            {
                return false;
            }

            if (EvasionChance > 0f && UnityEngine.Random.value < Mathf.Clamp01(EvasionChance))
            {
                return false;
            }

            int adjustedAmount = Mathf.Max(
                1,
                Mathf.RoundToInt(damage.Amount * Mathf.Clamp(IncomingDamageMultiplier, 0.05f, 10f)));
            var appliedDamage = new DamageInfo(adjustedAmount, damage.Source, damage.HitPoint, damage.Knockback);
            Current = Mathf.Max(0, Current - adjustedAmount);
            invulnerableUntil = Time.time + invulnerabilitySeconds;
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
