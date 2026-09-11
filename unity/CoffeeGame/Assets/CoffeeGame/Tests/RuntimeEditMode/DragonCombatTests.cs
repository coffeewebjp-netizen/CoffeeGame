using System;
using System.Reflection;
using CoffeeGame.Actors;
using CoffeeGame.Combat;
using CoffeeGame.Domain;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace CoffeeGame.Presentation.Tests
{
    public sealed class DragonCombatTests
    {
        private GameObject root, enemy;
        [Test] public void DragonInitializationPreservesItsOwnKimonoTexture()
        {
            var prefab=Resources.Load<GameObject>("Models/Characters/DragonGirl/dragon-girl");
            var texture=Resources.Load<Texture2D>("Models/Characters/DragonGirl/dragon-basecolor");
            root=new GameObject("Dragon texture test");var model=Object.Instantiate(prefab,root.transform);
            var visual=root.AddComponent<ModelCharacterVisual>();
            visual.Initialize(model.transform,null,CharacterModelStyle.Imported,null,applyTrialTextures:false);
            foreach(var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                foreach(var material in renderer.sharedMaterials)
                    Assert.That(material.GetTexture("_BaseMap"),Is.SameAs(texture));
        }
        [TearDown] public void Cleanup(){if(root!=null)Object.DestroyImmediate(root);if(enemy!=null)Object.DestroyImmediate(enemy);Time.timeScale=1f;}
        [Test] public void RecruitedDragonRestoresAsDistinctThirdPartyMember()
        {
            var p=new PlayerProgression(1,0,0,0,previouslyRecruitedRivalIds:new[]{RivalCharacterIds.WeaknessChallenger,RivalCharacterIds.SplitInk});
            Assert.That(p.Party.Members.Count,Is.EqualTo(3));
            Assert.That(p.Party.Find(PartyMemberIds.DragonGirl),Is.Not.SameAs(p.Party.Find(PartyMemberIds.CatMage)));
            p.SetDebugRivalAffinity(RivalCharacterIds.SplitInk,100);
            Assert.That(p.Party.Members.Count,Is.EqualTo(3));
        }
        [Test] public void DragonDebugRecruitmentPreservesCatAndRewards()
        {
            var p=new PlayerProgression();p.SetDebugRivalAffinity(RivalCharacterIds.WeaknessChallenger,100);p.SetDebugRivalAffinity(RivalCharacterIds.SplitInk,100);
            Assert.That(p.Party.Members.Count,Is.EqualTo(3));Assert.That(p.Gold,Is.EqualTo(0));Assert.That(p.ClaimedRewardCount,Is.EqualTo(0));
        }
        [Test] public void DoubleDefenseHalvesIncomingDamageAndDoesNotMakeInvincible()
        {
            root=new GameObject();enemy=new GameObject();var health=root.AddComponent<Health>();health.Initialize(100);health.SetTeam(DamageTeam.Party);health.AbilityDefenseMultiplier=2;
            enemy.AddComponent<Health>().SetTeam(DamageTeam.Enemy);
            Assert.That(health.ApplyDamage(new DamageInfo(20,enemy,Vector3.zero,Vector3.zero)),Is.True);Assert.That(health.Current,Is.EqualTo(90));
        }
        [Test] public void GateFreezesOnlyVictimAndCannotDamageWhileCaptured()
        {
            root=new GameObject();enemy=new GameObject();var hero=root.AddComponent<Health>();hero.Initialize(100);hero.SetTeam(DamageTeam.Party);
            var victim=enemy.AddComponent<Health>();victim.Initialize(100);victim.SetTeam(DamageTeam.Enemy);
            var trap=enemy.AddComponent<DragonGateCaptivity>();trap.Capture(root,12,root.transform);
            Assert.That(DragonGateCaptivity.IsCaptured(enemy),Is.True);Assert.That(DragonGateCaptivity.IsCaptured(root),Is.False);
            Assert.That(hero.ApplyDamage(new DamageInfo(20,enemy,Vector3.zero,Vector3.zero)),Is.False);
            Assert.That(victim.ApplyDamage(new DamageInfo(20,root,Vector3.zero,Vector3.zero)),Is.True);
            trap.Release(root);Assert.That(DragonGateCaptivity.IsCaptured(enemy),Is.False);
        }
        [Test] public void CancelRestoresAbilityMultipliers()
        {
            root=new GameObject();var motor=root.AddComponent<PlayerMotor3D>();var health=root.AddComponent<Health>();health.Initialize(100);var combat=root.AddComponent<PlayerCombatController>();
            typeof(PlayerCombatController).GetField("motor",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(combat,motor);
            typeof(PlayerCombatController).GetField("playerHealth",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(combat,health);
            motor.AbilitySpeedMultiplier=2;health.AbilityDefenseMultiplier=2;combat.CancelPendingActions();
            Assert.That(motor.AbilitySpeedMultiplier,Is.EqualTo(1));Assert.That(health.AbilityDefenseMultiplier,Is.EqualTo(1));
        }
        [Test] public void HealingNeverRevivesKnockedOutActor()
        {
            root=new GameObject();var health=root.AddComponent<Health>();health.Initialize(10);health.SetCurrentAndMaximum(0,10);health.Heal(20);Assert.That(health.Current,Is.EqualTo(0));
        }
        [Test] public void BreathDoublesAttackAndRecoveryThenExpires()
        {
            root=new GameObject();var motor=root.AddComponent<PlayerMotor3D>();var health=root.AddComponent<Health>();
            health.Initialize(900);health.SetCurrentAndMaximum(100,900);
            var resources=root.AddComponent<PlayerResources>();resources.Initialize(100,1000,1);resources.TrySpendMagic(1000);
            var combat=root.AddComponent<PlayerCombatController>();var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(PlayerCombatController).GetField("motor",flags).SetValue(combat,motor);
            typeof(PlayerCombatController).GetField("playerHealth",flags).SetValue(combat,health);
            typeof(PlayerCombatController).GetField("resources",flags).SetValue(combat,resources);
            typeof(PlayerCombatController).GetProperty("DragonBreathRemaining").SetValue(combat,60f);
            var damage=typeof(PlayerCombatController).GetMethod("CalculateDamage",flags);
            Assert.That(damage.Invoke(combat,new object[]{20}),Is.EqualTo(40));
            var tick=typeof(PlayerCombatController).GetMethod("TickDragon",flags);
            resources.Tick(10);tick.Invoke(combat,new object[]{10f});
            Assert.That(resources.MagicPoints,Is.EqualTo(20));Assert.That(health.Current,Is.EqualTo(120));
            tick.Invoke(combat,new object[]{50f});
            Assert.That(combat.DragonBreathRemaining,Is.Zero);Assert.That(motor.AbilitySpeedMultiplier,Is.EqualTo(1));
            Assert.That(health.AbilityDefenseMultiplier,Is.EqualTo(1));Assert.That(damage.Invoke(combat,new object[]{20}),Is.EqualTo(20));
        }
    }
}
