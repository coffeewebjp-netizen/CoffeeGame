using UnityEngine;

namespace CoffeeGame.Presentation
{
    public enum Hd2dFacingDirection
    {
        Down = 0,
        DownSide = 1,
        Side = 2,
        UpSide = 3,
        Up = 4
    }

    public static class Hd2dFacingPolicy
    {
        public static Hd2dFacingDirection ResolveDirection(
            float cameraForwardAmount,
            float cameraRightAmount,
            Hd2dFacingDirection previous,
            float diagonalBoundaryDegrees = 22.5f,
            float hysteresisDegrees = 4f)
        {
            float forwardMagnitude = Mathf.Abs(cameraForwardAmount);
            float sideMagnitude = Mathf.Abs(cameraRightAmount);
            if (forwardMagnitude + sideMagnitude < 0.001f)
            {
                return previous;
            }

            float lowerBoundary = Mathf.Clamp(diagonalBoundaryDegrees, 5f, 40f);
            float upperBoundary = 90f - lowerBoundary;
            float band = Mathf.Clamp(hysteresisDegrees, 0f, lowerBoundary * 0.8f);
            float angleFromForward = Mathf.Atan2(sideMagnitude, forwardMagnitude) * Mathf.Rad2Deg;

            bool previousAxial = previous == Hd2dFacingDirection.Down ||
                previous == Hd2dFacingDirection.Up;
            bool previousDiagonal = previous == Hd2dFacingDirection.DownSide ||
                previous == Hd2dFacingDirection.UpSide;
            if (previousAxial && angleFromForward <= lowerBoundary + band)
            {
                return ResolveAxial(cameraForwardAmount);
            }
            if (previous == Hd2dFacingDirection.Side && angleFromForward >= upperBoundary - band)
            {
                return Hd2dFacingDirection.Side;
            }
            if (previousDiagonal &&
                angleFromForward >= lowerBoundary - band &&
                angleFromForward <= upperBoundary + band)
            {
                return ResolveDiagonal(cameraForwardAmount);
            }

            if (angleFromForward < lowerBoundary)
            {
                return ResolveAxial(cameraForwardAmount);
            }
            if (angleFromForward > upperBoundary)
            {
                return Hd2dFacingDirection.Side;
            }
            return ResolveDiagonal(cameraForwardAmount);
        }

        public static Hd2dFacingDirection ResolveLegacyDirection(
            float cameraForwardAmount,
            float cameraRightAmount,
            Hd2dFacingDirection previous,
            float forwardDominance = 0.72f,
            float hysteresis = 0.06f)
        {
            float forwardMagnitude = Mathf.Abs(cameraForwardAmount);
            float sideMagnitude = Mathf.Abs(cameraRightAmount);
            if (forwardMagnitude + sideMagnitude < 0.001f)
            {
                return previous;
            }

            float boundary = Mathf.Clamp(forwardDominance, 0.05f, 1.5f);
            float band = Mathf.Clamp(hysteresis, 0f, boundary * 0.8f);
            bool chooseSide = previous == Hd2dFacingDirection.Side
                ? forwardMagnitude <= sideMagnitude * (boundary + band)
                : forwardMagnitude < sideMagnitude * (boundary - band);
            if (chooseSide)
            {
                return Hd2dFacingDirection.Side;
            }

            return ResolveAxial(cameraForwardAmount);
        }

        private static Hd2dFacingDirection ResolveAxial(float cameraForwardAmount)
        {
            return cameraForwardAmount >= 0f
                ? Hd2dFacingDirection.Up
                : Hd2dFacingDirection.Down;
        }

        private static Hd2dFacingDirection ResolveDiagonal(float cameraForwardAmount)
        {
            return cameraForwardAmount >= 0f
                ? Hd2dFacingDirection.UpSide
                : Hd2dFacingDirection.DownSide;
        }

        /// <summary>
        /// Horizontal mirroring is independent from selecting front/side/back
        /// art. Direction-neutral characters such as slimes still need to point
        /// a lunge toward a target on their left.
        /// </summary>
        public static bool ResolveHorizontalFlip(
            float cameraRightAmount,
            bool previousFlip,
            float deadZone = 0.05f)
        {
            float threshold = Mathf.Max(0f, deadZone);
            if (Mathf.Abs(cameraRightAmount) <= threshold)
            {
                return previousFlip;
            }
            // The supplied HD-2D side frames (for example hero_idle_right) are
            // authored facing texture-local left. CameraFacingBillboard maps that
            // local-left pose to camera-right, so only camera-left movement needs
            // a horizontal mirror.
            return cameraRightAmount < 0f;
        }
    }
}
