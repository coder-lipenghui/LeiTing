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
            UpdateWaves(ConfigManager.Instance.Config);
        }

        public EnemyController SpawnEnemy(string enemyId, Vector2 position)
        {
            return SpawnEnemy(enemyId, position, null);
        }

        private void UpdateWaves(GameConfig gameConfig)
        {
            foreach (var wave in gameConfig.waves)
            {
                if (wave == null || startedWaves.Contains(wave.id) || stageTime < wave.startTime)
                {
                    continue;
                }

                startedWaves.Add(wave.id);
                StartCoroutine(SpawnWave(wave));
            }
        }

        private IEnumerator SpawnWave(WaveConfig wave)
        {
            foreach (var spawn in wave.spawns)
            {
                if (spawn == null)
                {
                    continue;
                }

                yield return StartCoroutine(SpawnGroup(spawn));
            }
        }

        private IEnumerator SpawnGroup(WaveSpawnConfig spawn)
        {
            var count = Mathf.Max(1, spawn.count);
            var interval = Mathf.Max(0.01f, spawn.interval);

            for (var index = 0; index < count; index++)
            {
                while (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
                {
                    yield return null;
                }

                SpawnEnemy(spawn.enemyId, ResolveSpawnPosition(spawn, index, count), spawn);

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
                var boss = enemyObject.GetComponent<BossController>() ?? enemyObject.AddComponent<BossController>();
                boss.Initialize(enemyConfig, position);
                return null;
            }

            var enemy = enemyObject.GetComponent<EnemyController>() ?? enemyObject.AddComponent<EnemyController>();
            enemy.Initialize(enemyConfig, position, spawnConfig);
            return enemy;
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
                return AssetDatabase.LoadAssetAtPath<GameObject>(enemyConfig.prefabPath);
            }
#endif

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
            return enemyConfig != null
                && !string.IsNullOrEmpty(enemyConfig.id)
                && enemyConfig.id.StartsWith("boss", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
