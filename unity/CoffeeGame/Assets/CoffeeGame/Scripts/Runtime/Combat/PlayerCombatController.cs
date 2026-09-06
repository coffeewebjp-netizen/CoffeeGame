using System.Collections;
using System.Collections.Generic;
using CoffeeGame.Actors;
using CoffeeGame.Audio;
using CoffeeGame.Domain;
using CoffeeGame.Enemies;
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
        private PlayerDefense defense;
        private Coroutine specialReleaseRoutine;
        private GameObject activeIaiEffect;
        private GameObject activeMagicChargeEffect;
        private float majorMagicCooldown;
        private int volleyStage;
        private HeroineCombatVoice voice;
        private SpecialCombatVoice specialVoice;

        public bool IsCatMage { get; set; }
        public bool IsManual { get; set; } = true;
        public bool UseCommands { get; set; }
        public ActorCommandFrame Commands { get; set; }
        public bool CanSwitch => !IsCharging && attackCooldown <= 0f && motor != null && motor.CanAct;
        public bool CanCastMajorMagic => IsCatMage && majorMagicCooldown <= 0f && resources != null && resources.MagicPoints >= resources.MaxMagicPoints * 0.25f;
        public bool CanBeginGuard => attackCooldown <= 0f && !IsCharging;
        public bool IsGuarding => defense != null && defense.IsGuarding;

        public int AttackBonus { get; set; }
        public float AttackMultiplier { get; set; } = 1f;
        public float CriticalChance { get; set; }
        public float SpecialChargeSpeedMultiplier { get; set; } = 1f;
        public bool IsCharging => chargeKind != ChargeKind.None;
        public float ChargeNormalized { get; private set; }
        public string ChargeLabel => chargeKind == ChargeKind.Special ? "居合斬り" : chargeKind == ChargeKind.Magic ? (IsCatMage ? "星環の大魔法" : "氷魔法") : string.Empty;

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
            voice = GetComponent<HeroineCombatVoice>();
            specialVoice = gameObject.AddComponent<SpecialCombatVoice>();
            specialVoice.Initialize(IsCatMage);
            motor.Landed += HandleLanding;
            motor.Jumped += HandleJumped;
            motor.PlungeStarted += HandlePlungeStarted;
            motor.Dodged += HandleDodged;
            defense = gameObject.AddComponent<PlayerDefense>();
            defense.Initialize(input, tuning, motor, this, resources, playerHealth, audioDirector,
                characterVisual is Component component ? component.transform : transform);
            playerHealth.DodgeAvoided += HandleAttackAvoided;
        }

        public void ResetCombat()
        {
            CancelPendingActions();
            chargeKind = ChargeKind.None;
            chargeRemaining = 0f;
            activeChargeDuration = 0f;
            attackCooldown = 0f;
            majorMagicCooldown = 0f;
            volleyStage = 0;
            airSlashUsed = false;
            plungeWasActive = false;
            defense?.CancelGuard();
            ChargeNormalized = 0f;
        }

        public void CancelPendingActions()
        {
            defense?.CancelGuard();
            voice?.Stop();
            specialVoice?.Stop();
            if (activeMagicChargeEffect != null)
            {
                Destroy(activeMagicChargeEffect);
                activeMagicChargeEffect = null;
            }
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

            float deltaTime = CombatClock.DeltaTime(gameObject);
            if (deltaTime <= 0f || !motor.CanMove) return;
            resources.Tick(deltaTime);
            attackCooldown = Mathf.Max(0f, attackCooldown - deltaTime);
            majorMagicCooldown = Mathf.Max(0f, majorMagicCooldown - deltaTime);
            if (IsGuarding) return;

            if (chargeKind != ChargeKind.None)
            {
                TickCharge(deltaTime);
                return;
            }

            if (!motor.CanAct || attackCooldown > 0f)
            {
                return;
            }

            if (UseCommands ? Commands.Sword : input.SwordPressed)
            {
                TrySwordAttack();
                return;
            }

            if (UseCommands ? Commands.Special : input.SpecialPressed)
            {
                TryStartSpecial();
                return;
            }

            if (UseCommands ? Commands.Magic : input.MagicPressed)
            {
                TryStartMagic();
            }
        }

        private void TrySwordAttack()
        {
            if (IsCatMage)
            {
                ReleaseCatVolley();
                return;
            }
            if (!motor.IsGrounded)
            {
                if (airSlashUsed)
                {
                    return;
                }
                airSlashUsed = true;
            }

            bool airborne = !motor.IsGrounded;
            motor.FaceLockedTargetForAction(tuning.SwordCooldown);
            int damage = CalculateDamage(airborne ? tuning.AirSlashDamage : tuning.SwordDamage);
            float range = airborne ? tuning.AirSlashRange : tuning.SwordRange;
            attackCooldown = tuning.SwordCooldown;
            visual?.PlayAction(airborne ? CharacterAction.AirSlash : CharacterAction.Sword, tuning.SwordCooldown);
            audioDirector?.Play(CombatSound.SwordSwing, 0.72f, gameObject);
            voice?.Sword();
            CombatVfxFactory.SpawnSwordSlash(
                transform.position,
                motor.Facing,
                range,
                new Color(0.96f, 0.94f, 0.84f),
                tuning.SwordCooldown);

            int hitCount = DamageTargets(range, damage, false, true);
            if (hitCount > 0)
            {
                resources.GainStamina(tuning.StaminaPerHit);
                audioDirector?.Play(CombatSound.SwordHit, 0.95f, gameObject);
                CombatVfxFactory.SpawnSwordImpact(transform.position + motor.Facing * range * 0.65f, motor.Facing);
            }
        }

        private void TryStartSpecial()
        {
            if (IsCatMage)
            {
                var stop = TimeStopController.Instance;
                if (!IsManual || !motor.IsGrounded || stop == null || stop.IsActive || resources.Stamina < tuning.SpecialStaminaCost) return;
                if (stop.TryBegin(gameObject, 10f))
                {
                    resources.TrySpendStamina(tuning.SpecialStaminaCost);
                    specialVoice?.Play();
                    visual?.PlayAction(CharacterAction.MagicRelease, 0.35f);
                    CombatVfxFactory.SpawnMagicRelease(transform.position, motor.Facing, gameObject);
                }
                return;
            }
            if (!motor.IsGrounded || !resources.TrySpendStamina(tuning.SpecialStaminaCost))
            {
                return;
            }

            chargeKind = ChargeKind.Special;
            activeChargeDuration = tuning.SpecialChargeSeconds / Mathf.Clamp(SpecialChargeSpeedMultiplier, 0.2f, 10f);
            chargeRemaining = activeChargeDuration;
            ChargeNormalized = 0f;
            motor.MovementScale = 0.15f;
            motor.FaceLockedTargetForAction(activeChargeDuration);
            visual?.PlayAction(CharacterAction.SpinCharge, activeChargeDuration);
            audioDirector?.Play(CombatSound.SpinCharge, 0.55f, gameObject);
        }

        private void TryStartMagic()
        {
            if (!motor.IsGrounded || (IsCatMage && majorMagicCooldown > 0f) || !resources.TrySpendMagic(IsCatMage ? resources.MaxMagicPoints * 0.25f : tuning.MagicCost))
            {
                return;
            }

            chargeKind = ChargeKind.Magic;
            activeChargeDuration = IsCatMage ? 1.2f : tuning.MagicChargeSeconds;
            chargeRemaining = activeChargeDuration;
            ChargeNormalized = 0f;
            motor.MovementScale = 0.22f;
            motor.FaceLockedTargetForAction(activeChargeDuration);
            visual?.PlayAction(CharacterAction.MagicCharge, activeChargeDuration);
            audioDirector?.Play(CombatSound.MagicCharge, 0.6f, gameObject);
            activeMagicChargeEffect = CombatVfxFactory.SpawnMagicCharge(transform, activeChargeDuration);
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
            specialVoice?.Play();
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
            yield return WaitForActor(IaiCinematicTiming.StrikeTime);
            int hitCount = DamageTargets(
                tuning.SpecialRange,
                CalculateDamage(tuning.SpecialDamage),
                true,
                false);
            audioDirector?.Play(CombatSound.SpinRelease, hitCount > 0 ? 1f : 0.72f, gameObject);

            yield return WaitForActor(
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
            if (activeMagicChargeEffect != null) Destroy(activeMagicChargeEffect);
            if (IsCatMage)
            {
                ReleaseCatMajorMagic();
                return;
            }
            visual?.PlayAction(CharacterAction.MagicRelease, 0.36f);
            CombatVfxFactory.SpawnMagicRelease(transform.position, motor.Facing);
            var projectileObject = new GameObject("Ice bolt");
            projectileObject.transform.position = transform.position + Vector3.up * 0.72f + motor.Facing * 0.38f;
            IceProjectile projectile = projectileObject.AddComponent<IceProjectile>();
            projectile.Initialize(motor.Facing, CalculateDamage(tuning.MagicDamage), tuning.MagicProjectileSpeed, gameObject);
            projectile.Destroyed += HandleProjectileDestroyed;
            activeProjectiles.Add(projectile);
            audioDirector?.Play(CombatSound.IceRelease, 0.92f, gameObject);
            voice?.Magic();
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
                if (target == null || target == playerHealth || !target.IsAlive || !DamageFaction.CanDamage(gameObject, target) || !uniqueTargets.Add(target))
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
            defense?.BeginPlunge();
        }

        private void HandleDodged()
        {
            voice?.Dodge();
            defense?.BeginDodge();
            playerHealth?.BeginDodgeInvulnerability(tuning.DodgeInvulnerabilitySeconds);
        }

        private void HandleAttackAvoided(DamageInfo damage)
        {
            defense?.ObserveDodgedHit(damage);
        }

        private void HandleJumped()
        {
            audioDirector?.Play(CombatSound.Jump, 0.65f, gameObject);
        }

        private void HandleLanding(Vector3 position)
        {
            audioDirector?.Play(CombatSound.Land, 0.65f, gameObject);
            airSlashUsed = false;
            playerHealth?.EndDodgeInvulnerability();
            if (!plungeWasActive)
            {
                return;
            }

            plungeWasActive = false;
            int hitCount = DamageTargets(tuning.PlungeRadius, CalculateDamage(tuning.PlungeDamage), true, false);
            audioDirector?.Play(hitCount > 0 ? CombatSound.SwordHit : CombatSound.Impact, hitCount > 0 ? 1f : 0.7f, gameObject);
            defense?.ShowPlunge(position, tuning.PlungeRadius);
        }

        private void OnDestroy()
        {
            CancelPendingActions();
            if (motor != null)
            {
                motor.Landed -= HandleLanding;
                motor.Jumped -= HandleJumped;
                motor.PlungeStarted -= HandlePlungeStarted;
                motor.Dodged -= HandleDodged;
            }

            if (playerHealth != null)
            {
                playerHealth.DodgeAvoided -= HandleAttackAvoided;
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

        private IEnumerator WaitForActor(float seconds)
        {
            for (float elapsed = 0f; elapsed < seconds; elapsed += CombatClock.DeltaTime(gameObject)) yield return null;
        }

        private void ReleaseCatVolley()
        {
            motor.FaceLockedTargetForAction(.32f);
            volleyStage = volleyStage % 3 + 1;
            int count = volleyStage == 3 ? 3 : 1;
            visual?.PlayAction(CharacterAction.MagicRelease, 0.32f);
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = Quaternion.AngleAxis((i - (count - 1) * 0.5f) * 13f, Vector3.up) * motor.Facing;
                var root = new GameObject("Cat star bolt");
                root.transform.position = transform.position + Vector3.up * 0.72f + direction * 0.42f;
                var bolt = root.AddComponent<IceProjectile>();
                bolt.Initialize(direction, CalculateDamage(Mathf.Max(1, tuning.SwordDamage)), tuning.MagicProjectileSpeed, gameObject);
                bolt.Destroyed += HandleProjectileDestroyed;
                bolt.Hit += _ => resources.GainStamina(tuning.StaminaPerHit);
                activeProjectiles.Add(bolt);
            }
            attackCooldown = volleyStage == 3 ? 0.62f : 0.38f;
            audioDirector?.Play(CombatSound.IceRelease, 0.38f, gameObject);
        }

        private void ReleaseCatMajorMagic()
        {
            Vector3 center = transform.position + motor.Facing * 2.8f;
            var target = PartyTargeting.NearestEnemy(transform.position);
            if (target != null && Vector3.Distance(target.transform.position, transform.position) <= 7f) center = target.transform.position;
            visual?.PlayAction(CharacterAction.MagicRelease, 0.48f);
            CombatVfxFactory.SpawnMagicRelease(transform.position, motor.Facing, gameObject);
            CombatVfxFactory.SpawnRing(center, 2.5f, new Color(0.76f, 0.65f, 1f), 0.6f, gameObject);
            CombatVfxFactory.SpawnRing(center, 1.7f, new Color(1f, 0.9f, 0.65f), 0.45f, gameObject);
            CombatVfxFactory.SpawnIceBurst(center + Vector3.up * 0.3f, Vector3.up, 2.2f, 0.65f, gameObject);
            uniqueTargets.Clear();
            foreach (var collider in Physics.OverlapSphere(center + Vector3.up * 0.4f, 2.5f))
            {
                var enemy = collider.GetComponentInParent<Health>();
                if (enemy == null || !enemy.IsAlive || !DamageFaction.CanDamage(gameObject, enemy) || !uniqueTargets.Add(enemy)) continue;
                if (enemy.ApplyDamage(new DamageInfo(CalculateDamage(tuning.MagicDamage * 2), gameObject, enemy.transform.position, motor.Facing * 0.8f)))
                    resources.GainStamina(tuning.StaminaPerHit);
            }
            majorMagicCooldown = 6f;
            attackCooldown = 0.48f;
            audioDirector?.Play(CombatSound.IceRelease, 1f, gameObject);
        }
    }
}
