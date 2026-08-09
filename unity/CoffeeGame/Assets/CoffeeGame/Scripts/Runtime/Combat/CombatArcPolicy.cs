using UnityEngine;

namespace CoffeeGame.Combat
{
    /// <summary>
    /// Shared geometry for the player's front-arc attacks and their readable
    /// sword-range overlay. Keeping the threshold here prevents the overlay
    /// from advertising a different hit area than PlayerCombatController uses.
    /// </summary>
    public static class CombatArcPolicy
    {
        public const float FrontArcDotThreshold = 0.12f;

        public static float FrontArcHalfAngleDegrees =>
            Mathf.Acos(FrontArcDotThreshold) * Mathf.Rad2Deg;

        public static bool Contains(Vector3 facing, Vector3 targetDirection)
        {
            Vector3 planarFacing = Vector3.ProjectOnPlane(facing, Vector3.up);
            Vector3 planarTarget = Vector3.ProjectOnPlane(targetDirection, Vector3.up);
            if (planarTarget.sqrMagnitude <= 0.001f)
            {
                return true;
            }

            if (planarFacing.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            return Vector3.Dot(planarFacing.normalized, planarTarget.normalized) >=
                FrontArcDotThreshold;
        }
    }
}
