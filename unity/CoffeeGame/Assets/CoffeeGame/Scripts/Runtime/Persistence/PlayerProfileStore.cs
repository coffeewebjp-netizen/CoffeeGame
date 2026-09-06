using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CoffeeGame.Domain;
using UnityEngine;

namespace CoffeeGame.Persistence
{
    /// <summary>
    /// Versioned local persistence for shared progression and per-member party state.
    /// Input bindings intentionally remain in GameInputReader's PlayerPrefs storage.
    /// </summary>
    public sealed class PlayerProfileStore
    {
        public const int CurrentVersion = 3;
        public const int PreviousCompatibleVersion = 2;

        private readonly string profilePath;
        private readonly Func<DateTime> utcNowProvider;
        private bool unsupportedVersionBlocksWrites;

        public PlayerProfileStore(string path = null, Func<DateTime> utcNowProvider = null)
        {
            profilePath = string.IsNullOrWhiteSpace(path)
                ? Path.Combine(Application.persistentDataPath, "CoffeeGAME", "player-profile.json")
                : Path.GetFullPath(path);
            this.utcNowProvider = utcNowProvider ?? (() => DateTime.UtcNow);
        }

        public string ProfilePath => profilePath;
        public string BackupPath => profilePath + ".bak";
        public bool HasUnsupportedVersion => unsupportedVersionBlocksWrites;
        public int UnsupportedVersion { get; private set; }

        public PlayerProgression LoadOrCreate(out string message)
        {
            unsupportedVersionBlocksWrites = false;
            UnsupportedVersion = 0;
            TryRestoreMissingPrimaryFromBackup();
            if (!File.Exists(profilePath))
            {
                message = "新しいプレイヤープロフィールを作成しました。";
                return new PlayerProgression();
            }

            try
            {
                return LoadSupportedProfile(profilePath, out message);
            }
            catch (UnsupportedProfileVersionException exception)
            {
                unsupportedVersionBlocksWrites = true;
                UnsupportedVersion = exception.Version;
                message = $"新しいバージョン({exception.Version})のプロフィールを変更せず保持しました。この版からは保存できません。";
                return new PlayerProgression();
            }
            catch (Exception exception)
            {
                string preservedPath = PreserveInvalidProfile();
                if (TryRecoverBackup(out PlayerProgression recovered, out string recoveryMessage))
                {
                    message = recoveryMessage;
                    return recovered;
                }

                message = string.IsNullOrEmpty(preservedPath)
                    ? $"プロフィールを読み込めなかったため初期化しました: {exception.Message}"
                    : $"破損したプロフィールを {Path.GetFileName(preservedPath)} に退避して初期化しました。";
                return new PlayerProgression();
            }
        }

        public string DescribeSavedFile(string prefix = null)
        {
            if (!File.Exists(profilePath))
            {
                return (prefix ?? "セーブ") + " ファイルはまだありません: " + profilePath;
            }

            var info = new FileInfo(profilePath);
            return (prefix ?? "セーブ")
                + "\n場所: " + info.FullName
                + "\n日時: " + info.LastWriteTime.ToString("yyyy/MM/dd HH:mm:ss")
                + "\nサイズ: " + info.Length.ToString("N0") + " bytes";
        }

        public bool TrySave(PlayerProgression progression, out string message)
        {
            if (progression == null)
            {
                throw new ArgumentNullException(nameof(progression));
            }
            if (unsupportedVersionBlocksWrites)
            {
                message = $"version {UnsupportedVersion} のプロフィールを上書きしないため保存を中止しました。";
                return false;
            }

            return TryWriteFile(CreateFile(progression, CurrentVersion), out message);
        }

        /// <summary>
        /// Explicit rollback seam. v2 cannot represent party resources or recovery clocks;
        /// callers must retain the v3 source when exporting this projection.
        /// </summary>
        public bool TryExportVersion2Json(
            PlayerProgression progression,
            out string json,
            out string message)
        {
            if (progression == null)
            {
                throw new ArgumentNullException(nameof(progression));
            }

            try
            {
                json = JsonUtility.ToJson(CreateVersion2File(progression), true);
                message = "v2互換JSONを作成しました。仲間固有の資源と回復時計はv3にのみ保持されます。";
                return true;
            }
            catch (Exception exception)
            {
                json = string.Empty;
                message = "v2互換JSONを作成できませんでした: " + exception.Message;
                return false;
            }
        }

