using UnityEngine;

namespace CoffeeGame.Combat
{
    public static class CombatClock
    {
        public static float DeltaTime(GameObject owner)
        {
            if (IsPaused || IsFrozen(owner))
            {
                return 0f;
            }

            return UnityEngine.Time.deltaTime;
        }

        public static float Time(GameObject owner)
        {
            TimeStopController controller = TimeStopController.Instance;
            return controller != null ? controller.GetClockTime(owner) : UnityEngine.Time.time;
        }

        public static float WorldDeltaTime => DeltaTime(null);
        public static float WorldTime => Time(null);

        public static bool IsPaused
        {
            get
            {
                TimeStopController controller = TimeStopController.Instance;
                return (controller != null && controller.IsPaused) || UnityEngine.Time.timeScale <= 0f;
            }
        }

        private static bool IsFrozen(GameObject owner)
        {
            TimeStopController controller = TimeStopController.Instance;
            return controller != null && controller.IsActive && controller.IsFrozen(owner);
        }
    }
}
