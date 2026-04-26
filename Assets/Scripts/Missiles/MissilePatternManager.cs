using System;
using System.Collections;
using System.Collections.Generic;
using LeiTing.Config;
using LeiTing.Core;
using LeiTing.Player;
using UnityEngine;

namespace LeiTing.Missiles
{
    public class MissilePatternManager : MonoSingleton<MissilePatternManager>
    {
        private readonly Dictionary<string, float> rotationOffsets = new Dictionary<string, float>();

        public void FirePattern(string patternId, Vector2 origin)
        {
            var pattern = ConfigManager.Instance != null && ConfigManager.Instance.IsLoaded
                ? ConfigManager.Instance.GetMissilePattern(patternId)
                : null;

            if (pattern == null)
            {
                Debug.LogWarning($"Missile pattern config not found: {patternId}");
                return;
            }

            FirePattern(pattern, origin);
        }

        public void FirePattern(MissilePatternConfig pattern, Vector2 origin)
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

        private IEnumerator FireBurst(MissilePatternConfig pattern, Vector2 origin, int burstCount)
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

        private void FireVolley(MissilePatternConfig pattern, Vector2 origin)
        {
            if (ConfigManager.Instance == null || !ConfigManager.Instance.IsLoaded)
            {
                return;
            }

            var missileManager = EnsureMissileManager();
            if (missileManager == null)
            {
                return;
            }

            var missileConfig = ConfigManager.Instance.GetMissile(pattern.missileId);
            if (missileConfig == null)
            {
                Debug.LogWarning($"Missile config not found for pattern {pattern.id}: {pattern.missileId}");
                return;
            }

            var runtimeMissile = CreateRuntimeMissileConfig(missileConfig, pattern);
            var fireOrigin = origin + pattern.firePointOffset;
            var baseAngle = ResolveBaseAngle(pattern, fireOrigin);
            var angles = ResolveAngles(pattern, baseAngle);

            for (var index = 0; index < angles.Count; index++)
            {
                missileManager.Fire(runtimeMissile, fireOrigin, DirectionFromAngle(angles[index]));
            }
        }

        private MissileConfig CreateRuntimeMissileConfig(MissileConfig source, MissilePatternConfig pattern)
        {
            return new MissileConfig
            {
                id = source.id,
                missileId = source.missileId,
                name = source.name,
                behaviorType = source.behaviorType,
                speed = pattern.missileSpeed > 0f ? pattern.missileSpeed : source.speed,
                maxSpeed = source.maxSpeed,
                acceleration = source.acceleration,
                lifeTime = pattern.missileLifetime > 0f ? pattern.missileLifetime : source.lifeTime,
                damage = source.damage,
                radius = source.radius,
                turnSpeed = source.turnSpeed,
                trackTime = source.trackTime,
                lockDelay = source.lockDelay,
                warningTime = source.warningTime,
                explodeTime = source.explodeTime,
                explodeRadius = source.explodeRadius,
                splitTime = source.splitTime,
                splitCount = source.splitCount,
                splitAngle = source.splitAngle,
                childMissileId = source.childMissileId,
                canBeDestroyed = source.canBeDestroyed,
                hp = source.hp,
                isLoopTrack = source.isLoopTrack,
                prefabPath = source.prefabPath,
                bodyRes = source.bodyRes,
                flyAnim = source.flyAnim,
                tailType = source.tailType,
                tailRes = source.tailRes,
                tailColor = source.tailColor,
                warningRes = source.warningRes,
                lockEffectRes = source.lockEffectRes,
                explodeEffectRes = source.explodeEffectRes,
                hitEffectRes = source.hitEffectRes,
                destroyEffectRes = source.destroyEffectRes,
                effectRes = source.effectRes,
                soundRes = source.soundRes,
                soundLaunch = source.soundLaunch,
                soundLock = source.soundLock,
                soundExplode = source.soundExplode,
                waveAmplitude = source.waveAmplitude,
                waveFrequency = source.waveFrequency,
                releaseInterval = source.releaseInterval,
                triggerRadius = source.triggerRadius,
                returnDelay = source.returnDelay
            };
        }

        private float ResolveBaseAngle(MissilePatternConfig pattern, Vector2 origin)
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

        private List<float> ResolveAngles(MissilePatternConfig pattern, float baseAngle)
        {
            var patternType = string.IsNullOrEmpty(pattern.patternType) ? "Single" : pattern.patternType;
            var missileCount = Mathf.Max(1, pattern.missileCount);
            var angles = new List<float>(missileCount);

            if (IsPatternType(patternType, "Ring"))
            {
                var step = pattern.angleStep > 0f ? pattern.angleStep : 360f / missileCount;
                for (var index = 0; index < missileCount; index++)
                {
                    angles.Add(baseAngle + step * index);
                }

                return angles;
            }

            if (IsPatternType(patternType, "Fan") || IsPatternType(patternType, "Aim") || IsPatternType(patternType, "Rotating"))
            {
                var totalSpread = pattern.spreadAngle > 0f ? pattern.spreadAngle : pattern.angleStep * Mathf.Max(0, missileCount - 1);
                var step = missileCount > 1 ? totalSpread / (missileCount - 1) : 0f;
                var startAngle = baseAngle - totalSpread * 0.5f;

                for (var index = 0; index < missileCount; index++)
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

        private MissileManager EnsureMissileManager()
        {
            if (MissileManager.Instance != null)
            {
                return MissileManager.Instance;
            }

            var managers = GameObject.Find("Managers") ?? new GameObject("Managers");
            return managers.GetComponent<MissileManager>() ?? managers.AddComponent<MissileManager>();
        }

        private static Vector2 DirectionFromAngle(float angle)
        {
            var radians = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
        }

        private static bool IsPatternType(MissilePatternConfig pattern, string expected)
        {
            return IsPatternType(pattern.patternType, expected);
        }

        private static bool IsPatternType(string patternType, string expected)
        {
            return string.Equals(patternType, expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}
