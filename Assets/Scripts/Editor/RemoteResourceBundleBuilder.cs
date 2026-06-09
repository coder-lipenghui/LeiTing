using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LeiTing.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LeiTing.Editor
{
    public sealed class RemoteResourceBundleBuilder : IPreprocessBuildWithReport
    {
        private const string SettingsPath = "Assets/Resources/RemoteResourceSettings.json";

        public int callbackOrder => -1100;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!IsRemoteResourceBuildEnabled())
            {
                return;
            }

            BuildRemoteBundles(report.summary.platform);
        }

        [MenuItem("LeiTing/Build/Build CDN Runtime Bundles")]
        public static void BuildWebGLRemoteBundles()
        {
            BuildRemoteBundles(BuildTarget.WebGL);
        }

        public static bool IsRemoteResourceBuildEnabled()
        {
            return LoadSettings().enabled;
        }

        public static void BuildRemoteBundles(BuildTarget buildTarget)
        {
            var settings = LoadSettings();
            if (!settings.enabled)
            {
                Debug.Log("Remote resource bundles skipped because RemoteResourceSettings.enabled is false.");
                return;
            }

            var collection = RuntimeAssetCatalogBuilder.CollectRuntimeAssetPaths();
            if (collection == null)
            {
                return;
            }

            var assetPaths = collection.AllPaths
                .Where(path => AssetDatabase.LoadMainAssetAtPath(path) != null)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (assetPaths.Length == 0)
            {
                Debug.LogWarning("Remote resource bundle build skipped because no runtime assets were collected.");
                return;
            }

            var outputDirectory = ResolveOutputDirectory(settings);
            Directory.CreateDirectory(outputDirectory);

            var bundleName = RuntimeAssetCatalogBuilder.RuntimeBundleName;
            var builds = new[]
            {
                new AssetBundleBuild
                {
                    assetBundleName = bundleName,
                    assetNames = assetPaths
                }
            };

            var manifest = BuildPipeline.BuildAssetBundles(
                outputDirectory,
                builds,
                BuildAssetBundleOptions.ChunkBasedCompression,
                buildTarget);

            if (manifest == null)
            {
                Debug.LogError("Remote resource bundle build failed.");
                return;
            }

            var bundlePath = Path.Combine(outputDirectory, bundleName);
            if (!File.Exists(bundlePath))
            {
                Debug.LogError($"Remote resource bundle missing after build: {bundlePath}");
                return;
            }

            BuildPipeline.GetCRCForAssetBundle(bundlePath, out var crc);
            var bundleHash = manifest.GetAssetBundleHash(bundleName).ToString();
            var hashedFileName = $"{bundleName}_{bundleHash}";
            var hashedBundlePath = Path.Combine(outputDirectory, hashedFileName);
            File.Copy(bundlePath, hashedBundlePath, true);
            var bundleSize = new FileInfo(hashedBundlePath).Length;

            settings.version = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            settings.localBundleRoot = string.IsNullOrWhiteSpace(settings.localBundleRoot)
                ? RemoteResourceSettings.DefaultLocalBundleRoot
                : settings.localBundleRoot;
            settings.bundles = new List<RemoteResourceBundleInfo>
            {
                new RemoteResourceBundleInfo
                {
                    name = bundleName,
                    fileName = hashedFileName,
                    hash = bundleHash,
                    crc = crc,
                    size = bundleSize
                }
            };

            SaveSettings(settings);
            RuntimeAssetCatalogBuilder.RebuildCatalog(false);
            AssetDatabase.Refresh();

            Debug.Log($"Remote resource bundle built: {hashedBundlePath} ({bundleSize} bytes, {assetPaths.Length} assets). Upload this file to the CDN base url configured in {SettingsPath}.");
        }

        internal static RemoteResourceSettingsData LoadSettings()
        {
            if (!File.Exists(SettingsPath))
            {
                return RemoteResourceSettings.CreateDefaultSettings();
            }

            try
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonUtility.FromJson<RemoteResourceSettingsData>(json);
                if (settings == null)
                {
                    return RemoteResourceSettings.CreateDefaultSettings();
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
                Debug.LogWarning($"Remote resource settings read failed: {exception.Message}");
                return RemoteResourceSettings.CreateDefaultSettings();
            }
        }

        internal static void SaveSettings(RemoteResourceSettingsData settings)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            File.WriteAllText(SettingsPath, JsonUtility.ToJson(settings, true));
            RemoteResourceSettings.ClearCache();
            AssetDatabase.ImportAsset(SettingsPath);
        }

        private static string ResolveOutputDirectory(RemoteResourceSettingsData settings)
        {
            var root = string.IsNullOrWhiteSpace(settings.localBundleRoot)
                ? RemoteResourceSettings.DefaultLocalBundleRoot
                : settings.localBundleRoot;
            if (Path.IsPathRooted(root))
            {
                return root;
            }

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, root);
        }
    }
}
