using UnityEngine;

namespace CoffeeGame.UI
{
    /// <summary>
    /// Turns a held menu axis into one navigation event per actuation. The axis
    /// must return to the release dead zone before the same direction can fire again.
    /// </summary>
    public static class MenuNavigationAxisLatch
    {
        public const float ReleaseThreshold = 0.28f;
        public const float ActuationThreshold = 0.55f;

        public static int Read(float axis, ref int latchedDirection)
        {
            if (Mathf.Abs(axis) < ReleaseThreshold)
            {
                latchedDirection = 0;
                return 0;
            }

            int direction = axis > ActuationThreshold
                ? 1
                : axis < -ActuationThreshold ? -1 : 0;
            if (direction == 0 || direction == latchedDirection)
            {
                return 0;
            }

            latchedDirection = direction;
            return direction;
        }
    }
}
