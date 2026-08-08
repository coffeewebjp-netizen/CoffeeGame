using UnityEngine;

namespace CoffeeGame.Presentation
{
    [DisallowMultipleComponent]
    public sealed class FixedCameraRig : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 5.75f, -8.85f);
        [SerializeField] private Vector3 lookOffset = new Vector3(0f, 0.78f, 0f);
        [SerializeField, Min(0.01f)] private float smoothTime = 0.16f;

        private Vector3 velocity;
        private float targetGroundY;

        public void Initialize(Transform followTarget)
        {
            target = followTarget;
            targetGroundY = target != null ? target.position.y : 0f;
            Snap();
        }

        public void Snap()
        {
            if (target == null)
            {
                return;
            }

            Vector3 groundedTarget = GetGroundedTargetPosition();
            transform.position = groundedTarget + offset;
            transform.LookAt(groundedTarget + lookOffset, Vector3.up);
            velocity = Vector3.zero;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 groundedTarget = GetGroundedTargetPosition();
            Vector3 destination = groundedTarget + offset;
            transform.position = Vector3.SmoothDamp(transform.position, destination, ref velocity, smoothTime);
            transform.LookAt(groundedTarget + lookOffset, Vector3.up);
        }

        private Vector3 GetGroundedTargetPosition()
        {
            Vector3 position = target.position;
            position.y = targetGroundY;
            return position;
        }
    }
}
