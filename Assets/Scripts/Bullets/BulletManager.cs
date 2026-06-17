using System;
using System.Collections;
using System.Collections.Generic;
using LeiTing.Config;
using LeiTing.Core;
using LeiTing.Pickups;
using UnityEngine;

namespace LeiTing.Bullets
{
    public class BulletManager : MonoSingleton<BulletManager>
    {
        [SerializeField] private int initialPoolSize = 32;
        [SerializeField] private int lightningStarSpawnBudgetPerFrame = 48;
        [SerializeField] private Transform playerBulletLayer;
        [SerializeField] private Transform enemyBulletLayer;

        private readonly Stack<BulletProjectile> pooledBullets = new Stack<BulletProjectile>();
        private readonly Queue<Vector3> pendingLightningStarPositions = new Queue<Vector3>(256);
        private bool isPoolWarmed;
        private Coroutine lightningStarSpawnCoroutine;

        public BulletProjectile Fire(BulletConfig bulletConfig, Vector2 position, Vector2 direction, Transform followTarget = null)
        {
            if (bulletConfig == null)
            {
                return null;
            }

            EnsurePool();

            var projectile = GetProjectile();
            projectile.transform.SetParent(GetLayerRoot(bulletConfig.owner), false);
            projectile.transform.position = position;
            projectile.transform.rotation = Quaternion.identity;
            projectile.Activate(bulletConfig, direction, this, followTarget);
            return projectile;
        }

        public void Recycle(BulletProjectile projectile)
        {
            if (projectile == null)
            {
                return;
            }

            projectile.DeactivateForPool();
            projectile.transform.SetParent(transform, false);
            pooledBullets.Push(projectile);
        }

        public void ClearEnemyBullets()
        {
            ClearBulletsInLayer(GetLayerRoot("Enemy"));
        }

        public int ConvertVisibleEnemyBulletsToStars()
        {
            return ConvertVisibleBulletsToStars(GetLayerRoot("Enemy"));
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

            CacheLayerRoots();

            var count = Mathf.Max(0, initialPoolSize);
            for (var index = 0; index < count; index++)
            {
                pooledBullets.Push(CreateProjectile());
            }

            isPoolWarmed = true;
        }

        private BulletProjectile GetProjectile()
        {
            return pooledBullets.Count > 0 ? pooledBullets.Pop() : CreateProjectile();
        }

        private BulletProjectile CreateProjectile()
        {
            var projectileObject = new GameObject("Bullet");
            projectileObject.transform.SetParent(transform, false);
            projectileObject.SetActive(false);
            return projectileObject.AddComponent<BulletProjectile>();
        }

        private Transform GetLayerRoot(string owner)
        {
            CacheLayerRoots();
            return string.Equals(owner, "Enemy", StringComparison.OrdinalIgnoreCase) ? enemyBulletLayer : playerBulletLayer;
        }

        private void CacheLayerRoots()
        {
            playerBulletLayer = playerBulletLayer != null ? playerBulletLayer : FindLayerRoot("BulletLayer_Player");
            enemyBulletLayer = enemyBulletLayer != null ? enemyBulletLayer : FindLayerRoot("BulletLayer_Enemy");
        }

        private Transform FindLayerRoot(string layerName)
        {
            var found = GameObject.Find(layerName);
            return found != null ? found.transform : transform;
        }

        private void ClearBulletsInLayer(Transform layerRoot)
        {
            if (layerRoot == null)
            {
                return;
            }

            for (var index = layerRoot.childCount - 1; index >= 0; index--)
            {
                var child = layerRoot.GetChild(index);
                var projectile = child.GetComponent<BulletProjectile>();
                if (projectile != null)
                {
                    Recycle(projectile);
                }
            }
        }

        private int ConvertVisibleBulletsToStars(Transform layerRoot)
        {
            if (layerRoot == null)
            {
                return 0;
            }

            var camera = Camera.main;
            var convertedCount = 0;
            for (var index = layerRoot.childCount - 1; index >= 0; index--)
            {
                var child = layerRoot.GetChild(index);
                var projectile = child.GetComponent<BulletProjectile>();
                if (projectile != null
                    && projectile.gameObject.activeInHierarchy
                    && IsVisibleOnScreen(projectile.transform.position, camera))
                {
                    pendingLightningStarPositions.Enqueue(projectile.transform.position);
                    convertedCount++;
                    Recycle(projectile);
                }
            }

            if (convertedCount > 0 && lightningStarSpawnCoroutine == null)
            {
                lightningStarSpawnCoroutine = StartCoroutine(SpawnPendingLightningStars());
            }

            return convertedCount;
        }

        private IEnumerator SpawnPendingLightningStars()
        {
            var budget = Mathf.Max(1, lightningStarSpawnBudgetPerFrame);
            while (pendingLightningStarPositions.Count > 0)
            {
                if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
                {
                    pendingLightningStarPositions.Clear();
                    break;
                }

                var pickupManager = PickupManager.GetOrCreate();
                for (var spawnedThisFrame = 0;
                    spawnedThisFrame < budget && pendingLightningStarPositions.Count > 0;
                    spawnedThisFrame++)
                {
                    pickupManager.SpawnPickup("star", pendingLightningStarPositions.Dequeue(), false);
                }

                yield return null;
            }

            lightningStarSpawnCoroutine = null;
        }

        private static bool IsVisibleOnScreen(Vector3 worldPosition, Camera camera)
        {
            if (camera == null)
            {
                return true;
            }

            const float margin = 0.08f;
            var viewportPosition = camera.WorldToViewportPoint(worldPosition);
            return viewportPosition.z >= 0f
                && viewportPosition.x >= -margin
                && viewportPosition.x <= 1f + margin
                && viewportPosition.y >= -margin
                && viewportPosition.y <= 1f + margin;
        }
    }
}
