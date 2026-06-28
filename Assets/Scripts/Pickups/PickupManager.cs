using System.Collections.Generic;
using LeiTing.Config;
using LeiTing.Core;
using LeiTing.Enemy;
using LeiTing.Player;
using LeiTing.Progress;
using UnityEngine;

namespace LeiTing.Pickups
{
    public class PickupManager : MonoSingleton<PickupManager>
    {
        private const float DropScatterRadius = 0.7f;

        [SerializeField] private int initialPoolSize = 24;
        [SerializeField] private Transform pickupLayer;

        private readonly HashSet<string> droppedOnceItemIds = new HashSet<string>();
        private readonly HashSet<PickupItemController> activePickups = new HashSet<PickupItemController>();
        private readonly Stack<PickupItemController> pooledPickups = new Stack<PickupItemController>();
        private readonly Dictionary<string, PickupItemConfig> pickupConfigCache = new Dictionary<string, PickupItemConfig>();
        private bool isPoolWarmed;

        public static PickupManager GetOrCreate()
        {
            if (Instance != null)
            {
                return Instance;
            }

            var managers = GameObject.Find("Managers") ?? new GameObject("Managers");
            return managers.GetComponent<PickupManager>() ?? managers.AddComponent<PickupManager>();
        }

        public void SpawnDrops(EnemyConfig enemyConfig, Vector3 position)
        {
            if (enemyConfig == null || enemyConfig.drops == null || enemyConfig.drops.Count == 0)
            {
                return;
            }

            var drops = new List<PickupItemConfig>();
            foreach (var drop in enemyConfig.drops)
            {
                if (drop == null || string.IsNullOrEmpty(drop.itemId) || drop.count <= 0)
                {
                    continue;
                }

                if (drop.dropOnce && droppedOnceItemIds.Contains(drop.itemId))
                {
                    continue;
                }

                var pickupConfig = ResolvePickupConfig(drop.itemId);
                if (pickupConfig == null)
                {
                    continue;
                }

                if (drop.dropOnce)
                {
                    droppedOnceItemIds.Add(drop.itemId);
                }

                for (var index = 0; index < drop.count; index++)
                {
                    drops.Add(pickupConfig);
                }
            }

            for (var index = 0; index < drops.Count; index++)
            {
                SpawnPickup(drops[index], position, index, drops.Count, true);
            }
        }

        public PickupItemController SpawnPickup(PickupItemConfig pickupConfig, Vector3 position)
        {
            return SpawnPickup(pickupConfig, position, 0, 1, true);
        }

        public PickupItemController SpawnPickup(string itemId, Vector3 position)
        {
            return SpawnPickup(itemId, position, true);
        }

        public PickupItemController SpawnPickup(string itemId, Vector3 position, bool countTowardProgress)
        {
            return SpawnPickup(ResolvePickupConfig(itemId), position, 0, 1, countTowardProgress);
        }

        public void AttractAllStarsToPlayer(PlayerController player)
        {
            if (player == null)
            {
                return;
            }

            foreach (var pickup in activePickups)
            {
                if (pickup != null && !pickup.IsCollected && pickup.IsStarPickup)
                {
                    pickup.BeginForcedAttract(player);
                }
            }
        }

        public void AttractAllPickupsToPlayer(PlayerController player)
        {
            if (player == null)
            {
                return;
            }

            foreach (var pickup in activePickups)
            {
                if (pickup != null && !pickup.IsCollected)
                {
                    pickup.BeginForcedAttract(player);
                }
            }
        }

