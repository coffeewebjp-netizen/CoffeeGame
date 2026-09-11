using System.Collections;
using System.Collections.Generic;
using CoffeeGame.Actors;
using CoffeeGame.Audio;
using CoffeeGame.Presentation;
using UnityEngine;

namespace CoffeeGame.Combat
{
    public sealed partial class PlayerCombatController
    {
        private Coroutine dragonRoutine, dragonStompRoutine;
        private DragonGateCaptivity captive;
        private DragonEffect frontGate, rearGate, summonedDragon, breathAura;
        private float dragonStageRemaining, dragonSummonAttack, dragonComboRemaining, dragonHeal;
        private int dragonCombo;
        private int dragonBufferedClaws;
        private bool dragonClawActive;
        public int DragonGateStage { get; private set; }
        public float DragonBreathRemaining { get; private set; }
        public string DragonActionLabel => DragonGateStage == 0 ? "前龍門" : DragonGateStage == 1 ? "後龍門" : "甲龍手刀一段";

        private void TickDragon(float dt)
        {
            dragonComboRemaining = Mathf.Max(0f, dragonComboRemaining - dt);
            if (dragonComboRemaining == 0f && dragonBufferedClaws == 0) dragonCombo = 0;
            float buffDt = Mathf.Min(dt, DragonBreathRemaining);
            DragonBreathRemaining = Mathf.Max(0f, DragonBreathRemaining - dt);
            motor.AbilitySpeedMultiplier = DragonBreathRemaining > 0f ? 2f : 1f;
            playerHealth.AbilityDefenseMultiplier = DragonBreathRemaining > 0f ? 2f : 1f;
            // One extra regeneration tick doubles MP recovery while empowered.
            if (buffDt > 0f) resources.Tick(buffDt);
            dragonHeal += playerHealth.Maximum / 900f * (dt + buffDt);
            if (dragonHeal >= 1f) { int hp = Mathf.FloorToInt(dragonHeal); playerHealth.Heal(hp); dragonHeal -= hp; }
            if (DragonBreathRemaining <= 0f && breathAura != null) { Destroy(breathAura.gameObject); breathAura = null; }
            if (DragonGateStage == 0) return;
            dragonStageRemaining -= dt;
            if (dragonStageRemaining <= 0f) { ClearDragonGates(); return; }
            if (DragonGateStage != 2 || summonedDragon == null) return;
            dragonSummonAttack -= dt;
            if (dragonSummonAttack > 0f) return;
            Health target = DragonTarget();
            if (target == null) return;
            dragonSummonAttack = 1.5f;
            Vector3 origin = summonedDragon.transform.position;
            Vector3 end = target.transform.position + Vector3.up * .7f;
            DragonEffect.Beam(origin, end, .24f, .28f, gameObject);
            target.ApplyDamage(new DamageInfo(CalculateDamage(tuning.MagicDamage), gameObject, end, Vector3.zero));
        }

        private void TryDragonClaw()
        {
            bool air = !motor.IsGrounded;
            if (air && airSlashUsed) { dragonBufferedClaws = 0; return; }
            if (air) airSlashUsed = true;
            dragonBufferedClaws = Mathf.Max(0, dragonBufferedClaws - 1);
            dragonCombo = air ? 1 : dragonCombo % 3 + 1;
            dragonComboRemaining = 1.4f;
            int stage = dragonCombo;
            float duration = stage == 3 ? .85f : .42f;
            attackCooldown = duration;
            dragonClawActive = true;
            motor.FaceLockedTargetForAction(duration);
            visual?.PlayAction(air ? CharacterAction.AirSlash : stage == 3 ? CharacterAction.SpinRelease : CharacterAction.Sword, duration);
            DragonPose(stage == 3 ? "ClawHeavy" : stage == 2 ? "ClawLeft" : air ? "AirClaw" : "ClawRight", duration);
            dragonRoutine = StartCoroutine(DragonClawStrike(stage, air, duration));
        }

