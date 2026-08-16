using System.Collections;
using System.Collections.Generic;
using CoffeeGame.Actors;
using CoffeeGame.Audio;
using CoffeeGame.Domain;
using CoffeeGame.Input;
using CoffeeGame.Presentation;
using UnityEngine;

namespace CoffeeGame.Combat
{
    [DisallowMultipleComponent]
    public sealed class PlayerCombatController : MonoBehaviour
    {
        private enum ChargeKind
        {
            None,
            Special,
            Magic
        }

        private readonly HashSet<Health> uniqueTargets = new HashSet<Health>();
        private readonly List<IceProjectile> activeProjectiles = new List<IceProjectile>();
        private GameInputReader input;
        private CombatTuning tuning;
        private PlayerMotor3D motor;
        private PlayerResources resources;
        private Health playerHealth;
        private ICharacterVisual visual;
        private AudioDirector audioDirector;
        private ChargeKind chargeKind;
        private float chargeRemaining;
        private float activeChargeDuration;
        private float attackCooldown;
        private bool airSlashUsed;
        private bool plungeWasActive;
        private Coroutine specialReleaseRoutine;
        private GameObject activeIaiEffect;

        public int AttackBonus { get; set; }
        public float AttackMultiplier { get; set; } = 1f;
        public float CriticalChance { get; set; }
        public float SpecialChargeSpeedMultiplier { get; set; } = 1f;
        public bool IsCharging => chargeKind != ChargeKind.None;
        public float ChargeNormalized { get; private set; }
        public string ChargeLabel => chargeKind == ChargeKind.Special ? "居合斬り" : chargeKind == ChargeKind.Magic ? "氷魔法" : string.Empty;

        public void Initialize(
            GameInputReader inputReader,
            CombatTuning combatTuning,
            PlayerMotor3D playerMotor,
            PlayerResources playerResources,
            Health health,
            ICharacterVisual characterVisual,
            AudioDirector audio)
        {
            input = inputReader;
            tuning = combatTuning;
            motor = playerMotor;
            resources = playerResources;
            playerHealth = health;
            visual = characterVisual;
            audioDirector = audio;
            motor.Landed += HandleLanding;
            motor.PlungeStarted += HandlePlungeStarted;
        }

        public void ResetCombat()
        {
            CancelPendingActions();
            chargeKind = ChargeKind.None;
            chargeRemaining = 0f;
            activeChargeDuration = 0f;
            attackCooldown = 0f;
            airSlashUsed = false;
            plungeWasActive = false;
            ChargeNormalized = 0f;
        }

        public void CancelPendingActions()
        {
            if (specialReleaseRoutine != null)
            {
                StopCoroutine(specialReleaseRoutine);
                specialReleaseRoutine = null;
            }
            if (activeIaiEffect != null)
            {
                Destroy(activeIaiEffect);
                activeIaiEffect = null;
            }
            chargeKind = ChargeKind.None;
            chargeRemaining = 0f;
            activeChargeDuration = 0f;
            ChargeNormalized = 0f;
            for (int index = activeProjectiles.Count - 1; index >= 0; index--)
            {
                IceProjectile projectile = activeProjectiles[index];
                if (projectile != null)
                {
                    projectile.Destroyed -= HandleProjectileDestroyed;
                    Destroy(projectile.gameObject);
                }
            }
            activeProjectiles.Clear();
            if (motor != null)
            {
                motor.MovementScale = 1f;
            }
        }

        private void Update()
        {
            if (input == null || tuning == null || motor == null || resources == null || playerHealth == null || !playerHealth.IsAlive)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            resources.Tick(deltaTime);
            attackCooldown = Mathf.Max(0f, attackCooldown - deltaTime);

            if (chargeKind != ChargeKind.None)
            {
                TickCharge(deltaTime);
                return;
            }

            if (!motor.CanAct || attackCooldown > 0f)
            {
                return;
            }

            if (input.SwordPressed)
            {
                TrySwordAttack();
                return;
            }

            if (input.SpecialPressed)
            {
                TryStartSpecial();
                return;
            }

            if (input.MagicPressed)
            {
                TryStartMagic();
            }
        }