        public bool HasActivePickups()
        {
            foreach (var pickup in activePickups)
            {
                if (pickup != null && !pickup.IsCollected)
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasActiveStarOrCoinPickups()
        {
            foreach (var pickup in activePickups)
            {
                if (pickup != null && !pickup.IsCollected && pickup.IsStarOrCoinPickup)
                {
                    return true;
                }
            }

            return false;
        }

        public void KillAllMinions()
        {
            var enemies = FindObjectsOfType<EnemyController>();
            foreach (var enemy in enemies)
            {
                if (enemy != null && enemy.GetComponent<BossController>() == null)
                {
                    enemy.KillInstantly();
                }
            }
        }

        protected override void Awake()
        {
            base.Awake();

            if (Instance == this)
            {
                CacheLayerRoot();
                EnsurePool();
            }
        }

        public void Recycle(PickupItemController pickup)
        {
            if (pickup == null)
            {
                return;
            }

            if (!activePickups.Remove(pickup) && !pickup.gameObject.activeSelf)
            {
                return;
            }

            pickup.DeactivateForPool();
            pickup.transform.SetParent(transform, false);
            pooledPickups.Push(pickup);
        }

        private PickupItemController SpawnPickup(
            PickupItemConfig pickupConfig,
            Vector3 position,
            int index,
            int count,
            bool countTowardProgress)
        {
            if (pickupConfig == null)
            {
                return null;
            }

            EnsurePool();

            var pickup = GetPickup();
            pickup.transform.SetParent(GetLayerRoot(), false);
            pickup.transform.position = position + ResolveScatterOffset(index, count);
            pickup.transform.rotation = Quaternion.identity;
            pickup.gameObject.SetActive(true);
            activePickups.Add(pickup);

            pickup.Initialize(pickupConfig, this);
            if (countTowardProgress && LevelProgressService.IsStarPickup(pickupConfig))
            {
                LevelProgressService.RecordStarSpawned(LevelProgressService.GetPickupStarValue(pickupConfig));
            }

            return pickup;
        }

        private PickupItemConfig ResolvePickupConfig(string itemId)
        {
            if (!string.IsNullOrEmpty(itemId) && pickupConfigCache.TryGetValue(itemId, out var cachedConfig))
            {
                return cachedConfig;
            }

            var pickupConfig = ConfigManager.Instance != null && ConfigManager.Instance.IsLoaded
                ? ConfigManager.Instance.GetPickupItem(itemId)
                : null;

            if (pickupConfig != null)
            {
                CachePickupConfig(itemId, pickupConfig);
                return pickupConfig;
            }

            if (string.Equals(itemId, "star", System.StringComparison.OrdinalIgnoreCase))
            {
                pickupConfig = new PickupItemConfig
                {
                    id = "star",
                    displayName = "星星",
                    itemType = "Star",
                    spritePath = "Assets/Art/Sprites/Item/item_star.png",
                    starValue = 1,
                    lifetime = 12f,
                    driftSpeed = 1.1f,
                    pickupRadius = 0.22f,
                    visualScale = 0.62f
                };
                CachePickupConfig(itemId, pickupConfig);
                return pickupConfig;
            }

            if (string.Equals(itemId, "trophy", System.StringComparison.OrdinalIgnoreCase))
            {
                pickupConfig = new PickupItemConfig
                {
                    id = "trophy",
                    displayName = "奖杯",
                    itemType = "Trophy",
                    spritePath = "Sprites/Item/item_trophy",
                    lifetime = 0f,
                    driftSpeed = 2.1f,
                    pickupRadius = 1.5f,
                    visualScale = 1f
                };
                CachePickupConfig(itemId, pickupConfig);
                return pickupConfig;
            }

            Debug.LogWarning($"Pickup item config not found: {itemId}");
            return null;
        }

        private void EnsurePool()
        {
            if (isPoolWarmed)
            {
                return;
            }

            var count = Mathf.Max(0, initialPoolSize);
            for (var index = 0; index < count; index++)
            {
                pooledPickups.Push(CreatePickup());
            }

            isPoolWarmed = true;
        }

        private PickupItemController GetPickup()
        {
            return pooledPickups.Count > 0 ? pooledPickups.Pop() : CreatePickup();
        }

        private PickupItemController CreatePickup()
        {
            var pickupObject = new GameObject("Pickup");
            pickupObject.transform.SetParent(transform, false);
            pickupObject.SetActive(false);
            return pickupObject.AddComponent<PickupItemController>();
        }

        private void CachePickupConfig(string itemId, PickupItemConfig pickupConfig)
        {
            if (!string.IsNullOrEmpty(itemId) && pickupConfig != null)
            {
                pickupConfigCache[itemId] = pickupConfig;
            }
        }

        private Transform GetLayerRoot()
        {
            CacheLayerRoot();
            return pickupLayer != null ? pickupLayer : transform;
        }

        private void CacheLayerRoot()
        {
            if (pickupLayer != null)
            {
                return;
            }

            var found = GameObject.Find("PickupLayer");
            if (found == null)
            {
                found = new GameObject("PickupLayer");
                var root = GameObject.Find("GameRoot");
                if (root != null)
                {
                    found.transform.SetParent(root.transform);
                }
            }

            pickupLayer = found.transform;
        }

        private static Vector3 ResolveScatterOffset(int index, int count)
        {
            if (count <= 1)
            {
                return Vector3.zero;
            }

            var angle = (index / (float)count) * Mathf.PI * 2f + 0.35f;
            var radius = DropScatterRadius;
            return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
        }
    }
}
