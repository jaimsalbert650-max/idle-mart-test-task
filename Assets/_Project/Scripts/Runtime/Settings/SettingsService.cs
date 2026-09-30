using System;
using UnityEngine;

namespace IdleMart.Settings
{
    /// <summary>Player settings stored in PlayerPrefs and applied immediately.</summary>
    public static class SettingsService
    {
        private const string MusicKey = "settings.music";
        private const string SfxKey = "settings.sfx";
        private const string QualityKey = "settings.quality";
        private const string FullscreenKey = "settings.fullscreen";

        public static event Action Changed;

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(MusicKey, 0.5f);
            set => Set(MusicKey, Mathf.Clamp01(value));
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(SfxKey, 0.8f);
            set => Set(SfxKey, Mathf.Clamp01(value));
        }

        public static int QualityLevel
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1);
            set
            {
                PlayerPrefs.SetInt(QualityKey, value);
                QualitySettings.SetQualityLevel(QualityLevel, true);
                Changed?.Invoke();
            }
        }

        public static bool Fullscreen
        {
            get => PlayerPrefs.GetInt(FullscreenKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
                Screen.fullScreen = value;
                Changed?.Invoke();
            }
        }

        /// <summary>Applies stored values at startup.</summary>
        public static void ApplyAll()
        {
            QualitySettings.SetQualityLevel(QualityLevel, true);
            Screen.fullScreen = Fullscreen;
            Changed?.Invoke();
        }

        private static void Set(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            Changed?.Invoke();
        }
    }
}
