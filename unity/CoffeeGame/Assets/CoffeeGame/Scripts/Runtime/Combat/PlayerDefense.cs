using System;
using CoffeeGame.Actors;
using CoffeeGame.Audio;
using CoffeeGame.Domain;
using CoffeeGame.Input;
using CoffeeGame.Presentation;
using UnityEngine;

namespace CoffeeGame.Combat
{
    [DefaultExecutionOrder(-150), DisallowMultipleComponent]
    public sealed class PlayerDefense : MonoBehaviour
    {
        private GameInputReader input;
        private CombatTuning tuning;
        private PlayerMotor3D motor;
        private PlayerCombatController combat;
        private PlayerResources resources;
        private Health health;
        private DefensePosePresentation pose;
        private DefenseFeedback feedback;
        private bool heldLastTick;
        private float parryUntil = float.NegativeInfinity;
        private float nextParryAt;
        private float dodgeUntil = float.NegativeInfinity;
        private bool dodgeRewarded;
        private long lastParriedAttack;
        private long lastDodgedAttack;

        public bool IsGuarding { get; private set; }
        public bool IsPerfectGuardWindow => IsGuarding && CombatClock.Time(gameObject) <= parryUntil;
        public event Action PerfectGuard;
        public event Action PerfectDodge;

        public void Initialize(GameInputReader reader, CombatTuning settings, PlayerMotor3D actorMotor,
            PlayerCombatController actorCombat, PlayerResources actorResources, Health actorHealth,
            AudioDirector audio, Transform visualRoot)
        {
            input = reader; tuning = settings; motor = actorMotor; combat = actorCombat;
            resources = actorResources; health = actorHealth;
            feedback = gameObject.AddComponent<DefenseFeedback>(); feedback.Initialize(audio, gameObject);
            pose = gameObject.AddComponent<DefensePosePresentation>();
            pose.Initialize(visualRoot, combat.IsCatMage ? DefensePoseStyle.CatBarrier : DefensePoseStyle.HeroineBlade, gameObject);
        }

        private void Update()
        {
            if (health == null || motor == null || combat == null) return;
            if (!health.IsAlive) { CancelGuard(); return; }
            if (CombatClock.DeltaTime(gameObject) <= 0f) return;
            if (!motor.CanMove) { CancelGuard(); return; }
            bool held = combat.UseCommands ? combat.Commands.GuardHeld : input != null && input.GuardHeld;
            bool allowed = motor.IsGrounded && motor.CanAct && !combat.IsCharging && combat.CanBeginGuard;
            TickGuard(held, allowed, CombatClock.Time(gameObject));
            pose?.SetFacing(motor.Facing);
            pose?.SetPlunging(!combat.IsCatMage && motor.IsPlunging);
            pose?.SetPlungeRecovery(!combat.IsCatMage && tuning.LandingLag > 0f ? motor.PlungeRecoveryRemaining / tuning.LandingLag : 0f);
        }

        // Explicit actor clock allows exact boundary checks without frame timing sleeps.
        public void TickGuard(bool held, bool allowed, float now)
        {
            IsGuarding = held && allowed;
            if (IsGuarding && !heldLastTick && now >= nextParryAt)
            {
                parryUntil = now + tuning.JustGuardSeconds;
                nextParryAt = now + tuning.GuardRearmSeconds;
            }
            if (!IsGuarding) parryUntil = float.NegativeInfinity;
            heldLastTick = held;
            if (motor != null) motor.IsGuarding = IsGuarding;
            pose?.SetGuarding(IsGuarding);
        }

        public void BeginDodge()
        {
            CancelGuard();
            dodgeUntil = CombatClock.Time(gameObject) + tuning.JustDodgeSeconds;
            dodgeRewarded = false;
        }

        public void ObserveDodgedHit(DamageInfo damage)
        {
            if (dodgeRewarded || damage.AttackId == lastDodgedAttack || CombatClock.Time(gameObject) > dodgeUntil ||
                damage.Source == null || damage.Source.GetComponent<IEnemyAttack>() == null) return;
            dodgeRewarded = true; lastDodgedAttack = damage.AttackId;
            resources?.GainStamina(resources.MaxStamina);
            feedback?.Emit(DefenseFeedbackEvent.PerfectDodge, transform.position, motor != null ? motor.Facing : Vector3.forward);
            PerfectDodge?.Invoke();
        }

        public bool TryGuard(DamageInfo damage, int normalDamage, out DamageInfo guarded)
        {
            guarded = damage;
            if (!IsGuarding || !isActiveAndEnabled || (motor != null && !motor.CanMove) || damage.Source == null) return false;
            var attacker = damage.Source.GetComponent<IEnemyAttack>();
            if (attacker == null) return false;
            if (damage.AttackId == lastParriedAttack)
            {
                guarded = new DamageInfo(0, damage.Source, damage.HitPoint, Vector3.zero, damage.AttackId, true);
                return true;
            }
            if (CombatClock.Time(gameObject) <= parryUntil && damage.AttackId != lastParriedAttack)
            {
                lastParriedAttack = damage.AttackId;
                parryUntil = float.NegativeInfinity;
                attacker.Parry(tuning.ParryStaggerSeconds);
                guarded = new DamageInfo(0, damage.Source, damage.HitPoint, Vector3.zero, damage.AttackId, true);
                feedback?.Emit(DefenseFeedbackEvent.Parry, damage.HitPoint, motor != null ? motor.Facing : Vector3.forward, 1f, damage.Source);
                PerfectGuard?.Invoke();
                return true;
            }
            // HP is integral throughout the existing game; round the reduced hit up to one HP.
            int chip = Mathf.Max(1, Mathf.CeilToInt(normalDamage * tuning.GuardDamageMultiplier));
            guarded = new DamageInfo(chip, damage.Source, damage.HitPoint, Vector3.zero, damage.AttackId, true);
            feedback?.Emit(combat != null && combat.IsCatMage ? DefenseFeedbackEvent.BarrierBlock : DefenseFeedbackEvent.BladeBlock,
                transform.position + Vector3.up * 0.72f, motor != null ? motor.Facing : Vector3.forward);
            return true;
        }

        public void ShowCounter(Vector3 point) => feedback?.Emit(DefenseFeedbackEvent.Counter, point, motor != null ? motor.Facing : Vector3.forward);
        public void BeginPlunge() => pose?.SetPlunging(combat != null && !combat.IsCatMage);
        public void ShowPlunge(Vector3 point, float radius)
        {
            feedback?.Emit(DefenseFeedbackEvent.PlungeImpact, point, Vector3.up, radius);
        }

        public void CancelGuard()
        {
            IsGuarding = false; heldLastTick = false; parryUntil = float.NegativeInfinity;
            if (motor != null) motor.IsGuarding = false;
            pose?.SetGuarding(false); pose?.SetPlunging(false);
            pose?.SetPlungeRecovery(0f);
        }
        private void OnDisable() { CancelGuard(); feedback?.CancelAll(); }
    }
}
