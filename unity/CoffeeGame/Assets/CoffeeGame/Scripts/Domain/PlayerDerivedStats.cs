using System;

namespace CoffeeGame.Domain
{
    public sealed class PlayerDerivedStats
    {
        public PlayerDerivedStats(
            float attackMultiplier,
            float movementSpeedMultiplier,
            float criticalChance,
            float evasionChance,
            float specialChargeSpeedMultiplier,
            float maxStaminaMultiplier,
            float incomingDamageMultiplier)
        {
            AttackMultiplier = attackMultiplier;
            MovementSpeedMultiplier = movementSpeedMultiplier;
            CriticalChance = criticalChance;
            EvasionChance = evasionChance;
            SpecialChargeSpeedMultiplier = specialChargeSpeedMultiplier;
            MaxStaminaMultiplier = maxStaminaMultiplier;
            IncomingDamageMultiplier = incomingDamageMultiplier;
        }

        public float AttackMultiplier { get; }
        public float MovementSpeedMultiplier { get; }
        public float CriticalChance { get; }
        public float EvasionChance { get; }
        public float SpecialChargeSpeedMultiplier { get; }
        public float MaxStaminaMultiplier { get; }
        public float IncomingDamageMultiplier { get; }
    }

    public static class PlayerDerivedStatCalculator
    {
        private const int NeutralAttributeValue = 10;

        public static PlayerDerivedStats Calculate(PlayerStatus status)
        {
            if (status == null)
            {
                throw new ArgumentNullException(nameof(status));
            }

            int strengthDelta = status.Strength - NeutralAttributeValue;
            int agilityDelta = status.Agility - NeutralAttributeValue;
            int techniqueDelta = status.Technique - NeutralAttributeValue;
            int luckDelta = status.Luck - NeutralAttributeValue;
            int vitalityDelta = status.Vitality - NeutralAttributeValue;

            float attack = ClampMultiplier(1f + strengthDelta * 0.02f);
            float movement = ClampMultiplier(1f + agilityDelta * 0.01f);
            float critical = Clamp01(Math.Max(0, techniqueDelta) * 0.004f + Math.Max(0, luckDelta) * 0.002f, 0.6f);
            float evasion = Clamp01(Math.Max(0, agilityDelta) * 0.003f + Math.Max(0, luckDelta) * 0.002f, 0.5f);
            float specialSpeed = ClampMultiplier(1f + techniqueDelta * 0.02f);
            float stamina = ClampMultiplier(1f + vitalityDelta * 0.025f);
            float incomingDamage = vitalityDelta >= 0
                ? 1f / (1f + vitalityDelta * 0.025f)
                : 1f + -vitalityDelta * 0.025f;

            return new PlayerDerivedStats(
                attack,
                movement,
                critical,
                evasion,
                specialSpeed,
                stamina,
                Math.Max(0.2f, Math.Min(3f, incomingDamage)));
        }

        private static float ClampMultiplier(float value)
        {
            return Math.Max(0.2f, Math.Min(10f, value));
        }

        private static float Clamp01(float value, float maximum)
        {
            return Math.Max(0f, Math.Min(maximum, value));
        }
    }
}
