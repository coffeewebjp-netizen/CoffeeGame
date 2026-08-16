using System;
using System.IO;
using System.Linq;
using CoffeeGame.Domain;
using CoffeeGame.Persistence;
using NUnit.Framework;

namespace CoffeeGame.Persistence.Tests
{
    public sealed class PlayerProfileStoreTests
    {
        private string temporaryDirectory;
        private string profilePath;

        [SetUp]
        public void SetUp()
        {
            temporaryDirectory = Path.Combine(Path.GetTempPath(), "CoffeeGameProfileTests", Guid.NewGuid().ToString("N"));
            profilePath = Path.Combine(temporaryDirectory, "profile.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, true);
            }
        }

        [Test]
        public void SaveAndLoad_RoundTripsFutureAttributesGrowthAndRewardClaims()
        {
            var status = new PlayerStatus(
                PlayerStatus.DefaultArchetypeId,
                PlayerStatus.DefaultClassName,
                "late-bloomer",
                "大器晩成",
                new[]
                {
                    new PlayerAttributeValue(PlayerAttributeIds.Strength, 20),
                    new PlayerAttributeValue("spirit", 17)
                },
                null);
            status = status.ApplyLevelGrowth(
                1,
                new TalentGrowthProfile(
                    "late-bloomer",
                    new[] { new PlayerGrowthRule(PlayerAttributeIds.Strength, 1250) }));
            var progression = new PlayerProgression(
                2,
                1,
                45,
                3,
                null,
                status,
                talentPoints: 7,
                rivalAffinities: new[] { new RivalAffinityEntry("rival-silver-001", 95) });
            Assert.That(progression.TryApplyReward("claimed-before-save", new RewardBundle(0, 0, 0)), Is.True);
            var learningOutcome = new AuthoritativeLearningOutcome(
                "result-before-save",
                AuthoritativeLearningResultStatus.Completed,
                true,
                true,
                true,
                "grant-before-save",
                LearningDifficultyBand.Foundation,
                1);
            Assert.That(
                progression.TryApplyLearningOutcome(learningOutcome, "rival-silver-001").Status,
                Is.EqualTo(LearningRewardApplyStatus.Granted));

            var store = new PlayerProfileStore(profilePath);
            Assert.That(store.TrySave(progression, out string saveMessage), Is.True, saveMessage);
            PlayerProgression restored = store.LoadOrCreate(out string loadMessage);

            Assert.That(loadMessage, Does.Contain("読み込み"));
            Assert.That(restored.Level, Is.EqualTo(2));
            Assert.That(restored.Experience, Is.EqualTo(3));
            Assert.That(restored.Gold, Is.EqualTo(46));
            Assert.That(restored.SlimeJelly, Is.EqualTo(3));
            Assert.That(restored.TalentPoints, Is.EqualTo(8));
            Assert.That(restored.GetRivalAffinity("rival-silver-001"), Is.EqualTo(98));
            Assert.That(restored.Status.TalentId, Is.EqualTo("late-bloomer"));
            Assert.That(restored.Status.Strength, Is.EqualTo(21));
            Assert.That(restored.Status.Attributes.GetValue("spirit"), Is.EqualTo(17));
            Assert.That(
                restored.Status.CreateGrowthRemainderSnapshot()
                    .Single(item => item.AttributeId == PlayerAttributeIds.Strength).GrowthUnits,
                Is.EqualTo(250));
            Assert.That(restored.TryApplyReward("claimed-before-save", new RewardBundle(0, 0, 0)), Is.False);
            Assert.That(
                restored.TryApplyLearningOutcome(learningOutcome, "rival-silver-001").Status,
                Is.EqualTo(LearningRewardApplyStatus.DuplicateGrant));
        }

        [Test]
        public void LoadVersionOne_DefaultsLearningFieldsAndNextSaveMigratesToVersionTwo()
        {
            Directory.CreateDirectory(temporaryDirectory);
            File.WriteAllText(
                profilePath,
                "{\"version\":1,\"level\":1,\"experience\":0,\"gold\":4,\"slimeJelly\":2,\"claimedRewardIds\":[]}",
                System.Text.Encoding.UTF8);
            var store = new PlayerProfileStore(profilePath);

            PlayerProgression restored = store.LoadOrCreate(out string loadMessage);

            Assert.That(loadMessage, Does.Contain("読み込み"));
            Assert.That(restored.Gold, Is.EqualTo(4));
            Assert.That(restored.TalentPoints, Is.Zero);
            Assert.That(restored.GetRivalAffinity("rival-silver-001"), Is.Zero);
            Assert.That(restored.IsRivalRecruited("rival-silver-001"), Is.False);
            Assert.That(store.TrySave(restored, out string saveMessage), Is.True, saveMessage);
            Assert.That(File.ReadAllText(profilePath), Does.Contain("\"version\": 2"));
        }

        [Test]
        public void LoadOrCreate_PreservesInvalidFileAndReturnsFreshProfile()
        {
            Directory.CreateDirectory(temporaryDirectory);
            File.WriteAllText(profilePath, "{ definitely-not-json", System.Text.Encoding.UTF8);
            var store = new PlayerProfileStore(profilePath);

            PlayerProgression restored = store.LoadOrCreate(out string message);

            Assert.That(restored.Level, Is.EqualTo(1));
            Assert.That(message, Does.Contain("退避"));
            Assert.That(File.Exists(profilePath), Is.False);
            Assert.That(Directory.GetFiles(temporaryDirectory, "profile.json.invalid-*").Length, Is.EqualTo(1));
        }

        [Test]
        public void PortableExportAndClipboardImport_RoundTripsGold()
        {
            var source = new PlayerProfileStore(profilePath);
            var progression = new PlayerProgression(3, 8, 12, 4);
            Assert.That(source.TrySave(progression, out _), Is.True);
            string json = File.ReadAllText(profilePath);
            UnityEngine.GUIUtility.systemCopyBuffer = json;

            var destinationPath = Path.Combine(temporaryDirectory, "imported.json");
            var destination = new PlayerProfileStore(destinationPath);
            Assert.That(
                PlayerProfilePortability.TryImport(destination, out PlayerProgression imported, out string message),
                Is.True,
                message);
            Assert.That(imported.Gold, Is.EqualTo(12));
            Assert.That(imported.Level, Is.EqualTo(3));
        }
    }
}