        private IEnumerator DragonClawStrike(int stage, bool air, float duration)
        {
            float windup = stage == 3 ? .30f : .12f;
            yield return WaitForActor(windup);
            float radius = tuning.SwordRange * (stage == 3 ? 1.65f : 1.1f);
            DragonEffect.Claw(transform.position + Vector3.up * .8f, motor.Facing, radius, stage, gameObject);
            int hits = DamageTargets(radius, CalculateDamage((air ? tuning.AirSlashDamage : tuning.SwordDamage) * (stage == 3 ? 3 : 1)), false, true, stage == 3);
            if (hits > 0) resources.GainStamina(tuning.StaminaPerHit);
            audioDirector?.Play(hits > 0 ? CombatSound.SwordHit : CombatSound.SwordSwing, stage == 3 ? 1f : .7f, gameObject);
            yield return WaitForActor(duration - windup);
            dragonClawActive = false;
            dragonRoutine = null;
        }

        private Health DragonTarget()
        {
            Health target = motor.HasLockedTarget ? motor.LockedTarget : PartyTargeting.NearestEnemy(transform.position);
            return target != null && target.IsAlive && target.gameObject.activeInHierarchy &&
                DamageFaction.CanDamage(gameObject, target) && Vector3.Distance(transform.position, target.transform.position) <= 16f ? target : null;
        }

        private void TryDragonGate()
        {
            if (!motor.IsGrounded || majorMagicCooldown > 0f) return;
            Health target = DragonTarget();
            if (target == null || !resources.TrySpendMagic(resources.MaxMagicPoints * .15f)) return;
            dragonBufferedClaws = 0;
            int stage = DragonGateStage;
            float duration = stage == 2 ? 1.45f : .75f;
            motor.FaceTowards(target.transform.position);
            motor.FaceLockedTargetForAction(duration);
            attackCooldown = duration;
            majorMagicCooldown = duration + .15f;
            motor.MovementScale = .1f;
            visual?.PlayAction(stage == 2 ? CharacterAction.SpinRelease : CharacterAction.MagicRelease, duration);
            DragonPose(stage == 2 ? "DragonChop" : "DragonGate", duration);
            dragonRoutine = StartCoroutine(ResolveDragonGate(stage, target, duration));
        }

        private IEnumerator ResolveDragonGate(int stage, Health target, float duration)
        {
            float windup = stage == 2 ? .62f : .28f;
            yield return WaitForActor(windup);
            if (target == null || !target.IsAlive || !target.gameObject.activeInHierarchy)
            {
                motor.MovementScale = 1f; dragonRoutine = null; yield break;
            }
            Vector3 center = target.transform.position + Vector3.up;
            if (stage == 0)
            {
                ClearDragonGates();
                frontGate = DragonEffect.Gate(center, motor.Facing, 1.15f, 20f, gameObject);
                captive = target.GetComponent<DragonGateCaptivity>() ?? target.gameObject.AddComponent<DragonGateCaptivity>();
                captive.Capture(gameObject, 12f, frontGate.transform);
                DragonGateStage = 1; dragonStageRemaining = 20f;
            }
            else if (stage == 1)
            {
                rearGate = DragonEffect.Gate(center + motor.Facing * 1.4f, -motor.Facing, 1.15f, 30f, gameObject);
                summonedDragon = DragonEffect.Dragon(center + Vector3.up * .65f, motor.Facing, 30f, gameObject);
                DragonGateStage = 2; dragonStageRemaining = 30f; dragonSummonAttack = .4f;
            }
            else
            {
                DragonEffect.Beam(transform.position + Vector3.up * 2f, center + motor.Facing * 18f, 3.8f, .8f, gameObject);
                var hit = new HashSet<Health>();
                var camera = Camera.main;
                foreach (Collider c in Physics.OverlapSphere(transform.position, 40f, ~0, QueryTriggerInteraction.Collide))
                {
                    Health enemy = c.GetComponentInParent<Health>();
                    if (enemy == null || !enemy.IsAlive || !DamageFaction.CanDamage(gameObject, enemy) || !hit.Add(enemy)) continue;
                    Vector3 viewport = camera != null ? camera.WorldToViewportPoint(enemy.transform.position + Vector3.up * .5f) : new Vector3(.5f,.5f,1f);
                    if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f) continue;
                    enemy.ApplyDamage(new DamageInfo(CalculateDamage(tuning.MagicDamage * 6), gameObject, enemy.transform.position, motor.Facing * 1.5f));
                    DragonEffect.Claw(enemy.transform.position + Vector3.up, motor.Facing, 2f, 3, gameObject);
                }
                ClearDragonGates();
                majorMagicCooldown = 5f;
            }
            audioDirector?.Play(stage == 2 ? CombatSound.ThunderRelease : CombatSound.MagicCharge, .9f, gameObject);
            yield return WaitForActor(duration - windup);
            motor.MovementScale = 1f; dragonRoutine = null;
        }

