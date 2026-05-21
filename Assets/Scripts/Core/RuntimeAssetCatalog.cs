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

        private static RuntimeAssetCatalog cachedCatalog;
        private Dictionary<string, GameObject> prefabLookup;
        private Dictionary<string, Sprite> spriteLookup;
        private Dictionary<string, Font> fontLookup;

        [Serializable]
        public sealed class PrefabEntry
        {
            public string path;
            public GameObject prefab;

            public PrefabEntry(string path, GameObject prefab)
            {
                this.path = path;
                this.prefab = prefab;
            }
        }

        [Serializable]
        public sealed class SpriteEntry
        {
            public string path;
            public Sprite sprite;

            public SpriteEntry(string path, Sprite sprite)
            {
                this.path = path;
                this.sprite = sprite;
            }
        }

        [Serializable]
        public sealed class FontEntry
        {
            public string path;
            public Font font;

            public FontEntry(string path, Font font)
            {
                this.path = path;
                this.font = font;
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

        public void SetEntries(List<PrefabEntry> prefabEntries, List<SpriteEntry> spriteEntries)
        {
            SetEntries(prefabEntries, spriteEntries, null);
        }

        public void SetEntries(List<PrefabEntry> prefabEntries, List<SpriteEntry> spriteEntries, List<FontEntry> fontEntries)
        {
            prefabs = prefabEntries ?? new List<PrefabEntry>();
            sprites = spriteEntries ?? new List<SpriteEntry>();
            fonts = fontEntries ?? new List<FontEntry>();
            prefabLookup = null;
            spriteLookup = null;
            fontLookup = null;
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
            prefabLookup.TryGetValue(NormalizeKey(assetPath), out var prefab);
            return prefab;
        }

        private Sprite GetSprite(string assetPath)
        {
            EnsureLookups();
            spriteLookup.TryGetValue(NormalizeKey(assetPath), out var sprite);
            return sprite;
        }

        private Font GetFont(string assetPath)
        {
            EnsureLookups();
            fontLookup.TryGetValue(NormalizeKey(assetPath), out var font);
            return font;
        }

        private void EnsureLookups()
        {
            if (prefabLookup != null && spriteLookup != null && fontLookup != null)
            {
                return;
            }

            prefabLookup = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
            spriteLookup = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
            fontLookup = new Dictionary<string, Font>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in prefabs)
            {
                if (entry != null)
                {
                    AddLookup(prefabLookup, entry.path, entry.prefab);
                }
            }

            foreach (var entry in sprites)
            {
                if (entry != null)
                {
                    AddLookup(spriteLookup, entry.path, entry.sprite);
                }
            }

            foreach (var entry in fonts)
            {
                if (entry != null)
                {
                    AddLookup(fontLookup, entry.path, entry.font);
                }
            }
        }

        private static void AddLookup<TAsset>(Dictionary<string, TAsset> lookup, string assetPath, TAsset asset)
            where TAsset : UnityEngine.Object
        {
            if (lookup == null || asset == null || string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            foreach (var key in GetLookupKeys(assetPath))
            {
                if (!string.IsNullOrEmpty(key) && !lookup.ContainsKey(key))
                {
                    lookup.Add(key, asset);
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
