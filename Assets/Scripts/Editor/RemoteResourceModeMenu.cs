using LeiTing.Core;
using UnityEditor;
using UnityEngine;

namespace LeiTing.Editor
{
    public static class RemoteResourceModeMenu
    {
        private const string UseLocalMenu = "LeiTing/CDN Resources/Editor Mode/Use Local Assets";
        private const string UseCdnMenu = "LeiTing/CDN Resources/Editor Mode/Use CDN";
        private const string ClearLocalCacheMenu = "LeiTing/CDN Resources/Clear Local Cache";

        [MenuItem(UseLocalMenu)]
        private static void UseLocalAssets()
        {
            SetEditorMode(RemoteResourceSettingsData.EditorModeLocal);
        }

        [MenuItem(UseLocalMenu, true)]
        private static bool ValidateUseLocalAssets()
        {
            Menu.SetChecked(UseLocalMenu, IsCurrentMode(RemoteResourceSettingsData.EditorModeLocal));
            return true;
        }

        [MenuItem(UseCdnMenu)]
        private static void UseCdn()
        {
            SetEditorMode(RemoteResourceSettingsData.EditorModeCdn);
        }

        [MenuItem(UseCdnMenu, true)]
        private static bool ValidateUseCdn()
        {
            Menu.SetChecked(UseCdnMenu, IsCurrentMode(RemoteResourceSettingsData.EditorModeCdn));
            return true;
        }

        [MenuItem(ClearLocalCacheMenu)]
        private static void ClearLocalCache()
        {
            if (EditorApplication.isPlaying
                && !EditorUtility.DisplayDialog(
                    "Clear CDN Local Cache",
                    "This will unload currently loaded CDN bundles and clear Unity's local cache. The current play session may need to re-enter the loading flow.",
                    "Clear",
                    "Cancel"))
            {
                return;
            }

            var cleared = RuntimeRemoteResourceManager.ClearLocalCache();
            if (cleared)
            {
                Debug.Log("CDN local cache cleared. Enter Play Mode again, refresh the WebGL page, or re-enter the loading flow to download resources again.");
            }
            else
            {
                Debug.LogWarning("CDN local cache clear did not complete. Stop active downloads or unload in-use resources, then try again.");
            }
        }

        [MenuItem(ClearLocalCacheMenu, true)]
        private static bool ValidateClearLocalCache()
        {
            return !EditorApplication.isCompiling && !RuntimeRemoteResourceManager.IsLoading;
        }

        private static void SetEditorMode(string mode)
        {
            var settings = RemoteResourceBundleBuilder.LoadSettings();
            settings.enabled = true;
            settings.simulateInEditor = false;
            settings.editorResourceMode = mode;
            RemoteResourceBundleBuilder.SaveSettings(settings);
            Debug.Log($"Editor resource mode switched to {mode}.");
        }

        private static bool IsCurrentMode(string mode)
        {
            var settings = RemoteResourceBundleBuilder.LoadSettings();
            return string.Equals(settings.editorResourceMode, mode, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, RemoteResourceSettingsData.EditorModeCdn, System.StringComparison.OrdinalIgnoreCase)
                    && settings.simulateInEditor;
        }
    }
}
