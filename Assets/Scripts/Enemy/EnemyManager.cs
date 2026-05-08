using System.Collections;
using System.Collections.Generic;
using LeiTing.Config;
using LeiTing.Core;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LeiTing.Enemy
{
    public class EnemyManager : MonoSingleton<EnemyManager>
    {
        private readonly HashSet<string> startedWaves = new HashSet<string>();
        private int activeBossCount;
        private int pendingBossSpawnCount;
        private float stageTime;

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

            stageTime += Time.deltaTime;
            UpdateWaves(ConfigManager.Instance);
        }

        public EnemyController SpawnEnemy(string enemyId, Vector2 position)
        {
            return SpawnEnemy(enemyId, position, null);
        }

        public bool TrySpawnWaveNow(string waveId, out string message)
        {
            message = string.Empty;

            var configManager = ConfigManager.Instance;
            if (!IsConfigReady(configManager))
            {
                message = "配置尚未加载";
                return false;
            }

            waveId = NormalizeDebugId(waveId);
            var wave = configManager.GetWave(waveId);
            if (wave == null)
            {
                message = $"找不到波次: {waveId}";
                return false;
            }

            StartCoroutine(SpawnWave(wave, false));
            message = $"已刷波次: {wave.id}";
            return true;
        }

        public bool TrySpawnEnemyNow(string enemyId, out string message)
        {
            return TrySpawnEnemyNow(enemyId, false, out message);
        }

        public bool TrySpawnBossNow(string bossId, out string message)
        {
            return TrySpawnEnemyNow(bossId, true, out message);
        }

        private void UpdateWaves(ConfigManager configManager)
        {
            foreach (var wave in configManager.GetWavesForLevel(GameManager.Instance.CurrentLevelNumber))
            {
                if (wave == null || startedWaves.Contains(wave.id) || stageTime < wave.startTime)
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

                yield return StartCoroutine(SpawnGroup(spawn, enforceBossGate));
            }
        }

        private IEnumerator SpawnGroup(WaveSpawnConfig spawn, bool enforceBossGate)
        {
            var count = Mathf.Max(1, spawn.count);
            var interval = Mathf.Max(0.01f, spawn.interval);
            var enemyId = ResolveSpawnEnemyId(spawn);
            var isBossGroup = IsBossId(enemyId);

            for (var index = 0; index < count; index++)
            {
                while (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
                {
                    yield return null;
                }

                if (isBossGroup && enforceBossGate)
                {
                    pendingBossSpawnCount++;
                    while (activeBossCount > 0)
                    {
                        yield return null;
                    }

                    pendingBossSpawnCount = Mathf.Max(0, pendingBossSpawnCount - 1);
                }

                SpawnEnemy(enemyId, ResolveSpawnPosition(spawn, index, count), spawn);

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

        private EnemyController SpawnEnemy(string enemyId, Vector2 position, WaveSpawnConfig spawnConfig)
        {
            var enemyConfig = ConfigManager.Instance != null && ConfigManager.Instance.IsLoaded
                ? ConfigManager.Instance.GetEnemy(enemyId)
                : null;

            if (enemyConfig == null)
            {
                Debug.LogWarning($"Enemy config not found: {enemyId}");
                return null;
            }

            var enemyObject = CreateEnemyObject(enemyConfig);
            enemyObject.transform.SetParent(transform, false);

            if (IsBossEnemy(enemyConfig))
            {
                activeBossCount++;
                var boss = enemyObject.GetComponent<BossController>() ?? enemyObject.AddComponent<BossController>();
                boss.Initialize(enemyConfig, position);
                return null;
            }

            var enemy = enemyObject.GetComponent<EnemyController>() ?? enemyObject.AddComponent<EnemyController>();
            enemy.Initialize(enemyConfig, position, spawnConfig);
            return enemy;
        }

        public bool NotifyBossDefeated(string bossId)
        {
            activeBossCount = Mathf.Max(0, activeBossCount - 1);
            return activeBossCount <= 0 && pendingBossSpawnCount <= 0 && !HasUnstartedBossWaves();
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
            if (enemyConfig.prefabPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
            {
                var editorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(enemyConfig.prefabPath);
                if (editorPrefab != null)
                {
                    return editorPrefab;
                }
            }
#endif

            return RuntimeAssetCatalog.LoadPrefab(enemyConfig.prefabPath)
                ?? Resources.Load<GameObject>(NormalizeResourcesPath(enemyConfig.prefabPath));
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

        private bool TrySpawnEnemyNow(string enemyId, bool requireBoss, out string message)
        {
            message = string.Empty;

            var configManager = ConfigManager.Instance;
            if (!IsConfigReady(configManager))
            {
                message = "配置尚未加载";
                return false;
            }

            enemyId = NormalizeDebugId(enemyId);
            var enemyConfig = configManager.GetEnemy(enemyId);
            if (enemyConfig == null)
            {
                message = $"找不到敌机: {enemyId}";
                return false;
            }

            var isBoss = IsBossEnemy(enemyConfig);
            if (requireBoss && !isBoss)
            {
                message = $"不是 Boss: {enemyConfig.id}";
                return false;
            }

            if (!requireBoss && isBoss)
            {
                message = "Boss 请使用 Boss 模式刷出";
                return false;
            }

            SpawnEnemy(enemyConfig.id, ResolveDebugSpawnPosition(), null);
            message = isBoss ? $"已刷 Boss: {enemyConfig.id}" : $"已刷敌机: {enemyConfig.id}";
            return true;
        }

        private static Vector2 ResolveDebugSpawnPosition()
        {
            return new Vector2(0f, 5.8f);
        }

        private static bool IsConfigReady(ConfigManager configManager)
        {
            return configManager != null && configManager.IsLoaded && configManager.Config != null;
        }

        private static string NormalizeDebugId(string id)
        {
            return string.IsNullOrEmpty(id) ? string.Empty : id.Trim();
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
