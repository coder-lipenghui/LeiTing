using UnityEngine;

namespace LeiTing.UI
{
    public static class GameSettingManager
    {
        private const string MusicKey = "setting_music";
        private const string SoundKey = "setting_sound";
        private const string VibrationKey = "setting_vibration";

        public static bool MusicEnabled
        {
            get => PlayerPrefs.GetInt(MusicKey, 1) == 1;
            set => SetBool(MusicKey, value);
        }

        public static bool SoundEnabled
        {
            get => PlayerPrefs.GetInt(SoundKey, 1) == 1;
            set => SetBool(SoundKey, value);
        }

        public static bool VibrationEnabled
        {
            get => PlayerPrefs.GetInt(VibrationKey, 1) == 1;
            set => SetBool(VibrationKey, value);
        }

        private static void SetBool(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
