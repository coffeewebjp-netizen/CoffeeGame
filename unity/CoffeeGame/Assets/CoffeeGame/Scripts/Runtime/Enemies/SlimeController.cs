using System;
using CoffeeGame.Actors;
using CoffeeGame.Domain;
using CoffeeGame.Presentation;
using CoffeeGame.World;
using UnityEngine;

namespace CoffeeGame.Enemies
{
    [DisallowMultipleComponent]
    public sealed class SlimeController : MonoBehaviour
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
        private Vector3 knockbackVelocity;

        public event Action<SlimeController> Defeated;

        public string ClaimId { get; private set; }
        public Health Health => health;
        public bool IsWindingUp => windingUp;

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
            if (tuning == null || target == null || health == null || !health.IsAlive || targetHealth == null || !targetHealth.IsAlive)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
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
                float pulse = 1f + Mathf.Sin(Time.time * 28f) * 0.05f;
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
            attackCooldown = tuning.SlimeAttackInterval;
            transform.localScale = Vector3.one;
            visual?.SetTint(Color.white);
            transform.position += direction * 0.28f;
            ClampToArena();
            visual?.PlayAction(CharacterAction.Attack, 0.26f);

            float heightDifference = Mathf.Abs(target.position.y - transform.position.y);
            if (distanceAtRelease <= tuning.SlimeAttackRange * 1.18f && heightDifference <= AttackHeightTolerance)
            {
                var damage = new DamageInfo(tuning.SlimeDamage, gameObject, target.position, direction * 0.65f);
                if (targetHealth.ApplyDamage(damage))
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
            windingUp = false;
            attackCooldown = Mathf.Max(attackCooldown, 0.28f);
            knockbackVelocity += Vector3.ProjectOnPlane(damage.Knockback, Vector3.up) * 2.4f;
            visual?.SetTint(new Color(0.75f, 0.9f, 1f));
            visual?.PlayAction(CharacterAction.Hurt, 0.16f);
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
