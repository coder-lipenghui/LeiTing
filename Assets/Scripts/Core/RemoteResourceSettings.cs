using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LeiTing.Core
{
    [Serializable]
    public sealed class RemoteResourceSettingsData
    {
        public const string EditorModeLocal = "Local";
        public const string EditorModeCdn = "CDN";

        public bool enabled = true;
        public bool simulateInEditor;
        public string editorResourceMode = EditorModeLocal;
        public string cdnBaseUrl = string.Empty;
        public string localBundleRoot = "CDNResources/WebGL";
        public string version = string.Empty;
        public List<RemoteResourceBundleInfo> bundles = new List<RemoteResourceBundleInfo>();

        public bool HasBundles => bundles != null && bundles.Count > 0;

        public bool UsesCdnInEditor
        {
            get
            {
                return string.Equals(editorResourceMode, EditorModeCdn, StringComparison.OrdinalIgnoreCase)
                    || simulateInEditor;
            }
        }

        public bool ShouldLoadAtStartup
        {
            get
            {
                if (!enabled || !HasBundles)
                {
                    return false;
                }

#if UNITY_EDITOR
                return UsesCdnInEditor;
#else
                return true;
#endif
            }
        }

        public string ResolveBundleUrl(RemoteResourceBundleInfo bundle)
        {
            if (bundle == null)
            {
                return string.Empty;
            }

            var fileName = !string.IsNullOrWhiteSpace(bundle.fileName) ? bundle.fileName : bundle.name;
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return string.Empty;
            }

#if UNITY_EDITOR
            if (UsesCdnInEditor)
            {
                return !string.IsNullOrWhiteSpace(cdnBaseUrl)
                    ? CombineUrl(cdnBaseUrl, fileName)
                    : string.Empty;
            }
#endif

            if (!string.IsNullOrWhiteSpace(cdnBaseUrl))
            {
                return CombineUrl(cdnBaseUrl, fileName);
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            return fileName;
#else
            if (!string.IsNullOrWhiteSpace(localBundleRoot))
            {
                return ToFileUrl(ResolveLocalPath(localBundleRoot, fileName));
            }

            return fileName;
#endif
        }

        private static string ResolveLocalPath(string root, string fileName)
        {
            var normalizedRoot = root.Replace("\\", "/").Trim();
            if (Path.IsPathRooted(normalizedRoot))
            {
                return Path.Combine(normalizedRoot, fileName);
            }

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, normalizedRoot, fileName);
        }

        private static string ToFileUrl(string path)
        {
            var normalized = path.Replace("\\", "/");
            if (normalized.Contains("://"))
            {
                return normalized;
            }

            return "file:///" + normalized.TrimStart('/');
        }

        private static string CombineUrl(string root, string fileName)
        {
            var normalizedRoot = root.Replace("\\", "/").TrimEnd('/');
            var normalizedFileName = fileName.Replace("\\", "/").TrimStart('/');
            return $"{normalizedRoot}/{normalizedFileName}";
        }
    }

    [Serializable]
    public sealed class RemoteResourceBundleInfo
    {
        public string name;
        public string fileName;
        public string hash;
        public long crc;
        public long size;
    }

    public static class RemoteResourceSettings
    {
        public const string ResourcePath = "RemoteResourceSettings";
        public const string DefaultBundleName = "leiting_runtime_assets";
        public const string DefaultLocalBundleRoot = "CDNResources/WebGL";

        private static RemoteResourceSettingsData cachedSettings;

        public static RemoteResourceSettingsData Settings
        {
            get
            {
                if (cachedSettings == null)
                {
                    cachedSettings = Load();
                }

                return cachedSettings;
            }
        }

        public static void ClearCache()
        {
            cachedSettings = null;
        }

        private static RemoteResourceSettingsData Load()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
            {
                return CreateDisabledSettings();
            }

            try
            {
                var settings = JsonUtility.FromJson<RemoteResourceSettingsData>(asset.text);
                if (settings == null)
                {
                    return CreateDisabledSettings();
                }

                settings.bundles = settings.bundles ?? new List<RemoteResourceBundleInfo>();
                if (string.IsNullOrWhiteSpace(settings.editorResourceMode))
                {
                    settings.editorResourceMode = RemoteResourceSettingsData.EditorModeLocal;
                }

                return settings;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Remote resource settings parse failed: {exception.Message}");
                return CreateDisabledSettings();
            }
        }

        public static RemoteResourceSettingsData CreateDefaultSettings()
        {
            return new RemoteResourceSettingsData
            {
                enabled = true,
                simulateInEditor = false,
                editorResourceMode = RemoteResourceSettingsData.EditorModeLocal,
                cdnBaseUrl = string.Empty,
                localBundleRoot = DefaultLocalBundleRoot,
                version = string.Empty,
                bundles = new List<RemoteResourceBundleInfo>()
            };
        }

        private static RemoteResourceSettingsData CreateDisabledSettings()
        {
            var settings = CreateDefaultSettings();
            settings.enabled = false;
            return settings;
        }
    }
}
