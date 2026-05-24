#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using TTSDK;
using UnityEngine;

namespace LeiTing.Storage
{
    internal sealed class DouyinFileGameStorage : IGameStorage
    {
        private const string StorageDirectory = TTFileSystemManager.USER_DATA_PATH + "/leiting";
        private const string StorageFilePath = StorageDirectory + "/preferences.json";

        private readonly TTFileSystemManager fileSystem;
        private readonly StorageDocument document;
        private bool isDirty;

        public DouyinFileGameStorage()
        {
            fileSystem = TTFileSystemManagerWebGL.Instance;
            document = LoadDocument();
        }

        public int GetInt(string key, int defaultValue = 0)
        {
            if (TryGetValue(key, out var value))
            {
                return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
                    ? result
                    : defaultValue;
            }

            return defaultValue;
        }

        public void SetInt(string key, int value)
        {
            SetValue(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public float GetFloat(string key, float defaultValue = 0f)
        {
            if (TryGetValue(key, out var value))
            {
                return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
                    ? result
                    : defaultValue;
            }

            return defaultValue;
        }

        public void SetFloat(string key, float value)
        {
            SetValue(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public string GetString(string key, string defaultValue = "")
        {
            if (TryGetValue(key, out var value))
            {
                return value;
            }

            return defaultValue;
        }

        public void SetString(string key, string value)
        {
            SetValue(key, value ?? string.Empty);
        }

        public bool HasKey(string key)
        {
            return TryGetValue(key, out _);
        }

        public void DeleteKey(string key)
        {
            var index = FindEntryIndex(key);
            if (index >= 0)
            {
                document.entries.RemoveAt(index);
                isDirty = true;
            }
        }

        public void DeleteAll()
        {
            if (document.entries.Count > 0)
            {
                document.entries.Clear();
                isDirty = true;
            }
        }

        public void Save()
        {
            if (!isDirty)
            {
                return;
            }

            try
            {
                if (!fileSystem.AccessSync(StorageDirectory))
                {
                    var directoryError = fileSystem.MkdirSync(StorageDirectory, true);
                    if (!string.IsNullOrEmpty(directoryError))
                    {
                        Debug.LogWarning($"[Storage] Failed to create Douyin save directory: {directoryError}");
                        return;
                    }
                }

                var error = fileSystem.WriteFileSync(StorageFilePath, JsonUtility.ToJson(document), "utf8");
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogWarning($"[Storage] Failed to save Douyin data: {error}");
                    return;
                }

                isDirty = false;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Storage] Failed to save Douyin data: {exception.Message}");
            }
        }

        private StorageDocument LoadDocument()
        {
            try
            {
                if (!fileSystem.AccessSync(StorageFilePath))
                {
                    return new StorageDocument();
                }

                var json = fileSystem.ReadFileSync(StorageFilePath, "utf8");
                if (string.IsNullOrEmpty(json))
                {
                    return new StorageDocument();
                }

                var storedDocument = JsonUtility.FromJson<StorageDocument>(json);
                if (storedDocument == null)
                {
                    return new StorageDocument();
                }

                if (storedDocument.entries == null)
                {
                    storedDocument.entries = new List<StorageEntry>();
                }

                return storedDocument;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Storage] Failed to load Douyin data: {exception.Message}");
                return new StorageDocument();
            }
        }

        private bool TryGetValue(string key, out string value)
        {
            var index = FindEntryIndex(key);
            if (index >= 0)
            {
                value = document.entries[index].value;
                return true;
            }

            value = null;
            return false;
        }

        private void SetValue(string key, string value)
        {
            var index = FindEntryIndex(key);
            if (index >= 0)
            {
                if (document.entries[index].value == value)
                {
                    return;
                }

                document.entries[index].value = value;
            }
            else
            {
                document.entries.Add(new StorageEntry
                {
                    key = key,
                    value = value
                });
            }

            isDirty = true;
        }

        private int FindEntryIndex(string key)
        {
            for (var index = 0; index < document.entries.Count; index++)
            {
                if (document.entries[index].key == key)
                {
                    return index;
                }
            }

            return -1;
        }

        [Serializable]
        private sealed class StorageDocument
        {
            public List<StorageEntry> entries = new List<StorageEntry>();
        }

        [Serializable]
        private sealed class StorageEntry
        {
            public string key;
            public string value;
        }
    }
}
#endif
