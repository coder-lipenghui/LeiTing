using System.IO;
using LeiTing.UI;
using UnityEditor;
using UnityEngine;

namespace LeiTing.Editor
{
    public static class UIPrefabBuilder
    {
        private const string UiFolder = "Assets/Resources/UI";
        private const string PageFolder = UiFolder + "/Page";
        private const string CommonFolder = UiFolder + "/Common";

        [MenuItem("Tools/LeiTing/UI/Rebuild Main UI Prefabs")]
        public static void RebuildMainUiPrefabs()
        {
            EnsureFolder(PageFolder);
            EnsureFolder(CommonFolder);

            SavePagePrefab<LobbyPage>("LobbyPage", UIPageType.Lobby, 2);
            SavePagePrefab<HangarPage>("HangarPage", UIPageType.Hangar, 1);
            SavePagePrefab<SettingPage>("SettingPage", UIPageType.Setting, 3);
            SaveBottomPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Main UI prefabs rebuilt.");
        }

        private static void SavePagePrefab<TPage>(string prefabName, UIPageType pageType, int pageIndex)
            where TPage : BasePage
        {
            var root = new GameObject(prefabName, typeof(RectTransform));
            try
            {
                var page = root.AddComponent<TPage>();
                page.Configure(pageType, pageIndex);
                PrefabUtility.SaveAsPrefabAsset(root, $"{PageFolder}/{prefabName}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void SaveBottomPrefab()
        {
            var root = new GameObject("UIBottom", typeof(RectTransform));
            try
            {
                var bottomBar = root.AddComponent<BottomBar>();
                bottomBar.BuildDefaultView();
                PrefabUtility.SaveAsPrefabAsset(root, $"{CommonFolder}/UIBottom.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var folderName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
