using UnityEngine;

namespace CoffeeGame.Combat
{
    /// <summary>
    /// Builds the open billboard-space crescent used by the sword swing VFX.
    /// Damage remains governed by CombatArcPolicy; this geometry is an action
    /// trail that communicates direction and reach without drawing a static
    /// hit-sector outline around the player.
    /// </summary>
    public static class SwordSlashTrailGeometry
    {
        public const int DefaultSegments = 28;

        public static Vector3[] Build(float radius, bool mirrored, int segments = DefaultSegments)
        {
            if (radius <= 0f)
            {
                return System.Array.Empty<Vector3>();
            }

            int clampedSegments = Mathf.Max(2, segments);
            var points = new Vector3[clampedSegments + 1];
            float mirror = mirrored ? -1f : 1f;
            for (int index = 0; index < points.Length; index++)
            {
                float t = index / (float)clampedSegments;
                float angle = Mathf.Lerp(122f, -24f, t) * Mathf.Deg2Rad;
                float x = Mathf.Cos(angle) * 0.96f * radius * mirror;
                float y = Mathf.Sin(angle) * 0.88f * radius;
                points[index] = new Vector3(x, y, 0f);
            }

            return points;
        }
    }
}
