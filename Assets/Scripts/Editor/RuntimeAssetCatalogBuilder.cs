using System;
using System.Collections.Generic;
using System.Linq;
using LeiTing.Config;
using LeiTing.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LeiTing.Editor
{
    public sealed class RuntimeAssetCatalogBuilder : IPreprocessBuildWithReport
    {
        private const string ConfigPath = "Assets/Resources/Configs/GameConfig.json";
        private const string CatalogPath = "Assets/Resources/RuntimeAssetCatalog.asset";

        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            RebuildCatalog();
        }

        [MenuItem("LeiTing/Build/Rebuild Runtime Asset Catalog")]
        public static void RebuildCatalog()
        {
            var configAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(ConfigPath);
            if (configAsset == null)
            {
                Debug.LogError($"Runtime asset catalog build failed. Config not found: {ConfigPath}");
                return;
            }

            var config = JsonUtility.FromJson<GameConfig>(configAsset.text);
            if (config == null)
            {
                Debug.LogError($"Runtime asset catalog build failed. Config parse failed: {ConfigPath}");
                return;
            }

            var prefabPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var spritePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            CollectConfiguredPaths(config, prefabPaths, spritePaths);

            var prefabEntries = prefabPaths
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(path => CreatePrefabEntry(path))
                .Where(entry => entry != null)
                .ToList();

            var spriteEntries = spritePaths
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(path => CreateSpriteEntry(path))
                .Where(entry => entry != null)
                .ToList();

            EnsureResourcesFolder();

            var catalog = AssetDatabase.LoadAssetAtPath<RuntimeAssetCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<RuntimeAssetCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.SetEntries(prefabEntries, spriteEntries);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            Debug.Log($"Runtime asset catalog rebuilt: {prefabEntries.Count} prefabs, {spriteEntries.Count} sprites.");
        }

        private static void CollectConfiguredPaths(GameConfig config, HashSet<string> prefabPaths, HashSet<string> spritePaths)
        {
            AddPath(prefabPaths, config.player?.prefabPath);
            AddPath(spritePaths, config.background?.spritePath);

            AddPath(spritePaths, "Assets/Art/Animations/Enemies/enemy-01.png");
            AddPath(spritePaths, "Assets/Art/Animations/Enemies/BOSS-1.png");

            if (config.enemies != null)
            {
                foreach (var enemy in config.enemies)
                {
                    AddPath(prefabPaths, enemy?.prefabPath);
                }
            }

            if (config.bullets != null)
            {
                foreach (var bullet in config.bullets)
                {
                    AddPath(spritePaths, bullet?.spritePath);
                }
            }

            if (config.missiles != null)
            {
                foreach (var missile in config.missiles)
                {
                    AddPath(prefabPaths, missile?.prefabPath);
                    AddPath(spritePaths, missile?.bodyRes);
                    AddPath(spritePaths, missile?.warningRes);
                    AddPath(spritePaths, missile?.lockEffectRes);
                    AddPath(spritePaths, missile?.explodeEffectRes);
                    AddPath(spritePaths, missile?.hitEffectRes);
                    AddPath(spritePaths, missile?.destroyEffectRes);
                    AddPath(spritePaths, missile?.effectRes);
                }
            }

            if (config.pickupItems != null)
            {
                foreach (var item in config.pickupItems)
                {
                    AddPath(spritePaths, item?.spritePath);
                }
            }
        }

        private static void AddPath(HashSet<string> paths, string path)
        {
            if (paths == null || string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            paths.Add(path.Trim().Replace("\\", "/"));
        }

        private static RuntimeAssetCatalog.PrefabEntry CreatePrefabEntry(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"Runtime asset catalog skipped missing prefab: {path}");
                return null;
            }

            return new RuntimeAssetCatalog.PrefabEntry(path, prefab);
        }

        private static RuntimeAssetCatalog.SpriteEntry CreateSpriteEntry(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning($"Runtime asset catalog skipped missing sprite: {path}");
                return null;
            }

            return new RuntimeAssetCatalog.SpriteEntry(path, sprite);
        }

        private static void EnsureResourcesFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
        }
    }
}
