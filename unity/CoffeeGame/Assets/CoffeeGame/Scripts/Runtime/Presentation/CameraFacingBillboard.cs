using UnityEngine;

namespace CoffeeGame.Presentation
{
    [DisallowMultipleComponent]
    public sealed class CameraFacingBillboard : MonoBehaviour
    {
        private Camera targetCamera;

        public void SetCamera(Camera cameraToFace)
        {
            targetCamera = cameraToFace;
        }

        private void LateUpdate()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera == null)
            {
                return;
            }

            transform.rotation = ResolveRotation(targetCamera.transform.forward);
        }

        public static Quaternion ResolveRotation(Vector3 cameraForward)
        {
            Vector3 forward = cameraForward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }

            // Face the sprite's front toward the camera. Using cameraForward
            // directly shows the back of a two-sided sprite and mirrors its
            // horizontal facing relative to movement.
            return Quaternion.LookRotation(-forward.normalized, Vector3.up);
        }
    }
}
