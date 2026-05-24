using System;

namespace LeiTing.Storage
{
    public static class GameStorage
    {
        private static IGameStorage backend;

        public static IGameStorage Backend
        {
            get
            {
                if (backend == null)
                {
                    backend = CreatePlatformBackend();
                }

                return backend;
            }
        }

        public static void Configure(IGameStorage storageBackend)
        {
            backend = storageBackend ?? throw new ArgumentNullException(nameof(storageBackend));
        }

        public static int GetInt(string key, int defaultValue = 0)
        {
            return Backend.GetInt(key, defaultValue);
        }

        public static void SetInt(string key, int value)
        {
            Backend.SetInt(key, value);
        }

        public static float GetFloat(string key, float defaultValue = 0f)
        {
            return Backend.GetFloat(key, defaultValue);
        }

        public static void SetFloat(string key, float value)
        {
            Backend.SetFloat(key, value);
        }

        public static string GetString(string key, string defaultValue = "")
        {
            return Backend.GetString(key, defaultValue);
        }

        public static void SetString(string key, string value)
        {
            Backend.SetString(key, value);
        }

        public static bool HasKey(string key)
        {
            return Backend.HasKey(key);
        }

        public static void DeleteKey(string key)
        {
            Backend.DeleteKey(key);
        }

        public static void DeleteAll()
        {
            Backend.DeleteAll();
        }

        public static void Save()
        {
            Backend.Save();
        }

        private static IGameStorage CreatePlatformBackend()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new DouyinFileGameStorage();
#else
            return new UnityPlayerPrefsGameStorage();
#endif
        }
    }
}
