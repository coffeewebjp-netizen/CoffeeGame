using System;
using CoffeeGame.Actors;
using CoffeeGame.Domain;
using UnityEngine;

namespace CoffeeGame.Enemies
{
    public enum EnemyKind { Goblin, Slime }

    public static class EnemyEncounterRoster
    {
        public static EnemyKind At(int zeroBasedIndex)
        {
            if (zeroBasedIndex < 0) throw new ArgumentOutOfRangeException(nameof(zeroBasedIndex));
            return zeroBasedIndex % 2 == 0 ? EnemyKind.Goblin : EnemyKind.Slime;
        }
    }

    // Run/reward lifecycle is independent of each enemy's movement and animation.
    [DisallowMultipleComponent]
    public sealed class CombatEnemy : MonoBehaviour
    {
        private bool notified;
        public event Action<CombatEnemy> Defeated;
        public string ClaimId { get; private set; }
        public EnemyKind Kind { get; private set; }
        public string DisplayName => Kind == EnemyKind.Goblin ? "森のゴブリン" : "スライム";
        public Health Health { get; private set; }
        public RewardBundle Reward { get; private set; }
        public float DefeatDisplaySeconds => Kind == EnemyKind.Goblin ? 0.95f : 0.58f;

        public void Initialize(string claimId, EnemyKind kind, Health health, RewardBundle reward)
        {
            if (Health != null) Health.Died -= HandleDied;
            ClaimId = string.IsNullOrWhiteSpace(claimId) ? Guid.NewGuid().ToString("N") : claimId;
            Kind = kind;
            Health = health != null ? health : throw new ArgumentNullException(nameof(health));
            Reward = reward;
            notified = false;
            Health.Died += HandleDied;
        }

        private void HandleDied(Health health, DamageInfo damage)
        {
            if (notified) return;
            notified = true;
            Defeated?.Invoke(this);
        }

        private void OnDestroy()
        {
            if (Health != null) Health.Died -= HandleDied;
        }
    }
}
