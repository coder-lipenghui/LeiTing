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
        public const string RuntimeBundleName = "leiting_runtime_assets";

        private const string CatalogPath = "Assets/Resources/RuntimeAssetCatalog.asset";
        private const string LobbyPrefabPath = "Assets/Prefabs/UI/UILobby.prefab";
        private const string SettingPrefabPath = "Assets/Prefabs/UI/SettingPage.prefab";
        private const string VictorySettlementPrefabPath = "Assets/Prefabs/UI/UIVictorySettlement.prefab";
        private const string DefaultFontPath = "Assets/Art/Font/simhei.ttf";
        private const string UiSpriteFolder = "Assets/Art/Sprites/UI";
        private const string CoinPickupSoundPath = "Assets/Art/Sound/SFX/Item/coin.wav";
        private const string StarPickupSoundPath = "Assets/Art/Sound/SFX/Item/star.wav";
        private const string SpecialPickupSoundPath = "Assets/Art/Sound/SFX/Item/SFX_Item_Pickup_Special_01.wav";
        private const string BossEntryWarningSoundPath = "Assets/Art/Sound/SFX/Enemy/SFX_Boss_Attack_Warning_01.wav";
        private const string SplinePathPrefabFolder = "Assets/Prefabs/SplinePaths";
        private const string AircraftEngineAudioTypeName = "LeiTing.Audio.AircraftEngineAudio";

        public int callbackOrder => -1000;

        public sealed class RuntimeAssetPathCollection
        {
            public readonly HashSet<string> prefabPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> spritePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> fontPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> audioPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public IEnumerable<string> AllPaths => prefabPaths
                .Concat(spritePaths)
                .Concat(fontPaths)
                .Concat(audioPaths)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            RebuildCatalog();
        }

        [MenuItem("LeiTing/Build/Rebuild Runtime Asset Catalog")]
        public static void RebuildCatalog()
        {
            var includeAssetReferences = !RemoteResourceBundleBuilder.IsRemoteResourceBuildEnabled();
            RebuildCatalog(includeAssetReferences);
        }

        public static void RebuildCatalog(bool includeAssetReferences)
        {
            var pathCollection = CollectRuntimeAssetPaths();
            if (pathCollection == null)
            {
                return;
            }

            var prefabEntries = pathCollection.prefabPaths
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(path => CreatePrefabEntry(path, includeAssetReferences))
                .Where(entry => entry != null)
                .ToList();

            var spriteEntries = pathCollection.spritePaths
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(path => CreateSpriteEntry(path, includeAssetReferences))
                .Where(entry => entry != null)
                .ToList();
            var fontEntries = pathCollection.fontPaths
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(path => CreateFontEntry(path, includeAssetReferences))
                .Where(entry => entry != null)
                .ToList();
            var audioEntries = pathCollection.audioPaths
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(path => CreateAudioClipEntry(path, includeAssetReferences))
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

        public static RuntimeAssetPathCollection CollectRuntimeAssetPaths()
        {
            if (!TryLoadBuildConfig(out var config))
            {
                return null;
            }

            var collection = new RuntimeAssetPathCollection();
            CollectConfiguredPaths(
                config,
                collection.prefabPaths,
                collection.spritePaths,
                collection.fontPaths,
                collection.audioPaths);
            return collection;
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
            AddPath(prefabPaths, LobbyPrefabPath);
            AddPath(prefabPaths, SettingPrefabPath);
            AddPath(prefabPaths, VictorySettlementPrefabPath);
            AddPath(fontPaths, DefaultFontPath);
            CollectFolderSpritePaths(UiSpriteFolder, spritePaths);
            AddPath(audioPaths, CoinPickupSoundPath);
            AddPath(audioPaths, StarPickupSoundPath);
            AddPath(audioPaths, SpecialPickupSoundPath);
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

            CollectSplinePathPrefabPaths(config, prefabPaths);

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

        private static void CollectSplinePathPrefabPaths(GameConfig config, HashSet<string> prefabPaths)
        {
            if (config?.waves == null)
            {
                return;
            }

            foreach (var wave in config.waves)
            {
                if (wave?.spawns == null)
                {
                    continue;
                }

                foreach (var spawn in wave.spawns)
                {
                    if (spawn == null || !TryGetSplinePathId(spawn.movementPath, out var pathId))
                    {
                        continue;
                    }

                    AddPath(prefabPaths, ResolveSplinePathPrefabPath(pathId));
                }
            }
        }

        private static bool TryGetSplinePathId(string movementPath, out string pathId)
        {
            pathId = string.Empty;
            if (!IsMovementPath(movementPath, "Spline"))
            {
                return false;
            }

            var resolvedPathId = string.Empty;
            ForEachInlineMovementParameter(movementPath, (key, value) =>
            {
                if (string.Equals(key, "path", StringComparison.OrdinalIgnoreCase))
                {
                    resolvedPathId = value?.Trim();
                }
            });

            pathId = resolvedPathId;
            return !string.IsNullOrWhiteSpace(pathId);
        }

        private static string ResolveSplinePathPrefabPath(string pathId)
        {
            var normalized = pathId.Trim().Replace("\\", "/");
            if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return HasExtension(normalized) ? normalized : $"{normalized}.prefab";
            }

            return $"{SplinePathPrefabFolder}/{normalized}.prefab";
        }

        private static bool IsMovementPath(string movementPath, string expected)
        {
            return string.Equals(GetMovementPathName(movementPath), expected, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetMovementPathName(string movementPath)
        {
            if (string.IsNullOrWhiteSpace(movementPath))
            {
                return string.Empty;
            }

            var normalized = movementPath.Trim();
            var parameterStart = normalized.IndexOfAny(new[] { ':', '(' });
            return parameterStart >= 0 ? normalized.Substring(0, parameterStart).Trim() : normalized;
        }

        private static void ForEachInlineMovementParameter(string movementPath, Action<string, string> apply)
        {
            if (string.IsNullOrWhiteSpace(movementPath) || apply == null)
            {
                return;
            }

            var parameterStart = movementPath.IndexOfAny(new[] { ':', '(' });
            if (parameterStart < 0)
            {
                return;
            }

            var marker = movementPath[parameterStart];
            var body = movementPath.Substring(parameterStart + 1).Trim();
            if (marker == '(' && body.EndsWith(")", StringComparison.Ordinal))
            {
                body = body.Substring(0, body.Length - 1);
            }
            else if (body.StartsWith("(", StringComparison.Ordinal) && body.EndsWith(")", StringComparison.Ordinal))
            {
                body = body.Substring(1, body.Length - 2);
            }

            var pairs = body.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var separator = pair.IndexOf('=');
                if (separator <= 0 || separator >= pair.Length - 1)
                {
                    continue;
                }

                var key = pair.Substring(0, separator).Trim();
                var value = pair.Substring(separator + 1).Trim();
                apply(key, value);
            }
        }

        private static void CollectFolderSpritePaths(string folderPath, HashSet<string> spritePaths)
        {
            if (spritePaths == null || !AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { folderPath }))
            {
                AddPath(spritePaths, AssetDatabase.GUIDToAssetPath(guid));
            }
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

            paths.Add(ResolveAssetDatabasePath(path.Trim().Replace("\\", "/")));
        }

        private static string ResolveAssetDatabasePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            if (path.StartsWith("Sprites/", StringComparison.OrdinalIgnoreCase))
            {
                var resolvedPath = $"Assets/Art/{path}";
                return HasExtension(resolvedPath) ? resolvedPath : $"{resolvedPath}.png";
            }

            return path;
        }

        private static bool HasExtension(string path)
        {
            var extensionIndex = path.LastIndexOf(".", StringComparison.Ordinal);
            var slashIndex = path.LastIndexOf("/", StringComparison.Ordinal);
            return extensionIndex > slashIndex;
        }

        private static RuntimeAssetCatalog.PrefabEntry CreatePrefabEntry(string path, bool includeAssetReference)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"Runtime asset catalog skipped missing prefab: {path}");
                return null;
            }

            return new RuntimeAssetCatalog.PrefabEntry(
                path,
                includeAssetReference ? prefab : null,
                RuntimeBundleName,
                path);
        }

        private static RuntimeAssetCatalog.SpriteEntry CreateSpriteEntry(string path, bool includeAssetReference)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning($"Runtime asset catalog skipped missing sprite: {path}");
                return null;
            }

            return new RuntimeAssetCatalog.SpriteEntry(
                path,
                includeAssetReference ? sprite : null,
                RuntimeBundleName,
                path);
        }

        private static RuntimeAssetCatalog.FontEntry CreateFontEntry(string path, bool includeAssetReference)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(path);
            if (font == null)
            {
                Debug.LogWarning($"Runtime asset catalog skipped missing font: {path}");
                return null;
            }

            return new RuntimeAssetCatalog.FontEntry(
                path,
                includeAssetReference ? font : null,
                RuntimeBundleName,
                path);
        }

        private static RuntimeAssetCatalog.AudioClipEntry CreateAudioClipEntry(string path, bool includeAssetReference)
        {
            var audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (audioClip == null)
            {
                Debug.LogWarning($"Runtime asset catalog skipped missing audio clip: {path}");
                return null;
            }

            return new RuntimeAssetCatalog.AudioClipEntry(
                path,
                includeAssetReference ? audioClip : null,
                RuntimeBundleName,
                path);
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
