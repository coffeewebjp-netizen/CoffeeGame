using System.Collections.Generic;
using CoffeeGame.Combat;
using CoffeeGame.Enemies;
using UnityEngine;

namespace CoffeeGame.Actors
{
    public static class PartyTargeting
    {
        private static readonly List<PartyActor> actors = new List<PartyActor>();
        public static IReadOnlyList<PartyActor> Actors => actors;
        public static void Register(PartyActor actor) { if (!actors.Contains(actor)) actors.Add(actor); }
        public static void Unregister(PartyActor actor) => actors.Remove(actor);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => actors.Clear();

        public static Health NearestParty(Vector3 from)
        {
            Health best = null;
            float distance = float.PositiveInfinity;
            foreach (var actor in actors)
            {
                if (actor == null || !actor.Targetable) continue;
                float candidate = (actor.transform.position - from).sqrMagnitude;
                if (candidate >= distance) continue;
                distance = candidate;
                best = actor.Health;
            }
            return best;
        }

        public static Health NearestEnemy(Vector3 from)
        {
            Health best = null;
            float distance = float.PositiveInfinity;
            foreach (var enemy in Object.FindObjectsByType<CombatEnemy>(FindObjectsInactive.Exclude))
            {
                var health = enemy.GetComponent<Health>();
                if (health == null || !health.IsAlive) continue;
                float candidate = (health.transform.position - from).sqrMagnitude;
                if (candidate >= distance) continue;
                distance = candidate;
                best = health;
            }
            return best;
        }
    }
}
