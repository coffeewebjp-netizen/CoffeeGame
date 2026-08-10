using CoffeeGame.Presentation;
using UnityEngine;

namespace CoffeeGame.Combat
{
    [DisallowMultipleComponent]
    public sealed class MagicChargeAuraEffect : MonoBehaviour
    {
        private readonly Transform[] shards = new Transform[4];
        private Transform anchor;
        private Material ownedMaterial;
        private float lifetime;
        private float elapsed;

        public void Initialize(Transform followTarget, float duration)
        {
            anchor = followTarget;
            lifetime = Mathf.Max(0.12f, duration);
            ownedMaterial = RuntimeMaterialFactory.CreateUnlit(
                "Magic charge crystal material",
                new Color(0.42f, 0.92f, 1f));

            for (int index = 0; index < shards.Length; index++)
            {
                GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.name = $"Charge shard {index + 1}";
                shard.transform.SetParent(transform, false);
                shard.transform.localScale = new Vector3(0.045f, 0.13f, 0.045f);
                shard.transform.localRotation = Quaternion.Euler(45f, index * 90f, 45f);
                Collider collider = shard.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                Renderer renderer = shard.GetComponent<Renderer>();
                if (renderer != null && ownedMaterial != null)
                {
                    renderer.sharedMaterial = ownedMaterial;
                }
                shards[index] = shard.transform;
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
            transform.position = anchor.position + Vector3.up * 0.72f;
            float radius = Mathf.Lerp(0.34f, 0.17f, normalized);
            for (int index = 0; index < shards.Length; index++)
            {
                float angle = elapsed * 5.4f + index * Mathf.PI * 0.5f;
                shards[index].localPosition = new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle * 1.6f) * 0.075f,
                    Mathf.Sin(angle) * radius);
                shards[index].Rotate(110f * Time.deltaTime, 180f * Time.deltaTime, 70f * Time.deltaTime);
                float scale = Mathf.Lerp(0.8f, 1.25f, normalized);
                shards[index].localScale = new Vector3(0.045f, 0.13f, 0.045f) * scale;
            }

            if (elapsed >= lifetime)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (ownedMaterial != null)
            {
                Destroy(ownedMaterial);
                ownedMaterial = null;
            }
        }
    }
}