        private void TrySwordAttack()
        {
            if (!motor.IsGrounded)
            {
                if (airSlashUsed)
                {
                    return;
                }
                airSlashUsed = true;
            }

            bool airborne = !motor.IsGrounded;
            int damage = CalculateDamage(airborne ? tuning.AirSlashDamage : tuning.SwordDamage);
            float range = airborne ? tuning.AirSlashRange : tuning.SwordRange;
            attackCooldown = tuning.SwordCooldown;
            visual?.PlayAction(airborne ? CharacterAction.AirSlash : CharacterAction.Sword, tuning.SwordCooldown);
            audioDirector?.Play(CombatSound.SwordSwing, 0.72f);
            CombatVfxFactory.SpawnSwordSlash(
                transform.position,
                motor.Facing,
                range,
                new Color(0.78f, 0.94f, 1f),
                tuning.SwordCooldown);

            int hitCount = DamageTargets(range, damage, false, true);
            if (hitCount > 0)
            {
                resources.GainStamina(tuning.StaminaPerHit);
                audioDirector?.Play(CombatSound.SwordHit, 0.95f);
                CombatVfxFactory.SpawnRing(transform.position + motor.Facing * range, 0.28f, new Color(0.92f, 0.95f, 1f), 0.16f);
            }
        }

        private void TryStartSpecial()
        {
            if (!motor.IsGrounded || !resources.TrySpendStamina(tuning.SpecialStaminaCost))
            {
                return;
            }

            chargeKind = ChargeKind.Special;
            activeChargeDuration = tuning.SpecialChargeSeconds / Mathf.Clamp(SpecialChargeSpeedMultiplier, 0.2f, 10f);
            chargeRemaining = activeChargeDuration;
            ChargeNormalized = 0f;
            motor.MovementScale = 0.15f;
            visual?.PlayAction(CharacterAction.SpinCharge, activeChargeDuration);
            audioDirector?.Play(CombatSound.SpinCharge, 0.55f);
        }

        private void TryStartMagic()
        {
            if (!motor.IsGrounded || !resources.TrySpendMagic(tuning.MagicCost))
            {
                return;
            }

            chargeKind = ChargeKind.Magic;
            activeChargeDuration = tuning.MagicChargeSeconds;
            chargeRemaining = activeChargeDuration;
            ChargeNormalized = 0f;
            motor.MovementScale = 0.22f;
            visual?.PlayAction(CharacterAction.MagicCharge, tuning.MagicChargeSeconds);
            audioDirector?.Play(CombatSound.MagicCharge, 0.6f);
            CombatVfxFactory.SpawnMagicCharge(transform, tuning.MagicChargeSeconds);
        }

        private void TickCharge(float deltaTime)
        {
            float duration = activeChargeDuration;
            chargeRemaining = Mathf.Max(0f, chargeRemaining - deltaTime);
            ChargeNormalized = duration <= 0f ? 1f : 1f - chargeRemaining / duration;
            if (chargeRemaining > 0f)
            {
                return;
            }

            ChargeKind completed = chargeKind;
            chargeKind = ChargeKind.None;
            ChargeNormalized = 0f;
            activeChargeDuration = 0f;
            motor.MovementScale = 1f;

            if (completed == ChargeKind.Special)
            {
                ReleaseSpecial();
            }
            else
            {
                ReleaseMagic();
            }
        }

        private void ReleaseSpecial()
        {
            motor.MovementScale = 0f;
            visual?.PlayAction(CharacterAction.SpinRelease, IaiCinematicTiming.Duration);
            activeIaiEffect = CombatVfxFactory.SpawnIaiCinematic(
                transform.position,
                motor.Facing,
                tuning.SpecialRange);
            specialReleaseRoutine = StartCoroutine(ResolveIaiStrike());
            attackCooldown = IaiCinematicTiming.Duration;
        }

