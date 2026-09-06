using System.Reflection;
using CoffeeGame.Actors;
using CoffeeGame.Combat;
using CoffeeGame.Domain;
using CoffeeGame.Enemies;
using NUnit.Framework;
using UnityEngine;

namespace CoffeeGame.Tests
{
    public sealed class EnemyEscapeDodgeTests
    {
        private GameObject hero, enemy, floor;
        private Health health, enemyHealth;
        private PlayerMotor3D motor;
        private PlayerResources resources;
        private PlayerDefense defense;
        private CombatTuning tuning;
        private GoblinController goblin;

        [SetUp] public void Setup()
        {
            Time.timeScale = 1f;
            tuning = CombatTuning.CreateDefault();
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = Vector3.down * .5f; floor.transform.localScale = new Vector3(30, 1, 30);
            hero = new GameObject("escaping heroine");
            var cc = hero.AddComponent<CharacterController>(); cc.height = 1.7f; cc.center = Vector3.up * .85f; cc.radius = .22f;
            motor = hero.AddComponent<PlayerMotor3D>(); motor.UseCommands = true; motor.Initialize(null, tuning, null, null);
            motor.ResetMotor(new Vector3(0, .03f, 1));
            health = hero.AddComponent<Health>(); health.Initialize(100); health.SetTeam(DamageTeam.Party);
            resources = hero.AddComponent<PlayerResources>(); resources.Initialize(100, 20, 0);
            var combat = hero.AddComponent<PlayerCombatController>(); combat.UseCommands = true;
            combat.Initialize(null, tuning, motor, resources, health, null, null);
            defense = hero.GetComponent<PlayerDefense>();
            Physics.SyncTransforms(); for (int i = 0; i < 10; i++) motor.Tick(.01f);
            enemy = new GameObject("incoming goblin");
            enemyHealth = enemy.AddComponent<Health>(); enemyHealth.Initialize(100); enemyHealth.SetTeam(DamageTeam.Enemy);
            goblin = enemy.AddComponent<GoblinController>();
            goblin.Initialize(tuning, hero.transform, health, enemyHealth, null, null, 0);
        }
        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(hero); Object.DestroyImmediate(enemy); Object.DestroyImmediate(floor); Object.DestroyImmediate(tuning);
            Time.timeScale = 1f;
        }
        private void CommitGoblin()
        {
            goblin.Tick(.71f); Assert.That(goblin.IsWindingUp, Is.True);
            goblin.Tick(GoblinController.WindupSeconds + .01f);
        }
        private void Escape(bool backward = false)
        {
            motor.Commands = new ActorCommandFrame { Move = backward ? Vector2.down : Vector2.up, WorldSpace = true, Dodge = true };
            motor.Tick(.01f); motor.Commands = default;
            for (int i = 0; i < 26; i++) motor.Tick(.01f);
            Assert.That(motor.IsDodging, Is.True);
        }
        [Test] public void RealGoblinImpactRewardsEscapeBeyondVolumeOnce()
        {
            CommitGoblin(); Escape(); Assert.That(goblin.Threatens(hero.transform.position), Is.False);
            int perfect = 0; defense.PerfectDodge += () => perfect++;
            goblin.Tick(GoblinController.ImpactSeconds + .01f);
            Assert.That(resources.Stamina, Is.EqualTo(100)); Assert.That(health.Current, Is.EqualTo(100));
            resources.TrySpendStamina(100); goblin.Tick(.02f);
            Assert.That(resources.Stamina, Is.Zero); Assert.That(perfect, Is.EqualTo(1));
        }
        [TestCase("early")] [TestCase("behind")] [TestCase("parried")] [TestCase("jump")]
        public void RealGoblinWhiffsOrUnrelatedActionsDoNotReward(string kind)
        {
            CommitGoblin();
            if (kind == "behind") { motor.ResetMotor(new Vector3(0, .03f, -1)); Physics.SyncTransforms(); for(int i=0;i<10;i++)motor.Tick(.01f); }
            if (kind == "jump") { motor.Commands = new ActorCommandFrame { Jump = true }; motor.Tick(.01f); motor.Commands = default; }
            else Escape(kind == "behind");
            if (kind == "early") typeof(PlayerDefense).GetField("dodgeUntil", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(defense, CombatClock.Time(hero) - .01f);
            if (kind == "parried") goblin.Parry(1f);
            goblin.Tick(GoblinController.ImpactSeconds + .01f);
            Assert.That(resources.Stamina, Is.Zero);
        }
        [Test] public void RealSlimeImpactRewardsEscapedOrigin()
        {
            Object.DestroyImmediate(goblin);
            motor.ResetMotor(new Vector3(0, .03f, 1.3f)); Physics.SyncTransforms();
            for (int i = 0; i < 10; i++) motor.Tick(.01f);
            var slime = enemy.AddComponent<SlimeController>(); slime.Initialize("escape", tuning, hero.transform, health, enemyHealth, null, null);
            slime.Tick(.61f); Assert.That(slime.IsWindingUp, Is.True);
            Escape(); slime.Tick(tuning.SlimeWindupSeconds + .01f);
            Assert.That(Vector3.ProjectOnPlane(hero.transform.position - enemy.transform.position, Vector3.up).magnitude,
                Is.GreaterThan(tuning.SlimeAttackRange * 1.18f), "escaped even beyond the slime's release lunge");
            Assert.That(resources.Stamina, Is.EqualTo(100)); Assert.That(health.Current, Is.EqualTo(100));
        }
        [Test] public void ForgivingInvulnerabilityIsFiniteAndEndsOnLanding()
        {
            Assert.That(tuning.JustDodgeSeconds, Is.EqualTo(.30f));
            Assert.That(tuning.DodgeInvulnerabilitySeconds, Is.InRange(.60f, .62f));
            Escape();
            Assert.That(health.ApplyDamage(new DamageInfo(2, enemy, hero.transform.position, Vector3.zero)), Is.False);
            for(int i=0;i<50;i++)motor.Tick(.01f);
            Assert.That(motor.IsDodging, Is.False);
            Assert.That(health.ApplyDamage(new DamageInfo(2, enemy, hero.transform.position, Vector3.zero)), Is.True);
        }
    }
}
