using LeiTing.Bullets;
using LeiTing.Config;
using LeiTing.Core;
using UnityEngine;

namespace LeiTing.Player
{
    [DisallowMultipleComponent]
    public class PlayerShooter : MonoBehaviour
    {
        private const string DefaultBulletId = "player_bullet_01";

        [SerializeField] private PlayerConfig config;
        [SerializeField] private Transform firePoint;
        [SerializeField] private bool autoFire = true;
        [SerializeField] private float fallbackFireInterval = 0.2f;
        [SerializeField] private string fallbackBulletId = DefaultBulletId;
        [SerializeField] private Vector2 firePointOffset = new Vector2(0f, 0.45f);

        private string selectedBulletId;
        private float nextFireTime;

        public string CurrentBulletId => GetBulletId();

        public bool AutoFire
        {
            get => autoFire;
            set => autoFire = value;
        }

        public void ApplyConfig(PlayerConfig playerConfig)
        {
            config = playerConfig;
        }

        public void SetBulletId(string bulletId)
        {
            if (string.IsNullOrEmpty(bulletId))
            {
                return;
            }

            selectedBulletId = bulletId;
            nextFireTime = Time.time;
        }

        public bool TryFire()
        {
            var bulletConfig = ResolveBulletConfig();
            if (bulletConfig == null)
            {
                return false;
            }

            var bulletManager = EnsureBulletManager();
            if (bulletManager == null)
            {
                return false;
            }

            FirePattern(bulletManager, bulletConfig);
            return true;
        }

        private void Awake()
        {
            EnsureFirePoint();
        }

        private void Start()
        {
            if (!HasRuntimeConfig() && ConfigManager.Instance != null && ConfigManager.Instance.IsLoaded)
            {
                ApplyConfig(ConfigManager.Instance.Config.player);
            }

            nextFireTime = Time.time;
        }

        private void Update()
        {
            if (!autoFire)
            {
                return;
            }

            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            {
                return;
            }

            if (Time.time < nextFireTime)
            {
                return;
            }

            var interval = GetFireInterval();
            nextFireTime = Time.time + interval;
            TryFire();
        }

        private void EnsureFirePoint()
        {
            if (firePoint != null)
            {
                return;
            }

            var found = transform.Find("FirePoint");
            if (found == null)
            {
                found = new GameObject("FirePoint").transform;
                found.SetParent(transform);
                found.localRotation = Quaternion.identity;
                found.localScale = Vector3.one;
            }

            found.localPosition = firePointOffset;
            firePoint = found;
        }

        private Vector2 GetFirePosition()
        {
            EnsureFirePoint();
            return firePoint != null ? firePoint.position : transform.position + (Vector3)firePointOffset;
        }

        private float GetFireInterval()
        {
            if (config != null && config.fireInterval > 0f)
            {
                return config.fireInterval;
            }

            return Mathf.Max(0.02f, fallbackFireInterval);
        }

        private string GetBulletId()
        {
            if (!string.IsNullOrEmpty(selectedBulletId))
            {
                return selectedBulletId;
            }

            if (config != null && !string.IsNullOrEmpty(config.defaultBulletId))
            {
                return config.defaultBulletId;
            }

            return string.IsNullOrEmpty(fallbackBulletId) ? DefaultBulletId : fallbackBulletId;
        }

        private BulletConfig ResolveBulletConfig()
        {
            var bulletId = GetBulletId();
            var bulletConfig = ConfigManager.Instance != null && ConfigManager.Instance.IsLoaded
                ? ConfigManager.Instance.GetBullet(bulletId)
                : null;

            if (bulletConfig != null)
            {
                return bulletConfig;
            }

            return new BulletConfig
            {
                id = bulletId,
                owner = "Player",
                damage = 1,
                speed = 12f,
                lifetime = 2f,
                size = new Vector2(0.12f, 0.32f),
                projectileCount = 1
            };
        }

        private void FirePattern(BulletManager bulletManager, BulletConfig bulletConfig)
        {
            var pattern = string.IsNullOrEmpty(bulletConfig.firePattern) ? "Single" : bulletConfig.firePattern;
            var origin = GetFirePosition();
            var baseDirection = Vector2.up;

            if (IsPattern(pattern, "Laser"))
            {
                bulletManager.Fire(bulletConfig, origin, baseDirection);
                return;
            }

            var projectileCount = ResolveProjectileCount(bulletConfig, pattern);
            var spreadAngle = Mathf.Max(0f, bulletConfig.spreadAngle);
            var muzzleSpacing = Mathf.Max(0f, bulletConfig.muzzleSpacing);

            for (var index = 0; index < projectileCount; index++)
            {
                var centeredIndex = index - (projectileCount - 1) * 0.5f;
                var angle = projectileCount > 1 ? centeredIndex * spreadAngle / Mathf.Max(1, projectileCount - 1) : 0f;
                var direction = Rotate(baseDirection, angle);
                var spawnPosition = origin + Vector2.right * centeredIndex * muzzleSpacing;
                bulletManager.Fire(bulletConfig, spawnPosition, direction);
            }
        }

        private static int ResolveProjectileCount(BulletConfig bulletConfig, string pattern)
        {
            if (bulletConfig.projectileCount > 0)
            {
                return bulletConfig.projectileCount;
            }

            if (IsPattern(pattern, "Double"))
            {
                return 2;
            }

            if (IsPattern(pattern, "Spread"))
            {
                return 3;
            }

            return 1;
        }

        private static Vector2 Rotate(Vector2 vector, float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            var sin = Mathf.Sin(radians);
            var cos = Mathf.Cos(radians);
            return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos).normalized;
        }

        private static bool IsPattern(string pattern, string expected)
        {
            return string.Equals(pattern, expected, System.StringComparison.OrdinalIgnoreCase);
        }

        private BulletManager EnsureBulletManager()
        {
            if (BulletManager.Instance != null)
            {
                return BulletManager.Instance;
            }

            var managers = GameObject.Find("Managers");
            if (managers == null)
            {
                managers = new GameObject("Managers");
            }

            return managers.GetComponent<BulletManager>() ?? managers.AddComponent<BulletManager>();
        }

        private bool HasRuntimeConfig()
        {
            return config != null && !string.IsNullOrEmpty(config.id);
        }
    }
}
