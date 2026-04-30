using System;
using System.Collections.Generic;
using System.Linq;
using Luban.SimpleJSON;
using UnityEngine;

namespace LeiTing.Config
{
    public static class LubanConfigLoader
    {
        private const string ResourceRoot = "Luban";

        public static bool TryLoad(out GameConfig gameConfig)
        {
            gameConfig = null;

            try
            {
                if (!HasTable("leiting_tbplayer"))
                {
                    return false;
                }

                var tables = new global::cfg.Tables(LoadJsonNode);
                gameConfig = ToGameConfig(tables);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Luban config load failed, falling back to legacy GameConfig.json. {exception.Message}");
                gameConfig = null;
                return false;
            }
        }

        private static bool HasTable(string tableName)
        {
            return Resources.Load<TextAsset>($"{ResourceRoot}/{tableName}") != null;
        }

        private static JSONNode LoadJsonNode(string tableName)
        {
            var source = Resources.Load<TextAsset>($"{ResourceRoot}/{tableName}");
            if (source == null)
            {
                throw new InvalidOperationException($"Luban table not found: Resources/{ResourceRoot}/{tableName}.json");
            }

            var node = JSON.Parse(source.text);
            if (node == null)
            {
                throw new InvalidOperationException($"Luban table parse failed: Resources/{ResourceRoot}/{tableName}.json");
            }

            return node;
        }

        private static GameConfig ToGameConfig(global::cfg.Tables tables)
        {
            var gameConfig = new GameConfig
            {
                player = tables.TbPlayer.DataList.Count > 0 ? ToConfig(tables.TbPlayer.DataList[0]) : null,
                levels = tables.TbLevel.DataList.Select(ToConfig).ToList(),
                enemies = tables.TbEnemy.DataList.Select(ToConfig).ToList(),
                bullets = tables.TbBullet.DataList.Select(ToConfig).ToList(),
                missiles = tables.TbMissile.DataList.Select(ToConfig).ToList(),
                pickupItems = tables.TbPickupItem.DataList.Select(ToConfig).ToList(),
                bulletPatterns = tables.TbBulletPattern.DataList.Select(ToConfig).ToList(),
                missilePatterns = tables.TbMissilePattern.DataList.Select(ToConfig).ToList(),
                waves = tables.TbWave.DataList.Select(ToConfig).ToList(),
                stageEvents = tables.TbStageEvent.DataList.Select(ToConfig).ToList(),
                bossPhases = tables.TbBossPhase.DataList.Select(ToConfig).ToList(),
                bossSkills = tables.TbBossSkill.DataList.Select(ToConfig).ToList()
            };

            AttachEnemyDrops(gameConfig, tables.TbEnemyDrop.DataList);
            AttachWaveSpawns(gameConfig, tables.TbWaveSpawn.DataList);
            AttachBossPhasePatterns(gameConfig, tables.TbBossPhasePattern.DataList);

            return gameConfig;
        }

        private static void AttachEnemyDrops(GameConfig gameConfig, IReadOnlyList<global::cfg.leiting.EnemyDrop> dropRows)
        {
            if (gameConfig.enemies == null || dropRows == null || dropRows.Count == 0)
            {
                return;
            }

            var dropsByEnemy = dropRows
                .Where(row => row != null && !string.IsNullOrEmpty(row.EnemyId))
                .GroupBy(row => row.EnemyId)
                .ToDictionary(group => group.Key, group => group.OrderBy(row => row.Sort).Select(ToConfig).ToList());

            foreach (var enemy in gameConfig.enemies)
            {
                if (enemy == null || string.IsNullOrEmpty(enemy.id))
                {
                    continue;
                }

                if (dropsByEnemy.TryGetValue(enemy.id, out var drops))
                {
                    enemy.drops = drops;
                }
            }
        }

        private static void AttachWaveSpawns(GameConfig gameConfig, IReadOnlyList<global::cfg.leiting.WaveSpawn> spawnRows)
        {
            if (gameConfig.waves == null || spawnRows == null || spawnRows.Count == 0)
            {
                return;
            }

            var spawnsByWave = spawnRows
                .Where(row => row != null && !string.IsNullOrEmpty(row.WaveId))
                .GroupBy(row => row.WaveId)
                .ToDictionary(group => group.Key, group => group.OrderBy(row => row.Sort).Select(ToConfig).ToList());

            foreach (var wave in gameConfig.waves)
            {
                if (wave == null || string.IsNullOrEmpty(wave.id))
                {
                    continue;
                }

                if (spawnsByWave.TryGetValue(wave.id, out var spawns))
                {
                    wave.spawns = spawns;
                }
            }
        }

