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
        private static Material warningMaterial;

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
            FirePattern(pattern, null, origin, false);
        }

        public void FirePattern(MissilePatternConfig pattern, Transform originTransform)
        {
            var fallbackOrigin = originTransform != null ? (Vector2)originTransform.position : Vector2.zero;
            FirePattern(pattern, originTransform, fallbackOrigin, originTransform != null);
        }

        private void FirePattern(MissilePatternConfig pattern, Transform originTransform, Vector2 fallbackOrigin, bool requireLiveOrigin)
        {
            if (pattern == null)
            {
                return;
            }

            var burstCount = Mathf.Max(1, pattern.burstCount);
            if (burstCount <= 1)
            {
                FireVolley(pattern, originTransform, fallbackOrigin, requireLiveOrigin);
                return;
            }

            StartCoroutine(FireBurst(pattern, originTransform, fallbackOrigin, requireLiveOrigin, burstCount));
        }

        private IEnumerator FireBurst(MissilePatternConfig pattern, Transform originTransform, Vector2 fallbackOrigin, bool requireLiveOrigin, int burstCount)
        {
            var interval = Mathf.Max(0.01f, pattern.fireInterval);

            for (var index = 0; index < burstCount; index++)
            {
                if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
                {
                    yield break;
                }

                if (requireLiveOrigin && originTransform == null)
                {
                    yield break;
                }

                FireVolley(pattern, originTransform, fallbackOrigin, requireLiveOrigin);

                if (index < burstCount - 1)
                {
                    yield return new WaitForSeconds(interval);
                }
            }
        }

        private void FireVolley(MissilePatternConfig pattern, Transform originTransform, Vector2 fallbackOrigin, bool requireLiveOrigin)
        {
            if (requireLiveOrigin && originTransform == null)
            {
                return;
            }

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
            var fireOrigin = ResolveFireOrigin(pattern, originTransform, fallbackOrigin);
            var baseAngle = ResolveBaseAngle(pattern, fireOrigin);
            var angles = ResolveAngles(pattern, baseAngle);

            for (var index = 0; index < angles.Count; index++)
            {
                var fireDirection = DirectionFromAngle(angles[index]);
                if (ShouldUseExternalLock(runtimeMissile))
                {
                    StartCoroutine(FireLockDashAfterDelay(missileManager, runtimeMissile, pattern, originTransform, fallbackOrigin, requireLiveOrigin, fireDirection));
                    continue;
                }

                missileManager.Fire(runtimeMissile, fireOrigin, fireDirection);
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

        private IEnumerator FireLockDashAfterDelay(MissileManager missileManager, MissileConfig missileConfig, MissilePatternConfig pattern, Transform originTransform, Vector2 fallbackOrigin, bool requireLiveOrigin, Vector2 fallbackDirection)
        {
            var lockDuration = Mathf.Max(0.05f, missileConfig.lockDelay);
            var elapsed = 0f;
            var lockDirection = NormalizeDirection(fallbackDirection);
            var warningObject = CreateLockWarningLine(out var warningLine);

            while (elapsed < lockDuration)
            {
                if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
                {
                    DestroyLockWarning(warningObject);
                    yield break;
                }

                if (missileManager == null || requireLiveOrigin && originTransform == null)
                {
                    DestroyLockWarning(warningObject);
                    yield break;
                }

                var fireOrigin = ResolveFireOrigin(pattern, originTransform, fallbackOrigin);
                var playerTarget = ResolvePlayerTransform();
                lockDirection = ResolveDirectionToTarget(fireOrigin, lockDirection, playerTarget);
                UpdateLockWarningLine(warningLine, fireOrigin, lockDirection, playerTarget);

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (missileManager == null || requireLiveOrigin && originTransform == null)
            {
                DestroyLockWarning(warningObject);
                yield break;
            }

            var launchOrigin = ResolveFireOrigin(pattern, originTransform, fallbackOrigin);
            var launchTarget = ResolvePlayerTransform();
            var launchDirection = ResolveDirectionToTarget(launchOrigin, lockDirection, launchTarget);
            DestroyLockWarning(warningObject);
            missileManager.Fire(missileConfig, launchOrigin, launchDirection, true);
        }

        private GameObject CreateLockWarningLine(out LineRenderer warningLine)
        {
            var warningObject = new GameObject("MissileLockWarning");
            warningObject.transform.SetParent(transform, false);
            ApplyWarningLayer(warningObject);

            warningLine = warningObject.AddComponent<LineRenderer>();
            warningLine.useWorldSpace = true;
            warningLine.positionCount = 2;
            warningLine.startWidth = 0.045f;
            warningLine.endWidth = 0.018f;
            warningLine.startColor = new Color(1f, 0.08f, 0.05f, 0.86f);
            warningLine.endColor = new Color(1f, 0.22f, 0.06f, 0.28f);
            warningLine.material = GetWarningMaterial();
            warningLine.sortingOrder = 23;
            warningLine.numCapVertices = 4;
            warningLine.enabled = true;

            return warningObject;
        }

        private static void UpdateLockWarningLine(LineRenderer warningLine, Vector2 start, Vector2 direction, Transform playerTarget)
        {
            if (warningLine == null)
            {
                return;
            }

            var normalizedDirection = NormalizeDirection(direction);
            var end = playerTarget != null
                ? (Vector2)playerTarget.position
                : start + normalizedDirection * 8f;
            var pulse = Mathf.PingPong(Time.time * 8f, 1f);

            warningLine.startWidth = Mathf.Lerp(0.038f, 0.064f, pulse);
            warningLine.endWidth = Mathf.Lerp(0.014f, 0.026f, pulse);
            warningLine.startColor = new Color(1f, 0.06f, 0.04f, Mathf.Lerp(0.62f, 0.96f, pulse));
            warningLine.endColor = new Color(1f, 0.22f, 0.06f, Mathf.Lerp(0.18f, 0.44f, pulse));
            warningLine.SetPosition(0, start);
            warningLine.SetPosition(1, end);
        }

        private static void DestroyLockWarning(GameObject warningObject)
        {
            if (warningObject != null)
            {
                UnityEngine.Object.Destroy(warningObject);
            }
        }

        private static void ApplyWarningLayer(GameObject warningObject)
        {
            var layer = LayerMask.NameToLayer("EnemyMissile");
            if (layer < 0)
            {
                layer = LayerMask.NameToLayer("EnemyBullet");
            }

            if (layer >= 0)
            {
                warningObject.layer = layer;
            }
        }

        private static Material GetWarningMaterial()
        {
            if (warningMaterial == null)
            {
                warningMaterial = new Material(Shader.Find("Sprites/Default"));
            }

            return warningMaterial;
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

        private Transform ResolvePlayerTransform()
        {
            var player = FindObjectOfType<PlayerController>();
            return player != null ? player.transform : null;
        }

        private static Vector2 ResolveFireOrigin(MissilePatternConfig pattern, Transform originTransform, Vector2 fallbackOrigin)
        {
            var origin = originTransform != null ? (Vector2)originTransform.position : fallbackOrigin;
            return origin + pattern.firePointOffset;
        }

        private static Vector2 ResolveDirectionToTarget(Vector2 origin, Vector2 fallback, Transform target)
        {
            if (target == null)
            {
                return NormalizeDirection(fallback);
            }

            var direction = (Vector2)target.position - origin;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : NormalizeDirection(fallback);
        }

        private static Vector2 NormalizeDirection(Vector2 direction)
        {
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.down;
        }

        private static bool ShouldUseExternalLock(MissileConfig missileConfig)
        {
            return missileConfig != null
                && missileConfig.lockDelay > 0f
                && Enum.IsDefined(typeof(MissileBehaviorType), missileConfig.behaviorType)
                && (MissileBehaviorType)missileConfig.behaviorType == MissileBehaviorType.LockAndDash;
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
