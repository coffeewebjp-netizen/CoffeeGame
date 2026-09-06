using System.Reflection;
using CoffeeGame.Actors;
using CoffeeGame.Combat;
using CoffeeGame.Domain;
using CoffeeGame.Enemies;
using NUnit.Framework;
using UnityEngine;

namespace CoffeeGame.Tests
{
    public sealed class DefenseCombatTests
    {
        private GameObject hero, enemy;
        private Health heroHealth, enemyHealth;
        private PlayerResources resources;
        private PlayerDefense defense;
        private GoblinController goblin;
        private CombatTuning tuning;

        [SetUp] public void Setup()
        {
            Time.timeScale = 1f;
            tuning = CombatTuning.CreateDefault();
            hero = new GameObject("guard-test-hero");
            hero.transform.position = Vector3.forward;
            heroHealth = hero.AddComponent<Health>(); heroHealth.Initialize(100); heroHealth.SetTeam(DamageTeam.Party);
            resources = hero.AddComponent<PlayerResources>(); resources.Initialize(100, 20, 0);
            defense = hero.AddComponent<PlayerDefense>();
            defense.Initialize(null, tuning, null, hero.AddComponent<PlayerCombatController>(), resources, heroHealth, null, hero.transform);
            heroHealth.DodgeAvoided += defense.ObserveDodgedHit;
            enemy = new GameObject("guard-test-goblin");
            enemyHealth = enemy.AddComponent<Health>(); enemyHealth.Initialize(100); enemyHealth.SetTeam(DamageTeam.Enemy);
            goblin = enemy.AddComponent<GoblinController>();
            goblin.Initialize(tuning, hero.transform, heroHealth, enemyHealth, enemy.AddComponent<CapsuleCollider>(), null, 0);
        }

        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(hero); Object.DestroyImmediate(enemy); Object.DestroyImmediate(tuning);
            Time.timeScale = 1f;
        }
        private DamageInfo Hit(int amount = 20) => new DamageInfo(amount, enemy, hero.transform.position, Vector3.back);
        private void Windup()
        {
            for (int i = 0; i < 300 && !goblin.IsWindingUp; i++) goblin.Tick(0.01f);
            Assert.That(goblin.IsWindingUp, Is.True);
        }
        private void HoldPastJustWindow() => defense.TickGuard(true, true, CombatClock.Time(hero) - 1f);

