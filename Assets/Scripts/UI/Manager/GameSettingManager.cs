using LeiTing.Storage;

namespace LeiTing.UI
{
    public static class GameSettingManager
    {
        private const string MusicKey = "setting_music";
        private const string SoundKey = "setting_sound";
        private const string VibrationKey = "setting_vibration";

        public static bool MusicEnabled
        {
            get => GameStorage.GetInt(MusicKey, 1) == 1;
            set => SetBool(MusicKey, value);
        }

        public static bool SoundEnabled
        {
            get => GameStorage.GetInt(SoundKey, 1) == 1;
            set => SetBool(SoundKey, value);
        }

        public static bool VibrationEnabled
        {
            get => GameStorage.GetInt(VibrationKey, 1) == 1;
            set => SetBool(VibrationKey, value);
        }

        private static void SetBool(string key, bool value)
        {
            GameStorage.SetInt(key, value ? 1 : 0);
            GameStorage.Save();
        }
    }
}
