using CoffeeGame.Actors;
using UnityEngine;

namespace CoffeeGame.Combat
{
    public enum DamageTeam
    {
        Neutral = 0,
        Party = 1,
        Enemy = 2
    }

    [DisallowMultipleComponent]
    public sealed class DamageFaction : MonoBehaviour
    {
        [SerializeField] private DamageTeam team;

        public DamageTeam Team => team;

        public void SetTeam(DamageTeam value)
        {
            team = value;
        }

        public static DamageTeam GetTeam(GameObject source)
        {
            if (source == null)
            {
                return DamageTeam.Neutral;
            }

            Health health = source.GetComponentInParent<Health>();
            if (health != null)
            {
                return health.Team;
            }

            DamageFaction faction = source.GetComponentInParent<DamageFaction>();
            return faction != null ? faction.Team : DamageTeam.Neutral;
        }

        public static bool CanDamage(GameObject source, Health target)
        {
            if (target == null)
            {
                return false;
            }

            Health sourceHealth = source != null ? source.GetComponentInParent<Health>() : null;
            if (sourceHealth == target)
            {
                // Legacy isolated Health tests and environmental effects use a
                // neutral target as their own source. Configured combat teams
                // still reject self/friendly damage.
                return target.Team == DamageTeam.Neutral;
            }

            DamageTeam sourceTeam = GetTeam(source);
            DamageTeam targetTeam = target.Team;
            return sourceTeam == DamageTeam.Neutral ||
                targetTeam == DamageTeam.Neutral ||
                sourceTeam != targetTeam;
        }
    }
}
