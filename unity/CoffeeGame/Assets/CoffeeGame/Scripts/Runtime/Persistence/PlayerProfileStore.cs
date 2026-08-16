using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CoffeeGame.Domain;
using UnityEngine;

namespace CoffeeGame.Persistence
{
    /// <summary>
    /// Versioned local persistence for progression and status data. Attribute values
    /// remain keyed records so a future parameter can round-trip through older builds.
    /// Input bindings intentionally remain in GameInputReader's PlayerPrefs storage.
    /// </summary>
    public sealed class PlayerProfileStore
    {
        public const int CurrentVersion = 2;

        private readonly string profilePath;

        public PlayerProfileStore(string path = null)
        {
            profilePath = string.IsNullOrWhiteSpace(path)
                ? Path.Combine(Application.persistentDataPath, "CoffeeGAME", "player-profile.json")
                : Path.GetFullPath(path);
        }

        public string ProfilePath => profilePath;

        public PlayerProgression LoadOrCreate(out string message)
        {
            if (!File.Exists(profilePath))
            {
                message = "新しいプレイヤープロフィールを作成しました。";
                return new PlayerProgression();
            }

            try
            {
                string json = File.ReadAllText(profilePath, Encoding.UTF8);
                ProfileFile data = JsonUtility.FromJson<ProfileFile>(json);
                if (data == null || (data.version != 1 && data.version != CurrentVersion))
                {
                    throw new InvalidDataException($"Unsupported player profile version: {data?.version ?? -1}");
                }

                PlayerStatus status = RestoreStatus(data.status);
                var progression = new PlayerProgression(
                    data.level,
                    data.experience,
                    data.gold,
                    data.slimeJelly,
                    data.claimedRewardIds,
                    status,
                    talentPoints: data.version >= 2 ? data.talentPoints : 0,
                    rivalAffinities: data.version >= 2 ? RestoreRivalAffinities(data.rivalAffinities) : null,
                    previouslyRecruitedRivalIds: data.version >= 2 ? data.recruitedRivalIds : null);
                message = "プレイヤープロフィールを読み込みました。";
                return progression;
            }
            catch (Exception exception)
            {
                string preservedPath = PreserveInvalidProfile();
                message = string.IsNullOrEmpty(preservedPath)
                    ? $"プロフィールを読み込めなかったため初期化しました: {exception.Message}"
                    : $"破損したプロフィールを {Path.GetFileName(preservedPath)} に退避して初期化しました。";
                return new PlayerProgression();
            }
        }

        public bool TrySave(PlayerProgression progression, out string message)
        {
            if (progression == null)
            {
                throw new ArgumentNullException(nameof(progression));
            }

            string directory = Path.GetDirectoryName(profilePath);
            string temporaryPath = profilePath + ".tmp";
            try
            {
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                ProfileFile data = CreateFile(progression);
                string json = JsonUtility.ToJson(data, true);
                using (var stream = new FileStream(
                           temporaryPath,
                           FileMode.Create,
                           FileAccess.Write,
                           FileShare.None,
                           4096,
                           FileOptions.WriteThrough))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush(true);
                }

                if (File.Exists(profilePath))
                {
                    File.Replace(temporaryPath, profilePath, null);
                }
                else
                {
                    File.Move(temporaryPath, profilePath);
                }

                message = "プレイヤープロフィールを保存しました。";
                return true;
            }
            catch (Exception exception)
            {
                TryDeleteTemporaryFile(temporaryPath);
                message = $"プレイヤープロフィールを保存できませんでした: {exception.Message}";
                return false;
            }
        }

        private static ProfileFile CreateFile(PlayerProgression progression)
        {
            var file = new ProfileFile
            {
                version = CurrentVersion,
                level = progression.Level,
                experience = progression.Experience,
                gold = progression.Gold,
                slimeJelly = progression.SlimeJelly,
                talentPoints = progression.TalentPoints,
                claimedRewardIds = new List<string>(progression.CreateClaimedRewardSnapshot()),
                rivalAffinities = new List<RivalAffinityFile>(),
                recruitedRivalIds = new List<string>(progression.CreateRecruitedRivalSnapshot()),
                status = new StatusFile
                {
                    archetypeId = progression.Status.ArchetypeId,
                    className = progression.Status.ClassName,
                    talentId = progression.Status.TalentId,
                    talentName = progression.Status.Talent,
                    attributes = new List<AttributeFile>(),
                    growthRemainders = new List<GrowthRemainderFile>()
                }
            };

            foreach (PlayerAttributeValue attribute in progression.Status.Attributes.CreateSnapshot())
            {
                file.status.attributes.Add(new AttributeFile { id = attribute.Id, value = attribute.Value });
            }

            foreach (PlayerGrowthRemainder remainder in progression.Status.CreateGrowthRemainderSnapshot())
            {
                file.status.growthRemainders.Add(
                    new GrowthRemainderFile { attributeId = remainder.AttributeId, growthUnits = remainder.GrowthUnits });
            }

            foreach (RivalAffinityEntry entry in progression.CreateRivalAffinitySnapshot())
            {
                file.rivalAffinities.Add(
                    new RivalAffinityFile { rivalId = entry.RivalId, affinity = entry.Affinity });
            }

            return file;
        }

        private static IEnumerable<RivalAffinityEntry> RestoreRivalAffinities(
            IEnumerable<RivalAffinityFile> entries)
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
            if (data == null)
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
                        growthRemainders.Add(
                            new PlayerGrowthRemainder(remainder.attributeId, remainder.growthUnits));
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

        private string PreserveInvalidProfile()
        {
            if (!File.Exists(profilePath))
            {
                return null;
            }

            try
            {
                string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
                string invalidPath = profilePath + $".invalid-{timestamp}";
                File.Move(profilePath, invalidPath);
                return invalidPath;
            }
            catch
            {
                return null;
            }
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
