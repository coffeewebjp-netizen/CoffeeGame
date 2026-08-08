using System;
using UnityEngine;

namespace CoffeeGame.Actors
{
    [DisallowMultipleComponent]
    public sealed class PlayerResources : MonoBehaviour
    {
        public event Action Changed;

        public float Stamina { get; private set; }
        public float MaxStamina { get; private set; }
        public float MagicPoints { get; private set; }
        public float MaxMagicPoints { get; private set; }
        public float MagicRegenPerSecond { get; private set; }

        public void Initialize(float maxStamina, float maxMagicPoints, float magicRegenPerSecond)
        {
            MaxStamina = Mathf.Max(1f, maxStamina);
            MaxMagicPoints = Mathf.Max(0f, maxMagicPoints);
            MagicRegenPerSecond = Mathf.Max(0f, magicRegenPerSecond);
            Stamina = 0f;
            MagicPoints = MaxMagicPoints;
            Changed?.Invoke();
        }

        public void Tick(float deltaTime)
        {
            if (MagicPoints >= MaxMagicPoints || MagicRegenPerSecond <= 0f)
            {
                return;
            }

            float before = MagicPoints;
            MagicPoints = Mathf.Min(MaxMagicPoints, MagicPoints + MagicRegenPerSecond * Mathf.Max(0f, deltaTime));
            if (!Mathf.Approximately(before, MagicPoints))
            {
                Changed?.Invoke();
            }
        }

        public bool TrySpendStamina(float amount)
        {
            float cost = Mathf.Max(0f, amount);
            if (Stamina + 0.001f < cost)
            {
                return false;
            }

            Stamina = Mathf.Max(0f, Stamina - cost);
            Changed?.Invoke();
            return true;
        }

        public bool TrySpendMagic(float amount)
        {
            float cost = Mathf.Max(0f, amount);
            if (MagicPoints + 0.001f < cost)
            {
                return false;
            }

            MagicPoints = Mathf.Max(0f, MagicPoints - cost);
            Changed?.Invoke();
            return true;
        }

        public void GainStamina(float amount)
        {
            float before = Stamina;
            Stamina = Mathf.Min(MaxStamina, Stamina + Mathf.Max(0f, amount));
            if (!Mathf.Approximately(before, Stamina))
            {
                Changed?.Invoke();
            }
        }

        public void IncreaseMagicMaximum(float amount, bool refill)
        {
            float delta = Mathf.Max(0f, amount);
            MaxMagicPoints += delta;
            MagicPoints = refill ? MaxMagicPoints : Mathf.Min(MaxMagicPoints, MagicPoints);
            Changed?.Invoke();
        }

        public void Refill()
        {
            Stamina = 0f;
            MagicPoints = MaxMagicPoints;
            Changed?.Invoke();
        }
    }
}

