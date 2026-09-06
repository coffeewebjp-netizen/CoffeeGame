using CoffeeGame.Actors;
using CoffeeGame.Domain;
using CoffeeGame.Enemies;
using CoffeeGame.Presentation;
using CoffeeGame.Run;
using NUnit.Framework;
using UnityEngine;

namespace CoffeeGame.Presentation.Tests
{
    public sealed class GoblinCombatTests
    {
        private GameObject goblin, player;
        private Health health, playerHealth;
        private GoblinController controller;
        private CombatTuning tuning;
        private FakeVisual visual;

        [SetUp] public void Setup()
        {
            tuning = CombatTuning.CreateDefault();
            goblin = new GameObject("goblin-test");
            player = new GameObject("player-test");
            player.transform.position = Vector3.forward;
            health = goblin.AddComponent<Health>(); health.Initialize(15);
            playerHealth = player.AddComponent<Health>(); playerHealth.Initialize(24);
            visual = new FakeVisual();
            controller = goblin.AddComponent<GoblinController>();
            controller.Initialize(tuning, player.transform, playerHealth, health,
                goblin.AddComponent<CapsuleCollider>(), visual, 0);
        }
        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(goblin); Object.DestroyImmediate(player);
            Object.DestroyImmediate(tuning); Time.timeScale = 1f;
        }
        private void Windup()
        {
            for (int i = 0; i < 300 && !controller.IsWindingUp; i++) controller.Tick(0.01f);
            Assert.That(controller.IsWindingUp, Is.True);
        }
        private void Strike()
        {
            for (int i = 0; i < 150 && controller.Phase != GoblinController.CombatPhase.Recovery; i++) controller.Tick(0.01f);
        }
        [Test] public void AttackWaitsForWindupAndImpactAndHitsOnlyOnce()
        {
            Windup(); Assert.That(playerHealth.Current, Is.EqualTo(24));
            controller.Tick(0.70f); Assert.That(playerHealth.Current, Is.EqualTo(24));
            controller.Tick(0.03f); Assert.That(controller.Phase, Is.EqualTo(GoblinController.CombatPhase.Strike));
            controller.Tick(0.14f); Assert.That(playerHealth.Current, Is.EqualTo(24));
            controller.Tick(0.03f); Assert.That(playerHealth.Current, Is.EqualTo(24 - tuning.SlimeDamage));
            controller.Tick(0.03f); Assert.That(playerHealth.Current, Is.EqualTo(24 - tuning.SlimeDamage));
        }
        [TestCase(1f,0f,0f)] [TestCase(0f,1f,1f)] [TestCase(0f,0f,3f)] [TestCase(0f,0f,-1f)]
        public void SideStepJumpDistanceAndBehindAvoidCommittedStrike(float x,float y,float z)
        {
            Windup(); Vector3 aim = controller.AttackDirection;
            player.transform.position = goblin.transform.position + new Vector3(x,y,z);
            Strike(); Assert.That(playerHealth.Current, Is.EqualTo(24));
            Assert.That(controller.AttackDirection, Is.EqualTo(aim));
        }
        [Test] public void HurtCancelsWindupAndHasRecovery()
        {
            Windup(); health.ApplyDamage(new DamageInfo(1,player,Vector3.zero,Vector3.zero));
            Assert.That(controller.Phase, Is.EqualTo(GoblinController.CombatPhase.Hurt));
            controller.Tick(0.2f); Assert.That(controller.IsWindingUp, Is.False);
            Assert.That(playerHealth.Current, Is.EqualTo(24));
            Assert.That(visual.LastAction, Is.EqualTo(CharacterAction.Hurt));
        }
        [Test] public void PauseDoesNotAdvanceOrRotateCommittedAttack()
        {
            Windup(); Vector3 position = goblin.transform.position;
            for(int i=0;i<100;i++) controller.Tick(0f);
            Assert.That(controller.IsWindingUp, Is.True);
            Assert.That(goblin.transform.position, Is.EqualTo(position));
            Assert.That(playerHealth.Current, Is.EqualTo(24));
        }
        [Test] public void DeathStopsAttackDisablesColliderAndRewardsOnce()
        {
            Windup(); var enemy = goblin.AddComponent<CombatEnemy>();
            enemy.Initialize("goblin:unique", EnemyKind.Goblin, health, GoblinController.Reward);
            var progression = new PlayerProgression(); int notifications=0;
            enemy.Defeated += defeated => { notifications++; progression.TryApplyReward(defeated.ClaimId,defeated.Reward); };
            var kill = new DamageInfo(999,player,Vector3.zero,Vector3.zero);
            health.ApplyDamage(kill); health.ApplyDamage(kill); controller.Tick(3f);
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(progression.TryApplyReward(enemy.ClaimId,enemy.Reward), Is.False);
            Assert.That(progression.Gold, Is.EqualTo(1));
            Assert.That(progression.SlimeJelly, Is.Zero);
            Assert.That(goblin.GetComponent<Collider>().enabled, Is.False);
            Assert.That(controller.Phase, Is.EqualTo(GoblinController.CombatPhase.Defeated));
            Assert.That(playerHealth.Current, Is.EqualTo(24));
        }
        [Test] public void PlayerDeathPreventsFurtherEnemyActions()
        {
            Windup(); playerHealth.ApplyDamage(new DamageInfo(999,goblin,Vector3.zero,Vector3.zero));
            controller.Tick(10f); Assert.That(controller.IsWindingUp, Is.True);
        }
        [Test] public void EncounterVarietyPreservesFiveKillRivalCadence()
        {
            int goblins=0, slimes=0, rivals=0;
            for(int i=0;i<10;i++)
            {
                if(EnemyEncounterRoster.At(i)==EnemyKind.Goblin)goblins++;else slimes++;
                if(CombatRunController.IsRivalEncounterMilestone(i+1,5))rivals++;
            }
            Assert.That(EnemyEncounterRoster.At(0), Is.EqualTo(EnemyKind.Goblin));
            Assert.That(goblins, Is.EqualTo(5)); Assert.That(slimes, Is.EqualTo(5)); Assert.That(rivals, Is.EqualTo(2));
            Assert.That(tuning.SlimeReward, Is.EqualTo(new RewardBundle(1,1,1)));
        }
        [Test] public void RuntimeVisualInitializesWithTexturesAndRetainsLeatherTint()
        {
            var root = new GameObject("goblin-resource-test");
            try
            {
                var runtimeVisual = root.AddComponent<GoblinCharacterVisual>();
                runtimeVisual.Initialize();
                var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>();
                Assert.That(skins.Length, Is.EqualTo(3));
                Assert.That(runtimeVisual.Animator.runtimeAnimatorController.animationClips.Length, Is.EqualTo(6));
                foreach (var skin in skins)
                {
                    Assert.That(skin.sharedMaterial, Is.Not.Null);
                    var properties = new MaterialPropertyBlock(); skin.GetPropertyBlock(properties);
                    Assert.That(Vector4.Distance(properties.GetColor("_BaseColor"),skin.sharedMaterial.GetColor("_BaseColor")), Is.LessThan(0.00001f));
                    if (skin.name != "ClubGripGlove") Assert.That(skin.sharedMaterial.GetTexture("_BaseMap"), Is.Not.Null);
                }
            }
            finally { Object.DestroyImmediate(root); }
        }
        private sealed class FakeVisual : ICharacterVisual
        {
            public CharacterAction LastAction;
            public void ResetState(Vector3 direction) { }
            public void SetFacing(Vector3 direction) { }
            public void SetLocomotion(CharacterAction action,float speed) { }
            public void PlayAction(CharacterAction action,float duration) { LastAction=action; }
            public void SetAirHeight(float height) { }
            public void SetTint(Color tint) { }
        }
    }
}
