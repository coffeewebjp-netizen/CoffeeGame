using UnityEngine;

namespace CoffeeGame.Input
{
    public static class TouchOverlayMath
    {
        public const float MoveDeadZone = 18f;
        public const float MoveFullRadius = 92f;

        public static Vector2 ResolveHoldMove(Vector2 origin, Vector2 current)
        {
            Vector2 offset = current - origin;
            float magnitude = offset.magnitude;
            if (magnitude < MoveDeadZone)
            {
                return Vector2.zero;
            }

            float usable = Mathf.Max(1f, MoveFullRadius - MoveDeadZone);
            return Vector2.ClampMagnitude(offset.normalized * ((magnitude - MoveDeadZone) / usable), 1f);
        }
    }
}
