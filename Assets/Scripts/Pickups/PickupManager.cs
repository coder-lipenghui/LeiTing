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

        [SerializeField] private Transform pickupLayer;

        private readonly HashSet<string> droppedOnceItemIds = new HashSet<string>();

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
                SpawnPickup(drops[index], position, index, drops.Count);
            }
        }

        public PickupItemController SpawnPickup(PickupItemConfig pickupConfig, Vector3 position)
        {
            return SpawnPickup(pickupConfig, position, 0, 1);
        }

        public void AttractAllStarsToPlayer(PlayerController player)
        {
            if (player == null)
            {
                return;
            }

            var pickups = FindObjectsOfType<PickupItemController>();
            foreach (var pickup in pickups)
            {
                if (pickup != null && pickup.IsStarPickup)
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

            var pickups = FindObjectsOfType<PickupItemController>();
            foreach (var pickup in pickups)
            {
                if (pickup != null && !pickup.IsCollected)
                {
                    pickup.BeginForcedAttract(player);
                }
            }
        }

        public bool HasActivePickups()
        {
            var pickups = FindObjectsOfType<PickupItemController>();
            foreach (var pickup in pickups)
            {
                if (pickup != null && !pickup.IsCollected)
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
            }
        }

        private PickupItemController SpawnPickup(PickupItemConfig pickupConfig, Vector3 position, int index, int count)
        {
            if (pickupConfig == null)
            {
                return null;
            }

            var pickupObject = new GameObject(pickupConfig.id);
            pickupObject.transform.SetParent(GetLayerRoot(), false);
            pickupObject.transform.position = position + ResolveScatterOffset(index, count);

            var pickup = pickupObject.AddComponent<PickupItemController>();
            pickup.Initialize(pickupConfig);
            if (LevelProgressService.IsStarPickup(pickupConfig))
            {
                LevelProgressService.RecordStarSpawned(LevelProgressService.GetPickupStarValue(pickupConfig));
            }

            return pickup;
        }

        private PickupItemConfig ResolvePickupConfig(string itemId)
        {
            var pickupConfig = ConfigManager.Instance != null && ConfigManager.Instance.IsLoaded
                ? ConfigManager.Instance.GetPickupItem(itemId)
                : null;

            if (pickupConfig != null)
            {
                return pickupConfig;
            }

            if (string.Equals(itemId, "star", System.StringComparison.OrdinalIgnoreCase))
            {
                return new PickupItemConfig
                {
                    id = "star",
                    displayName = "Star",
                    itemType = "Star",
                    spritePath = "Assets/Art/Sprites/Item/item_star.png",
                    starValue = 1,
                    lifetime = 12f,
                    driftSpeed = 1.1f,
                    pickupRadius = 0.22f,
                    visualScale = 0.62f
                };
            }

            Debug.LogWarning($"Pickup item config not found: {itemId}");
            return null;
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
