using System.Collections;
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
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CoffeeGame.Presentation.Tests
{
    public sealed class CombatRunEncounterLifecycleTests
    {
        private GameObject host;
        private CombatTuning tuning;
        private CombatRunController run;
        private Health playerHealth;
        private int spawnedEnemies;

        [SetUp]
        public void SetUp()
        {
            Time.timeScale = 1f;
            host = new GameObject("encounter-lifecycle-test");
            tuning = CombatTuning.CreateDefault();

            GameInputReader input = host.AddComponent<GameInputReader>();
            AudioDirector audio = host.AddComponent<AudioDirector>();
            audio.Initialize();

            host.AddComponent<CharacterController>();
            playerHealth = host.AddComponent<Health>();
            playerHealth.Initialize(tuning.PlayerMaxHealth);
            playerHealth.SetTeam(DamageTeam.Party);
            PlayerResources resources = host.AddComponent<PlayerResources>();
            resources.Initialize(tuning.MaxStamina, tuning.PlayerMaxMp, tuning.MagicMpRegenPerSecond);
            var motor = host.AddComponent<PlayerMotor3D>();
            motor.Initialize(input, tuning, null, new FakeVisual());
            var combat = host.AddComponent<PlayerCombatController>();
            combat.Initialize(input, tuning, motor, resources, playerHealth, new FakeVisual(), audio);

            run = host.AddComponent<CombatRunController>();
            run.Initialize(
                tuning,
                new PlayerProgression(),
                input,
                audio,
                playerHealth,
                resources,
                motor,
                combat,
                claim => CreateEnemy(claim, EnemyEncounterRoster.At(spawnedEnemies++)),
                () => spawnedEnemies = 0);
            Assert.That(run.TrySelectInputMode(InputMode.KeyboardMouse, out string message), Is.True, message);
            run.StartNewRun();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(tuning);
            Time.timeScale = 1f;
        }

        [Test]
        public void StartsWithGoblinAndSlimeAndExposesBothSlots()
        {
            Assert.That(run.ActiveEnemyCount, Is.EqualTo(CombatRunController.EnemiesPerEncounter));
            Assert.That(run.AliveEnemyCount, Is.EqualTo(2));
            Assert.That(run.ActiveEnemies[0].Kind, Is.EqualTo(EnemyKind.Goblin));
            Assert.That(run.ActiveEnemies[1].Kind, Is.EqualTo(EnemyKind.Slime));
            Assert.That(run.CurrentEnemy, Is.SameAs(run.ActiveEnemies[0]));
        }

        [UnityTest]
        public IEnumerator DefeatedSlotIsReplacedAfterDisplayDelay()
        {
            CombatEnemy defeated = run.ActiveEnemies[0];
            int initialGold = run.Progression.Gold;
            defeated.Health.ApplyDamage(new DamageInfo(999, null, Vector3.zero, Vector3.zero));

            Assert.That(run.Kills, Is.EqualTo(1));
            Assert.That(run.Progression.Gold, Is.EqualTo(initialGold + 1));
            Assert.That(run.AliveEnemyCount, Is.EqualTo(1));

            yield return new WaitForSecondsRealtime(defeated.DefeatDisplaySeconds + 0.1f);

            Assert.That(run.Mode, Is.EqualTo(CombatRunMode.Playing));
            Assert.That(run.ActiveEnemyCount, Is.EqualTo(2));
            Assert.That(run.AliveEnemyCount, Is.EqualTo(2));
            Assert.That(spawnedEnemies, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator SimultaneousFifthKillsEnterOneRivalAndClearAttackers()
        {
            for (int i = 0; i < 4; i++)
            {
                CombatEnemy defeated = run.ActiveEnemies[0];
                defeated.Health.ApplyDamage(new DamageInfo(999, null, Vector3.zero, Vector3.zero));
                yield return new WaitForSecondsRealtime(defeated.DefeatDisplaySeconds + 0.1f);
            }

            Assert.That(run.Kills, Is.EqualTo(4));
            CombatEnemy fifth = run.ActiveEnemies[0];
            CombatEnemy sixth = run.ActiveEnemies[1];
            fifth.Health.ApplyDamage(new DamageInfo(999, null, Vector3.zero, Vector3.zero));
            sixth.Health.ApplyDamage(new DamageInfo(999, null, Vector3.zero, Vector3.zero));
            Assert.That(run.Kills, Is.EqualTo(6));

            yield return new WaitForSecondsRealtime(fifth.DefeatDisplaySeconds + 0.1f);

            Assert.That(run.Mode, Is.EqualTo(CombatRunMode.RivalEncounter));
            Assert.That(run.ActiveEnemyCount, Is.Zero);
            Assert.That(run.AliveEnemyCount, Is.Zero);
            Assert.That(run.Progression.Gold, Is.EqualTo(6));

            run.ContinueAfterRivalEncounter();
            Assert.That(run.Mode, Is.EqualTo(CombatRunMode.Playing));
            Assert.That(run.ActiveEnemyCount, Is.EqualTo(2));
            Assert.That(run.AliveEnemyCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator PauseFreezesDefeatReplacement()
        {
            CombatEnemy defeated = run.ActiveEnemies[0];
            defeated.Health.ApplyDamage(new DamageInfo(999, null, Vector3.zero, Vector3.zero));
            run.Pause();

            yield return new WaitForSecondsRealtime(defeated.DefeatDisplaySeconds + 0.1f);

            Assert.That(run.Mode, Is.EqualTo(CombatRunMode.Paused));
            Assert.That(run.AliveEnemyCount, Is.EqualTo(1));
            run.Resume();
            yield return new WaitForSecondsRealtime(defeated.DefeatDisplaySeconds + 0.1f);
            Assert.That(run.AliveEnemyCount, Is.EqualTo(2));
        }

        [Test]
        public void PlayerDefeatRemovesAllLiveAttackers()
        {
            playerHealth.ApplyDamage(new DamageInfo(999, null, Vector3.zero, Vector3.zero));

            Assert.That(run.Mode, Is.EqualTo(CombatRunMode.GameOver));
            Assert.That(run.ActiveEnemyCount, Is.Zero);
            Assert.That(run.AliveEnemyCount, Is.Zero);
        }

        private CombatEnemy CreateEnemy(string claimId, EnemyKind kind)
        {
            GameObject enemyObject = new GameObject(kind + " encounter");
            enemyObject.transform.SetParent(host.transform);
            Health health = enemyObject.AddComponent<Health>();
            health.Initialize(1);
            health.SetTeam(DamageTeam.Enemy);
            CombatEnemy enemy = enemyObject.AddComponent<CombatEnemy>();
            enemy.Initialize(claimId, kind, health,
                kind == EnemyKind.Slime ? new RewardBundle(1, 1, 1) : new RewardBundle(1, 1, 0));
            return enemy;
        }

        private sealed class FakeVisual : ICharacterVisual
        {
            public void ResetState(Vector3 _) { }
            public void SetFacing(Vector3 _) { }
            public void SetLocomotion(CharacterAction _, float __) { }
            public void PlayAction(CharacterAction _, float __) { }
            public void SetAirHeight(float _) { }
            public void SetTint(Color _) { }
        }
    }
}
