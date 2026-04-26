using LeiTing.Config;
using LeiTing.Core;
using UnityEngine;

namespace LeiTing.Pickups
{
    public class PickupManager : MonoSingleton<PickupManager>
    {
        [SerializeField] private Transform pickupLayer;

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

            foreach (var drop in enemyConfig.drops)
            {
                if (drop == null || string.IsNullOrEmpty(drop.itemId) || drop.count <= 0)
                {
                    continue;
                }

                var pickupConfig = ResolvePickupConfig(drop.itemId);
                for (var index = 0; index < drop.count; index++)
                {
                    SpawnPickup(pickupConfig, position, index, drop.count);
                }
            }
        }

        public PickupItemController SpawnPickup(PickupItemConfig pickupConfig, Vector3 position)
        {
            return SpawnPickup(pickupConfig, position, 0, 1);
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
            var radius = Mathf.Min(0.55f, 0.12f + count * 0.018f);
            return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
        }
    }
}
