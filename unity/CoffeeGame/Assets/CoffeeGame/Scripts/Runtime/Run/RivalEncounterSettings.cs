using UnityEngine;

namespace CoffeeGame.Run
{
    public static class RivalEncounterSettings
    {
        public const string PreferenceKey = "CoffeeGame.RivalEncounterInterval.v1";
        public const int Minimum = 1;
        public const int Maximum = 50;

        public static int Get(int defaultValue)
        {
            int fallback = Mathf.Clamp(defaultValue, Minimum, Maximum);
            return Mathf.Clamp(PlayerPrefs.GetInt(PreferenceKey, fallback), Minimum, Maximum);
        }

        public static int Set(int value)
        {
            int clamped = Mathf.Clamp(value, Minimum, Maximum);
            PlayerPrefs.SetInt(PreferenceKey, clamped);
            PlayerPrefs.Save();
            return clamped;
        }
    }
}
