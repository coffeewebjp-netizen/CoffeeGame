using System;
using UnityEngine;

namespace CoffeeGame.UI
{
    public enum GamePerformancePreset
    {
        KeepCurrent,
        Balanced1080p60,
        SmoothNative120,
        QualityNative60
    }

    public readonly struct GamePerformanceProfile
    {
        public GamePerformanceProfile(
            GamePerformancePreset preset,
            string displayName,
            string qualityName,
            int targetFrameRate,
            bool capAt1080p)
        {
            Preset = preset;
            DisplayName = displayName;
            QualityName = qualityName;
            TargetFrameRate = targetFrameRate;
            CapAt1080p = capAt1080p;
        }

        public GamePerformancePreset Preset { get; }
        public string DisplayName { get; }
        public string QualityName { get; }
        public int TargetFrameRate { get; }
        public bool CapAt1080p { get; }
    }

    /// <summary>
    /// Small local graphics preference surface. The default deliberately keeps
    /// the project's current resolution/quality untouched until the player
    /// chooses a preset in System.
    /// </summary>
    public static class GamePerformanceSettings
    {
        private const string PresetKey = "CoffeeGame.PerformancePreset.v1";
        private const string StatsKey = "CoffeeGame.ShowFrameStats.v1";

        private static readonly GamePerformanceProfile[] Profiles =
        {
            new GamePerformanceProfile(
                GamePerformancePreset.KeepCurrent,
                "現在の設定を維持",
                string.Empty,
                -1,
                false),
            new GamePerformanceProfile(
                GamePerformancePreset.Balanced1080p60,
                "バランス（最大1080p / 60fps）",
                "High",
                60,
                true),
            new GamePerformanceProfile(
                GamePerformancePreset.SmoothNative120,
                "なめらか（ネイティブ / 120fps）",
                "High",
                120,
                false),
            new GamePerformanceProfile(
                GamePerformancePreset.QualityNative60,
                "画質優先（ネイティブ / 60fps）",
                "Ultra",
                60,
                false)
        };

        public static GamePerformancePreset SelectedPreset
        {
            get
            {
                int value = PlayerPrefs.GetInt(PresetKey, (int)GamePerformancePreset.KeepCurrent);
                return Enum.IsDefined(typeof(GamePerformancePreset), value)
                    ? (GamePerformancePreset)value
                    : GamePerformancePreset.KeepCurrent;
            }
        }

        public static bool ShowFrameStats => PlayerPrefs.GetInt(StatsKey, 1) != 0;

        public static GamePerformanceProfile GetProfile(GamePerformancePreset preset)
        {
            int index = Mathf.Clamp((int)preset, 0, Profiles.Length - 1);
            return Profiles[index];
        }

        public static string CurrentPresetLabel => GetProfile(SelectedPreset).DisplayName;

        public static void ApplySavedPreset()
        {
            Apply(SelectedPreset);
        }

        public static GamePerformancePreset SelectNextPreset()
        {
            int next = ((int)SelectedPreset + 1) % Profiles.Length;
            GamePerformancePreset preset = (GamePerformancePreset)next;
            PlayerPrefs.SetInt(PresetKey, next);
            PlayerPrefs.Save();
            Apply(preset);
            return preset;
        }

        public static bool ToggleFrameStats()
        {
            bool next = !ShowFrameStats;
            PlayerPrefs.SetInt(StatsKey, next ? 1 : 0);
            PlayerPrefs.Save();
            return next;
        }

        private static void Apply(GamePerformancePreset preset)
        {
            if (preset == GamePerformancePreset.KeepCurrent || !Application.isPlaying)
            {
                return;
            }

            GamePerformanceProfile profile = GetProfile(preset);
            int qualityIndex = FindQualityLevel(profile.QualityName);
            if (qualityIndex >= 0)
            {
                QualitySettings.SetQualityLevel(qualityIndex, true);
            }

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = profile.TargetFrameRate;

            Resolution desktop = Screen.currentResolution;
            int width = Mathf.Max(640, desktop.width);
            int height = Mathf.Max(360, desktop.height);
            if (profile.CapAt1080p)
            {
                CapResolution(ref width, ref height, 1920, 1080);
            }

            Screen.SetResolution(
                width,
                height,
                FullScreenMode.FullScreenWindow,
                new RefreshRate { numerator = (uint)Mathf.Max(60, profile.TargetFrameRate), denominator = 1 });
        }

        private static int FindQualityLevel(string qualityName)
        {
            string[] names = QualitySettings.names;
            for (int index = 0; index < names.Length; index++)
            {
                if (string.Equals(names[index], qualityName, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return names.Length > 0 ? names.Length - 1 : -1;
        }

        private static void CapResolution(ref int width, ref int height, int maxWidth, int maxHeight)
        {
            float scale = Mathf.Min(1f, Mathf.Min(maxWidth / (float)width, maxHeight / (float)height));
            width = Mathf.Max(640, Mathf.RoundToInt(width * scale));
            height = Mathf.Max(360, Mathf.RoundToInt(height * scale));
        }
    }
}
