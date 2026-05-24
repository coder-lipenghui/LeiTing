using System;
using System.Collections.Generic;
using System.Linq;
using LeiTing.Audio;
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
        private const string CatalogPath = "Assets/Resources/RuntimeAssetCatalog.asset";
        private const string BottomBarPrefabPath = "Assets/Prefabs/UI/UIBottom.prefab";
        private const string HallPrefabPath = "Assets/Prefabs/UI/UIHall.prefab";
        private const string StagePrefabPath = "Assets/Prefabs/UI/UIStage.prefab";
        private const string DefaultFontPath = "Assets/Art/Font/simhei.ttf";
        private const string MainUiBackgroundPath = "Assets/Art/Sprites/UI/backgroundH.png";
        private const string CoinPickupSoundPath = "Assets/Art/Sound/SFX/Item/coin.wav";
        private const string StarPickupSoundPath = "Assets/Art/Sound/SFX/Item/star.wav";
        private const string BossEntryWarningSoundPath = "Assets/Art/Sound/SFX/Enemy/SFX_Boss_Attack_Warning_01.wav";
        private const string AircraftEngineAudioTypeName = "LeiTing.Audio.AircraftEngineAudio";

        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            RebuildCatalog();
        }

        [MenuItem("LeiTing/Build/Rebuild Runtime Asset Catalog")]
        public static void RebuildCatalog()
        {
            if (!TryLoadBuildConfig(out var config))
            {
                return;
            }

            var prefabPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var spritePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var fontPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var audioPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            CollectConfiguredPaths(config, prefabPaths, spritePaths, fontPaths, audioPaths);

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
            var fontEntries = fontPaths
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(path => CreateFontEntry(path))
                .Where(entry => entry != null)
                .ToList();
            var audioEntries = audioPaths
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(path => CreateAudioClipEntry(path))
                .Where(entry => entry != null)
                .ToList();

            EnsureResourcesFolder();

            var catalog = AssetDatabase.LoadAssetAtPath<RuntimeAssetCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<RuntimeAssetCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.SetEntries(prefabEntries, spriteEntries, fontEntries, audioEntries);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            Debug.Log($"Runtime asset catalog rebuilt: {prefabEntries.Count} prefabs, {spriteEntries.Count} sprites, {fontEntries.Count} fonts, {audioEntries.Count} audio clips.");
        }

        private static bool TryLoadBuildConfig(out GameConfig config)
        {
            if (LubanConfigLoader.TryLoad(out config))
            {
                return true;
            }

            Debug.LogError("Runtime asset catalog build failed. Luban config could not be loaded.");
            return false;
        }

        private static void CollectConfiguredPaths(
            GameConfig config,
            HashSet<string> prefabPaths,
            HashSet<string> spritePaths,
            HashSet<string> fontPaths,
            HashSet<string> audioPaths)
        {
            AddPath(prefabPaths, config.player?.prefabPath);
            AddPath(audioPaths, config.player?.fireSoundPath);
            AddPath(prefabPaths, BottomBarPrefabPath);
            AddPath(prefabPaths, HallPrefabPath);
            AddPath(prefabPaths, StagePrefabPath);
            AddPath(fontPaths, DefaultFontPath);
            AddPath(spritePaths, MainUiBackgroundPath);
            AddPath(audioPaths, CoinPickupSoundPath);
            AddPath(audioPaths, StarPickupSoundPath);
            AddPath(audioPaths, BossEntryWarningSoundPath);
            AddPath(audioPaths, AudioManager.MenuBgmPath);

            AddPath(spritePaths, "Assets/Art/Animations/Enemies/enemy-01.png");
            AddPath(spritePaths, "Assets/Art/Animations/Enemies/BOSS-1.png");

            if (config.levels != null)
            {
                foreach (var level in config.levels)
                {
                    AddPath(spritePaths, level?.backgroundSpritePath);
                    AddPath(audioPaths, level?.bgmPath);
                }
            }

            if (config.enemies != null)
            {
                foreach (var enemy in config.enemies)
                {
                    AddPath(prefabPaths, enemy?.prefabPath);
                    AddPath(audioPaths, enemy?.hitSoundPath);
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
                    AddPath(audioPaths, missile?.soundRes);
                    AddPath(audioPaths, missile?.soundLaunch);
                    AddPath(audioPaths, missile?.soundLock);
                    AddPath(audioPaths, missile?.soundExplode);
                }
            }

            if (config.pickupItems != null)
            {
                foreach (var item in config.pickupItems)
                {
                    AddPath(spritePaths, item?.spritePath);
                }
            }

            CollectPrefabAudioPaths(prefabPaths, audioPaths);
        }

        private static void CollectPrefabAudioPaths(IEnumerable<string> prefabPaths, HashSet<string> audioPaths)
        {
            foreach (var prefabPath in prefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    continue;
                }

                foreach (var component in prefab.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component == null || !string.Equals(component.GetType().FullName, AircraftEngineAudioTypeName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var serializedAudio = new SerializedObject(component);
                    var clipPath = serializedAudio.FindProperty("clipPath");
                    AddPath(audioPaths, clipPath?.stringValue);

                    var clipOverride = serializedAudio.FindProperty("clipOverride")?.objectReferenceValue as AudioClip;
                    if (clipOverride != null)
                    {
                        AddPath(audioPaths, AssetDatabase.GetAssetPath(clipOverride));
                    }
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

        private static RuntimeAssetCatalog.FontEntry CreateFontEntry(string path)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(path);
            if (font == null)
            {
                Debug.LogWarning($"Runtime asset catalog skipped missing font: {path}");
                return null;
            }

            return new RuntimeAssetCatalog.FontEntry(path, font);
        }

        private static RuntimeAssetCatalog.AudioClipEntry CreateAudioClipEntry(string path)
        {
            var audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (audioClip == null)
            {
                Debug.LogWarning($"Runtime asset catalog skipped missing audio clip: {path}");
                return null;
            }

            return new RuntimeAssetCatalog.AudioClipEntry(path, audioClip);
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
