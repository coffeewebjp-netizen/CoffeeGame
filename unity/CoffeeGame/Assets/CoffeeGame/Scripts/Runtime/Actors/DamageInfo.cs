using UnityEngine;

namespace CoffeeGame.Actors
{
    public readonly struct DamageInfo
    {
        public DamageInfo(int amount, GameObject source, Vector3 hitPoint, Vector3 knockback)
        {
            Amount = Mathf.Max(0, amount);
            Source = source;
            HitPoint = hitPoint;
            Knockback = knockback;
        }

        public int Amount { get; }
        public GameObject Source { get; }
        public Vector3 HitPoint { get; }
        public Vector3 Knockback { get; }
    }

    public interface IDamageable
    {
        bool IsAlive { get; }
        bool ApplyDamage(DamageInfo damage);
    }
}

