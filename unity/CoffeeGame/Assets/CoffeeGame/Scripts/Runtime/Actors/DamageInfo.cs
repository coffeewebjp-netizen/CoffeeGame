using System.Threading;
using UnityEngine;

namespace CoffeeGame.Actors
{
    public readonly struct DamageInfo
    {
        private static long nextAttackId;

        public DamageInfo(int amount, GameObject source, Vector3 hitPoint, Vector3 knockback, long attackId = 0, bool isGuarded = false, bool isCounter = false)
        {
            Amount = Mathf.Max(0, amount);
            Source = source;
            HitPoint = hitPoint;
            Knockback = knockback;
            AttackId = attackId != 0 ? attackId : Interlocked.Increment(ref nextAttackId);
            IsGuarded = isGuarded;
            IsCounter = isCounter;
        }

        public int Amount { get; }
        public GameObject Source { get; }
        public Vector3 HitPoint { get; }
        public Vector3 Knockback { get; }
        public long AttackId { get; }
        public bool IsGuarded { get; }
        public bool IsCounter { get; }
    }

    public interface IDamageable
    {
        bool IsAlive { get; }
        bool ApplyDamage(DamageInfo damage);
    }
}
