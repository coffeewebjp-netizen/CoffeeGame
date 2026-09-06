using System;
using CoffeeGame.Actors;
using CoffeeGame.Combat;
using CoffeeGame.Domain;
using CoffeeGame.Presentation;
using CoffeeGame.World;
using UnityEngine;

namespace CoffeeGame.Enemies
{
    [DisallowMultipleComponent]
    public sealed class SlimeController : MonoBehaviour, IEnemyAttack
    {
        private const float AttackHeightTolerance = 0.72f;

        private CombatTuning tuning;
        private Transform target;
        private Health targetHealth;
        private Health health;
        private ICharacterVisual visual;
        private Collider bodyCollider;
        private float attackCooldown;
        private float windupRemaining;
        private bool windingUp;
        private float staggerRemaining;
        private float strikeRemaining;
        private Vector3 knockbackVelocity;

        public event Action<SlimeController> Defeated;

        public string ClaimId { get; private set; }
        public Health Health => health;
        public bool IsWindingUp => windingUp;
        public bool IsAttacking => staggerRemaining <= 0f && (windingUp || strikeRemaining > 0f);
        public bool IsParried => staggerRemaining > 0f;

        public void Initialize(
            string claimId,
            CombatTuning combatTuning,
            Transform attackTarget,
            Health attackTargetHealth,
            Health ownHealth,
            Collider collider,
            ICharacterVisual characterVisual)
        {
            ClaimId = string.IsNullOrWhiteSpace(claimId) ? Guid.NewGuid().ToString("N") : claimId;
            tuning = combatTuning;
            target = attackTarget;
            targetHealth = attackTargetHealth;
            health = ownHealth;
            bodyCollider = collider;
            visual = characterVisual;
            attackCooldown = 0.6f;
            health.Damaged += HandleDamaged;
            health.Died += HandleDied;
        }

        private void Update()
        {
            var next = PartyTargeting.NearestParty(transform.position);
            if (next != null) { targetHealth = next; target = next.transform; }
            if (tuning == null || target == null || health == null || !health.IsAlive || targetHealth == null || !targetHealth.IsAlive)
            {
                return;
            }

            float deltaTime = CombatClock.DeltaTime(gameObject);
            if (deltaTime <= 0f) return;
            Tick(deltaTime);
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || tuning == null || health == null || !health.IsAlive || target == null || targetHealth == null || !targetHealth.IsAlive) return;
            if (staggerRemaining > 0f)
            {
                staggerRemaining = Mathf.Max(0f, staggerRemaining - deltaTime);
                if (staggerRemaining == 0f) visual?.SetTint(Color.white);
                return;
            }
            strikeRemaining = Mathf.Max(0f, strikeRemaining - deltaTime);
            attackCooldown = Mathf.Max(0f, attackCooldown - deltaTime);
            knockbackVelocity = Vector3.MoveTowards(knockbackVelocity, Vector3.zero, 4.5f * deltaTime);
            transform.position += knockbackVelocity * deltaTime;
            ClampToArena();

            Vector3 toTarget = Vector3.ProjectOnPlane(target.position - transform.position, Vector3.up);
            float distance = toTarget.magnitude;
            Vector3 direction = distance > 0.001f ? toTarget / distance : Vector3.forward;
            visual?.SetFacing(direction);

            if (windingUp)
            {
                windupRemaining -= deltaTime;
                float pulse = 1f + Mathf.Sin(CombatClock.Time(gameObject) * 28f) * 0.05f;
                transform.localScale = Vector3.one * pulse;
                if (windupRemaining <= 0f)
                {
                    ReleaseAttack(distance, direction);
                }
                return;
            }

            transform.localScale = Vector3.one;
            if (distance <= tuning.SlimeAttackRange && attackCooldown <= 0f)
            {
                windingUp = true;
                windupRemaining = tuning.SlimeWindupSeconds;
                visual?.SetTint(new Color(1f, 0.55f, 0.55f));
                visual?.PlayAction(CharacterAction.AttackWindup, tuning.SlimeWindupSeconds);
                return;
            }

