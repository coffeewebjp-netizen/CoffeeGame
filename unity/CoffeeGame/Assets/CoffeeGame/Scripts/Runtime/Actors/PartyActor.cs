using CoffeeGame.Combat;
using CoffeeGame.Domain;
using UnityEngine;

namespace CoffeeGame.Actors
{
    public sealed class PartyActor : MonoBehaviour
    {
        public string MemberId { get; private set; }
        public Health Health { get; private set; }
        public PlayerResources Resources { get; private set; }
        public PlayerMotor3D Motor { get; private set; }
        public PlayerCombatController Combat { get; private set; }
        public double HitPointFraction { get; set; }
        public bool IsCat => MemberId == PartyMemberIds.CatMage;
        public bool Targetable => gameObject.activeInHierarchy && Health != null && Health.IsAlive;

        public void Initialize(string id)
        {
            MemberId = id;
            Health = GetComponent<Health>();
            Resources = GetComponent<PlayerResources>();
            Motor = GetComponent<PlayerMotor3D>();
            Combat = GetComponent<PlayerCombatController>();
            Health.SetTeam(DamageTeam.Party);
            Motor.UseCommands = true;
            Combat.UseCommands = true;
            Combat.IsCatMage = IsCat;
        }
        private void OnEnable() => PartyTargeting.Register(this);
        private void OnDisable() => PartyTargeting.Unregister(this);
    }
}