        private PlayerProgression LoadSupportedProfile(
            string path,
            out string message,
            bool persistSettlement = true)
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            ProfileFile data = JsonUtility.FromJson<ProfileFile>(json);
            if (data == null)
            {
                throw new InvalidDataException("The player profile is empty.");
            }
            if (data.version < 1 || data.version > CurrentVersion)
            {
                throw new UnsupportedProfileVersionException(data.version);
            }

            PlayerStatus legacyStatus = RestoreStatus(data.status);
            PlayerParty party = data.version >= 3
                ? RestoreParty(data)
                : CreateUninitializedLegacyParty(data.level, data.experience, legacyStatus);
            PartyMember hero = party.Find(PartyMemberIds.Hero);
            if (hero == null)
            {
                throw new InvalidDataException("The v3 party does not contain the stable hero member.");
            }

            var progression = new PlayerProgression(
                hero.Level,
                hero.Experience,
                data.gold,
                data.slimeJelly,
                data.claimedRewardIds,
                hero.Status,
                talentPoints: data.version >= 2 ? data.talentPoints : 0,
                rivalAffinities: data.version >= 2 ? RestoreRivalAffinities(data.rivalAffinities) : null,
                previouslyRecruitedRivalIds: data.version >= 2 ? data.recruitedRivalIds : null,
                party: party);

            bool settled = data.version >= 3 && progression.Party.Settle(NormalizeUtc(utcNowProvider()));
            message = data.version == CurrentVersion
                ? "プレイヤープロフィールを読み込みました。"
                : $"version {data.version} のプロフィールを読み込みました。次回保存時にversion 3へ移行します。";
            if (settled && persistSettlement && !TrySave(progression, out string saveMessage))
            {
                message += " 経過時間の保存に失敗しました: " + saveMessage;
            }

            return progression;
        }

        private bool TryWriteFile(ProfileFile data, out string message)
        {
            string directory = Path.GetDirectoryName(profilePath);
            string temporaryPath = profilePath + ".tmp";
            try
            {
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonUtility.ToJson(data, true);
                byte[] bytes = new UTF8Encoding(false).GetBytes(json);
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                if (File.Exists(profilePath))
                {
                    File.Replace(temporaryPath, profilePath, BackupPath, true);
                }
                else
                {
                    File.Move(temporaryPath, profilePath);
                }

                message = DescribeSavedFile("プレイヤープロフィールを保存しました。");
                return true;
            }
            catch (Exception exception)
            {
                TryDeleteTemporaryFile(temporaryPath);
                message = $"プレイヤープロフィールを保存できませんでした: {exception.Message}";
                return false;
            }
        }

        private static ProfileFile CreateFile(PlayerProgression progression, int version)
        {
            var file = new ProfileFile
            {
                version = version,
                level = progression.Level,
                experience = progression.Experience,
                gold = progression.Gold,
                slimeJelly = progression.SlimeJelly,
                talentPoints = progression.TalentPoints,
                claimedRewardIds = new List<string>(progression.CreateClaimedRewardSnapshot()),
                rivalAffinities = new List<RivalAffinityFile>(),
                recruitedRivalIds = new List<string>(progression.CreateRecruitedRivalSnapshot()),
                status = CreateStatusFile(progression.Status)
            };

            foreach (RivalAffinityEntry entry in progression.CreateRivalAffinitySnapshot())
            {
                file.rivalAffinities.Add(new RivalAffinityFile { rivalId = entry.RivalId, affinity = entry.Affinity });
            }

            if (version >= 3)
            {
                file.preferredControlledCharacterId = progression.Party.PreferredControlledMemberId;
                foreach (PartyMember member in progression.Party.Members)
                {
                    file.partyMemberIds.Add(member.Id);
                    file.members.Add(new PartyMemberFile
                    {
                        characterId = member.Id,
                        level = member.Level,
                        experience = member.Experience,
                        status = CreateStatusFile(member.Status),
                        health = member.Resources.HitPoints,
                        maximumHealth = member.Resources.MaximumHitPoints,
                        magicPoints = member.Resources.MagicPoints,
                        maximumMagicPoints = member.Resources.MaximumMagicPoints,
                        specialGauge = member.Resources.Special,
                        maximumSpecialGauge = member.Resources.MaximumSpecial,
                        resourcesInitialized = member.ResourcesInitialized,
                        recoveryState = (int)member.RecoveryState,
                        recoveryAnchorUtcTicks = member.RecoveryAnchorUtc.Ticks,
                        recoverableAtUtcTicks = member.RecoverableAtUtc?.Ticks ?? 0L
                    });
                }
            }

            return file;
        }

