using System;
using System.Collections.Generic;
using System.Linq;

namespace CoffeeGame.Domain
{
    public sealed class PlayerGrowthRule
    {
        public PlayerGrowthRule(string attributeId, int growthUnitsPerLevel)
        {
            AttributeId = PlayerAttributeSet.RequireId(attributeId, nameof(attributeId));
            if (growthUnitsPerLevel < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(growthUnitsPerLevel));
            }

            GrowthUnitsPerLevel = growthUnitsPerLevel;
        }

        public string AttributeId { get; }
        public int GrowthUnitsPerLevel { get; }
    }

    public sealed class TalentGrowthProfile
    {
        public const int GrowthUnitsPerPoint = 1000;

        public TalentGrowthProfile(string talentId, IEnumerable<PlayerGrowthRule> rules)
        {
            TalentId = RequireId(talentId, nameof(talentId));
            var uniqueRules = new Dictionary<string, PlayerGrowthRule>(StringComparer.Ordinal);
            foreach (PlayerGrowthRule rule in rules ?? throw new ArgumentNullException(nameof(rules)))
            {
                if (rule == null || uniqueRules.ContainsKey(rule.AttributeId))
                {
                    throw new ArgumentException("Growth rules must be non-null and unique by attribute ID.", nameof(rules));
                }

                uniqueRules.Add(rule.AttributeId, rule);
            }

            Rules = uniqueRules.Values.OrderBy(rule => rule.AttributeId, StringComparer.Ordinal).ToList().AsReadOnly();
        }

        public string TalentId { get; }
        public IReadOnlyList<PlayerGrowthRule> Rules { get; }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A talent ID cannot be empty.", parameterName);
            }

            return value.Trim();
        }
    }

    public static class TalentGrowthCatalog
    {
        public const string NoneTalentId = "none";

        private static readonly TalentGrowthProfile NoneProfile = new TalentGrowthProfile(
            NoneTalentId,
            PlayerAttributeCatalog.Definitions.Select(definition =>
                new PlayerGrowthRule(definition.Id, TalentGrowthProfile.GrowthUnitsPerPoint)));

        public static TalentGrowthProfile Resolve(string talentId)
        {
            // Unknown talents deliberately fall back to neutral growth. Adding a
            // talent later only requires another profile; status/save shape stays unchanged.
            return NoneProfile;
        }
    }

    public sealed class PlayerGrowthRemainder
    {
        public PlayerGrowthRemainder(string attributeId, int growthUnits)
        {
            AttributeId = PlayerAttributeSet.RequireId(attributeId, nameof(attributeId));
            if (growthUnits < 0 || growthUnits >= TalentGrowthProfile.GrowthUnitsPerPoint)
            {
                throw new ArgumentOutOfRangeException(nameof(growthUnits));
            }

            GrowthUnits = growthUnits;
        }

        public string AttributeId { get; }
        public int GrowthUnits { get; }
    }
}
