using System;
using BooTrayal.Core.Localization;
using BooTrayal.Game.Player;
using UnityEngine;

namespace BooTrayal.Game.Settings
{
    /// <summary>Player preferences (GDD 11 Settings), saved in PlayerPrefs and applied at startup and on every change.</summary>
    public static class GameSettings
    {
        const string VolumeKey = "bt.volume";
        const string SensitivityKey = "bt.sens";
        const string LanguageKey = "bt.lang";
        const string ReducedKey = "bt.reduced";

        public const float MinSensitivity = 0.03f;
        public const float MaxSensitivity = 0.4f;

        public static float Volume { get; private set; } = 0.8f;
        public static float Sensitivity { get; private set; } = 0.12f;
        public static Language Language { get; private set; } = Language.Thai;
        /// <summary>No camera shake or chromatic aberration, half vignette (GDD 6.2 motion-sickness option).</summary>
        public static bool ReducedEffects { get; private set; }

        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Load()
        {
            Volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 0.8f));
            Sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, 0.12f), MinSensitivity, MaxSensitivity);
            Language = PlayerPrefs.GetInt(LanguageKey, 0) == 1 ? Language.English : Language.Thai;
            ReducedEffects = PlayerPrefs.GetInt(ReducedKey, 0) == 1;
            Changed = null;
            Apply();
        }

        public static void SetVolume(float v) { Volume = Mathf.Clamp01(v); Save(); }
        public static void SetSensitivity(float v) { Sensitivity = Mathf.Clamp(v, MinSensitivity, MaxSensitivity); Save(); }
        public static void SetLanguage(Language l) { Language = l; Save(); }
        public static void SetReducedEffects(bool on) { ReducedEffects = on; Save(); }

        static void Save()
        {
            PlayerPrefs.SetFloat(VolumeKey, Volume);
            PlayerPrefs.SetFloat(SensitivityKey, Sensitivity);
            PlayerPrefs.SetInt(LanguageKey, Language == Language.English ? 1 : 0);
            PlayerPrefs.SetInt(ReducedKey, ReducedEffects ? 1 : 0);
            PlayerPrefs.Save();
            Apply();
            Changed?.Invoke();
        }

        static void Apply()
        {
            AudioListener.volume = Volume;
            PlayerInputReader.MouseSensitivity = Sensitivity;
        }
    }
}
