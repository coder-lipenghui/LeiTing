using System;
using System.Collections.Generic;
using System.Linq;
using LeiTing.Core;
using UnityEngine;

namespace LeiTing.Config
{
    public class ConfigManager : MonoSingleton<ConfigManager>
    {
        private const string DefaultConfigPath = "Configs/GameConfig";

        [SerializeField] private TextAsset configJson;
        [SerializeField] private GameConfig config;

        public GameConfig Config => config;
        public bool IsLoaded => config != null;

        public void LoadDefaultConfig()
        {
            if (LubanConfigLoader.TryLoad(out var lubanConfig))
            {
                config = lubanConfig;
                return;
            }

            var source = configJson != null ? configJson : Resources.Load<TextAsset>(DefaultConfigPath);

            if (source == null)
            {
                Debug.LogError($"Config not found at Resources/{DefaultConfigPath}.json");
                return;
            }

            LoadFromJson(source.text);
        }

        public void LoadFromJson(string json)
        {
            config = JsonUtility.FromJson<GameConfig>(json);

            if (config == null)
            {
                Debug.LogError("GameConfig parse failed.");
            }
        }

        public EnemyConfig GetEnemy(string id)
        {
            return config?.enemies.FirstOrDefault(item => item.id == id);
        }

        public BulletConfig GetBullet(string id)
        {
            return config?.bullets.FirstOrDefault(item => item.id == id);
        }

        public MissileConfig GetMissile(string id)
        {
            return config?.missiles?.FirstOrDefault(item => item.id == id);
        }

        public PickupItemConfig GetPickupItem(string id)
        {
            return config?.pickupItems.FirstOrDefault(item => item.id == id);
        }

        public LevelConfig GetLevel(int levelNumber)
        {
            var levels = config?.levels;
            if (levels == null || levels.Count == 0)
            {
                return null;
            }

            var index = Mathf.Clamp(levelNumber - 1, 0, levels.Count - 1);
            return levels[index];
        }

        public BulletPatternConfig GetBulletPattern(string id)
        {
            return config?.bulletPatterns.FirstOrDefault(item => item.id == id);
        }

        public MissilePatternConfig GetMissilePattern(string id)
        {
            return config?.missilePatterns?.FirstOrDefault(item => item.id == id);
        }

        public BossPhaseConfig[] GetBossPhases(string bossId)
        {
            return config?.bossPhases == null
                ? null
                : config.bossPhases
                .Where(item => item != null && item.bossId == bossId)
                .OrderByDescending(item => item.triggerHpPercent)
                .ToArray();
        }

        public BossSkillConfig GetBossSkill(string id)
        {
            return config?.bossSkills.FirstOrDefault(item => item.id == id);
        }

        public string GetBossEnemyIdForLevel(int levelNumber)
        {
            return GetWavesForLevel(levelNumber)
                .OrderByDescending(wave => wave.startTime)
                .Where(wave => wave?.spawns != null)
                .SelectMany(wave => wave.spawns)
                .Select(spawn => spawn?.enemyId)
                .FirstOrDefault(IsBossId);
        }

        public IEnumerable<WaveConfig> GetWavesForLevel(int levelNumber)
        {
            if (config?.waves == null)
            {
                yield break;
            }

            foreach (var wave in config.waves)
            {
                if (wave != null && IsConfigForLevel(wave.levelId, levelNumber))
                {
                    yield return wave;
                }
            }
        }

        public IEnumerable<StageEventConfig> GetStageEventsForLevel(int levelNumber)
        {
            if (config?.stageEvents == null)
            {
                yield break;
            }

            foreach (var stageEvent in config.stageEvents)
            {
                if (stageEvent != null && IsConfigForLevel(stageEvent.levelId, levelNumber))
                {
                    yield return stageEvent;
                }
            }
        }

        private bool IsConfigForLevel(string levelId, int levelNumber)
        {
            if (string.IsNullOrEmpty(levelId))
            {
                return true;
            }

            var currentLevel = GetLevel(levelNumber);
            return string.Equals(levelId, currentLevel?.id, StringComparison.OrdinalIgnoreCase)
                || string.Equals(levelId, levelNumber.ToString(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(levelId, $"level_{levelNumber}", StringComparison.OrdinalIgnoreCase)
                || string.Equals(levelId, $"level_{levelNumber:00}", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBossId(string enemyId)
        {
            return !string.IsNullOrEmpty(enemyId)
                && enemyId.StartsWith("boss", StringComparison.OrdinalIgnoreCase);
        }
    }
}
