using System;
using System.Collections.Generic;
using System.Linq;

namespace CoffeeGame.Domain
{
    /// <summary>
    /// Persistent, presentation-independent identity and base attributes for the player.
    /// Attributes and growth remainders use stable IDs so new parameters do not require
    /// a save-shape migration. Combat formulas remain in PlayerDerivedStatCalculator.
    /// </summary>
    public sealed class PlayerStatus
    {
        public const string DefaultArchetypeId = "swordsman";
        public const string DefaultClassName = "名もなき剣士";
        public const string DefaultTalentName = "なし";

        private readonly Dictionary<string, int> growthRemainders;

        public PlayerStatus()
            : this(
                DefaultArchetypeId,
                DefaultClassName,
                TalentGrowthCatalog.NoneTalentId,
                DefaultTalentName,
                null,
                null)
        {
        }

        public PlayerStatus(
            string className,
            int strength,
            int agility,
            int technique,
            int luck,
            int vitality,
            string talent)
            : this(
                DefaultArchetypeId,
                className,
                TalentGrowthCatalog.NoneTalentId,
                talent,
                new[]
                {
                    new PlayerAttributeValue(PlayerAttributeIds.Strength, strength),
                    new PlayerAttributeValue(PlayerAttributeIds.Agility, agility),
                    new PlayerAttributeValue(PlayerAttributeIds.Technique, technique),
                    new PlayerAttributeValue(PlayerAttributeIds.Luck, luck),
                    new PlayerAttributeValue(PlayerAttributeIds.Vitality, vitality)
                },
                null)
        {
        }

        public PlayerStatus(
            string archetypeId,
            string className,
            string talentId,
            string talentName,
            IEnumerable<PlayerAttributeValue> attributes,
            IEnumerable<PlayerGrowthRemainder> restoredGrowthRemainders)
        {
            ArchetypeId = RequireText(archetypeId, nameof(archetypeId));
            ClassName = RequireText(className, nameof(className));
            TalentId = RequireText(talentId, nameof(talentId));
            Talent = RequireText(talentName, nameof(talentName));
            Attributes = new PlayerAttributeSet(attributes);
            growthRemainders = new Dictionary<string, int>(StringComparer.Ordinal);
            if (restoredGrowthRemainders != null)
            {
                foreach (PlayerGrowthRemainder remainder in restoredGrowthRemainders)
                {
                    if (remainder != null)
                    {
                        growthRemainders[remainder.AttributeId] = remainder.GrowthUnits;
                    }
                }
            }
        }

        public string ArchetypeId { get; }
        public string ClassName { get; }
        public string TalentId { get; }
        public string Talent { get; }
        public PlayerAttributeSet Attributes { get; }
        public int Strength => Attributes.GetValue(PlayerAttributeIds.Strength);
        public int Agility => Attributes.GetValue(PlayerAttributeIds.Agility);
        public int Technique => Attributes.GetValue(PlayerAttributeIds.Technique);
        public int Luck => Attributes.GetValue(PlayerAttributeIds.Luck);
        public int Vitality => Attributes.GetValue(PlayerAttributeIds.Vitality);

        public PlayerStatus ApplyLevelGrowth(int levelsGained, TalentGrowthProfile profile)
        {
            if (levelsGained < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(levelsGained));
            }
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }
            if (levelsGained == 0)
            {
                return this;
            }

            PlayerAttributeSet nextAttributes = new PlayerAttributeSet(Attributes.CreateSnapshot());
            var nextRemainders = new Dictionary<string, int>(growthRemainders, StringComparer.Ordinal);
            foreach (PlayerGrowthRule rule in profile.Rules)
            {
                nextRemainders.TryGetValue(rule.AttributeId, out int previousRemainder);
                long totalGrowthUnits = previousRemainder + checked((long)rule.GrowthUnitsPerLevel * levelsGained);
                int pointIncrease = checked((int)(totalGrowthUnits / TalentGrowthProfile.GrowthUnitsPerPoint));
                int remainder = (int)(totalGrowthUnits % TalentGrowthProfile.GrowthUnitsPerPoint);
                if (pointIncrease > 0)
                {
                    nextAttributes = nextAttributes.WithIncrease(rule.AttributeId, pointIncrease);
                }
                nextRemainders[rule.AttributeId] = remainder;
            }

            return new PlayerStatus(
                ArchetypeId,
                ClassName,
                TalentId,
                Talent,
                nextAttributes.CreateSnapshot(),
                nextRemainders.Select(entry => new PlayerGrowthRemainder(entry.Key, entry.Value)));
        }

        public IReadOnlyList<PlayerGrowthRemainder> CreateGrowthRemainderSnapshot()
        {
            return growthRemainders
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => new PlayerGrowthRemainder(entry.Key, entry.Value))
                .ToList()
                .AsReadOnly();
        }

        private static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A status label or ID cannot be empty.", parameterName);
            }

            return value.Trim();
        }
    }
}
