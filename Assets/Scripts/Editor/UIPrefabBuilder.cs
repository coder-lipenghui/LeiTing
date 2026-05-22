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
        private const string RuntimePrefabFolder = "Assets/Prefabs/UI";
        private const string HallArtFolder = "Assets/Art/Sprites/UI/UIHall";
        private const string PlayerPlaneSpritePath = "Assets/Art/Animations/Player/warplane-01.png";

        [MenuItem("Tools/LeiTing/UI/Rebuild Main UI Prefabs")]
        public static void RebuildMainUiPrefabs()
        {
            EnsureFolder(PageFolder);
            EnsureFolder(CommonFolder);
            EnsureFolder(RuntimePrefabFolder);

            SavePagePrefab<HangarPage>("HangarPage", UIPageType.Hangar, 1);
            SavePagePrefab<SettingPage>("SettingPage", UIPageType.Setting, 3);
            SaveHallPrefab();
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
                SetUiLayer(root);
                var rect = root.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(1080f, 268f);

                var bottomBar = root.AddComponent<BottomBar>();
                bottomBar.ConfigureSprites(
                    LoadSprite($"{HallArtFolder}/tab_left01.png"),
                    LoadSprite($"{HallArtFolder}/tab_left02.png"),
                    LoadSprite($"{HallArtFolder}/tab_center01.png"),
                    LoadSprite($"{HallArtFolder}/tab_center02.png"),
                    LoadSprite($"{HallArtFolder}/tab_right01.png"),
                    LoadSprite($"{HallArtFolder}/tab_right02.png"));
                bottomBar.BuildDefaultView();
                PrefabUtility.SaveAsPrefabAsset(root, $"{RuntimePrefabFolder}/UIBottom.prefab");
                PrefabUtility.SaveAsPrefabAsset(root, $"{CommonFolder}/UIBottom.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void SaveHallPrefab()
        {
            var root = new GameObject("UIHall", typeof(RectTransform));
            try
            {
                SetUiLayer(root);
                var rect = root.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = Vector2.zero;

                var page = root.AddComponent<LobbyPage>();
                page.ConfigureSprites(
                    LoadSprite($"{HallArtFolder}/hall_bg.png"),
                    LoadSprite($"{HallArtFolder}/btn_start.png"),
                    LoadSprite(PlayerPlaneSpritePath));
                page.Configure(UIPageType.Lobby, 2);
                PrefabUtility.SaveAsPrefabAsset(root, $"{RuntimePrefabFolder}/UIHall.prefab");
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

        private static Sprite LoadSprite(string assetPath)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                Debug.LogWarning($"UI sprite missing: {assetPath}");
            }

            return sprite;
        }

        private static void SetUiLayer(GameObject root)
        {
            var uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0)
            {
                root.layer = uiLayer;
            }
        }
    }
}
