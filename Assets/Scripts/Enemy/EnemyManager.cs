using System.Collections;
using System.Collections.Generic;
using LeiTing.Bullets;
using LeiTing.Config;
using LeiTing.Core;
using LeiTing.Missiles;
using LeiTing.Progress;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LeiTing.Enemy
{
    public class EnemyManager : MonoSingleton<EnemyManager>
    {
        private const float BossHandoffDelay = 1.25f;

        private readonly HashSet<string> startedWaves = new HashSet<string>();
        private int activeBossCount;
        private int timelinePausingBossCount;
        private int pendingBossSpawnCount;
        private int activeOrdinarySpawnGroupCount;
        private bool bossSpawnReserved;

        private void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Playing)
            {
                return;
            }

            if (ConfigManager.Instance == null || !ConfigManager.Instance.IsLoaded || ConfigManager.Instance.Config == null)
            {
                return;
            }

            UpdateWaves(ConfigManager.Instance);
        }

        public EnemyController SpawnEnemy(string enemyId, Vector2 position)
        {
            return SpawnEnemy(enemyId, position, null, null, IsBossId(enemyId));
        }

        private void UpdateWaves(ConfigManager configManager)
        {
            foreach (var wave in configManager.GetWavesForLevel(GameManager.Instance.CurrentLevelNumber))
            {
                if (wave == null || startedWaves.Contains(wave.id))
                {
                    continue;
                }

                if (wave.startTime < GameManager.Instance.CurrentBattleStartTime)
                {
                    startedWaves.Add(wave.id);
                    continue;
                }

                var waveTime = IsFinalBossWave(wave)
                    ? GameManager.Instance.BattleElapsedTime
                    : GameManager.Instance.BattleTimelineTime;
                if (waveTime < wave.startTime)
                {
                    continue;
                }

                startedWaves.Add(wave.id);
                StartCoroutine(SpawnWave(wave, true));
            }
        }

        private IEnumerator SpawnWave(WaveConfig wave, bool enforceBossGate)
        {
            if (wave?.spawns == null)
            {
                yield break;
            }

            foreach (var spawn in wave.spawns)
            {
                if (spawn == null)
                {
                    continue;
                }

                var isOrdinaryGroup = !IsBossId(ResolveSpawnEnemyId(spawn));
                if (isOrdinaryGroup)
                {
                    activeOrdinarySpawnGroupCount++;
                }

                yield return StartCoroutine(SpawnGroup(wave, spawn, enforceBossGate));

                if (isOrdinaryGroup)
                {
                    activeOrdinarySpawnGroupCount = Mathf.Max(0, activeOrdinarySpawnGroupCount - 1);
                }
            }
        }

        private IEnumerator SpawnGroup(WaveConfig wave, WaveSpawnConfig spawn, bool enforceBossGate)
        {
            var count = Mathf.Max(1, spawn.count);
            var interval = Mathf.Max(0.01f, spawn.interval);
            var enemyId = ResolveSpawnEnemyId(spawn);
            var isBossGroup = IsBossId(enemyId);
            var isFinalBossWave = isBossGroup && IsFinalBossWave(wave);

            for (var index = 0; index < count; index++)
            {
                while (!CanAdvanceSpawn())
                {
                    yield return null;
                }

                if (isBossGroup && enforceBossGate)
                {
                    pendingBossSpawnCount++;
                    var waitedForBoss = false;
                    while (activeBossCount > 0
                           || bossSpawnReserved
                           || activeOrdinarySpawnGroupCount > 0
                           || (isFinalBossWave && HasUnstartedRequiredWaves(wave)))
                    {
                        waitedForBoss |= activeBossCount > 0;
                        yield return null;
                    }

                    bossSpawnReserved = true;
                    if (waitedForBoss)
                    {
                        var handoffElapsed = 0f;
                        while (handoffElapsed < BossHandoffDelay)
                        {
                            if (CanAdvanceSpawn())
                            {
                                handoffElapsed += Time.deltaTime;
                            }

                            yield return null;
                        }
                    }

                    ClearEnemyProjectiles();
                }
                else if (!isBossGroup)
                {
                    while (activeBossCount > 0 || bossSpawnReserved)
                    {
                        yield return null;
                    }
                }

                var forcedDropItemId = ResolveConfiguredDropItemId(spawn, index);
                SpawnEnemy(
                    enemyId,
                    ResolveSpawnPosition(spawn, index, count),
                    spawn,
                    forcedDropItemId,
                    isBossGroup);

                if (isBossGroup && enforceBossGate)
                {
                    pendingBossSpawnCount = Mathf.Max(0, pendingBossSpawnCount - 1);
                    bossSpawnReserved = false;
                }

                if (index < count - 1)
                {
                    yield return new WaitForSeconds(interval);
                }
            }
        }

        private static Vector2 ResolveSpawnPosition(WaveSpawnConfig spawn, int index, int count)
        {
            var position = spawn.startPosition;

            if (count > 1)
            {
                var centeredIndex = index - (count - 1) * 0.5f;
                position.x += centeredIndex * 0.7f;
            }

            return position;
        }

        private EnemyController SpawnEnemy(
            string enemyId,
            Vector2 position,
            WaveSpawnConfig spawnConfig,
            string forcedDropItemId = null,
            bool pauseBattleTimeline = false)
        {
            var enemyConfig = ConfigManager.Instance != null && ConfigManager.Instance.IsLoaded
                ? ConfigManager.Instance.GetEnemy(enemyId)
                : null;

            if (enemyConfig == null)
            {
                Debug.LogWarning($"Enemy config not found: {enemyId}");
                return null;
            }

            LevelProgressService.RecordEnemySpawned();

            var enemyObject = CreateEnemyObject(enemyConfig);
            enemyObject.transform.SetParent(transform, false);

            if (IsBossEnemy(enemyConfig))
            {
                activeBossCount++;
                if (pauseBattleTimeline)
                {
                    timelinePausingBossCount++;
                    GameManager.Instance?.SetBattleTimelinePaused(true);
                }

                var boss = enemyObject.GetComponent<BossController>() ?? enemyObject.AddComponent<BossController>();
                boss.Initialize(enemyConfig, position, pauseBattleTimeline);
                return null;
            }

            var enemy = enemyObject.GetComponent<EnemyController>() ?? enemyObject.AddComponent<EnemyController>();
            enemy.Initialize(enemyConfig, position, spawnConfig, forcedDropItemId);
            return enemy;
        }

        private static string ResolveConfiguredDropItemId(WaveSpawnConfig spawn, int zeroBasedIndex)
        {
            if (spawn == null || string.IsNullOrEmpty(spawn.dropItemId) || spawn.dropIndex <= 0)
            {
                return null;
            }

            return zeroBasedIndex + 1 == spawn.dropIndex ? spawn.dropItemId : null;
        }

        public bool NotifyBossDefeated(string bossId)
        {
            return NotifyBossDefeated(bossId, false);
        }

        public bool NotifyBossDefeated(string bossId, bool pausedBattleTimeline)
        {
            activeBossCount = Mathf.Max(0, activeBossCount - 1);
            if (pausedBattleTimeline)
            {
                timelinePausingBossCount = Mathf.Max(0, timelinePausingBossCount - 1);
                GameManager.Instance?.SetBattleTimelinePaused(timelinePausingBossCount > 0);
            }

            return activeBossCount <= 0 && pendingBossSpawnCount <= 0 && !HasUnstartedBossWaves();
        }

        private static bool CanAdvanceSpawn()
        {
            return GameManager.Instance == null || GameManager.Instance.CurrentState == GameState.Playing;
        }

        private static void ClearEnemyProjectiles()
        {
            BulletManager.Instance?.ClearEnemyBullets();
            MissileManager.Instance?.ClearEnemyMissiles();
        }

        private bool IsFinalBossWave(WaveConfig wave)
        {
            if (!IsBossWave(wave) || ConfigManager.Instance == null || GameManager.Instance == null)
            {
                return false;
            }

            foreach (var candidate in ConfigManager.Instance.GetWavesForLevel(GameManager.Instance.CurrentLevelNumber))
            {
                if (candidate != null && candidate.startTime > wave.startTime && IsBossWave(candidate))
                {
                    return false;
                }
            }

            return true;
        }

        private bool HasUnstartedRequiredWaves(WaveConfig bossWave)
        {
            if (bossWave == null || ConfigManager.Instance == null || GameManager.Instance == null)
            {
                return false;
            }

            foreach (var wave in ConfigManager.Instance.GetWavesForLevel(GameManager.Instance.CurrentLevelNumber))
            {
                if (wave == null
                    || wave.startTime > bossWave.startTime
                    || wave.startTime < GameManager.Instance.CurrentBattleStartTime
                    || startedWaves.Contains(wave.id)
                    || IsBossWave(wave))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static bool IsBossWave(WaveConfig wave)
        {
            if (wave?.spawns == null)
            {
                return false;
            }

            foreach (var spawn in wave.spawns)
            {
                if (spawn != null && IsBossId(ResolveSpawnEnemyId(spawn)))
                {
                    return true;
                }
            }

            return false;
        }

        private static GameObject CreateEnemyObject(EnemyConfig enemyConfig)
        {
            var prefab = LoadEnemyPrefab(enemyConfig);
            if (prefab != null)
            {
                var instance = Instantiate(prefab);
                instance.name = enemyConfig.id;
                return instance;
            }

            return new GameObject(enemyConfig.id);
        }

        private static GameObject LoadEnemyPrefab(EnemyConfig enemyConfig)
        {
            if (enemyConfig == null || string.IsNullOrEmpty(enemyConfig.prefabPath))
            {
                return null;
            }

#if UNITY_EDITOR
            if (RuntimeRemoteResourceManager.CanUseEditorLocalAssets
                && enemyConfig.prefabPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
            {
                var editorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(enemyConfig.prefabPath);
                if (editorPrefab != null)
                {
                    return editorPrefab;
                }
            }
#endif

            var catalogPrefab = RuntimeAssetCatalog.LoadPrefab(enemyConfig.prefabPath);
            if (catalogPrefab != null || !RuntimeRemoteResourceManager.CanUseResourcesFallback)
            {
                return catalogPrefab;
            }

            return Resources.Load<GameObject>(NormalizeResourcesPath(enemyConfig.prefabPath));
        }

        private static string NormalizeResourcesPath(string assetPath)
        {
            const string resourcesSegment = "/Resources/";
            var normalized = assetPath.Replace("\\", "/");
            var resourcesIndex = normalized.IndexOf(resourcesSegment, System.StringComparison.OrdinalIgnoreCase);

            if (resourcesIndex >= 0)
            {
                normalized = normalized.Substring(resourcesIndex + resourcesSegment.Length);
            }

            var extensionIndex = normalized.LastIndexOf(".", System.StringComparison.Ordinal);
            if (extensionIndex >= 0)
            {
                normalized = normalized.Substring(0, extensionIndex);
            }

            return normalized;
        }

        private static bool IsBossEnemy(EnemyConfig enemyConfig)
        {
            return enemyConfig != null && IsBossId(enemyConfig.id);
        }

        private bool HasUnstartedBossWaves()
        {
            if (ConfigManager.Instance == null || !ConfigManager.Instance.IsLoaded || ConfigManager.Instance.Config == null)
            {
                return false;
            }

            foreach (var wave in ConfigManager.Instance.GetWavesForLevel(GameManager.Instance.CurrentLevelNumber))
            {
                if (wave == null || startedWaves.Contains(wave.id) || wave.spawns == null)
                {
                    continue;
                }

                foreach (var spawn in wave.spawns)
                {
                    if (spawn != null && IsBossId(ResolveSpawnEnemyId(spawn)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string ResolveSpawnEnemyId(WaveSpawnConfig spawn)
        {
            return spawn == null ? string.Empty : spawn.enemyId;
        }

        private static bool IsBossId(string enemyId)
        {
            return !string.IsNullOrEmpty(enemyId)
                && enemyId.StartsWith("boss", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
