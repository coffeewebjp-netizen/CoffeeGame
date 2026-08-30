using UnityEngine;

namespace CoffeeGame.Combat
{
    [DisallowMultipleComponent]
    public sealed class MagicChargeAuraEffect : MonoBehaviour
    {
        private readonly Transform[] shards = new Transform[6];
        private readonly Material[] materials = new Material[6];
        private Transform anchor;
        private float lifetime;
        private float elapsed;

        public void Initialize(Transform followTarget, float duration)
        {
            anchor = followTarget;
            lifetime = Mathf.Max(0.12f, duration);
            for (int index = 0; index < shards.Length; index++)
            {
                GameObject crystal = IceCrystalVisuals.CreateCrystal(
                    transform,
                    $"Charge crystal {index + 1}",
                    new Vector3(0.03f, 0.09f, 0.03f),
                    index % 2 == 0 ? IceCrystalVisuals.Core : IceCrystalVisuals.Frost,
                    out Material material);
                shards[index] = crystal.transform;
                materials[index] = material;
            }
        }

        private void Update()
        {
            if (anchor == null)
            {
                Destroy(gameObject);
                return;
            }

            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / lifetime);
            transform.position = anchor.position + Vector3.up * 0.78f;
            float radius = Mathf.Lerp(0.32f, 0.11f, normalized * normalized);
            float rise = Mathf.Lerp(0.04f, 0.16f, normalized);
            for (int index = 0; index < shards.Length; index++)
            {
                float angle = elapsed * 3.2f + index * Mathf.PI * 2f / shards.Length;
                shards[index].localPosition = new Vector3(
                    Mathf.Cos(angle) * radius,
                    rise + Mathf.Sin(angle * 2f) * 0.03f,
                    Mathf.Sin(angle) * radius);
                shards[index].localRotation = Quaternion.Euler(90f, angle * Mathf.Rad2Deg, elapsed * 80f);
                float scale = Mathf.Lerp(0.7f, 1.15f, normalized);
                shards[index].localScale = new Vector3(0.03f, 0.09f, 0.03f) * scale;
            }

            if (elapsed >= lifetime)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            for (int index = 0; index < materials.Length; index++)
            {
                if (materials[index] != null)
                {
                    Destroy(materials[index]);
                    materials[index] = null;
                }
            }
        }
    }
}
