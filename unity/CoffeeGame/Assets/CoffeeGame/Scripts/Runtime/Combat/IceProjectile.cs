using System;
using CoffeeGame.Actors;
using CoffeeGame.Presentation;
using UnityEngine;

namespace CoffeeGame.Combat
{
    [DisallowMultipleComponent]
    public sealed class IceProjectile : MonoBehaviour
    {
        private Vector3 direction;
        private int damage;
        private float speed;
        private float remainingLifetime;
        private GameObject source;
        private Material visualMaterial;

        public event Action<IceProjectile> Destroyed;

        public void Initialize(Vector3 worldDirection, int damageAmount, float movementSpeed, GameObject damageSource)
        {
            direction = Vector3.ProjectOnPlane(worldDirection, Vector3.up).normalized;
            if (direction.sqrMagnitude < 0.01f)
            {
                direction = Vector3.forward;
            }
            damage = Mathf.Max(0, damageAmount);
            speed = Mathf.Max(0.1f, movementSpeed);
            remainingLifetime = 2.8f;
            source = damageSource;
            CreateVisual();
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            transform.position += direction * (speed * deltaTime);
            transform.Rotate(direction, 240f * deltaTime, Space.World);
            remainingLifetime -= deltaTime;

            Collider[] overlaps = Physics.OverlapSphere(transform.position, 0.16f, ~0, QueryTriggerInteraction.Collide);
            foreach (Collider overlap in overlaps)
            {
                Health health = overlap.GetComponentInParent<Health>();
                if (health == null || !health.IsAlive || health.gameObject == source)
                {
                    continue;
                }

                var hit = new DamageInfo(damage, source, transform.position, direction * 0.45f);
                if (health.ApplyDamage(hit))
                {
                    CombatVfxFactory.SpawnRing(transform.position, 0.32f, new Color(0.45f, 0.9f, 1f), 0.22f);
                    Destroy(gameObject);
                    return;
                }
            }

            if (remainingLifetime <= 0f)
            {
                Destroy(gameObject);
            }
        }

        private void CreateVisual()
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Ice crystal visual";
            visual.transform.SetParent(transform, false);
            visual.transform.localScale = new Vector3(0.14f, 0.32f, 0.14f);
            visual.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            Collider visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
            {
                Destroy(visualCollider);
            }

            Renderer renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                Color ice = new Color(0.35f, 0.88f, 1f, 1f);
                Material material = RuntimeMaterialFactory.CreateUnlit("Ice crystal material", ice);
                if (material != null)
                {
                    visualMaterial = material;
                    renderer.sharedMaterial = visualMaterial;
                }
            }

            var trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.18f;
            trail.minVertexDistance = 0.025f;
            trail.startWidth = 0.13f;
            trail.endWidth = 0.012f;
            trail.numCapVertices = 3;
            trail.startColor = new Color(0.72f, 0.97f, 1f, 0.88f);
            trail.endColor = new Color(0.28f, 0.72f, 1f, 0f);
            if (visualMaterial != null)
            {
                trail.sharedMaterial = visualMaterial;
            }

            Light glow = gameObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(0.35f, 0.82f, 1f);
            glow.intensity = 1.25f;
            glow.range = 1.35f;
            glow.shadows = LightShadows.None;
        }

        private void OnDestroy()
        {
            Destroyed?.Invoke(this);
            if (visualMaterial != null)
            {
                Destroy(visualMaterial);
                visualMaterial = null;
            }
        }
    }
}
