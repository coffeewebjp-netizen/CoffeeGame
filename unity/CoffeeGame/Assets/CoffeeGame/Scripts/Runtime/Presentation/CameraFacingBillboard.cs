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

            Vector3 forward = targetCamera.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }

            transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }
    }
}