            if (distance > tuning.SlimeAttackRange * 0.78f)
            {
                transform.position += direction * (tuning.SlimeSpeed * deltaTime);
                ClampToArena();
                visual?.SetLocomotion(CharacterAction.Walk, 1f);
            }
            else
            {
                visual?.SetLocomotion(CharacterAction.Idle, 0f);
            }
        }

        private void ReleaseAttack(float distanceAtRelease, Vector3 direction)
        {
            windingUp = false;
            strikeRemaining = 0.26f;
            attackCooldown = tuning.SlimeAttackInterval;
            transform.localScale = Vector3.one;
            visual?.SetTint(Color.white);
            transform.position += direction * 0.28f;
            ClampToArena();
            visual?.PlayAction(CharacterAction.Attack, 0.26f);
            long strikeId = new DamageInfo(tuning.SlimeDamage, gameObject, transform.position, Vector3.zero).AttackId;

            if (PartyTargeting.Actors.Count > 0)
            {
                var victims = new System.Collections.Generic.List<PartyActor>(PartyTargeting.Actors);
                foreach (var actor in victims)
                {
                    if (staggerRemaining > 0f) break;
                    if (actor == null || !actor.Targetable) continue;
                    Vector3 offset = actor.transform.position - transform.position;
                    if (Mathf.Abs(offset.y) > AttackHeightTolerance || Vector3.ProjectOnPlane(offset, Vector3.up).magnitude > tuning.SlimeAttackRange * 1.18f) continue;
                    if (actor.Health.ApplyDamage(new DamageInfo(tuning.SlimeDamage, gameObject, actor.transform.position, direction * 0.65f, strikeId)) && actor.GetComponent<PlayerDefense>()?.IsGuarding != true)
                        actor.Motor.AddKnockback(direction * 1.3f);
                }
                return;
            }

            float heightDifference = Mathf.Abs(target.position.y - transform.position.y);
            if (distanceAtRelease <= tuning.SlimeAttackRange * 1.18f && heightDifference <= AttackHeightTolerance)
            {
                var damage = new DamageInfo(tuning.SlimeDamage, gameObject, target.position, direction * 0.65f, strikeId);
                if (targetHealth.ApplyDamage(damage) && target.GetComponent<PlayerDefense>()?.IsGuarding != true)
                {
                    PlayerMotor3D targetMotor = target.GetComponent<PlayerMotor3D>();
                    targetMotor?.AddKnockback(direction * 1.3f);
                }
            }
        }

        private void ClampToArena()
        {
            transform.position = StageLayout.ClampActorPosition(transform.position);
        }

        private void HandleDamaged(Health _, DamageInfo damage)
        {
            if (staggerRemaining > 0f) return;
            windingUp = false;
            strikeRemaining = 0f;
            attackCooldown = Mathf.Max(attackCooldown, 0.28f);
            knockbackVelocity += Vector3.ProjectOnPlane(damage.Knockback, Vector3.up) * 2.4f;
            visual?.SetTint(new Color(0.75f, 0.9f, 1f));
            visual?.PlayAction(CharacterAction.Hurt, 0.16f);
        }

        public void Parry(float seconds)
        {
            if (health == null || !health.IsAlive) return;
            windingUp = false; strikeRemaining = 0f;
            staggerRemaining = Mathf.Max(0f, seconds);
            attackCooldown = Mathf.Max(attackCooldown, 0.6f);
            knockbackVelocity = Vector3.zero;
            transform.localScale = Vector3.one;
            visual?.SetTint(new Color(1f, 0.85f, 0.45f));
            visual?.PlayAction(CharacterAction.Hurt, staggerRemaining);
        }

        private void HandleDied(Health _, DamageInfo damage)
        {
            windingUp = false;
            transform.localScale = Vector3.one;
            if (bodyCollider != null)
            {
                bodyCollider.enabled = false;
            }
            visual?.SetTint(new Color(0.5f, 0.65f, 0.8f, 0.45f));
            visual?.PlayAction(CharacterAction.Defeated, 0.34f);
            Defeated?.Invoke(this);
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.Damaged -= HandleDamaged;
                health.Died -= HandleDied;
            }
        }
    }
}
