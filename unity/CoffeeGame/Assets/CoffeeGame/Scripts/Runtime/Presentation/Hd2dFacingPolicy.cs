using UnityEngine;

namespace CoffeeGame.Presentation
{
    public enum Hd2dFacingDirection
    {
        Down = 0,
        Side = 1,
        Up = 2
    }

    public static class Hd2dFacingPolicy
    {
        public static Hd2dFacingDirection ResolveDirection(
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

            return cameraForwardAmount >= 0f
                ? Hd2dFacingDirection.Up
                : Hd2dFacingDirection.Down;
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
            // CameraFacingBillboard points the sprite's local forward back toward
            // the camera, which also reverses its local right axis. Mirror the
            // texture for camera-right movement so the visible pose still points
            // in the movement direction.
            return cameraRightAmount > 0f;
        }
    }
}
