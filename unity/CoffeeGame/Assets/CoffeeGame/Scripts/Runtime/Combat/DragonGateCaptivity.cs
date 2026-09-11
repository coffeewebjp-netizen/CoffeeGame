using CoffeeGame.Actors;
using UnityEngine;

namespace CoffeeGame.Combat
{
    // The owner clock controls expiry so freezing the victim cannot make the trap permanent.
    public sealed class DragonGateCaptivity : MonoBehaviour
    {
        private GameObject owner;
        private float remaining;
        private Transform visual, gate;
        private Vector3 originalScale, originalPosition;
        private float elapsed;
        public bool Active => owner != null && owner.activeInHierarchy && remaining > 0f;
        public static bool IsCaptured(GameObject target) => target != null && target.GetComponentInParent<DragonGateCaptivity>()?.Active == true;
        public void Capture(GameObject source, float duration, Transform destination)
        {
            if (Active) return;
            owner = source; remaining = duration; gate = destination; elapsed = 0f;
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                Transform candidate = renderer.transform;
                while (candidate.parent != null && candidate.parent != transform) candidate = candidate.parent;
                if (candidate.parent == transform) { visual = candidate; break; }
            }
            if (visual != null) { originalScale = visual.localScale; originalPosition = visual.localPosition; }
            GetComponent<IEnemyAttack>()?.Parry(.5f);
        }
        private void Update()
        {
            if (owner == null || !owner.activeInHierarchy || GetComponent<Health>()?.IsAlive != true) { Release(owner); return; }
            float dt = CombatClock.DeltaTime(owner);
            remaining -= dt; elapsed += dt;
            if (remaining <= 0f) { Release(owner); return; }
            if (visual != null && gate != null)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / .55f);
                visual.localScale = Vector3.Lerp(originalScale, originalScale * .3f, t);
                visual.localPosition = Vector3.Lerp(originalPosition, transform.InverseTransformPoint(gate.position), t);
            }
        }
        public void Release(GameObject source)
        {
            if (source != owner) return;
            remaining = 0f; owner = null;
            if (visual != null) { visual.localScale = originalScale; visual.localPosition = originalPosition; }
            visual = gate = null;
        }
        private void OnDisable() => Release(owner);
    }
}
