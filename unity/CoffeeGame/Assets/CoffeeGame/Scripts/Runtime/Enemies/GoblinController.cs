using CoffeeGame.Actors;
using CoffeeGame.Combat;
using CoffeeGame.Domain;
using CoffeeGame.Presentation;
using CoffeeGame.World;
using UnityEngine;

namespace CoffeeGame.Enemies
{
    [DisallowMultipleComponent]
    public sealed class GoblinController : MonoBehaviour
    {
        public enum CombatPhase { Approach, Windup, Strike, Recovery, Hurt, Defeated }
        public const int MaximumHealth = 15;
        public const float AttackRange = 1.45f;
        public const float WindupSeconds = 0.72f;
        public const float ImpactSeconds = 0.16f;
        public const float StrikeSeconds = 0.38f;
        public static RewardBundle Reward => new RewardBundle(1, 1, 0);
        private Transform target;
        private Health targetHealth;
        private Health health;
        private Collider bodyCollider;
        private ICharacterVisual visual;
        private int damage;
        private float elapsed;
        private float approachSeconds;
        private float circleSign;
        private bool impacted;
        private Vector3 knockback;
        private Vector3 attackDirection = Vector3.forward;
        public CombatPhase Phase { get; private set; }
        public bool IsWindingUp => Phase == CombatPhase.Windup;
        public Vector3 AttackDirection => attackDirection;

        public void Initialize(CombatTuning tuning, Transform attackTarget, Health attackTargetHealth,
            Health ownHealth, Collider collider, ICharacterVisual characterVisual, int encounterIndex)
        {
            target = attackTarget;
            targetHealth = attackTargetHealth;
            health = ownHealth;
            bodyCollider = collider;
            visual = characterVisual;
            damage = tuning.SlimeDamage;
            circleSign = encounterIndex % 4 == 0 ? 1f : -1f;
            Phase = CombatPhase.Approach;
            health.Damaged += HandleDamaged;
            health.Died += HandleDied;
        }

        private void Update()
        {
            var next = PartyTargeting.NearestParty(transform.position);
            if (next != null && (Phase == CombatPhase.Approach || targetHealth == null || !targetHealth.IsAlive || !targetHealth.gameObject.activeInHierarchy))
            { targetHealth = next; target = next.transform; }
            Tick(CombatClock.DeltaTime(gameObject));
        }

        // Explicit clock permits deterministic combat checks without animation events.
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || health == null || !health.IsAlive || target == null ||
                targetHealth == null || !targetHealth.IsAlive) return;
            knockback = Vector3.MoveTowards(knockback, Vector3.zero, 5f * deltaTime);
            transform.position = StageLayout.ClampActorPosition(transform.position + knockback * deltaTime);
            elapsed += deltaTime;
            Vector3 toTarget = Vector3.ProjectOnPlane(target.position - transform.position, Vector3.up);
            float distance = toTarget.magnitude;
            Vector3 direction = distance > 0.001f ? toTarget / distance : attackDirection;
            switch (Phase)
            {
                case CombatPhase.Approach:
                    approachSeconds += deltaTime;
                    visual?.SetTint(Color.white);
                    visual?.SetFacing(direction);
                    if (distance <= 1.15f && approachSeconds >= 0.7f)
                    {
                        attackDirection = direction; // Commit aim; stepping aside must work.
                        ChangePhase(CombatPhase.Windup);
                        visual?.PlayAction(CharacterAction.AttackWindup, WindupSeconds);
                    }
                    else
                    {
                        Vector3 tangent = Vector3.Cross(Vector3.up, direction) * circleSign;
                        float circle = distance < 2.7f && approachSeconds < 1.5f ? 0.65f : 0f;
                        Vector3 movement = (direction + tangent * circle).normalized;
                        float step = Mathf.Min(0.95f * deltaTime, Mathf.Max(0f, distance - 0.82f));
                        transform.position = StageLayout.ClampActorPosition(transform.position + movement * step);
                        visual?.SetLocomotion(step > 0f ? CharacterAction.Walk : CharacterAction.Idle, step > 0f ? 1f : 0f);
                    }
                    break;
                case CombatPhase.Windup:
                    if (elapsed >= WindupSeconds)
                    {
                        ChangePhase(CombatPhase.Strike);
                        impacted = false;
                        visual?.PlayAction(CharacterAction.Attack, StrikeSeconds);
                    }
                    break;
                case CombatPhase.Strike:
                    if (!impacted && elapsed >= ImpactSeconds)
                    {
                        impacted = true;
                        if (PartyTargeting.Actors.Count > 0)
                        {
                            foreach (var actor in new System.Collections.Generic.List<PartyActor>(PartyTargeting.Actors))
                            {
                                if (actor != null && actor.Targetable && Threatens(actor.transform.position))
                                    HitTarget(actor.Health);
                            }
                        }
                        else if (Threatens(target.position))
                        {
                            if (targetHealth.ApplyDamage(new DamageInfo(damage, gameObject,
                                target.position, attackDirection * 0.8f)))
                                target.GetComponent<PlayerMotor3D>()?.AddKnockback(attackDirection * 1.4f);
                        }
                    }
                    if (elapsed >= StrikeSeconds) ChangePhase(CombatPhase.Recovery);
                    break;
                case CombatPhase.Recovery:
                    visual?.SetLocomotion(CharacterAction.Idle, 0f);
                    if (elapsed >= 0.75f) BeginApproach();
                    break;
                case CombatPhase.Hurt:
                    if (elapsed >= 0.32f) BeginApproach();
                    break;
            }
        }

        public bool Threatens(Vector3 position, float rangeMultiplier = 1f)
        {
            Vector3 offset = Vector3.ProjectOnPlane(position - transform.position, Vector3.up);
            return Mathf.Abs(position.y - transform.position.y) <= 0.72f &&
                offset.magnitude <= AttackRange * rangeMultiplier &&
                (offset.sqrMagnitude < 0.001f || Vector3.Dot(offset.normalized, attackDirection) >= 0.64f);
        }

        private void HitTarget(Health victim)
        {
            if (victim.ApplyDamage(new DamageInfo(damage, gameObject, victim.transform.position, attackDirection * 0.8f)))
                victim.GetComponent<PlayerMotor3D>()?.AddKnockback(attackDirection * 1.4f);
        }

        private void ChangePhase(CombatPhase phase) { Phase = phase; elapsed = 0f; }
        private void BeginApproach()
        {
            ChangePhase(CombatPhase.Approach);
            approachSeconds = 0f;
            circleSign = -circleSign;
            visual?.SetTint(Color.white);
        }
        private void HandleDamaged(Health _, DamageInfo hit)
        {
            ChangePhase(CombatPhase.Hurt);
            knockback += Vector3.ProjectOnPlane(hit.Knockback, Vector3.up) * 2.1f;
            visual?.SetTint(new Color(0.8f, 0.9f, 1f));
            visual?.PlayAction(CharacterAction.Hurt, 0.32f);
        }
        private void HandleDied(Health _, DamageInfo hit)
        {
            ChangePhase(CombatPhase.Defeated);
            if (bodyCollider != null) bodyCollider.enabled = false;
            visual?.SetTint(Color.white);
            visual?.PlayAction(CharacterAction.Defeated, 0.9f);
        }
        private void OnDestroy()
        {
            if (health == null) return;
            health.Damaged -= HandleDamaged;
            health.Died -= HandleDied;
        }
    }
}