        private IEnumerator ResolveIaiStrike()
        {
            yield return new WaitForSeconds(IaiCinematicTiming.StrikeTime);
            int hitCount = DamageTargets(
                tuning.SpecialRange,
                CalculateDamage(tuning.SpecialDamage),
                true,
                false);
            audioDirector?.Play(CombatSound.SpinRelease, hitCount > 0 ? 1f : 0.72f);

            yield return new WaitForSeconds(
                IaiCinematicTiming.Duration - IaiCinematicTiming.StrikeTime);
            if (motor != null)
            {
                motor.MovementScale = 1f;
            }
            activeIaiEffect = null;
            specialReleaseRoutine = null;
        }

        private void ReleaseMagic()
        {
            visual?.PlayAction(CharacterAction.MagicRelease, 0.36f);
            CombatVfxFactory.SpawnMagicRelease(transform.position, motor.Facing);
            var projectileObject = new GameObject("Ice bolt");
            projectileObject.transform.position = transform.position + Vector3.up * 0.72f + motor.Facing * 0.38f;
            IceProjectile projectile = projectileObject.AddComponent<IceProjectile>();
            projectile.Initialize(motor.Facing, CalculateDamage(tuning.MagicDamage), tuning.MagicProjectileSpeed, gameObject);
            projectile.Destroyed += HandleProjectileDestroyed;
            activeProjectiles.Add(projectile);
            audioDirector?.Play(CombatSound.IceRelease, 0.92f);
            attackCooldown = 0.32f;
        }

        private int DamageTargets(float range, int damage, bool fullCircle, bool frontArc)
        {
            uniqueTargets.Clear();
            Collider[] overlaps = Physics.OverlapSphere(transform.position + Vector3.up * 0.48f, range, ~0, QueryTriggerInteraction.Collide);
            int hitCount = 0;

            foreach (Collider overlap in overlaps)
            {
                Health target = overlap.GetComponentInParent<Health>();
                if (target == null || target == playerHealth || !target.IsAlive || !uniqueTargets.Add(target))
                {
                    continue;
                }

                Vector3 direction = Vector3.ProjectOnPlane(target.transform.position - transform.position, Vector3.up);
                if (!fullCircle && frontArc && !CombatArcPolicy.Contains(motor.Facing, direction))
                {
                    continue;
                }

                Vector3 knockback = direction.sqrMagnitude > 0.001f ? direction.normalized * 0.5f : motor.Facing * 0.5f;
                var damageInfo = new DamageInfo(damage, gameObject, target.transform.position, knockback);
                if (target.ApplyDamage(damageInfo))
                {
                    hitCount++;
                }
            }
            return hitCount;
        }

        private int CalculateDamage(int baseDamage)
        {
            int damage = Mathf.Max(
                1,
                Mathf.RoundToInt((baseDamage + AttackBonus) * Mathf.Clamp(AttackMultiplier, 0.2f, 10f)));
            if (CriticalChance > 0f && UnityEngine.Random.value < Mathf.Clamp01(CriticalChance))
            {
                damage = Mathf.Max(1, Mathf.RoundToInt(damage * 1.5f));
            }
            return damage;
        }

        private void HandlePlungeStarted()
        {
            plungeWasActive = true;
        }

        private void HandleLanding(Vector3 position)
        {
            airSlashUsed = false;
            if (!plungeWasActive)
            {
                return;
            }

            plungeWasActive = false;
            int hitCount = DamageTargets(tuning.PlungeRadius, CalculateDamage(tuning.PlungeDamage), true, false);
            audioDirector?.Play(hitCount > 0 ? CombatSound.SwordHit : CombatSound.Impact, hitCount > 0 ? 1f : 0.7f);
            CombatVfxFactory.SpawnRing(position, tuning.PlungeRadius, new Color(0.8f, 0.9f, 1f), 0.34f);
        }

        private void OnDestroy()
        {
            CancelPendingActions();
            if (motor != null)
            {
                motor.Landed -= HandleLanding;
                motor.PlungeStarted -= HandlePlungeStarted;
            }
        }

        private void HandleProjectileDestroyed(IceProjectile projectile)
        {
            if (projectile != null)
            {
                projectile.Destroyed -= HandleProjectileDestroyed;
            }
            activeProjectiles.Remove(projectile);
        }
    }
}