        private static Version2ProfileFile CreateVersion2File(PlayerProgression progression)
        {
            var file = new Version2ProfileFile
            {
                version = PreviousCompatibleVersion,
                level = progression.Level,
                experience = progression.Experience,
                gold = progression.Gold,
                slimeJelly = progression.SlimeJelly,
                talentPoints = progression.TalentPoints,
                claimedRewardIds = new List<string>(progression.CreateClaimedRewardSnapshot()),
                recruitedRivalIds = new List<string>(progression.CreateRecruitedRivalSnapshot()),
                status = CreateStatusFile(progression.Status)
            };
            foreach (RivalAffinityEntry entry in progression.CreateRivalAffinitySnapshot())
            {
                file.rivalAffinities.Add(new RivalAffinityFile { rivalId = entry.RivalId, affinity = entry.Affinity });
            }
            return file;
        }

        private static StatusFile CreateStatusFile(PlayerStatus status)
        {
            var file = new StatusFile
            {
                archetypeId = status.ArchetypeId,
                className = status.ClassName,
                talentId = status.TalentId,
                talentName = status.Talent
            };
            foreach (PlayerAttributeValue attribute in status.Attributes.CreateSnapshot())
            {
                file.attributes.Add(new AttributeFile { id = attribute.Id, value = attribute.Value });
            }
            foreach (PlayerGrowthRemainder remainder in status.CreateGrowthRemainderSnapshot())
            {
                file.growthRemainders.Add(
                    new GrowthRemainderFile { attributeId = remainder.AttributeId, growthUnits = remainder.GrowthUnits });
            }
            return file;
        }

        private PlayerParty RestoreParty(ProfileFile data)
        {
            if (data.members == null)
            {
                throw new InvalidDataException("The v3 party member list is missing.");
            }

            var members = new List<PartyMember>();
            bool catRecruited = data.recruitedRivalIds != null
                && data.recruitedRivalIds.Contains(RivalCharacterIds.WeaknessChallenger);
            foreach (PartyMemberFile member in data.members)
            {
                if (member == null || string.IsNullOrWhiteSpace(member.characterId))
                {
                    throw new InvalidDataException("The v3 party contains an invalid member.");
                }
                if (member.characterId == PartyMemberIds.CatMage && !catRecruited)
                {
                    continue;
                }

                members.Add(new PartyMember(
                    member.characterId,
                    member.level,
                    member.experience,
                    RestoreStatus(member.status),
                    member.health,
                    member.maximumHealth,
                    member.magicPoints,
                    member.maximumMagicPoints,
                    member.specialGauge,
                    member.maximumSpecialGauge,
                    (PartyRecoveryState)member.recoveryState,
                    RestoreUtc(member.recoveryAnchorUtcTicks),
                    member.recoverableAtUtcTicks > 0L ? RestoreUtc(member.recoverableAtUtcTicks) : (DateTime?)null,
                    resourcesInitialized: member.resourcesInitialized));
            }

            return new PlayerParty(members, data.preferredControlledCharacterId);
        }

        private PlayerParty CreateUninitializedLegacyParty(int level, int experience, PlayerStatus status)
        {
            return new PlayerParty(new[]
            {
                new PartyMember(
                    PartyMemberIds.Hero,
                    level,
                    experience,
                    status,
                    recoveryState: PartyRecoveryState.Resting,
                    recoveryAnchorUtc: NormalizeUtc(utcNowProvider()),
                    resourcesInitialized: false)
            });
        }

        private static IEnumerable<RivalAffinityEntry> RestoreRivalAffinities(IEnumerable<RivalAffinityFile> entries)
        {
            if (entries == null)
            {
                yield break;
            }
            foreach (RivalAffinityFile entry in entries)
            {
                if (entry != null)
                {
                    yield return new RivalAffinityEntry(entry.rivalId, entry.affinity);
                }
            }
        }

        private static PlayerStatus RestoreStatus(StatusFile data)
        {
            if (data == null || (string.IsNullOrWhiteSpace(data.archetypeId)
                && string.IsNullOrWhiteSpace(data.className)
                && string.IsNullOrWhiteSpace(data.talentId)
                && string.IsNullOrWhiteSpace(data.talentName)
                && (data.attributes == null || data.attributes.Count == 0)
                && (data.growthRemainders == null || data.growthRemainders.Count == 0)))
            {
                return new PlayerStatus();
            }

            var attributes = new List<PlayerAttributeValue>();
            if (data.attributes != null)
            {
                foreach (AttributeFile attribute in data.attributes)
                {
                    if (attribute != null)
                    {
                        attributes.Add(new PlayerAttributeValue(attribute.id, attribute.value));
                    }
                }
            }

            var growthRemainders = new List<PlayerGrowthRemainder>();
            if (data.growthRemainders != null)
            {
                foreach (GrowthRemainderFile remainder in data.growthRemainders)
                {
                    if (remainder != null)
                    {
                        growthRemainders.Add(new PlayerGrowthRemainder(remainder.attributeId, remainder.growthUnits));
                    }
                }
            }

            return new PlayerStatus(
                data.archetypeId,
                data.className,
                data.talentId,
                data.talentName,
                attributes,
                growthRemainders);
        }

