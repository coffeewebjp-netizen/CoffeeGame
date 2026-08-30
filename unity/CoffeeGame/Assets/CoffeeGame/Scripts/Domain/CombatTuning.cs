using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace CoffeeGame.Domain
{
    /// <summary>
    /// First combat-slice values migrated from the browser prototype.
    /// Browser distances use 100 pixels per Unity metre.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatTuning", menuName = "Coffee Game/Combat Tuning")]
    public sealed class CombatTuning : ScriptableObject
    {
        public const float PixelsPerMeter = 100f;

        [Header("Player resources")]
        [SerializeField, Min(1)] private int playerMaxHealth = 24;
        [SerializeField, Min(0)] private int playerMaxMagic = 12;
        [SerializeField, Min(1)] private int playerMaxStamina = 100;
        [SerializeField, Min(0)] private int staminaPerSwordHit = 25;

        [Header("Player movement (metres)")]
        [SerializeField, Min(0f)] private float walkSpeed = 1.55f;
        [SerializeField, Min(0f)] private float runSpeed = 2.45f;
        [SerializeField, Min(0f)] private float runHoldDuration = 0.65f;
        [SerializeField, Min(0f)] private float jumpVelocity = 4.8f;
        [SerializeField, Min(0f)] private float gravity = 11.8f;
        [SerializeField, Range(0f, 1f)] private float airControl = 0.72f;
        [SerializeField, Min(0f)] private float dodgeSpeed = 4.2f;
        [SerializeField, Range(0.05f, 1f)] private float dodgeInvulnerabilityFraction = 0.5f;
        [SerializeField, Min(1f)] private float perfectDodgeRangeMultiplier = 1.65f;

        [Header("Sword")]
        [SerializeField, Min(0)] private int swordDamage = 3;
        [SerializeField, Min(0f)] private float swordRange = 0.78f;
        [SerializeField, Min(0f)] private float swordCooldown = 0.34f;
        [SerializeField, Min(0)] private int airSlashDamage = 4;
        [SerializeField, Min(0f)] private float airSlashRange = 0.94f;
        [SerializeField, Min(0)] private int plungeDamage = 8;
        [SerializeField, Min(0f)] private float plungeRadius = 1.18f;
        [SerializeField, Min(0f)] private float plungeSpeed = 7.6f;
        [SerializeField, Min(0f)] private float plungeLandingLag = 0.48f;

        [Header("Special")]
        [SerializeField, Min(0)] private int specialDamage = 12;
        [SerializeField, Min(0f)] private float specialRange = 1.42f;
        [SerializeField, Min(0f)] private float specialChargeDuration = 0.8f;
        [SerializeField, Min(0)] private int specialStaminaCost = 100;

        [Header("Ice magic")]
        [SerializeField, Min(0)] private int magicDamage = 5;
        [SerializeField, Min(0)] private int magicCost = 4;
        [SerializeField, Min(0f)] private float magicChargeDuration = 0.65f;
        [SerializeField, Min(0f)] private float magicProjectileSpeed = 4.4f;
        [SerializeField, Min(0f)] private float magicMpRegenPerSecond = 0.45f;

        [Header("Slime")]
        [SerializeField, Min(1)] private int slimeMaxHealth = 12;
        [SerializeField, Min(0)] private int slimeDamage = 2;
        [SerializeField, Min(0f)] private float slimeSpeed = 0.68f;
        [SerializeField, Min(0f)] private float slimeAttackRange = 1.6f;
        [SerializeField, Min(0f)] private float slimeAttackInterval = 2.4f;
        [SerializeField, Min(0f)] private float slimeAttackWindup = 0.55f;
        [SerializeField, Min(0)] private int slimeRewardExperience = 1;
        [SerializeField, Min(0)] private int slimeRewardGold = 1;
        [SerializeField, Min(0)] private int slimeRewardJelly = 1;
        [FormerlySerializedAs("goalKills")]
        [SerializeField, Min(1)] private int rivalEncounterIntervalKills = 5;

        public int PlayerMaxHealth => playerMaxHealth;
        public int PlayerMaxMp => playerMaxMagic;
        public int MaxStamina => playerMaxStamina;
        public int StaminaPerHit => staminaPerSwordHit;
        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;
        public float RunHoldSeconds => runHoldDuration;
        public float JumpVelocity => jumpVelocity;
        public float Gravity => gravity;
        public float AirControl => airControl;
        public float DodgeSpeed => dodgeSpeed;
        public float DodgeInvulnerabilityFraction => dodgeInvulnerabilityFraction;
        public float PerfectDodgeRangeMultiplier => perfectDodgeRangeMultiplier;
        public float ExpectedDodgeAirSeconds => gravity <= 0f ? 0f : 2f * jumpVelocity / gravity;
        public float DodgeInvulnerabilitySeconds => ExpectedDodgeAirSeconds * dodgeInvulnerabilityFraction;
        public int SwordDamage => swordDamage;
        public float SwordRange => swordRange;
        public float SwordCooldown => swordCooldown;
        public int AirSlashDamage => airSlashDamage;
        public float AirSlashRange => airSlashRange;
        public int PlungeDamage => plungeDamage;
        public float PlungeRadius => plungeRadius;
        public float PlungeSpeed => plungeSpeed;
        public float LandingLag => plungeLandingLag;
        public int SpecialDamage => specialDamage;
        public float SpecialRange => specialRange;
        public float SpecialChargeSeconds => specialChargeDuration;
        public int SpecialStaminaCost => specialStaminaCost;
        public int MagicDamage => magicDamage;
        public int MagicCost => magicCost;
        public float MagicChargeSeconds => magicChargeDuration;
        public float MagicProjectileSpeed => magicProjectileSpeed;
        public float MagicMpRegenPerSecond => magicMpRegenPerSecond;
        public int SlimeMaxHealth => slimeMaxHealth;
        public int SlimeDamage => slimeDamage;
        public float SlimeSpeed => slimeSpeed;
        public float SlimeAttackRange => slimeAttackRange;
        public float SlimeAttackInterval => slimeAttackInterval;
        public float SlimeWindupSeconds => slimeAttackWindup;
        public int RivalEncounterIntervalKills => rivalEncounterIntervalKills;
        public RewardBundle SlimeReward => new RewardBundle(
            slimeRewardExperience,
            slimeRewardGold,
            slimeRewardJelly);

        public bool IsValid => GetValidationErrors().Count == 0;

        public static CombatTuning CreateDefault()
        {
            var tuning = CreateInstance<CombatTuning>();
            tuning.name = "CombatTuning (Browser Defaults)";
            tuning.ApplyBrowserDefaults();
            return tuning;
        }

        public IReadOnlyList<string> GetValidationErrors()
        {
            var errors = new List<string>();

            RequirePositive(errors, nameof(playerMaxHealth), playerMaxHealth);
            RequireNonNegative(errors, nameof(playerMaxMagic), playerMaxMagic);
            RequirePositive(errors, nameof(playerMaxStamina), playerMaxStamina);
            RequireNonNegative(errors, nameof(staminaPerSwordHit), staminaPerSwordHit);
            RequireNonNegative(errors, nameof(walkSpeed), walkSpeed);
            RequireNonNegative(errors, nameof(runSpeed), runSpeed);
            RequireNonNegative(errors, nameof(runHoldDuration), runHoldDuration);
            RequirePositive(errors, nameof(jumpVelocity), jumpVelocity);
            RequirePositive(errors, nameof(gravity), gravity);
            RequireUnitInterval(errors, nameof(airControl), airControl);
            RequireNonNegative(errors, nameof(dodgeSpeed), dodgeSpeed);
            RequireUnitInterval(errors, nameof(dodgeInvulnerabilityFraction), dodgeInvulnerabilityFraction);
            RequirePositive(errors, nameof(perfectDodgeRangeMultiplier), perfectDodgeRangeMultiplier);
            RequireNonNegative(errors, nameof(swordDamage), swordDamage);
            RequireNonNegative(errors, nameof(swordRange), swordRange);
            RequireNonNegative(errors, nameof(swordCooldown), swordCooldown);
            RequireNonNegative(errors, nameof(airSlashDamage), airSlashDamage);
            RequireNonNegative(errors, nameof(airSlashRange), airSlashRange);
            RequireNonNegative(errors, nameof(plungeDamage), plungeDamage);
            RequireNonNegative(errors, nameof(plungeRadius), plungeRadius);
            RequirePositive(errors, nameof(plungeSpeed), plungeSpeed);
            RequireNonNegative(errors, nameof(plungeLandingLag), plungeLandingLag);
            RequireNonNegative(errors, nameof(specialDamage), specialDamage);
            RequireNonNegative(errors, nameof(specialRange), specialRange);
            RequireNonNegative(errors, nameof(specialChargeDuration), specialChargeDuration);
            RequireNonNegative(errors, nameof(specialStaminaCost), specialStaminaCost);
            RequireNonNegative(errors, nameof(magicDamage), magicDamage);
            RequireNonNegative(errors, nameof(magicCost), magicCost);
            RequireNonNegative(errors, nameof(magicChargeDuration), magicChargeDuration);
            RequirePositive(errors, nameof(magicProjectileSpeed), magicProjectileSpeed);
            RequireNonNegative(errors, nameof(magicMpRegenPerSecond), magicMpRegenPerSecond);
            RequirePositive(errors, nameof(slimeMaxHealth), slimeMaxHealth);
            RequireNonNegative(errors, nameof(slimeDamage), slimeDamage);
            RequireNonNegative(errors, nameof(slimeSpeed), slimeSpeed);
            RequireNonNegative(errors, nameof(slimeAttackRange), slimeAttackRange);
            RequirePositive(errors, nameof(slimeAttackInterval), slimeAttackInterval);
            RequireNonNegative(errors, nameof(slimeAttackWindup), slimeAttackWindup);
            RequireNonNegative(errors, nameof(slimeRewardExperience), slimeRewardExperience);
            RequireNonNegative(errors, nameof(slimeRewardGold), slimeRewardGold);
            RequireNonNegative(errors, nameof(slimeRewardJelly), slimeRewardJelly);
            RequirePositive(errors, nameof(rivalEncounterIntervalKills), rivalEncounterIntervalKills);

            if (runSpeed < walkSpeed)
            {
                errors.Add($"{nameof(runSpeed)} must be greater than or equal to {nameof(walkSpeed)}.");
            }

            if (specialStaminaCost > playerMaxStamina)
            {
                errors.Add($"{nameof(specialStaminaCost)} cannot exceed {nameof(playerMaxStamina)}.");
            }

            if (magicCost > playerMaxMagic)
            {
                errors.Add($"{nameof(magicCost)} cannot exceed {nameof(playerMaxMagic)}.");
            }

            return errors.AsReadOnly();
        }

        public void ValidateOrThrow()
        {
            var errors = GetValidationErrors();
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Combat tuning is invalid:{Environment.NewLine}- {string.Join(Environment.NewLine + "- ", errors)}");
            }
        }

        private void ApplyBrowserDefaults()
        {
            playerMaxHealth = 24;
            playerMaxMagic = 12;
            playerMaxStamina = 100;
            staminaPerSwordHit = 25;
            walkSpeed = 155f / PixelsPerMeter;
            runSpeed = 245f / PixelsPerMeter;
            runHoldDuration = 0.65f;
            jumpVelocity = 480f / PixelsPerMeter;
            gravity = 1180f / PixelsPerMeter;
            airControl = 0.72f;
            dodgeSpeed = 4.2f;
            dodgeInvulnerabilityFraction = 0.5f;
            perfectDodgeRangeMultiplier = 1.65f;
            swordDamage = 3;
            swordRange = 78f / PixelsPerMeter;
            swordCooldown = 0.34f;
            airSlashDamage = 4;
            airSlashRange = 94f / PixelsPerMeter;
            plungeDamage = 8;
            plungeRadius = 118f / PixelsPerMeter;
            plungeSpeed = 760f / PixelsPerMeter;
            plungeLandingLag = 0.48f;
            specialDamage = 12;
            specialRange = 142f / PixelsPerMeter;
            specialChargeDuration = 0.8f;
            specialStaminaCost = 100;
            magicDamage = 5;
            magicCost = 4;
            magicChargeDuration = 0.65f;
            magicProjectileSpeed = 440f / PixelsPerMeter;
            magicMpRegenPerSecond = 0.45f;
            slimeMaxHealth = 12;
            slimeDamage = 2;
            slimeSpeed = 68f / PixelsPerMeter;
            slimeAttackRange = 160f / PixelsPerMeter;
            slimeAttackInterval = 2.4f;
            slimeAttackWindup = 0.55f;
            slimeRewardExperience = 1;
            slimeRewardGold = 1;
            slimeRewardJelly = 1;
            rivalEncounterIntervalKills = 5;
        }

        private static void RequirePositive(List<string> errors, string fieldName, int value)
        {
            if (value <= 0)
            {
                errors.Add($"{fieldName} must be greater than zero.");
            }
        }

        private static void RequirePositive(List<string> errors, string fieldName, float value)
        {
            if (!IsFinite(value) || value <= 0f)
            {
                errors.Add($"{fieldName} must be finite and greater than zero.");
            }
        }

        private static void RequireNonNegative(List<string> errors, string fieldName, int value)
        {
            if (value < 0)
            {
                errors.Add($"{fieldName} cannot be negative.");
            }
        }

        private static void RequireNonNegative(List<string> errors, string fieldName, float value)
        {
            if (!IsFinite(value) || value < 0f)
            {
                errors.Add($"{fieldName} must be finite and non-negative.");
            }
        }

        private static void RequireUnitInterval(List<string> errors, string fieldName, float value)
        {
            if (!IsFinite(value) || value < 0f || value > 1f)
            {
                errors.Add($"{fieldName} must be finite and between zero and one.");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