        private static void AttachBossPhasePatterns(GameConfig gameConfig, IReadOnlyList<global::cfg.leiting.BossPhasePattern> patternRows)
        {
            if (gameConfig.bossPhases == null || patternRows == null || patternRows.Count == 0)
            {
                return;
            }

            var patternsByPhase = patternRows
                .Where(row => row != null && !string.IsNullOrEmpty(row.BossPhaseId))
                .GroupBy(row => row.BossPhaseId)
                .ToDictionary(group => group.Key, group => group.OrderBy(row => row.Sort).Select(row => row.PatternId).Where(id => !string.IsNullOrEmpty(id)).ToList());

            foreach (var phase in gameConfig.bossPhases)
            {
                if (phase == null || string.IsNullOrEmpty(phase.id))
                {
                    continue;
                }

                if (patternsByPhase.TryGetValue(phase.id, out var patternIds))
                {
                    phase.bulletPatternIds = patternIds;
                }
            }
        }

        private static PlayerConfig ToConfig(global::cfg.leiting.Player row)
        {
            return new PlayerConfig
            {
                id = row.Id,
                displayName = row.DisplayName,
                prefabPath = row.PrefabPath,
                hp = row.Hp,
                shield = row.Shield,
                stars = row.Stars,
                coins = row.Coins,
                moveSpeed = row.MoveSpeed,
                invincibleTime = row.InvincibleTime,
                pickupAttractRange = row.PickupAttractRange,
                pickupAttractSpeed = row.PickupAttractSpeed,
                visualScale = row.VisualScale,
                hitboxRadius = row.HitboxRadius,
                hitboxOffset = new Vector2(row.HitboxOffsetX, row.HitboxOffsetY),
                defaultBulletId = row.DefaultBulletId,
                fireInterval = row.FireInterval
            };
        }

        private static LevelConfig ToConfig(global::cfg.leiting.Level row)
        {
            return new LevelConfig
            {
                id = row.Id,
                displayName = row.DisplayName,
                backgroundSpritePath = row.BackgroundSpritePath,
                backgroundScrollSpeed = row.BackgroundScrollSpeed,
                bgmPath = row.BgmPath
            };
        }

        private static EnemyConfig ToConfig(global::cfg.leiting.Enemy row)
        {
            return new EnemyConfig
            {
                id = row.Id,
                displayName = row.DisplayName,
                prefabPath = row.PrefabPath,
                hp = row.Hp,
                moveSpeed = row.MoveSpeed,
                attackInterval = row.AttackInterval,
                bulletId = row.BulletId,
                bulletPatternId = row.BulletPatternId,
                hitScaleFeedback = row.HitScaleFeedback,
                score = row.Score,
                drops = new List<DropConfig>()
            };
        }

        private static DropConfig ToConfig(global::cfg.leiting.EnemyDrop row)
        {
            return new DropConfig
            {
                itemId = row.ItemId,
                count = row.Count,
                dropOnce = row.DropOnce
            };
        }

        private static PickupItemConfig ToConfig(global::cfg.leiting.PickupItem row)
        {
            return new PickupItemConfig
            {
                id = row.Id,
                displayName = row.DisplayName,
                itemType = row.ItemType,
                spritePath = row.SpritePath,
                starValue = row.StarValue,
                coinValue = row.CoinValue,
                healValue = row.HealValue,
                shieldDuration = row.ShieldDuration,
                lifetime = row.Lifetime,
                driftSpeed = row.DriftSpeed,
                pickupRadius = row.PickupRadius,
                visualScale = row.VisualScale
            };
        }

        private static BulletConfig ToConfig(global::cfg.leiting.Bullet row)
        {
            return new BulletConfig
            {
                id = row.Id,
                owner = row.Owner,
                firePattern = row.FirePattern,
                spritePath = row.SpritePath,
                damage = row.Damage,
                speed = row.Speed,
                lifetime = row.Lifetime,
                size = new Vector2(row.SizeX, row.SizeY),
                glowColor = new Color(row.GlowColorR, row.GlowColorG, row.GlowColorB, row.GlowColorA),
                glowRange = row.GlowRange,
                projectileCount = row.ProjectileCount,
                spreadAngle = row.SpreadAngle,
                muzzleSpacing = row.MuzzleSpacing,
                pierceCount = row.PierceCount,
                laserLength = row.LaserLength
            };
        }