        private void TryDragonBreath()
        {
            if (!motor.IsGrounded || DragonBreathRemaining > 0f || !resources.TrySpendStamina(tuning.SpecialStaminaCost)) return;
            DragonBreathRemaining = 60f;
            motor.AbilitySpeedMultiplier = 2f; playerHealth.AbilityDefenseMultiplier = 2f;
            attackCooldown = .7f;
            visual?.PlayAction(CharacterAction.MagicCharge, .7f);
            DragonPose("DragonBreath", .7f);
            breathAura = DragonEffect.Wave(transform.position, .85f, 60f, gameObject);
            breathAura.transform.SetParent(transform, true);
            audioDirector?.Play(CombatSound.SpinCharge, .85f, gameObject);
        }

        private void ReleaseDragonStomp(Vector3 position)
        {
            DragonPose("StompLand", Mathf.Max(.55f, tuning.LandingLag));
            DragonEffect.Wave(position, 4.2f, .8f, gameObject);
            if (dragonStompRoutine != null) StopCoroutine(dragonStompRoutine);
            dragonStompRoutine = StartCoroutine(DragonStompWave(position));
            audioDirector?.Play(CombatSound.PlungeImpact, 1f, gameObject);
        }

        private IEnumerator DragonStompWave(Vector3 center)
        {
            var hit = new HashSet<Health>();
            for (float elapsed = 0f; elapsed < .8f; elapsed += CombatClock.DeltaTime(gameObject))
            {
                if (CombatClock.DeltaTime(gameObject) <= 0f) { yield return null; continue; }
                float radius = Mathf.Lerp(.3f, 4.2f, elapsed / .8f);
                foreach (Collider c in Physics.OverlapSphere(center + Vector3.up * .4f, radius, ~0, QueryTriggerInteraction.Collide))
                {
                    Health target = c.GetComponentInParent<Health>();
                    if (target == null || !target.IsAlive || !DamageFaction.CanDamage(gameObject, target) || !hit.Add(target)) continue;
                    Vector3 away = Vector3.ProjectOnPlane(target.transform.position - center, Vector3.up).normalized;
                    target.ApplyDamage(new DamageInfo(CalculateDamage(tuning.PlungeDamage), gameObject, target.transform.position, away));
                }
                yield return null;
            }
            dragonStompRoutine = null;
        }

        private void DragonPose(string clip, float duration)
        {
            if (visual is Component component) component.GetComponent<DragonCharacterMotion>()?.Play(clip, duration);
        }

        private void ClearDragonGates()
        {
            if (captive != null) captive.Release(gameObject);
            captive = null;
            foreach (var effect in new[] { frontGate, rearGate, summonedDragon }) if (effect != null) Destroy(effect.gameObject);
            frontGate = rearGate = summonedDragon = null;
            DragonGateStage = 0; dragonStageRemaining = 0f;
        }

        private void CancelDragonActions()
        {
            if (dragonRoutine != null) StopCoroutine(dragonRoutine);
            dragonRoutine = null;
            if (dragonStompRoutine != null) StopCoroutine(dragonStompRoutine);
            dragonStompRoutine = null;
            dragonBufferedClaws = 0; dragonClawActive = false; dragonCombo = 0;
            ClearDragonGates();
            DragonBreathRemaining = 0f; dragonHeal = 0f;
            if (motor != null) motor.AbilitySpeedMultiplier = 1f;
            if (playerHealth != null) playerHealth.AbilityDefenseMultiplier = 1f;
            if (breathAura != null) Destroy(breathAura.gameObject);
            breathAura = null;
        }
    }
}
