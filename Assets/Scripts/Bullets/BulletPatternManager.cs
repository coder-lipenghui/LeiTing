using System;
using System.Collections;
using System.Collections.Generic;
using LeiTing.Config;
using LeiTing.Core;
using LeiTing.Player;
using UnityEngine;

namespace LeiTing.Bullets
{
    public class BulletPatternManager : MonoSingleton<BulletPatternManager>
    {
        private readonly Dictionary<string, float> rotationOffsets = new Dictionary<string, float>();

        public void FirePattern(string patternId, Vector2 origin)
        {
            var pattern = ConfigManager.Instance != null && ConfigManager.Instance.IsLoaded
                ? ConfigManager.Instance.GetBulletPattern(patternId)
                : null;

            if (pattern == null)
            {
                Debug.LogWarning($"Bullet pattern config not found: {patternId}");
                return;
            }

            FirePattern(pattern, origin);
        }

        public void FirePattern(BulletPatternConfig pattern, Vector2 origin)
        {
            if (pattern == null)
            {
                return;
            }

            var burstCount = Mathf.Max(1, pattern.burstCount);
            if (burstCount <= 1)
            {
                FireVolley(pattern, origin);
                return;
            }

            StartCoroutine(FireBurst(pattern, origin, burstCount));
        }

        private IEnumerator FireBurst(BulletPatternConfig pattern, Vector2 origin, int burstCount)
        {
            var interval = Mathf.Max(0.01f, pattern.fireInterval);

            for (var index = 0; index < burstCount; index++)
            {
                if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
                {
                    yield break;
                }

                FireVolley(pattern, origin);

                if (index < burstCount - 1)
                {
                    yield return new WaitForSeconds(interval);
                }
            }
        }

        private void FireVolley(BulletPatternConfig pattern, Vector2 origin)
        {
            if (BulletManager.Instance == null || ConfigManager.Instance == null || !ConfigManager.Instance.IsLoaded)
            {
                return;
            }

            var bulletConfig = ConfigManager.Instance.GetBullet(pattern.bulletId);
            if (bulletConfig == null)
            {
                Debug.LogWarning($"Bullet config not found for pattern {pattern.id}: {pattern.bulletId}");
                return;
            }

            var runtimeBullet = CreateRuntimeBulletConfig(bulletConfig, pattern);
            var fireOrigin = origin + pattern.firePointOffset;
            var baseAngle = ResolveBaseAngle(pattern, fireOrigin);
            var angles = ResolveAngles(pattern, baseAngle);

            for (var index = 0; index < angles.Count; index++)
            {
                BulletManager.Instance.Fire(runtimeBullet, fireOrigin, DirectionFromAngle(angles[index]));
            }
        }

        private BulletConfig CreateRuntimeBulletConfig(BulletConfig source, BulletPatternConfig pattern)
        {
            return new BulletConfig
            {
                id = source.id,
                owner = source.owner,
                firePattern = source.firePattern,
                spritePath = source.spritePath,
                damage = source.damage,
                speed = pattern.bulletSpeed > 0f ? pattern.bulletSpeed : source.speed,
                lifetime = pattern.bulletLifetime > 0f ? pattern.bulletLifetime : source.lifetime,
                size = source.size,
                projectileCount = source.projectileCount,
                spreadAngle = source.spreadAngle,
                muzzleSpacing = source.muzzleSpacing,
                pierceCount = source.pierceCount,
                laserLength = source.laserLength
            };
        }

        private float ResolveBaseAngle(BulletPatternConfig pattern, Vector2 origin)
        {
            var baseAngle = pattern.baseAngle;

            if (pattern.aimAtPlayer || IsPatternType(pattern, "Aim"))
            {
                baseAngle = AngleToPlayer(origin, baseAngle);
            }

            if (pattern.rotate || IsPatternType(pattern, "Rotating"))
            {
                var key = string.IsNullOrEmpty(pattern.id) ? pattern.GetHashCode().ToString() : pattern.id;
                rotationOffsets.TryGetValue(key, out var offset);
                baseAngle += offset;
                rotationOffsets[key] = offset + pattern.rotationSpeed;
            }

            return baseAngle;
        }

        private List<float> ResolveAngles(BulletPatternConfig pattern, float baseAngle)
        {
            var patternType = string.IsNullOrEmpty(pattern.patternType) ? "Single" : pattern.patternType;
            var bulletCount = Mathf.Max(1, pattern.bulletCount);
            var angles = new List<float>(bulletCount);

            if (IsPatternType(patternType, "Ring"))
            {
                var step = pattern.angleStep > 0f ? pattern.angleStep : 360f / bulletCount;
                for (var index = 0; index < bulletCount; index++)
                {
                    angles.Add(baseAngle + step * index);
                }

                return angles;
            }

            if (IsPatternType(patternType, "Fan") || IsPatternType(patternType, "Aim") || IsPatternType(patternType, "Rotating"))
            {
                var totalSpread = pattern.spreadAngle > 0f ? pattern.spreadAngle : pattern.angleStep * Mathf.Max(0, bulletCount - 1);
                var step = bulletCount > 1 ? totalSpread / (bulletCount - 1) : 0f;
                var startAngle = baseAngle - totalSpread * 0.5f;

                for (var index = 0; index < bulletCount; index++)
                {
                    angles.Add(startAngle + step * index);
                }

                return angles;
            }

            angles.Add(baseAngle);
            return angles;
        }

        private float AngleToPlayer(Vector2 origin, float fallbackAngle)
        {
            var player = FindObjectOfType<PlayerController>();
            if (player == null)
            {
                return fallbackAngle;
            }

            var direction = (Vector2)(player.transform.position - (Vector3)origin);
            if (direction.sqrMagnitude <= 0.001f)
            {
                return fallbackAngle;
            }

            return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        }

        private static Vector2 DirectionFromAngle(float angle)
        {
            var radians = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
        }

        private static bool IsPatternType(BulletPatternConfig pattern, string expected)
        {
            return IsPatternType(pattern.patternType, expected);
        }

        private static bool IsPatternType(string patternType, string expected)
        {
            return string.Equals(patternType, expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}