        private static MissileConfig ToConfig(global::cfg.leiting.Missile row)
        {
            return new MissileConfig
            {
                id = row.Id,
                missileId = row.MissileId,
                name = row.Name,
                behaviorType = row.BehaviorType,
                speed = row.Speed,
                maxSpeed = row.MaxSpeed,
                acceleration = row.Acceleration,
                lifeTime = row.LifeTime,
                damage = row.Damage,
                radius = row.Radius,
                turnSpeed = row.TurnSpeed,
                trackTime = row.TrackTime,
                lockDelay = row.LockDelay,
                warningTime = row.WarningTime,
                explodeTime = row.ExplodeTime,
                explodeRadius = row.ExplodeRadius,
                splitTime = row.SplitTime,
                splitCount = row.SplitCount,
                splitAngle = row.SplitAngle,
                childMissileId = row.ChildMissileId,
                canBeDestroyed = row.CanBeDestroyed,
                hp = row.Hp,
                isLoopTrack = row.IsLoopTrack,
                prefabPath = row.PrefabPath,
                bodyRes = row.BodyRes,
                flyAnim = row.FlyAnim,
                tailType = row.TailType,
                tailRes = row.TailRes,
                tailColor = row.TailColor,
                warningRes = row.WarningRes,
                lockEffectRes = row.LockEffectRes,
                explodeEffectRes = row.ExplodeEffectRes,
                hitEffectRes = row.HitEffectRes,
                destroyEffectRes = row.DestroyEffectRes,
                effectRes = row.EffectRes,
                soundRes = row.SoundRes,
                soundLaunch = row.SoundLaunch,
                soundLock = row.SoundLock,
                soundExplode = row.SoundExplode,
                waveAmplitude = row.WaveAmplitude,
                waveFrequency = row.WaveFrequency,
                releaseInterval = row.ReleaseInterval,
                triggerRadius = row.TriggerRadius,
                returnDelay = row.ReturnDelay
            };
        }

        private static WaveConfig ToConfig(global::cfg.leiting.Wave row)
        {
            return new WaveConfig
            {
                id = row.Id,
                levelId = row.LevelId,
                startTime = row.StartTime,
                spawns = new List<WaveSpawnConfig>()
            };
        }

        private static WaveSpawnConfig ToConfig(global::cfg.leiting.WaveSpawn row)
        {
            return new WaveSpawnConfig
            {
                enemyId = row.EnemyId,
                count = row.Count,
                interval = row.Interval,
                startPosition = new Vector2(row.StartPositionX, row.StartPositionY),
                attackPatternId = row.AttackPatternId,
                movementPath = row.MovementPath,
                pathAmplitude = row.PathAmplitude,
                pathSpeed = row.PathSpeed,
                holdDuration = row.HoldDuration
            };
        }

        private static StageEventConfig ToConfig(global::cfg.leiting.StageEvent row)
        {
            return new StageEventConfig
            {
                id = row.Id,
                levelId = row.LevelId,
                startTime = row.StartTime,
                message = row.Message,
                clearEnemyBullets = row.ClearEnemyBullets
            };
        }

        private static BulletPatternConfig ToConfig(global::cfg.leiting.BulletPattern row)
        {
            return new BulletPatternConfig
            {
                id = row.Id,
                patternType = row.PatternType,
                bulletId = row.BulletId,
                firePointGroup = row.FirePointGroup,
                firePointOffset = new Vector2(row.FirePointOffsetX, row.FirePointOffsetY),
                baseAngle = row.BaseAngle,
                bulletCount = row.BulletCount,
                angleStep = row.AngleStep,
                spreadAngle = row.SpreadAngle,
                bulletSpeed = row.BulletSpeed,
                bulletLifetime = row.BulletLifetime,
                rotate = row.Rotate,
                rotationSpeed = row.RotationSpeed,
                aimAtPlayer = row.AimAtPlayer,
                burstCount = row.BurstCount,
                fireInterval = row.FireInterval
            };
        }

        private static MissilePatternConfig ToConfig(global::cfg.leiting.MissilePattern row)
        {
            return new MissilePatternConfig
            {
                id = row.Id,
                patternType = row.PatternType,
                missileId = row.MissileId,
                firePointGroup = row.FirePointGroup,
                firePointOffset = new Vector2(row.FirePointOffsetX, row.FirePointOffsetY),
                baseAngle = row.BaseAngle,
                missileCount = row.MissileCount,
                angleStep = row.AngleStep,
                spreadAngle = row.SpreadAngle,
                missileSpeed = row.MissileSpeed,
                missileLifetime = row.MissileLifetime,
                rotate = row.Rotate,
                rotationSpeed = row.RotationSpeed,
                aimAtPlayer = row.AimAtPlayer,
                burstCount = row.BurstCount,
                fireInterval = row.FireInterval
            };
        }

        private static BossPhaseConfig ToConfig(global::cfg.leiting.BossPhase row)
        {
            return new BossPhaseConfig
            {
                id = row.Id,
                bossId = row.BossId,
                displayName = row.DisplayName,
                triggerHpPercent = row.TriggerHpPercent,
                attackInterval = row.AttackInterval,
                burstCount = row.BurstCount,
                burstInterval = row.BurstInterval,
                movementRange = new Vector2(row.MovementRangeX, row.MovementRangeY),
                movementSpeed = row.MovementSpeed,
                bulletPatternIds = new List<string>()
            };
        }

        private static BossSkillConfig ToConfig(global::cfg.leiting.BossSkill row)
        {
            return new BossSkillConfig
            {
                id = row.Id,
                bossId = row.BossId,
                bulletId = row.BulletId,
                triggerHpPercent = row.TriggerHpPercent,
                cooldown = row.Cooldown,
                bulletCount = row.BulletCount,
                spreadAngle = row.SpreadAngle
            };
        }
    }
}