        [Test] public void HeldGuardTakesTenPercentAndSuppressesKnockback()
        {
            HoldPastJustWindow(); DamageInfo observed = default;
            heroHealth.Damaged += (_, hit) => observed = hit;
            Assert.That(heroHealth.ApplyDamage(Hit(30)), Is.True);
            Assert.That(heroHealth.Current, Is.EqualTo(97));
            Assert.That(observed.IsGuarded, Is.True);
            Assert.That(observed.Knockback, Is.EqualTo(Vector3.zero));
        }
        [Test] public void IntegerChipRoundsUpAndGuardReleaseRestoresNormalDamage()
        {
            HoldPastJustWindow(); heroHealth.ApplyDamage(Hit(2));
            Assert.That(heroHealth.Current, Is.EqualTo(99));
            defense.TickGuard(false, true, CombatClock.Time(hero));
            heroHealth.ApplyDamage(Hit(20)); Assert.That(heroHealth.Current, Is.EqualTo(79));
        }
        [Test] public void JustGuardCancelsSwingAndHoldsEnemyForFullStagger()
        {
            Windup(); defense.TickGuard(true, true, CombatClock.Time(hero));
            Assert.That(heroHealth.ApplyDamage(Hit()), Is.False);
            Assert.That(heroHealth.Current, Is.EqualTo(100));
            Assert.That(goblin.Phase, Is.EqualTo(GoblinController.CombatPhase.Parried));
            enemyHealth.ApplyDamage(new DamageInfo(3, hero, enemy.transform.position, Vector3.zero));
            goblin.Tick(1.4f); Assert.That(goblin.Phase, Is.EqualTo(GoblinController.CombatPhase.Parried));
            goblin.Tick(0.11f); Assert.That(goblin.Phase, Is.EqualTo(GoblinController.CombatPhase.Approach));
        }
        [Test] public void ExpiredGuardWindowAndRapidRepressDoNotParry()
        {
            float now = CombatClock.Time(hero);
            defense.TickGuard(true, true, now); defense.TickGuard(false, true, now);
            defense.TickGuard(true, true, now); heroHealth.ApplyDamage(Hit());
            Assert.That(heroHealth.Current, Is.EqualTo(98));
            Assert.That(goblin.Phase, Is.Not.EqualTo(GoblinController.CombatPhase.Parried));
        }
        [Test] public void BlockedActionDoesNotQueueAnAutomaticJustGuard()
        {
            defense.TickGuard(true, false, CombatClock.Time(hero));
            defense.TickGuard(true, true, CombatClock.Time(hero));
            heroHealth.ApplyDamage(Hit()); Assert.That(heroHealth.Current, Is.EqualTo(98));
        }
        [Test] public void DodgeRequiresActualAvoidedEnemyHitAndAwardsOnlyOnce()
        {
            defense.BeginDodge(); heroHealth.BeginDodgeInvulnerability(1f);
            Assert.That(resources.Stamina, Is.Zero);
            DamageInfo hit = Hit(); heroHealth.ApplyDamage(hit);
            Assert.That(resources.Stamina, Is.EqualTo(100));
            resources.TrySpendStamina(100); heroHealth.ApplyDamage(hit); heroHealth.ApplyDamage(Hit());
            Assert.That(resources.Stamina, Is.Zero);
        }
        [Test] public void LateInvulnerableDodgeBlocksDamageWithoutPerfectReward()
        {
            defense.BeginDodge(); heroHealth.BeginDodgeInvulnerability(1f);
            typeof(PlayerDefense).GetField("dodgeUntil", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(defense, CombatClock.Time(hero) - 0.01f);
            heroHealth.ApplyDamage(Hit()); Assert.That(heroHealth.Current, Is.EqualTo(100));
            Assert.That(resources.Stamina, Is.Zero);
        }
        [Test] public void DodgeAgainstUnrelatedDamageDoesNotFillGauge()
        {
            defense.BeginDodge(); heroHealth.BeginDodgeInvulnerability(1f);
            heroHealth.ApplyDamage(new DamageInfo(5, null, hero.transform.position, Vector3.zero));
            Assert.That(resources.Stamina, Is.Zero);
        }
        [Test] public void CounterDoublesDuringWindupButNotAfterInterruption()
        {
            Windup(); DamageInfo last = default; enemyHealth.Damaged += (_, hit) => last = hit;
            enemyHealth.ApplyDamage(new DamageInfo(7, hero, enemy.transform.position, Vector3.zero));
            Assert.That(enemyHealth.Current, Is.EqualTo(86)); Assert.That(last.IsCounter, Is.True);
            enemyHealth.ApplyDamage(new DamageInfo(7, hero, enemy.transform.position, Vector3.zero));
            Assert.That(enemyHealth.Current, Is.EqualTo(79)); Assert.That(last.IsCounter, Is.False);
        }
        [Test] public void CounterAppliesDuringCommittedStrikeButNotRecovery()
        {
            Windup(); goblin.Tick(GoblinController.WindupSeconds + 0.01f);
            Assert.That(goblin.IsAttacking, Is.True);
            enemyHealth.ApplyDamage(new DamageInfo(4, hero, enemy.transform.position, Vector3.zero));
            Assert.That(enemyHealth.Current, Is.EqualTo(92));
        }
        [Test] public void PausedHitCannotDamageParryOrReward()
        {
            defense.TickGuard(true, true, CombatClock.Time(hero)); Time.timeScale = 0;
            Assert.That(heroHealth.ApplyDamage(Hit()), Is.False);
            Assert.That(goblin.Phase, Is.EqualTo(GoblinController.CombatPhase.Approach));
            Assert.That(heroHealth.Current, Is.EqualTo(100)); Assert.That(resources.Stamina, Is.Zero);
        }
        [Test] public void RecoveryIsNotACounterWindow()
        {
            Windup(); goblin.Tick(GoblinController.WindupSeconds + .01f);
            goblin.Tick(GoblinController.StrikeSeconds + .01f);
            Assert.That(goblin.Phase, Is.EqualTo(GoblinController.CombatPhase.Recovery));
            enemyHealth.ApplyDamage(new DamageInfo(7, hero, enemy.transform.position, Vector3.zero));
            Assert.That(enemyHealth.Current, Is.EqualTo(93));
        }
        [Test] public void FrozenCounterIsQueuedOnceAndNotMultipliedAgainAtRelease()
        {
            Windup(); var stop = new GameObject("defense-time-stop").AddComponent<TimeStopController>();
            try
            {
                Assert.That(stop.TryBegin(hero, 1f), Is.True);
                var hit = new DamageInfo(7, hero, enemy.transform.position, Vector3.zero);
                Assert.That(enemyHealth.ApplyDamage(hit), Is.True);
                Assert.That(enemyHealth.ApplyDamage(hit), Is.False);
                Assert.That(enemyHealth.Current, Is.EqualTo(100));
                stop.Advance(1.1f);
                Assert.That(enemyHealth.Current, Is.EqualTo(86));
            }
            finally { Object.DestroyImmediate(stop.gameObject); }
        }
        [Test] public void SlimeParryStopsMovementAndCounterWindow()
        {
            Object.DestroyImmediate(goblin);
            var slime = enemy.AddComponent<SlimeController>();
            slime.Initialize("test", tuning, hero.transform, heroHealth, enemyHealth, enemy.GetComponent<Collider>(), null);
            slime.Tick(0.7f); Assert.That(slime.IsAttacking, Is.True);
            slime.Parry(1.5f); Vector3 position = enemy.transform.position;
            slime.Tick(1.4f); Assert.That(enemy.transform.position, Is.EqualTo(position));
            Assert.That(slime.IsParried, Is.True); Assert.That(slime.IsAttacking, Is.False);
            slime.Tick(0.11f); Assert.That(slime.IsParried, Is.False);
        }
    }
}
