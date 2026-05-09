using System;
using System.Collections.Generic;
using LeiTing.Config;
using LeiTing.Core;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LeiTing.Missiles
{
    public class MissileManager : MonoSingleton<MissileManager>
    {
        private const string DefaultPoolKey = "__default";

        [SerializeField] private int initialPoolSize;
        [SerializeField] private Transform missileLayer;

        private readonly Dictionary<string, Stack<MissileController>> pooledMissiles = new Dictionary<string, Stack<MissileController>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<MissileController, string> activePoolKeys = new Dictionary<MissileController, string>();
        private bool isPoolWarmed;

        public MissileController Fire(MissileConfig missileConfig, Vector2 position, Vector2 direction, bool skipLockDelay = false)
        {
            if (missileConfig == null)
            {
                return null;
            }

            EnsurePool();

            var poolKey = ResolvePoolKey(missileConfig);
            var missile = GetMissile(missileConfig, poolKey);
            activePoolKeys[missile] = poolKey;
            missile.transform.SetParent(GetLayerRoot(), false);
            missile.transform.position = position;
            missile.transform.rotation = Quaternion.identity;
            missile.Activate(missileConfig, direction, this, skipLockDelay);
            return missile;
        }

        public void Recycle(MissileController missile)
        {
            if (missile == null)
            {
                return;
            }

            missile.RecycleToPool();
        }

        public void CompleteRecycle(MissileController missile)
        {
            if (missile == null)
            {
                return;
            }

            missile.DeactivateForPool();
            missile.transform.SetParent(transform, false);

            if (!activePoolKeys.TryGetValue(missile, out var poolKey))
            {
                poolKey = DefaultPoolKey;
            }

            activePoolKeys.Remove(missile);
            GetPool(poolKey).Push(missile);
        }

        public void ClearEnemyMissiles()
        {
            ClearMissilesInLayer(GetLayerRoot());
        }

        protected override void Awake()
        {
            base.Awake();

            if (Instance == this)
            {
                EnsurePool();
            }
        }

        private void EnsurePool()
        {
            if (isPoolWarmed)
            {
                return;
            }

            CacheLayerRoot();

            for (var index = 0; index < Mathf.Max(0, initialPoolSize); index++)
            {
                GetPool(DefaultPoolKey).Push(CreateMissile(null));
            }

            isPoolWarmed = true;
        }

        private MissileController GetMissile(MissileConfig missileConfig, string poolKey)
        {
            var pool = GetPool(poolKey);
            return pool.Count > 0 ? pool.Pop() : CreateMissile(missileConfig);
        }

        private MissileController CreateMissile(MissileConfig missileConfig)
        {
            var prefab = LoadMissilePrefab(missileConfig);
            var missileObject = prefab != null ? Instantiate(prefab) : new GameObject("Missile");
            missileObject.name = prefab != null ? prefab.name : "Missile";
            missileObject.transform.SetParent(transform, false);
            missileObject.SetActive(false);
            return missileObject.GetComponent<MissileController>() ?? missileObject.AddComponent<MissileController>();
        }

        private Transform GetLayerRoot()
        {
            CacheLayerRoot();
            return missileLayer;
        }

        private void CacheLayerRoot()
        {
            missileLayer = missileLayer != null ? missileLayer : FindLayerRoot("MissileLayer_Enemy");
            missileLayer = missileLayer != null ? missileLayer : FindLayerRoot("BulletLayer_Enemy");
            missileLayer = missileLayer != null ? missileLayer : transform;
        }

        private Stack<MissileController> GetPool(string poolKey)
        {
            poolKey = string.IsNullOrEmpty(poolKey) ? DefaultPoolKey : poolKey;
            if (!pooledMissiles.TryGetValue(poolKey, out var pool))
            {
                pool = new Stack<MissileController>();
                pooledMissiles[poolKey] = pool;
            }

            return pool;
        }

        private static string ResolvePoolKey(MissileConfig missileConfig)
        {
            return missileConfig != null && !string.IsNullOrEmpty(missileConfig.prefabPath)
                ? missileConfig.prefabPath
                : DefaultPoolKey;
        }

        private static GameObject LoadMissilePrefab(MissileConfig missileConfig)
        {
            if (missileConfig == null || string.IsNullOrEmpty(missileConfig.prefabPath))
            {
                return null;
            }

#if UNITY_EDITOR
            if (missileConfig.prefabPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                var editorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(missileConfig.prefabPath);
                if (editorPrefab != null)
                {
                    return editorPrefab;
                }
            }
#endif

            return RuntimeAssetCatalog.LoadPrefab(missileConfig.prefabPath)
                ?? Resources.Load<GameObject>(NormalizeResourcesPath(missileConfig.prefabPath));
        }

        private static string NormalizeResourcesPath(string assetPath)
        {
            const string resourcesSegment = "/Resources/";
            var normalized = assetPath.Replace("\\", "/");
            var resourcesIndex = normalized.IndexOf(resourcesSegment, StringComparison.OrdinalIgnoreCase);

            if (resourcesIndex >= 0)
            {
                normalized = normalized.Substring(resourcesIndex + resourcesSegment.Length);
            }

            var extensionIndex = normalized.LastIndexOf(".", StringComparison.Ordinal);
            if (extensionIndex >= 0)
            {
                normalized = normalized.Substring(0, extensionIndex);
            }

            return normalized;
        }

        private static Transform FindLayerRoot(string layerName)
        {
            var found = GameObject.Find(layerName);
            return found != null ? found.transform : null;
        }

        private void ClearMissilesInLayer(Transform layerRoot)
        {
            if (layerRoot == null)
            {
                return;
            }

            var missiles = new List<MissileController>();
            foreach (Transform child in layerRoot)
            {
                var missile = child.GetComponent<MissileController>();
                if (missile != null)
                {
                    missiles.Add(missile);
                }
            }

            foreach (var missile in missiles)
            {
                Recycle(missile);
            }
        }
    }
}
