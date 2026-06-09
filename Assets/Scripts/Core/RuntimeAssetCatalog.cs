using System;
using System.Collections.Generic;
using UnityEngine;

namespace LeiTing.Core
{
    [CreateAssetMenu(fileName = "RuntimeAssetCatalog", menuName = "LeiTing/Runtime Asset Catalog")]
    public sealed class RuntimeAssetCatalog : ScriptableObject
    {
        private const string CatalogResourcesPath = "RuntimeAssetCatalog";

        [SerializeField] private List<PrefabEntry> prefabs = new List<PrefabEntry>();
        [SerializeField] private List<SpriteEntry> sprites = new List<SpriteEntry>();
        [SerializeField] private List<FontEntry> fonts = new List<FontEntry>();
        [SerializeField] private List<AudioClipEntry> audioClips = new List<AudioClipEntry>();

        private static RuntimeAssetCatalog cachedCatalog;
        private Dictionary<string, PrefabEntry> prefabLookup;
        private Dictionary<string, SpriteEntry> spriteLookup;
        private Dictionary<string, FontEntry> fontLookup;
        private Dictionary<string, AudioClipEntry> audioClipLookup;

        [Serializable]
        public sealed class PrefabEntry
        {
            public string path;
            public string bundleName;
            public string assetName;
            public GameObject prefab;

            public PrefabEntry(string path, GameObject prefab)
                : this(path, prefab, string.Empty, string.Empty)
            {
            }

            public PrefabEntry(string path, GameObject prefab, string bundleName, string assetName)
            {
                this.path = path;
                this.prefab = prefab;
                this.bundleName = bundleName;
                this.assetName = assetName;
            }
        }

        [Serializable]
        public sealed class SpriteEntry
        {
            public string path;
            public string bundleName;
            public string assetName;
            public Sprite sprite;

            public SpriteEntry(string path, Sprite sprite)
                : this(path, sprite, string.Empty, string.Empty)
            {
            }

            public SpriteEntry(string path, Sprite sprite, string bundleName, string assetName)
            {
                this.path = path;
                this.sprite = sprite;
                this.bundleName = bundleName;
                this.assetName = assetName;
            }
        }

        [Serializable]
        public sealed class FontEntry
        {
            public string path;
            public string bundleName;
            public string assetName;
            public Font font;

            public FontEntry(string path, Font font)
                : this(path, font, string.Empty, string.Empty)
            {
            }

            public FontEntry(string path, Font font, string bundleName, string assetName)
            {
                this.path = path;
                this.font = font;
                this.bundleName = bundleName;
                this.assetName = assetName;
            }
        }

        [Serializable]
        public sealed class AudioClipEntry
        {
            public string path;
            public string bundleName;
            public string assetName;
            public AudioClip audioClip;

            public AudioClipEntry(string path, AudioClip audioClip)
                : this(path, audioClip, string.Empty, string.Empty)
            {
            }

            public AudioClipEntry(string path, AudioClip audioClip, string bundleName, string assetName)
            {
                this.path = path;
                this.audioClip = audioClip;
                this.bundleName = bundleName;
                this.assetName = assetName;
            }
        }

        public static GameObject LoadPrefab(string assetPath)
        {
            return string.IsNullOrEmpty(assetPath) || Catalog == null ? null : Catalog.GetPrefab(assetPath);
        }

        public static Sprite LoadSprite(string assetPath)
        {
            return string.IsNullOrEmpty(assetPath) || Catalog == null ? null : Catalog.GetSprite(assetPath);
        }

        public static Font LoadFont(string assetPath)
        {
            return string.IsNullOrEmpty(assetPath) || Catalog == null ? null : Catalog.GetFont(assetPath);
        }

        public static AudioClip LoadAudioClip(string assetPath)
        {
            return string.IsNullOrEmpty(assetPath) || Catalog == null ? null : Catalog.GetAudioClip(assetPath);
        }

        public void SetEntries(List<PrefabEntry> prefabEntries, List<SpriteEntry> spriteEntries)
        {
            SetEntries(prefabEntries, spriteEntries, null, null);
        }

        public void SetEntries(List<PrefabEntry> prefabEntries, List<SpriteEntry> spriteEntries, List<FontEntry> fontEntries)
        {
            SetEntries(prefabEntries, spriteEntries, fontEntries, null);
        }

        public void SetEntries(
            List<PrefabEntry> prefabEntries,
            List<SpriteEntry> spriteEntries,
            List<FontEntry> fontEntries,
            List<AudioClipEntry> audioClipEntries)
        {
            prefabs = prefabEntries ?? new List<PrefabEntry>();
            sprites = spriteEntries ?? new List<SpriteEntry>();
            fonts = fontEntries ?? new List<FontEntry>();
            audioClips = audioClipEntries ?? new List<AudioClipEntry>();
            prefabLookup = null;
            spriteLookup = null;
            fontLookup = null;
            audioClipLookup = null;
        }

        private static RuntimeAssetCatalog Catalog
        {
            get
            {
                if (cachedCatalog == null)
                {
                    cachedCatalog = Resources.Load<RuntimeAssetCatalog>(CatalogResourcesPath);
                }

                return cachedCatalog;
            }
        }

