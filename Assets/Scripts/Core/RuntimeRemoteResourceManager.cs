using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace LeiTing.Core
{
    public static class RuntimeRemoteResourceManager
    {
        private enum LoadState
        {
            NotStarted,
            Loading,
            Ready,
            Failed
        }

        private sealed class LoadedBundle
        {
            public AssetBundle bundle;
            public readonly Dictionary<string, string> assetNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public LoadedBundle(AssetBundle bundle)
            {
                this.bundle = bundle;

                if (bundle == null)
                {
                    return;
                }

                foreach (var assetName in bundle.GetAllAssetNames())
                {
                    var key = NormalizeAssetKey(assetName);
                    if (!assetNames.ContainsKey(key))
                    {
                        assetNames.Add(key, assetName);
                    }
                }
            }
        }

        private static readonly Dictionary<string, LoadedBundle> loadedBundles =
            new Dictionary<string, LoadedBundle>(StringComparer.OrdinalIgnoreCase);

        private static LoadState state = LoadState.NotStarted;
        private static float lastProgress;
        private static string lastStatus = string.Empty;
        private static string lastError = string.Empty;
        private static string lastRuntimeAssetLoadError = string.Empty;

        public static event Action<string> RuntimeAssetLoadFailed;

        public static bool NeedsStartupDownload => ShouldUseRemoteBundles && state != LoadState.Ready;

        public static bool IsReady => !ShouldUseRemoteBundles || state == LoadState.Ready;

        public static bool IsLoading => state == LoadState.Loading;

        public static bool HasDownloadFailed => state == LoadState.Failed;

        private static bool ShouldUseRemoteBundles => RemoteResourceSettings.Settings.ShouldLoadAtStartup;

        public static bool CanUseEditorLocalAssets
        {
            get
            {
#if UNITY_EDITOR
                return !RemoteResourceSettings.Settings.UsesCdnInEditor;
#else
                return false;
#endif
            }
        }

        public static bool RequiresRemoteAssetLoad => ShouldUseRemoteBundles;

        public static bool CanUseResourcesFallback => !RequiresRemoteAssetLoad;

        public static bool ClearLocalCache(bool unloadAllLoadedObjects = true)
        {
            if (state == LoadState.Loading)
            {
                Debug.LogWarning("CDN local cache clear skipped because remote resources are currently loading.");
                return false;
            }

            UnloadLoadedBundles(unloadAllLoadedObjects);
            state = LoadState.NotStarted;
            lastProgress = 0f;
            lastStatus = string.Empty;
            lastError = string.Empty;
            lastRuntimeAssetLoadError = string.Empty;

#if UNITY_WEBGL && !UNITY_EDITOR
            return true;
#else
            var cleared = Caching.ClearCache();
            if (!cleared)
            {
                Debug.LogWarning("Unity cache could not be fully cleared. Some cached files may still be in use.");
            }

            return cleared;
#endif
        }

        public static IEnumerator EnsureReady(Action<float, string> onProgress, Action<bool, string> onComplete = null)
        {
            if (!ShouldUseRemoteBundles)
            {
                state = LoadState.Ready;
                ReportProgress(onProgress, 1f, "READY");
                onComplete?.Invoke(true, string.Empty);
                yield break;
            }

            if (state == LoadState.Ready)
            {
                ReportProgress(onProgress, 1f, "READY");
                onComplete?.Invoke(true, string.Empty);
                yield break;
            }

            if (state == LoadState.Loading)
            {
                while (state == LoadState.Loading)
                {
                    onProgress?.Invoke(lastProgress, lastStatus);
                    yield return null;
                }

                onComplete?.Invoke(state == LoadState.Ready, lastError);
                yield break;
            }

            state = LoadState.Loading;
            lastError = string.Empty;
            loadedBundles.Clear();

            var settings = RemoteResourceSettings.Settings;
            var bundles = settings.bundles;
            var bundleCount = bundles != null ? bundles.Count : 0;

            for (var index = 0; index < bundleCount; index++)
            {
                var bundleInfo = bundles[index];
                var bundleName = !string.IsNullOrWhiteSpace(bundleInfo.name)
                    ? bundleInfo.name
                    : RemoteResourceSettings.DefaultBundleName;
                var url = settings.ResolveBundleUrl(bundleInfo);

                if (string.IsNullOrWhiteSpace(url))
                {
                    Fail($"Remote bundle url is empty: {bundleName}", onProgress, onComplete);
                    yield break;
                }

                using (var request = CreateBundleRequest(url, ToCrc(bundleInfo.crc)))
                {
                    var operation = request.SendWebRequest();
                    while (!operation.isDone)
                    {
                        var progress = (index + Mathf.Clamp01(request.downloadProgress)) / Mathf.Max(1, bundleCount);
                        ReportProgress(onProgress, progress, $"DOWNLOADING {index + 1}/{bundleCount}");
                        yield return null;
                    }

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Fail($"Remote bundle download failed: {url}. {request.error}", onProgress, onComplete);
                        yield break;
                    }

                    var assetBundle = GetAssetBundle(request);
                    if (assetBundle == null)
                    {
                        Fail($"Remote bundle load failed: {url}", onProgress, onComplete);
                        yield break;
                    }

                    loadedBundles[bundleName] = new LoadedBundle(assetBundle);
                }

                ReportProgress(onProgress, (index + 1f) / Mathf.Max(1, bundleCount), $"LOADED {index + 1}/{bundleCount}");
                yield return null;
            }

            state = LoadState.Ready;
            ReportProgress(onProgress, 1f, "READY");
            onComplete?.Invoke(true, string.Empty);
        }

        public static TAsset LoadAsset<TAsset>(string bundleName, string assetName)
            where TAsset : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(assetName))
            {
                return null;
            }

            var resolvedBundleName = !string.IsNullOrWhiteSpace(bundleName)
                ? bundleName
                : RemoteResourceSettings.DefaultBundleName;
            if (!IsReady)
            {
                ReportRuntimeAssetLoadFailure($"CDN asset requested before resources were ready: {resolvedBundleName}/{assetName}");
                return null;
            }

            if (loadedBundles.Count == 0)
            {
                ReportRuntimeAssetLoadFailure($"CDN asset bundle cache is empty while loading: {resolvedBundleName}/{assetName}");
                return null;
            }

            if (!loadedBundles.TryGetValue(resolvedBundleName, out var loadedBundle) || loadedBundle?.bundle == null)
            {
                ReportRuntimeAssetLoadFailure($"CDN asset bundle is not loaded: {resolvedBundleName}");
                return null;
            }

            var resolvedAssetName = ResolveAssetName(loadedBundle, assetName);
            if (string.IsNullOrWhiteSpace(resolvedAssetName))
            {
                ReportRuntimeAssetLoadFailure($"CDN asset is missing from bundle {resolvedBundleName}: {assetName}");
                return null;
            }

            try
            {
                var asset = loadedBundle.bundle.LoadAsset<TAsset>(resolvedAssetName);
                if (asset == null)
                {
                    asset = LoadSubAsset<TAsset>(loadedBundle.bundle, resolvedAssetName);
                }

                if (asset == null)
                {
                    ReportRuntimeAssetLoadFailure($"CDN asset returned null: {resolvedBundleName}/{resolvedAssetName}");
                }

                return asset;
            }
            catch (Exception exception)
            {
                ReportRuntimeAssetLoadFailure($"CDN asset load failed: {resolvedBundleName}/{resolvedAssetName}. {exception.Message}");
                return null;
            }
        }

        private static TAsset LoadSubAsset<TAsset>(AssetBundle bundle, string assetName)
            where TAsset : UnityEngine.Object
        {
            if (bundle == null || string.IsNullOrWhiteSpace(assetName))
            {
                return null;
            }

            var subAssets = bundle.LoadAssetWithSubAssets<TAsset>(assetName);
            if (subAssets == null)
            {
                return null;
            }

            foreach (var subAsset in subAssets)
            {
                if (subAsset != null)
                {
                    return subAsset;
                }
            }

            return null;
        }

        private static UnityWebRequest CreateBundleRequest(string url, uint crc)
        {
            return crc != 0
                ? UnityWebRequestAssetBundle.GetAssetBundle(url, crc)
                : UnityWebRequestAssetBundle.GetAssetBundle(url);
        }

        private static uint ToCrc(long crc)
        {
            if (crc <= 0)
            {
                return 0;
            }

            return crc > uint.MaxValue ? uint.MaxValue : (uint)crc;
        }

        private static AssetBundle GetAssetBundle(UnityWebRequest request)
        {
            return DownloadHandlerAssetBundle.GetContent(request);
        }

        private static void UnloadLoadedBundles(bool unloadAllLoadedObjects)
        {
            foreach (var loadedBundle in loadedBundles.Values)
            {
                if (loadedBundle?.bundle == null)
                {
                    continue;
                }

                loadedBundle.bundle.Unload(unloadAllLoadedObjects);
            }

            loadedBundles.Clear();
        }

        private static string ResolveAssetName(LoadedBundle loadedBundle, string assetName)
        {
            if (loadedBundle.assetNames.TryGetValue(NormalizeAssetKey(assetName), out var resolvedName))
            {
                return resolvedName;
            }

            return assetName;
        }

        private static string NormalizeAssetKey(string assetName)
        {
            if (string.IsNullOrWhiteSpace(assetName))
            {
                return string.Empty;
            }

            var normalized = assetName.Replace("\\", "/").Trim();
            var extensionIndex = normalized.LastIndexOf(".", StringComparison.Ordinal);
            if (extensionIndex >= 0)
            {
                normalized = normalized.Substring(0, extensionIndex);
            }

            return normalized;
        }

        private static void ReportProgress(Action<float, string> onProgress, float progress, string status)
        {
            lastProgress = Mathf.Clamp01(progress);
            lastStatus = status ?? string.Empty;
            onProgress?.Invoke(lastProgress, lastStatus);
        }

        private static void Fail(string message, Action<float, string> onProgress, Action<bool, string> onComplete)
        {
            lastError = message;
            state = LoadState.Failed;
            Debug.LogError(message);
            ReportProgress(onProgress, lastProgress, "FAILED");
            onComplete?.Invoke(false, message);
        }

        public static void ReportRuntimeAssetLoadFailure(string message)
        {
            if (!RequiresRemoteAssetLoad || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            Debug.LogError(message);
            if (string.Equals(lastRuntimeAssetLoadError, message, StringComparison.Ordinal))
            {
                return;
            }

            lastRuntimeAssetLoadError = message;
            RuntimeAssetLoadFailed?.Invoke(message);
        }
    }
}
