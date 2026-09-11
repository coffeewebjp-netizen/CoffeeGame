using System;
using CoffeeGame.Actors;
using CoffeeGame.Audio;
using CoffeeGame.Combat;
using CoffeeGame.Domain;
using CoffeeGame.Enemies;
using CoffeeGame.Input;
using CoffeeGame.Presentation;
using CoffeeGame.Run;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CoffeeGame.Presentation.Tests
{
    public sealed class PartyRuntimeTests
    {
        private GameObject host, attacker;
        private CombatTuning tuning;
        private PlayerProgression progress;
        private GameInputReader input;
        private AudioDirector audio;
        private CombatRunController run;
        private PartyRuntime party;
        private bool hadPreference;
        private int preference;

        [SetUp] public void SetUp()
        {
            hadPreference = PlayerPrefs.HasKey(GameInputReader.InputModePlayerPrefsKey);
            preference = PlayerPrefs.GetInt(GameInputReader.InputModePlayerPrefsKey);
            host = new GameObject("party-test");
            attacker = new GameObject("hostile-source");
            attacker.AddComponent<Health>().SetTeam(DamageTeam.Enemy);
            tuning = CombatTuning.CreateDefault();
            input = host.AddComponent<GameInputReader>();
            audio = host.AddComponent<AudioDirector>();
            progress = new PlayerProgression(1, 0, 0, 0, previouslyRecruitedRivalIds: new[] { RivalCharacterIds.WeaknessChallenger });
            var hero = CreateActor(PartyMemberIds.Hero);
            run = host.AddComponent<CombatRunController>();
            run.Initialize(tuning, progress, input, audio, hero.Health, hero.Resources, hero.Motor, hero.Combat,
                claim =>
                {
                    var enemy = new GameObject("encounter"); enemy.transform.SetParent(host.transform);
                    var health = enemy.AddComponent<Health>(); health.Initialize(100); health.SetTeam(DamageTeam.Enemy);
                    var combatEnemy = enemy.AddComponent<CombatEnemy>();
                    combatEnemy.Initialize(claim, EnemyKind.Slime, health, new RewardBundle(1, 1, 0));
                    return combatEnemy;
                }, () => { });
            party = host.AddComponent<PartyRuntime>();
            party.Initialize(run, tuning, input, hero, () => CreateActor(PartyMemberIds.CatMage), () => { }, _ => { }, () => CreateActor(PartyMemberIds.DragonGirl));
            run.TrySelectInputMode(InputMode.KeyboardMouse, out _);
            if (progress.Party.Find(PartyMemberIds.Hero).RecoveryState == PartyRecoveryState.Resting) party.ToggleParticipation(PartyMemberIds.Hero);
            if (progress.Party.Find(PartyMemberIds.CatMage).RecoveryState == PartyRecoveryState.Resting) party.ToggleParticipation(PartyMemberIds.CatMage);
            run.StartNewRun();
        }

        private PartyActor CreateActor(string id)
        {
            var root = new GameObject(id); root.transform.SetParent(host.transform);
            root.AddComponent<CharacterController>();
            root.AddComponent<Health>().Initialize(24);
            root.AddComponent<PlayerResources>().Initialize(100, 20, 0);
            var motor = root.AddComponent<PlayerMotor3D>(); motor.Initialize(input, tuning, null, new FakeVisual());
            root.AddComponent<PlayerCombatController>().Initialize(input, tuning, motor, root.GetComponent<PlayerResources>(), root.GetComponent<Health>(), new FakeVisual(), audio);
            var actor = root.AddComponent<PartyActor>(); actor.Initialize(id); return actor;
        }

        [TearDown] public void TearDown()
        {
            Object.DestroyImmediate(host); Object.DestroyImmediate(attacker); Object.DestroyImmediate(tuning);
            if (hadPreference) PlayerPrefs.SetInt(GameInputReader.InputModePlayerPrefsKey, preference);
            else PlayerPrefs.DeleteKey(GameInputReader.InputModePlayerPrefsKey);
            Time.timeScale = 1;
        }

        [Test] public void ThreeMemberSwitchCyclesAndSkipsKnockedOutDragon()
        {
            progress.SetDebugRivalAffinity(RivalCharacterIds.SplitInk, 100);
            party.RefreshMembers();
            if (progress.Party.Find(PartyMemberIds.DragonGirl).RecoveryState == PartyRecoveryState.Resting)
                party.ToggleParticipation(PartyMemberIds.DragonGirl);
            Assert.That(party.RequestSwitch(), Is.True);
            Assert.That(party.Active.MemberId, Is.EqualTo(PartyMemberIds.CatMage));
            Assert.That(party.RequestSwitch(), Is.True);
            Assert.That(party.Active.MemberId, Is.EqualTo(PartyMemberIds.DragonGirl));
            party.Active.Health.ApplyDamage(new DamageInfo(99999, attacker, Vector3.zero, Vector3.zero));
            Assert.That(party.Active.MemberId, Is.EqualTo(PartyMemberIds.Hero));
            Assert.That(party.RequestSwitch(), Is.True);
            Assert.That(party.Active.MemberId, Is.EqualTo(PartyMemberIds.CatMage));
            Assert.That(party.RequestSwitch(), Is.True);
            Assert.That(party.Active.MemberId, Is.EqualTo(PartyMemberIds.Hero));
        }

        [Test] public void SwitchingKeepsBothPositionsAndVitals()
        {
            var hero = party.Actors[PartyMemberIds.Hero]; var cat = party.Actors[PartyMemberIds.CatMage];
            hero.Health.ApplyDamage(new DamageInfo(3, attacker, Vector3.zero, Vector3.zero));
            int hp = hero.Health.Current; var heroPosition = hero.transform.position; var catPosition = cat.transform.position;
            Assert.That(party.RequestSwitch(cat.MemberId), Is.True);
            Assert.That(run.PlayerHealth, Is.SameAs(cat.Health));
            Assert.That(hero.Health.Current, Is.EqualTo(hp));
            Assert.That(hero.transform.position, Is.EqualTo(heroPosition)); Assert.That(cat.transform.position, Is.EqualTo(catPosition));
        }

        [Test] public void KnockoutSwitchesThenBothDownEndRun()
        {
            var hero = party.Actors[PartyMemberIds.Hero]; var cat = party.Actors[PartyMemberIds.CatMage];
            hero.Health.ApplyDamage(new DamageInfo(9999, attacker, Vector3.zero, Vector3.zero));
            Assert.That(progress.Party.Find(hero.MemberId).RecoveryState, Is.EqualTo(PartyRecoveryState.KnockedOut));
            Assert.That(party.Active, Is.SameAs(cat)); Assert.That(run.Mode, Is.EqualTo(CombatRunMode.Playing));
            cat.Health.ApplyDamage(new DamageInfo(9999, attacker, Vector3.zero, Vector3.zero));
            Assert.That(run.Mode, Is.EqualTo(CombatRunMode.GameOver));
            Assert.That(party.TryStartRun(), Is.False);
            Assert.That(hero.Health.Current, Is.Zero); Assert.That(cat.Health.Current, Is.Zero);
        }

        [Test] public void HealthyReserveTakesOverAfterActiveKnockout()
        {
            var hero = party.Actors[PartyMemberIds.Hero]; var cat = party.Actors[PartyMemberIds.CatMage];
            run.Pause(); Assert.That(party.ToggleParticipation(cat.MemberId), Is.True); run.Resume();
            Assert.That(cat.gameObject.activeSelf, Is.False);
            hero.Health.ApplyDamage(new DamageInfo(9999, attacker, Vector3.zero, Vector3.zero));
            Assert.That(party.Active, Is.SameAs(cat)); Assert.That(cat.gameObject.activeSelf, Is.True);
            Assert.That(run.Mode, Is.EqualTo(CombatRunMode.Playing));
        }

        [Test] public void RetryAndLevelTuningDoNotRestoreLostHealth()
        {
            var hero = party.Actors[PartyMemberIds.Hero];
            hero.Health.ApplyDamage(new DamageInfo(3, attacker, Vector3.zero, Vector3.zero));
            int hp = hero.Health.Current;
            progress.TryApplyReward("level-test", new RewardBundle(100, 0, 0));
            Assert.That(hero.Health.Current, Is.EqualTo(hp));
            run.StartNewRun(); Assert.That(hero.Health.Current, Is.EqualTo(hp));
        }

        [Test] public void RestingLastMemberEndsRunWithoutDefeat()
        {
            run.Pause(); party.ToggleParticipation(PartyMemberIds.CatMage); party.ToggleParticipation(PartyMemberIds.Hero);
            Assert.That(run.Mode, Is.EqualTo(CombatRunMode.Ready)); Assert.That(progress.Party.IsDefeated, Is.False);
            Assert.That(progress.Party.Find(PartyMemberIds.Hero).RecoveryState, Is.EqualTo(PartyRecoveryState.Resting));
        }

        [Test] public void FractionalRecoveredHealthSurvivesActorHydrationAndCheckpoint()
        {
            var member = progress.Party.Find(PartyMemberIds.Hero);
            double recoveredHp = member.Resources.MaximumHitPoints * 0.5 + 0.4;
            member.SetResources(recoveredHp, member.Resources.MagicPoints, 0, party.UtcNow);
            party.RefreshMembers(); party.Snapshot();
            Assert.That(member.Resources.HitPoints, Is.EqualTo(recoveredHp).Within(0.00001));
        }

        [TestCase(0,1)] [TestCase(0,-1)] [TestCase(1,0)] [TestCase(-1,0)]
        [TestCase(1,1)] [TestCase(-1,-1)]
        public void HumanMovementKeepsDirectionThroughoutTimeStop(float x,float y)
        {
            var cat=party.Actors[PartyMemberIds.CatMage];
            Assert.That(party.RequestSwitch(cat.MemberId),Is.True);
            input.BeginInputModeSelection();
            Assert.That(input.TrySelectInputMode(InputMode.TouchOnScreen,out string message),Is.True,message);
            input.EnableBattle();
            input.SetTouchMove(Vector2.zero);input.RefreshContextSwitchReleaseGate();
            var stop=TimeStopController.EnsureExists();
            try
            {
                var direction=new Vector2(x,y).normalized;
                for(int phase=0;phase<3;phase++)
                {
                    if(phase==1)Assert.That(stop.TryBegin(cat.gameObject,10),Is.True);
                    if(phase==2)stop.Cancel();
                    input.SetTouchMove(direction);
                    typeof(PartyRuntime).GetMethod("Update",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(party,null);
                    Assert.That(cat.Motor.Commands.Move,Is.EqualTo(direction),"Motor phase "+phase);
                    Assert.That(cat.Combat.Commands.Move,Is.EqualTo(direction),"Attack/down-input phase "+phase);
                    Assert.That(cat.Motor.Commands.WorldSpace,Is.False);
                }
            }
            finally {Object.DestroyImmediate(stop.gameObject);}
        }

        private sealed class FakeVisual : ICharacterVisual
        {
            public void ResetState(Vector3 _) { } public void SetFacing(Vector3 _) { }
            public void SetLocomotion(CharacterAction _, float __) { } public void PlayAction(CharacterAction _, float __) { }
            public void SetAirHeight(float _) { } public void SetTint(Color _) { }
        }
    }
}