        private GameObject GetPrefab(string assetPath)
        {
            EnsureLookups();
            if (!prefabLookup.TryGetValue(NormalizeKey(assetPath), out var entry))
            {
                return null;
            }

            var remoteAsset = LoadRemoteAsset<GameObject>(entry.bundleName, entry.assetName, entry.path);
            return remoteAsset != null || RuntimeRemoteResourceManager.RequiresRemoteAssetLoad ? remoteAsset : entry.prefab;
        }

        private Sprite GetSprite(string assetPath)
        {
            EnsureLookups();
            if (!spriteLookup.TryGetValue(NormalizeKey(assetPath), out var entry))
            {
                return null;
            }

            var remoteAsset = LoadRemoteAsset<Sprite>(entry.bundleName, entry.assetName, entry.path);
            return remoteAsset != null || RuntimeRemoteResourceManager.RequiresRemoteAssetLoad ? remoteAsset : entry.sprite;
        }

        private Font GetFont(string assetPath)
        {
            EnsureLookups();
            if (!fontLookup.TryGetValue(NormalizeKey(assetPath), out var entry))
            {
                return null;
            }

            var remoteAsset = LoadRemoteAsset<Font>(entry.bundleName, entry.assetName, entry.path);
            return remoteAsset != null || RuntimeRemoteResourceManager.RequiresRemoteAssetLoad ? remoteAsset : entry.font;
        }

        private AudioClip GetAudioClip(string assetPath)
        {
            EnsureLookups();
            if (!audioClipLookup.TryGetValue(NormalizeKey(assetPath), out var entry))
            {
                return null;
            }

            var remoteAsset = LoadRemoteAsset<AudioClip>(entry.bundleName, entry.assetName, entry.path);
            return remoteAsset != null || RuntimeRemoteResourceManager.RequiresRemoteAssetLoad ? remoteAsset : entry.audioClip;
        }

        private void EnsureLookups()
        {
            if (prefabLookup != null && spriteLookup != null && fontLookup != null && audioClipLookup != null)
            {
                return;
            }

            prefabLookup = new Dictionary<string, PrefabEntry>(StringComparer.OrdinalIgnoreCase);
            spriteLookup = new Dictionary<string, SpriteEntry>(StringComparer.OrdinalIgnoreCase);
            fontLookup = new Dictionary<string, FontEntry>(StringComparer.OrdinalIgnoreCase);
            audioClipLookup = new Dictionary<string, AudioClipEntry>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in prefabs)
            {
                if (entry != null)
                {
                    AddLookup(prefabLookup, entry.path, entry);
                }
            }

            foreach (var entry in sprites)
            {
                if (entry != null)
                {
                    AddLookup(spriteLookup, entry.path, entry);
                }
            }

            foreach (var entry in fonts)
            {
                if (entry != null)
                {
                    AddLookup(fontLookup, entry.path, entry);
                }
            }

            foreach (var entry in audioClips)
            {
                if (entry != null)
                {
                    AddLookup(audioClipLookup, entry.path, entry);
                }
            }
        }

        private static TAsset LoadRemoteAsset<TAsset>(string bundleName, string assetName, string fallbackAssetPath)
            where TAsset : UnityEngine.Object
        {
            var resolvedAssetName = !string.IsNullOrWhiteSpace(assetName) ? assetName : fallbackAssetPath;
            return RuntimeRemoteResourceManager.LoadAsset<TAsset>(bundleName, resolvedAssetName);
        }

        private static void AddLookup<TEntry>(Dictionary<string, TEntry> lookup, string assetPath, TEntry entry)
            where TEntry : class
        {
            if (lookup == null || entry == null || string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            foreach (var key in GetLookupKeys(assetPath))
            {
                if (!string.IsNullOrEmpty(key) && !lookup.ContainsKey(key))
                {
                    lookup.Add(key, entry);
                }
            }
        }

        private static IEnumerable<string> GetLookupKeys(string assetPath)
        {
            var normalized = assetPath.Replace("\\", "/").Trim();
            yield return NormalizeKey(normalized);

            const string resourcesSegment = "/Resources/";
            var resourcesIndex = normalized.IndexOf(resourcesSegment, StringComparison.OrdinalIgnoreCase);
            if (resourcesIndex >= 0)
            {
                yield return NormalizeKey(normalized.Substring(resourcesIndex + resourcesSegment.Length));
            }

            const string assetsPrefix = "Assets/";
            if (normalized.StartsWith(assetsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                yield return NormalizeKey(normalized.Substring(assetsPrefix.Length));
            }

            const string spritesSegment = "/Sprites/";
            var spritesIndex = normalized.IndexOf(spritesSegment, StringComparison.OrdinalIgnoreCase);
            if (spritesIndex >= 0)
            {
                yield return NormalizeKey(normalized.Substring(spritesIndex + 1));
            }
        }

        private static string NormalizeKey(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return string.Empty;
            }

            var normalized = assetPath.Replace("\\", "/").Trim();
            var extensionIndex = normalized.LastIndexOf(".", StringComparison.Ordinal);
            if (extensionIndex >= 0)
            {
                normalized = normalized.Substring(0, extensionIndex);
            }

            return normalized;
        }
    }
}