        private void TryRestoreMissingPrimaryFromBackup()
        {
            if (File.Exists(profilePath) || !File.Exists(BackupPath))
            {
                return;
            }
            try
            {
                string directory = Path.GetDirectoryName(profilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.Copy(BackupPath, profilePath, false);
            }
            catch
            {
                // Normal load reporting handles an unavailable backup.
            }
        }

        private bool TryRecoverBackup(out PlayerProgression progression, out string message)
        {
            progression = null;
            message = string.Empty;
            if (!File.Exists(BackupPath))
            {
                return false;
            }

            try
            {
                progression = LoadSupportedProfile(BackupPath, out _, false);
                File.Copy(BackupPath, profilePath, true);
                TrySave(progression, out _);
                message = "バックアップからプレイヤープロフィールを復旧しました。";
                return true;
            }
            catch
            {
                progression = null;
                return false;
            }
        }

        private string PreserveInvalidProfile()
        {
            if (!File.Exists(profilePath))
            {
                return null;
            }
            try
            {
                string timestamp = NormalizeUtc(utcNowProvider()).ToString("yyyyMMdd-HHmmss-fff");
                string invalidPath = profilePath + $".invalid-{timestamp}";
                File.Move(profilePath, invalidPath);
                return invalidPath;
            }
            catch
            {
                return null;
            }
        }

        private static DateTime RestoreUtc(long ticks)
        {
            if (ticks <= 0L || ticks > DateTime.MaxValue.Ticks)
            {
                throw new InvalidDataException("The recovery UTC timestamp is invalid.");
            }
            return new DateTime(ticks, DateTimeKind.Utc);
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc)
            {
                return value;
            }
            return value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        private static void TryDeleteTemporaryFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // The original save failure remains the useful diagnostic.
            }
        }

        private sealed class UnsupportedProfileVersionException : Exception
        {
            public UnsupportedProfileVersionException(int version)
                : base("Unsupported player profile version: " + version)
            {
                Version = version;
            }

            public int Version { get; }
        }

        [Serializable]
        private sealed class ProfileFile
        {
            public int version;
            public int level;
            public int experience;
            public int gold;
            public int slimeJelly;
            public int talentPoints;
            public StatusFile status;
            public List<string> claimedRewardIds = new List<string>();
            public List<RivalAffinityFile> rivalAffinities = new List<RivalAffinityFile>();
            public List<string> recruitedRivalIds = new List<string>();
            public List<string> partyMemberIds = new List<string>();
            public List<PartyMemberFile> members = new List<PartyMemberFile>();
            public string preferredControlledCharacterId;
        }

        [Serializable]
        private sealed class Version2ProfileFile
        {
            public int version;
            public int level;
            public int experience;
            public int gold;
            public int slimeJelly;
            public int talentPoints;
            public StatusFile status;
            public List<string> claimedRewardIds = new List<string>();
            public List<RivalAffinityFile> rivalAffinities = new List<RivalAffinityFile>();
            public List<string> recruitedRivalIds = new List<string>();
        }

        [Serializable]
        private sealed class PartyMemberFile
        {
            public string characterId;
            public int level;
            public int experience;
            public StatusFile status;
            public double health;
            public double maximumHealth;
            public double magicPoints;
            public double maximumMagicPoints;
            public double specialGauge;
            public double maximumSpecialGauge;
            public bool resourcesInitialized;
            public int recoveryState;
            public long recoveryAnchorUtcTicks;
            public long recoverableAtUtcTicks;
        }

        [Serializable]
        private sealed class RivalAffinityFile
        {
            public string rivalId;
            public int affinity;
        }

        [Serializable]
        private sealed class StatusFile
        {
            public string archetypeId;
            public string className;
            public string talentId;
            public string talentName;
            public List<AttributeFile> attributes = new List<AttributeFile>();
            public List<GrowthRemainderFile> growthRemainders = new List<GrowthRemainderFile>();
        }

        [Serializable]
        private sealed class AttributeFile
        {
            public string id;
            public int value;
        }

        [Serializable]
        private sealed class GrowthRemainderFile
        {
            public string attributeId;
            public int growthUnits;
        }
    }
}
