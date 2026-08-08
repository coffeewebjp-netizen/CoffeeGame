using System.Collections;
using UnityEngine;
using CoffeeGame.Presentation;

namespace CoffeeGame.Combat
{
    public static class CombatVfxFactory
    {
        public static void SpawnRing(Vector3 center, float radius, Color color, float lifetime = 0.3f)
        {
            var effect = new GameObject("Combat ring VFX");
            effect.transform.position = center + Vector3.up * 0.035f;
            var line = effect.AddComponent<LineRenderer>();
            line.loop = true;
            line.useWorldSpace = false;
            line.positionCount = 49;
            line.widthMultiplier = 0.055f;
            line.startColor = color;
            line.endColor = color;

            Material material = RuntimeMaterialFactory.CreateUnlit("Combat VFX material", color);
            if (material != null)
            {
                line.material = material;
            }

            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i / (float)(line.positionCount - 1) * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }

            effect.AddComponent<FadeLineEffect>().Initialize(line, lifetime, material);
        }

        private sealed class FadeLineEffect : MonoBehaviour
        {
            private LineRenderer line;
            private float lifetime;
            private float elapsed;
            private Material ownedMaterial;

            public void Initialize(LineRenderer target, float duration, Material material)
            {
                line = target;
                ownedMaterial = material;
                lifetime = Mathf.Max(0.05f, duration);
                transform.localScale = Vector3.one * 0.1f;
            }

            private void Update()
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / lifetime);
                transform.localScale = Vector3.one * Mathf.Lerp(0.1f, 1f, t);
                Color color = line.startColor;
                color.a = 1f - t;
                line.startColor = color;
                line.endColor = color;
                if (t >= 1f)
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
}
