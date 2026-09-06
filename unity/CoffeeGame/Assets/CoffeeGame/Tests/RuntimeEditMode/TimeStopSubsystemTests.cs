using CoffeeGame.Actors;
using NUnit.Framework;
using UnityEngine;

namespace CoffeeGame.Combat.Tests
{
    public sealed class TimeStopSubsystemTests
    {
        private GameObject controllerHost;
        private GameObject caster;
        private GameObject targetHost;
        private TimeStopController controller;
        private Health casterHealth;
        private Health target;
        private float previousTimeScale;

        [SetUp]
        public void SetUp()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            if (TimeStopController.Instance != null)
            {
                Object.DestroyImmediate(TimeStopController.Instance.gameObject);
            }

            controllerHost = new GameObject("time-stop-test-controller");
            controller = controllerHost.AddComponent<TimeStopController>();
            caster = new GameObject("cat-caster");
            casterHealth = caster.AddComponent<Health>();
            casterHealth.Initialize(10);
            casterHealth.SetTeam(DamageTeam.Party);
            targetHost = new GameObject("enemy-target");
            target = targetHost.AddComponent<Health>();
            target.Initialize(10);
            target.SetTeam(DamageTeam.Enemy);
        }

        [TearDown]
        public void TearDown()
        {
            if (controllerHost != null)
            {
                Object.DestroyImmediate(controllerHost);
            }
            if (caster != null)
            {
                Object.DestroyImmediate(caster);
            }
            if (targetHost != null)
            {
                Object.DestroyImmediate(targetHost);
            }
            Time.timeScale = previousTimeScale;
        }

        [Test]
        public void QueuedHits_AreDeduplicatedAndAppliedAsOneRelease()
        {
            int damageEvents = 0;
            target.Damaged += (_, __) => damageEvents++;
            Assert.That(controller.TryBegin(caster, 10f), Is.True);

            Assert.That(target.ApplyDamage(Hit(2, 101)), Is.True);
            Assert.That(target.ApplyDamage(Hit(2, 101)), Is.False);
            Assert.That(target.ApplyDamage(Hit(3, 102)), Is.True);
            Assert.That(target.Current, Is.EqualTo(10));

            controller.Advance(10f);

            Assert.That(controller.IsActive, Is.False);
            Assert.That(target.Current, Is.EqualTo(5));
            Assert.That(damageEvents, Is.EqualTo(1));
        }

        [Test]
        public void QueuedHits_IgnoreInvulnerabilityCreatedByEarlierQueuedHit()
        {
            target.Initialize(10, 5f);
            Assert.That(controller.TryBegin(caster, 10f), Is.True);
            Assert.That(target.ApplyDamage(Hit(2, 201)), Is.True);
            Assert.That(target.ApplyDamage(Hit(3, 202)), Is.True);

            controller.Advance(10f);

            Assert.That(target.Current, Is.EqualTo(5));
        }

        [Test]
        public void ExistingInvulnerability_IsEvaluatedBeforeAHitCanQueue()
        {
            target.Initialize(10, 5f);
            Assert.That(target.ApplyDamage(Hit(1, 301)), Is.True);
            Assert.That(controller.TryBegin(caster, 10f), Is.True);

            Assert.That(target.ApplyDamage(Hit(3, 302)), Is.False);
            controller.Advance(10f);

            Assert.That(target.Current, Is.EqualTo(9));
        }

        [Test]
        public void EvadedAttackId_CannotRetryDuringTheStop()
        {
            Assert.That(controller.TryBegin(caster, 10f), Is.True);
            target.EvasionChance = 1f;
            Assert.That(target.ApplyDamage(Hit(3, 351)), Is.False);
            target.EvasionChance = 0f;

            Assert.That(target.ApplyDamage(Hit(3, 351)), Is.False);
            controller.Advance(10f);

            Assert.That(target.Current, Is.EqualTo(10));
        }

        [Test]
        public void Cancel_DropsQueuedHitsAndRestoresFrozenComponents()
        {
            Rigidbody body = targetHost.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.linearVelocity = new Vector3(2f, 0f, 0f);
            Animator animator = targetHost.AddComponent<Animator>();
            animator.speed = 0.65f;

            Assert.That(controller.TryBegin(caster, 10f), Is.True);
            Assert.That(body.isKinematic, Is.True);
            Assert.That(animator.speed, Is.Zero);
            Assert.That(target.ApplyDamage(Hit(4, 401)), Is.True);

            controller.Cancel();

            Assert.That(target.Current, Is.EqualTo(10));
            Assert.That(body.isKinematic, Is.False);
            Assert.That(body.linearVelocity.x, Is.EqualTo(2f).Within(0.001f));
            Assert.That(animator.speed, Is.EqualTo(0.65f).Within(0.001f));
        }

        [Test]
        public void Pause_DoesNotConsumeAbilityDuration()
        {
            Assert.That(controller.TryBegin(caster, 10f), Is.True);
            controller.SetPaused(true);
            controller.Advance(6f);
            Assert.That(controller.Remaining, Is.EqualTo(10f));

            controller.SetPaused(false);
            controller.Advance(3f);
            Assert.That(controller.Remaining, Is.EqualTo(7f));
        }

        [Test]
        public void FrozenSource_CannotQueueTouchingDamage()
        {
            var enemySource = new GameObject("frozen-enemy-source");
            Health enemyHealth = enemySource.AddComponent<Health>();
            enemyHealth.Initialize(10);
            enemyHealth.SetTeam(DamageTeam.Enemy);
            target.SetTeam(DamageTeam.Party);
            try
            {
                Assert.That(controller.TryBegin(caster, 10f), Is.True);
                var damage = new DamageInfo(3, enemySource, Vector3.zero, Vector3.zero, 501);
                Assert.That(target.ApplyDamage(damage), Is.False);
                controller.Advance(10f);
                Assert.That(target.Current, Is.EqualTo(10));
            }
            finally
            {
                Object.DestroyImmediate(enemySource);
            }
        }

        [Test]
        public void SameTeamDamage_IsRejected()
        {
            target.SetTeam(DamageTeam.Party);

            Assert.That(target.ApplyDamage(Hit(3, 601)), Is.False);
            Assert.That(target.Current, Is.EqualTo(10));
        }

        [Test]
        public void SetCurrentAndMaximum_RestoresWithoutHealing()
        {
            target.SetCurrentAndMaximum(4, 25);

            Assert.That(target.Current, Is.EqualTo(4));
            Assert.That(target.Maximum, Is.EqualTo(25));
        }

        [Test]
        public void CasterLoss_AbortsQueuedDamage()
        {
            Assert.That(controller.TryBegin(caster, 10f), Is.True);
            Assert.That(target.ApplyDamage(Hit(4, 701)), Is.True);

            Object.DestroyImmediate(caster);
            controller.Advance(1f);

            Assert.That(controller.IsActive, Is.False);
            Assert.That(target.Current, Is.EqualTo(10));
        }

        private DamageInfo Hit(int amount, long attackId)
        {
            return new DamageInfo(amount, caster, target.transform.position, Vector3.zero, attackId);
        }
    }
}
