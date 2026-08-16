using CoffeeGame.Input;
using UnityEngine;

namespace CoffeeGame.Presentation
{
    /// <summary>
    /// Converts Battle-context camera input into horizontal orbit without coupling
    /// the camera-follow geometry to a specific input backend.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraOrbitInputDriver : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float yawDegreesPerSecond = 110f;
        [SerializeField, Min(1f)] private float pitchDegreesPerSecond = 75f;
        [SerializeField, Min(0.01f)] private float mouseDegreesPerPixel = 0.22f;

        private FixedCameraRig rig;
        private GameInputReader input;

        public void Initialize(FixedCameraRig cameraRig, GameInputReader inputReader)
        {
            rig = cameraRig;
            input = inputReader;
        }

        private void Update()
        {
            if (rig == null || input == null)
            {
                return;
            }

            Vector2 pointerDelta = input.CameraPointerDelta;
            float yawDegrees = input.CameraYaw * yawDegreesPerSecond * Time.unscaledDeltaTime +
                               pointerDelta.x * mouseDegreesPerPixel;
            float pitchDegrees = input.CameraPitch * pitchDegreesPerSecond * Time.unscaledDeltaTime +
                                 pointerDelta.y * mouseDegreesPerPixel;
            rig.AddOrbitDegrees(yawDegrees, pitchDegrees);
        }
    }
}
