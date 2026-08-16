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
        [SerializeField, Min(0.01f)] private float orbitSmoothTime = 0.08f;
        [SerializeField, Range(-60f, 0f)] private float minimumPitchDegrees = -20f;
        [SerializeField, Range(0f, 75f)] private float maximumPitchDegrees = 35f;

        private Vector3 velocity;
        private float currentYawDegrees;
        private float targetYawDegrees;
        private float yawVelocity;
        private float currentPitchDegrees;
        private float targetPitchDegrees;
        private float pitchVelocity;
        private float targetGroundY;
        private bool hasBounds;
        private float minX;
        private float maxX;
        private float minZ;
        private float maxZ;

        public float OrbitYawDegrees => targetYawDegrees;
        public float OrbitPitchDegrees => targetPitchDegrees;
        public float MinimumPitchDegrees => minimumPitchDegrees;
        public float MaximumPitchDegrees => maximumPitchDegrees;

        public void Initialize(Transform followTarget)
        {
            target = followTarget;
            targetGroundY = target != null ? target.position.y : 0f;
            Snap();
        }

        public void SetBounds(float minimumX, float maximumX, float minimumZ, float maximumZ)
        {
            minX = Mathf.Min(minimumX, maximumX);
            maxX = Mathf.Max(minimumX, maximumX);
            minZ = Mathf.Min(minimumZ, maximumZ);
            maxZ = Mathf.Max(minimumZ, maximumZ);
            hasBounds = true;
            Snap();
        }

        public void AddOrbitDegrees(float degrees)
        {
            AddOrbitDegrees(degrees, 0f);
        }

        public void AddOrbitDegrees(float yawDegrees, float pitchDegrees)
        {
            if (!Mathf.Approximately(yawDegrees, 0f))
            {
                targetYawDegrees = Mathf.Repeat(targetYawDegrees + yawDegrees, 360f);
            }
            if (!Mathf.Approximately(pitchDegrees, 0f))
            {
                targetPitchDegrees = Mathf.Clamp(
                    targetPitchDegrees + pitchDegrees,
                    minimumPitchDegrees,
                    maximumPitchDegrees);
            }
        }

        public void SetOrbitYaw(float degrees, bool snapImmediately = false)
        {
            targetYawDegrees = Mathf.Repeat(degrees, 360f);
            if (snapImmediately)
            {
                currentYawDegrees = targetYawDegrees;
                yawVelocity = 0f;
                Snap();
            }
        }

        public void SetOrbitPitch(float degrees, bool snapImmediately = false)
        {
            targetPitchDegrees = Mathf.Clamp(degrees, minimumPitchDegrees, maximumPitchDegrees);
            if (snapImmediately)
            {
                currentPitchDegrees = targetPitchDegrees;
                pitchVelocity = 0f;
                Snap();
            }
        }

        public void Snap()
        {
            if (target == null)
            {
                return;
            }

            Vector3 groundedTarget = GetGroundedTargetPosition();
            currentYawDegrees = targetYawDegrees;
            currentPitchDegrees = targetPitchDegrees;
            transform.position = groundedTarget + GetOrbitOffset(currentYawDegrees, currentPitchDegrees);
            transform.LookAt(groundedTarget + lookOffset, Vector3.up);
            velocity = Vector3.zero;
            yawVelocity = 0f;
            pitchVelocity = 0f;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 groundedTarget = GetGroundedTargetPosition();
            currentYawDegrees = Mathf.SmoothDampAngle(
                currentYawDegrees,
                targetYawDegrees,
                ref yawVelocity,
                orbitSmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime);
            currentPitchDegrees = Mathf.SmoothDamp(
                currentPitchDegrees,
                targetPitchDegrees,
                ref pitchVelocity,
                orbitSmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime);
            Vector3 destination = groundedTarget + GetOrbitOffset(currentYawDegrees, currentPitchDegrees);
            transform.position = Vector3.SmoothDamp(transform.position, destination, ref velocity, smoothTime);
            transform.LookAt(groundedTarget + lookOffset, Vector3.up);
        }

        private Vector3 GetOrbitOffset(float yawDegrees, float pitchDegrees)
        {
            Quaternion yaw = Quaternion.AngleAxis(yawDegrees, Vector3.up);
            Quaternion pitch = Quaternion.AngleAxis(pitchDegrees, Vector3.right);
            return yaw * (pitch * offset);
        }

        private Vector3 GetGroundedTargetPosition()
        {
            Vector3 position = target.position;
            position.y = targetGroundY;
            if (hasBounds)
            {
                position.x = Mathf.Clamp(position.x, minX, maxX);
                position.z = Mathf.Clamp(position.z, minZ, maxZ);
            }
            return position;
        }
    }
}
