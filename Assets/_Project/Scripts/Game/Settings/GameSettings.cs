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
        const string VoiceModeKey = "bt.voice";
        const string VoiceVolumeKey = "bt.voicevol";

        public const float MinSensitivity = 0.03f;
        public const float MaxSensitivity = 0.4f;

        public static float Volume { get; private set; } = 0.8f;
        public static float Sensitivity { get; private set; } = 0.12f;
        public static Language Language { get; private set; } = Language.Thai;
        /// <summary>No camera shake or chromatic aberration, half vignette (GDD 6.2 motion-sickness option).</summary>
        public static bool ReducedEffects { get; private set; }
        /// <summary>Open mic (default) or push-to-talk on V (GDD 5).</summary>
        public static bool PushToTalk { get; private set; }
        public static float VoiceVolume { get; private set; } = 0.8f;

        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Load()
        {
            Volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 0.8f));
            Sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, 0.12f), MinSensitivity, MaxSensitivity);
            Language = PlayerPrefs.GetInt(LanguageKey, 0) == 1 ? Language.English : Language.Thai;
            ReducedEffects = PlayerPrefs.GetInt(ReducedKey, 0) == 1;
            PushToTalk = PlayerPrefs.GetInt(VoiceModeKey, 0) == 1;
            VoiceVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(VoiceVolumeKey, 0.8f));
            Changed = null;
            Apply();
        }

        public static void SetVolume(float v) { Volume = Mathf.Clamp01(v); Save(); }
        public static void SetSensitivity(float v) { Sensitivity = Mathf.Clamp(v, MinSensitivity, MaxSensitivity); Save(); }
        public static void SetLanguage(Language l) { Language = l; Save(); }
        public static void SetReducedEffects(bool on) { ReducedEffects = on; Save(); }
        public static void SetPushToTalk(bool on) { PushToTalk = on; Save(); }
        public static void SetVoiceVolume(float v) { VoiceVolume = Mathf.Clamp01(v); Save(); }

        static void Save()
        {
            PlayerPrefs.SetFloat(VolumeKey, Volume);
            PlayerPrefs.SetFloat(SensitivityKey, Sensitivity);
            PlayerPrefs.SetInt(LanguageKey, Language == Language.English ? 1 : 0);
            PlayerPrefs.SetInt(ReducedKey, ReducedEffects ? 1 : 0);
            PlayerPrefs.SetInt(VoiceModeKey, PushToTalk ? 1 : 0);
            PlayerPrefs.SetFloat(VoiceVolumeKey, VoiceVolume);
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
