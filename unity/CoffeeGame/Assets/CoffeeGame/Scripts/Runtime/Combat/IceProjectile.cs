using System;
using CoffeeGame.Actors;
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
            transform.Rotate(0f, 0f, 140f * deltaTime, Space.Self);
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
                    CombatVfxFactory.SpawnIceBurst(transform.position, direction, 0.38f, 0.24f);
                    CombatVfxFactory.SpawnRing(transform.position, 0.28f, IceCrystalVisuals.Frost, 0.18f);
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
            transform.rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(90f, 0f, 0f);
            GameObject visual = IceCrystalVisuals.CreateCrystal(
                transform,
                "Ice crystal visual",
                new Vector3(0.055f, 0.22f, 0.055f),
                IceCrystalVisuals.Core,
                out visualMaterial);

            GameObject halo = IceCrystalVisuals.CreateCrystal(
                visual.transform,
                "Ice crystal halo",
                new Vector3(1.35f, 0.72f, 1.35f),
                new Color(0.72f, 0.9f, 1f, 0.35f),
                out Material haloMaterial);
            if (haloMaterial != null)
            {
                // Halo material is owned by the child; destroy with the projectile material.
                Destroy(haloMaterial, remainingLifetime + 0.1f);
            }

            var trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.16f;
            trail.minVertexDistance = 0.02f;
            trail.startWidth = 0.045f;
            trail.endWidth = 0.004f;
            trail.numCapVertices = 0;
            trail.alignment = LineAlignment.View;
            trail.startColor = new Color(0.88f, 0.97f, 1f, 0.7f);
            trail.endColor = new Color(0.7f, 0.88f, 1f, 0f);
            if (visualMaterial != null)
            {
                trail.sharedMaterial = visualMaterial;
            }

            Light glow = gameObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(0.72f, 0.9f, 1f);
            glow.intensity = 0.85f;
            glow.range = 1.1f;
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
