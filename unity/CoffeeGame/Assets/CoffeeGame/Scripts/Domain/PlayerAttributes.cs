using System;
using System.Collections.Generic;
using System.Linq;

namespace CoffeeGame.Domain
{
    public static class PlayerAttributeIds
    {
        public const string Strength = "strength";
        public const string Agility = "agility";
        public const string Technique = "technique";
        public const string Luck = "luck";
        public const string Vitality = "vitality";
    }

    public sealed class PlayerAttributeDefinition
    {
        public PlayerAttributeDefinition(
            string id,
            string displayName,
            string effectDescription,
            int defaultValue,
            int displayOrder)
        {
            Id = PlayerAttributeSet.RequireId(id, nameof(id));
            DisplayName = RequireText(displayName, nameof(displayName));
            EffectDescription = RequireText(effectDescription, nameof(effectDescription));
            DefaultValue = PlayerAttributeSet.RequireValue(defaultValue, nameof(defaultValue));
            DisplayOrder = displayOrder;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string EffectDescription { get; }
        public int DefaultValue { get; }
        public int DisplayOrder { get; }

        private static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("An attribute label cannot be empty.", parameterName);
            }

            return value.Trim();
        }
    }

    public static class PlayerAttributeCatalog
    {
        private static readonly IReadOnlyList<PlayerAttributeDefinition> DefinitionsValue =
            new List<PlayerAttributeDefinition>
            {
                new PlayerAttributeDefinition(PlayerAttributeIds.Strength, "力", "攻撃力", 10, 10),
                new PlayerAttributeDefinition(PlayerAttributeIds.Agility, "素早さ", "回避率・移動速度", 10, 20),
                new PlayerAttributeDefinition(PlayerAttributeIds.Technique, "技", "クリティカル率・必殺技速度", 10, 30),
                new PlayerAttributeDefinition(PlayerAttributeIds.Luck, "運", "クリティカル率・回避率", 10, 40),
                new PlayerAttributeDefinition(PlayerAttributeIds.Vitality, "体力", "スタミナ・防御力", 10, 50)
            }.AsReadOnly();

        private static readonly IReadOnlyDictionary<string, PlayerAttributeDefinition> DefinitionsById =
            DefinitionsValue.ToDictionary(definition => definition.Id, StringComparer.Ordinal);

        public static IReadOnlyList<PlayerAttributeDefinition> Definitions => DefinitionsValue;

        public static PlayerAttributeDefinition Find(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            DefinitionsById.TryGetValue(id.Trim(), out PlayerAttributeDefinition definition);
            return definition;
        }
    }

    public sealed class PlayerAttributeValue
    {
        public PlayerAttributeValue(string id, int value)
        {
            Id = PlayerAttributeSet.RequireId(id, nameof(id));
            Value = PlayerAttributeSet.RequireValue(value, nameof(value));
        }

        public string Id { get; }
        public int Value { get; }
    }

    public sealed class PlayerAttributeSet
    {
        public const int MinimumValue = 1;
        public const int MaximumValue = 9999;

        private readonly Dictionary<string, int> values = new Dictionary<string, int>(StringComparer.Ordinal);

        public PlayerAttributeSet(IEnumerable<PlayerAttributeValue> restoredValues = null)
        {
            foreach (PlayerAttributeDefinition definition in PlayerAttributeCatalog.Definitions)
            {
                values[definition.Id] = definition.DefaultValue;
            }

            if (restoredValues == null)
            {
                return;
            }

            foreach (PlayerAttributeValue entry in restoredValues)
            {
                if (entry == null)
                {
                    continue;
                }

                values[RequireId(entry.Id, nameof(restoredValues))] =
                    RequireValue(entry.Value, nameof(restoredValues));
            }
        }

        public int GetValue(string id)
        {
            string safeId = RequireId(id, nameof(id));
            if (values.TryGetValue(safeId, out int value))
            {
                return value;
            }

            PlayerAttributeDefinition definition = PlayerAttributeCatalog.Find(safeId);
            return definition != null ? definition.DefaultValue : MinimumValue;
        }

        public bool Contains(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && values.ContainsKey(id.Trim());
        }

        public PlayerAttributeSet WithIncrease(string id, int amount)
        {
            string safeId = RequireId(id, nameof(id));
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "Attribute increases cannot be negative.");
            }
            var snapshot = CreateSnapshot().ToList();
            int index = snapshot.FindIndex(entry => string.Equals(entry.Id, safeId, StringComparison.Ordinal));
            int current = index >= 0 ? snapshot[index].Value : GetValue(safeId);
            int next = (int)Math.Min(MaximumValue, (long)current + amount);
            var replacement = new PlayerAttributeValue(safeId, next);
            if (index >= 0)
            {
                snapshot[index] = replacement;
            }
            else
            {
                snapshot.Add(replacement);
            }

            return new PlayerAttributeSet(snapshot);
        }

        public IReadOnlyList<PlayerAttributeValue> CreateSnapshot()
        {
            var ordered = new List<PlayerAttributeValue>();
            var included = new HashSet<string>(StringComparer.Ordinal);
            foreach (PlayerAttributeDefinition definition in PlayerAttributeCatalog.Definitions.OrderBy(item => item.DisplayOrder))
            {
                ordered.Add(new PlayerAttributeValue(definition.Id, GetValue(definition.Id)));
                included.Add(definition.Id);
            }

            foreach (KeyValuePair<string, int> entry in values.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                if (included.Add(entry.Key))
                {
                    ordered.Add(new PlayerAttributeValue(entry.Key, entry.Value));
                }
            }

            return ordered.AsReadOnly();
        }

        internal static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("An attribute ID cannot be empty.", parameterName);
            }

            return value.Trim();
        }

        internal static int RequireValue(int value, string parameterName)
        {
            if (value < MinimumValue || value > MaximumValue)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    $"Attributes must be between {MinimumValue} and {MaximumValue}.");
            }

            return value;
        }
    }
}
