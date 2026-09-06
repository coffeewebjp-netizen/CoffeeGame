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
        private Material facetA, facetB;
        private long attackId;

        public event Action<IceProjectile> Destroyed;
        public event Action<Health> Hit;

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
            attackId = new DamageInfo(0, damageSource, Vector3.zero, Vector3.zero).AttackId;
            CombatOwnership.Assign(gameObject, damageSource);
            CreateVisual();
        }

        private void Update()
        {
            float deltaTime = CombatClock.DeltaTime(gameObject);
            if (deltaTime <= 0f)
            {
                return;
            }
            transform.position += direction * (speed * deltaTime);
            transform.Rotate(0f, 0f, 55f * deltaTime, Space.Self);
            remainingLifetime -= deltaTime;

            Collider[] overlaps = Physics.OverlapSphere(transform.position, 0.16f, ~0, QueryTriggerInteraction.Collide);
            foreach (Collider overlap in overlaps)
            {
                Health health = overlap.GetComponentInParent<Health>();
                if (health == null || !health.IsAlive || !DamageFaction.CanDamage(source, health))
                {
                    continue;
                }

                var hit = new DamageInfo(damage, source, transform.position, direction * 0.45f, attackId);
                if (health.ApplyDamage(hit))
                {
                    Hit?.Invoke(health);
                    CombatVfxFactory.SpawnIceBurst(transform.position, direction, 0.38f, 0.24f, source);
                    CombatVfxFactory.SpawnRing(transform.position, 0.28f, IceCrystalVisuals.Frost, 0.18f, source);
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
            IceCrystalVisuals.CreateCrystal(
                transform,
                "Ice spear core",
                new Vector3(0.07f, 0.34f, 0.07f),
                IceCrystalVisuals.Core,
                out visualMaterial);

            IceCrystalVisuals.CreateCrystal(
                transform,
                "Ice spear facet a",
                new Vector3(0.045f, 0.22f, 0.045f),
                IceCrystalVisuals.Frost,
                out facetA);
            transform.GetChild(transform.childCount - 1).localRotation = Quaternion.Euler(18f, 35f, 0f);
            IceCrystalVisuals.CreateCrystal(
                transform,
                "Ice spear facet b",
                new Vector3(0.04f, 0.18f, 0.04f),
                IceCrystalVisuals.Frost,
                out facetB);
            transform.GetChild(transform.childCount - 1).localRotation = Quaternion.Euler(-16f, -40f, 12f);

            var trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.12f;
            trail.minVertexDistance = 0.02f;
            trail.startWidth = 0.028f;
            trail.endWidth = 0.002f;
            trail.numCapVertices = 0;
            trail.alignment = LineAlignment.View;
            trail.startColor = new Color(0.92f, 0.98f, 1f, 0.55f);
            trail.endColor = new Color(0.85f, 0.95f, 1f, 0f);
            if (visualMaterial != null)
            {
                trail.sharedMaterial = visualMaterial;
            }

            Light glow = gameObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(0.82f, 0.94f, 1f);
            glow.intensity = 0.7f;
            glow.range = 0.95f;
            glow.shadows = LightShadows.None;
        }

        private void OnDestroy()
        {
            if (facetA != null) Destroy(facetA);
            if (facetB != null) Destroy(facetB);
            Destroyed?.Invoke(this);
            if (visualMaterial != null)
            {
                Destroy(visualMaterial);
                visualMaterial = null;
            }
        }
    }
}
