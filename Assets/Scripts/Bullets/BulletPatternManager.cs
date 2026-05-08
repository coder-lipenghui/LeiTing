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
        private delegate bool TryResolveOrigin(out Vector2 origin);

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
            FirePattern(pattern, (out Vector2 resolvedOrigin) =>
            {
                resolvedOrigin = origin;
                return true;
            });
        }

        public void FirePattern(BulletPatternConfig pattern, Transform originTransform)
        {
            FirePattern(pattern, (out Vector2 resolvedOrigin) =>
            {
                if (originTransform == null)
                {
                    resolvedOrigin = Vector2.zero;
                    return false;
                }

                resolvedOrigin = originTransform.position;
                return true;
            });
        }

        private void FirePattern(BulletPatternConfig pattern, TryResolveOrigin tryResolveOrigin)
        {
            if (pattern == null)
            {
                return;
            }

            if (IsPatternType(pattern, "Spiral"))
            {
                StartCoroutine(FireSpiral(pattern, tryResolveOrigin));
                return;
            }

            var burstCount = Mathf.Max(1, pattern.burstCount);
            if (burstCount <= 1)
            {
                if (tryResolveOrigin(out var origin))
                {
                    FireVolley(pattern, origin);
                }

                return;
            }

            StartCoroutine(FireBurst(pattern, tryResolveOrigin, burstCount));
        }

        private IEnumerator FireBurst(BulletPatternConfig pattern, TryResolveOrigin tryResolveOrigin, int burstCount)
        {
            var interval = Mathf.Max(0.01f, pattern.fireInterval);

            for (var index = 0; index < burstCount; index++)
            {
                if (!CanFirePattern() || !tryResolveOrigin(out var origin))
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

        private IEnumerator FireSpiral(BulletPatternConfig pattern, TryResolveOrigin tryResolveOrigin)
        {
            if (!TryCreateRuntimeBulletConfig(pattern, out var runtimeBullet))
            {
                yield break;
            }

            var interval = Mathf.Max(0.01f, pattern.fireInterval);
            var volleyCount = ResolveSpiralVolleyCount(pattern, interval);
            var bulletCount = ResolveBulletCountPerBurst(pattern);
            var rotateStep = ResolveSpiralRotateStep(pattern);
            var key = GetRotationKey(pattern);
            rotationOffsets.TryGetValue(key, out var offset);

            for (var index = 0; index < volleyCount; index++)
            {
                if (!CanFirePattern() || !tryResolveOrigin(out var origin))
                {
                    yield break;
                }

                var fireOrigin = origin + pattern.firePointOffset;
                var baseAngle = ResolveAimAdjustedBaseAngle(pattern, fireOrigin) + offset;
                FireRingVolley(runtimeBullet, fireOrigin, baseAngle, bulletCount);

                offset += rotateStep;
                rotationOffsets[key] = offset;

                if (index < volleyCount - 1)
                {
                    yield return new WaitForSeconds(interval);
                }
            }
        }

        private void FireVolley(BulletPatternConfig pattern, Vector2 origin)
        {
            if (!TryCreateRuntimeBulletConfig(pattern, out var runtimeBullet))
            {
                return;
            }

            var fireOrigin = origin + pattern.firePointOffset;
            var baseAngle = ResolveBaseAngle(pattern, fireOrigin);
            var angles = ResolveAngles(pattern, baseAngle);

            for (var index = 0; index < angles.Count; index++)
            {
                BulletManager.Instance.Fire(runtimeBullet, fireOrigin, DirectionFromAngle(angles[index]));
            }
        }

        private bool TryCreateRuntimeBulletConfig(BulletPatternConfig pattern, out BulletConfig runtimeBullet)
        {
            runtimeBullet = null;
            if (BulletManager.Instance == null || ConfigManager.Instance == null || !ConfigManager.Instance.IsLoaded)
            {
                return false;
            }

            var bulletConfig = ConfigManager.Instance.GetBullet(pattern.bulletId);
            if (bulletConfig == null)
            {
                Debug.LogWarning($"Bullet config not found for pattern {pattern.id}: {pattern.bulletId}");
                return false;
            }

            runtimeBullet = CreateRuntimeBulletConfig(bulletConfig, pattern);
            return true;
        }

        private void FireRingVolley(BulletConfig runtimeBullet, Vector2 fireOrigin, float baseAngle, int bulletCount)
        {
            var angleStep = 360f / bulletCount;
            for (var index = 0; index < bulletCount; index++)
            {
                BulletManager.Instance.Fire(runtimeBullet, fireOrigin, DirectionFromAngle(baseAngle + angleStep * index));
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
                glowColor = source.glowColor,
                glowRange = source.glowRange,
                projectileCount = source.projectileCount,
                spreadAngle = source.spreadAngle,
                muzzleSpacing = source.muzzleSpacing,
                pierceCount = source.pierceCount,
                laserLength = source.laserLength
            };
        }

        private float ResolveBaseAngle(BulletPatternConfig pattern, Vector2 origin)
        {
            var baseAngle = ResolveAimAdjustedBaseAngle(pattern, origin);

            if (pattern.rotate || IsPatternType(pattern, "Rotating"))
            {
                var key = GetRotationKey(pattern);
                rotationOffsets.TryGetValue(key, out var offset);
                baseAngle += offset;
                rotationOffsets[key] = offset + ResolveRotateStep(pattern);
            }

            return baseAngle;
        }

        private float ResolveAimAdjustedBaseAngle(BulletPatternConfig pattern, Vector2 origin)
        {
            var baseAngle = pattern.baseAngle;
            return pattern.aimAtPlayer || IsPatternType(pattern, "Aim")
                ? AngleToPlayer(origin, baseAngle)
                : baseAngle;
        }

        private int ResolveSpiralVolleyCount(BulletPatternConfig pattern, float interval)
        {
            if (pattern.duration > 0f)
            {
                return Mathf.Max(1, Mathf.CeilToInt(pattern.duration / interval - 0.0001f));
            }

            return Mathf.Max(1, pattern.burstCount);
        }

        private int ResolveBulletCountPerBurst(BulletPatternConfig pattern)
        {
            return Mathf.Max(1, pattern.bulletCountPerBurst > 0 ? pattern.bulletCountPerBurst : pattern.bulletCount);
        }

        private float ResolveRotateStep(BulletPatternConfig pattern)
        {
            if (pattern.rotateStepDegrees > 0f)
            {
                return pattern.clockwise ? -pattern.rotateStepDegrees : pattern.rotateStepDegrees;
            }

            if (pattern.clockwise && pattern.rotationSpeed > 0f)
            {
                return -pattern.rotationSpeed;
            }

            return pattern.rotationSpeed;
        }

        private float ResolveSpiralRotateStep(BulletPatternConfig pattern)
        {
            var step = pattern.rotateStepDegrees > 0f ? pattern.rotateStepDegrees : Mathf.Abs(pattern.rotationSpeed);
            if (step <= 0f)
            {
                return 0f;
            }

            var clockwise = pattern.clockwise || pattern.rotationSpeed < 0f;
            return clockwise ? -step : step;
        }

        private static bool CanFirePattern()
        {
            return GameManager.Instance == null || GameManager.Instance.CurrentState == GameState.Playing;
        }

        private static string GetRotationKey(BulletPatternConfig pattern)
        {
            return string.IsNullOrEmpty(pattern.id) ? pattern.GetHashCode().ToString() : pattern.id;
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
