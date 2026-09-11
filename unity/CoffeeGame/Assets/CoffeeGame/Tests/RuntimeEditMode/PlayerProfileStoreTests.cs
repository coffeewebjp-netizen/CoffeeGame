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

        [Test] public void DragonPartySurvivesSaveWithoutDuplicatingExistingMembers()
        {
            var p=new PlayerProgression(1,0,0,0,previouslyRecruitedRivalIds:new[]{RivalCharacterIds.WeaknessChallenger,RivalCharacterIds.SplitInk});
            var member=p.Party.Find(PartyMemberIds.DragonGirl);
            member.SetMaximumResources(24,20,100,DateTime.UtcNow);
            member.SetResources(7,3,12,DateTime.UtcNow);
            var store=new PlayerProfileStore(profilePath);
            Assert.That(store.TrySave(p,out string message),Is.True,message);
            var restored=store.LoadOrCreate(out _);
            Assert.That(restored.Party.Members.Count,Is.EqualTo(3));
            Assert.That(restored.Party.Find(PartyMemberIds.DragonGirl).Resources.HitPoints,Is.EqualTo(7).Within(.01));
            Assert.That(restored.Party.Find(PartyMemberIds.DragonGirl).Resources.MagicPoints,Is.EqualTo(3).Within(.01));
        }

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
        public void LoadVersionOne_DefaultsLearningFieldsAndNextSaveMigratesToVersionThree()
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
            Assert.That(File.ReadAllText(profilePath), Does.Contain("\"version\": 3"));
            Assert.That(restored.Party.Find(PartyMemberIds.Hero).ResourcesInitialized, Is.False);
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
            var progression = new PlayerProgression(3, 6, 12, 4);
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

        [Test]
        public void VersionTwoMigration_RestoresRecruitedCatWithUninitializedRuntimeResources()
        {
            Directory.CreateDirectory(temporaryDirectory);
            File.WriteAllText(
                profilePath,
                "{\"version\":2,\"level\":4,\"experience\":2,\"gold\":9,\"slimeJelly\":3,"
                + "\"talentPoints\":5,\"claimedRewardIds\":[\"claim-a\"],"
                + "\"rivalAffinities\":[{\"rivalId\":\"rival-silver-001\",\"affinity\":100}],"
                + "\"recruitedRivalIds\":[\"rival-silver-001\"],"
                + "\"status\":{\"archetypeId\":\"swordsman\",\"className\":\"名もなき剣士\","
                + "\"talentId\":\"none\",\"talentName\":\"なし\",\"attributes\":[],\"growthRemainders\":[]}}",
                System.Text.Encoding.UTF8);
            var now = new DateTime(2026, 9, 6, 1, 0, 0, DateTimeKind.Utc);
            var store = new PlayerProfileStore(profilePath, () => now);

            PlayerProgression restored = store.LoadOrCreate(out string message);

            Assert.That(message, Does.Contain("version 2"));
            Assert.That(restored.Party.Members.Count, Is.EqualTo(2));
            Assert.That(restored.Party.Find(PartyMemberIds.Hero).ResourcesInitialized, Is.False);
            Assert.That(restored.Party.Find(PartyMemberIds.CatMage).ResourcesInitialized, Is.False);
            Assert.That(restored.TryApplyReward("claim-a", new RewardBundle(99, 99, 99)), Is.False);
        }

        [Test]
        public void VersionThree_RoundTripsIndependentMemberStateAndOfflineSettlementOnce()
        {
            var anchor = new DateTime(2030, 9, 6, 2, 0, 0, DateTimeKind.Utc);
            var progression = RecruitedProgression();
            PartyMember hero = progression.Party.Find(PartyMemberIds.Hero);
            PartyMember cat = progression.Party.Find(PartyMemberIds.CatMage);
            hero.SetMaximumResources(100d, 40d, 100d, anchor);
            cat.SetMaximumResources(80d, 150d, 100d, anchor);
            hero.SetResources(50d, 10d, 70d, anchor);
            cat.SetResources(20d, 30d, 90d, anchor);
            progression.Party.KnockOut(cat.Id, anchor);
            progression.Party.RestAllLiving(anchor);
            Assert.That(hero.RecoveryState, Is.EqualTo(PartyRecoveryState.Resting));

            var saveStore = new PlayerProfileStore(profilePath, () => anchor);
            Assert.That(saveStore.TrySave(progression, out string saveMessage), Is.True, saveMessage);

            DateTime reloadAt = anchor.AddSeconds(600d);
            var firstLoad = new PlayerProfileStore(profilePath, () => reloadAt);
            PlayerProgression first = firstLoad.LoadOrCreate(out string firstMessage);
            PartyMember firstHero = first.Party.Find(PartyMemberIds.Hero);
            PartyMember firstCat = first.Party.Find(PartyMemberIds.CatMage);

            Assert.That(firstMessage, Does.Contain("読み込み"));
            Assert.That(firstHero.RecoveryState, Is.EqualTo(PartyRecoveryState.Resting));
            Assert.That(firstHero.Resources.HitPoints, Is.EqualTo(100d));
            Assert.That(firstHero.Resources.MagicPoints, Is.EqualTo(110d / 3d).Within(0.0000001d));
            Assert.That(firstHero.Resources.Special, Is.EqualTo(70d));
            Assert.That(firstCat.RecoveryState, Is.EqualTo(PartyRecoveryState.Resting));
            Assert.That(firstCat.Resources.HitPoints, Is.EqualTo(1d));
            Assert.That(firstCat.Resources.MagicPoints, Is.EqualTo(30d));
            Assert.That(firstCat.Resources.Special, Is.Zero);

            var secondLoad = new PlayerProfileStore(profilePath, () => reloadAt);
            PlayerProgression second = secondLoad.LoadOrCreate(out _);
            PartyMember secondCat = second.Party.Find(PartyMemberIds.CatMage);
            Assert.That(secondCat.Resources.HitPoints, Is.EqualTo(1d));
            Assert.That(secondCat.Resources.MagicPoints, Is.EqualTo(30d));
        }

        [Test]
        public void UnknownVersion_RemainsByteForByteAndBlocksOverwrite()
        {
            Directory.CreateDirectory(temporaryDirectory);
            const string futureJson = "{\"version\":99,\"gold\":123,\"future\":{\"opaque\":true}}";
            File.WriteAllText(profilePath, futureJson, System.Text.Encoding.UTF8);
            var store = new PlayerProfileStore(profilePath);

            PlayerProgression fallback = store.LoadOrCreate(out string message);

            Assert.That(fallback.Level, Is.EqualTo(1));
            Assert.That(store.HasUnsupportedVersion, Is.True);
            Assert.That(store.UnsupportedVersion, Is.EqualTo(99));
            Assert.That(message, Does.Contain("変更せず保持"));
            Assert.That(store.TrySave(fallback, out string saveMessage), Is.False);
            Assert.That(saveMessage, Does.Contain("上書きしない"));
            Assert.That(File.ReadAllText(profilePath), Is.EqualTo(futureJson));
            Assert.That(Directory.GetFiles(temporaryDirectory, "profile.json.invalid-*"), Is.Empty);
        }

        [Test]
        public void VersionTwoExport_PreservesSharedCanonicalProgressAndOmitsPartyFields()
        {
            var progression = RecruitedProgression();
            progression.TryApplyReward("shared", new RewardBundle(2, 4, 3));
            var store = new PlayerProfileStore(profilePath);

            Assert.That(
                store.TryExportVersion2Json(progression, out string json, out string message),
                Is.True,
                message);

            Assert.That(json, Does.Contain("\"version\": 2"));
            Assert.That(json, Does.Contain("\"gold\": 9"));
            Assert.That(json, Does.Contain("\"claimedRewardIds\""));
            Assert.That(json, Does.Not.Contain("\"members\""));
            Assert.That(json, Does.Not.Contain("\"recoveryState\""));
        }

        [Test]
        public void AtomicReplacement_KeepsPreviousProfileAsRecoverableBackup()
        {
            var store = new PlayerProfileStore(profilePath);
            Assert.That(store.TrySave(new PlayerProgression(1, 0, 4, 0), out _), Is.True);
            Assert.That(store.TrySave(new PlayerProgression(1, 0, 9, 0), out _), Is.True);
            Assert.That(File.Exists(store.BackupPath), Is.True);

            File.WriteAllText(profilePath, "{broken", System.Text.Encoding.UTF8);
            PlayerProgression recovered = store.LoadOrCreate(out string message);

            Assert.That(message, Does.Contain("バックアップ"));
            Assert.That(recovered.Gold, Is.EqualTo(4));
            Assert.That(File.Exists(profilePath), Is.True);
        }

        private static PlayerProgression RecruitedProgression()
        {
            return new PlayerProgression(
                2,
                1,
                5,
                0,
                rivalAffinities: new[] { new RivalAffinityEntry(RivalCharacterIds.WeaknessChallenger, 100) },
                previouslyRecruitedRivalIds: new[] { RivalCharacterIds.WeaknessChallenger });
        }
    }
}
