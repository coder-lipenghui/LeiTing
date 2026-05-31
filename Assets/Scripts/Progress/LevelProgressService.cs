using System;
using System.Collections.Generic;
using LeiTing.Config;
using LeiTing.Core;
#if UNITY_WEBGL && !UNITY_EDITOR
using LeiTing.Platform;
#endif
using LeiTing.Storage;
using UnityEngine;

#if UNITY_WEBGL && !UNITY_EDITOR
using TTSDK;
using TTSDK.UNBridgeLib.LitJson;
#endif

namespace LeiTing.Progress
{
    public enum LevelAchievementType
    {
        Destroy70PercentEnemies = 0,
        DestroyAllEnemies = 1,
        Collect80PercentStars = 2,
        NoHit = 3
    }

    [Serializable]
    public sealed class LevelProgressRecord
    {
        public int levelNumber;
        public int score;
        public int enemyKillCount;
        public int starCount;
        public bool wasHit;
        public int totalEnemyCount;
        public int totalStarCount;
        public int achievementMask;
        public long updatedAtUnixSeconds;

        public bool HasAchievement(LevelAchievementType achievementType)
        {
            return (achievementMask & (1 << (int)achievementType)) != 0;
        }

        public int EarnedAchievementCount
        {
            get
            {
                var count = 0;
                for (var index = 0; index < LevelProgressService.AchievementCount; index++)
                {
                    if ((achievementMask & (1 << index)) != 0)
                    {
                        count++;
                    }
                }

                return count;
            }
        }
    }

    public static class LevelProgressService
    {
        public const int AchievementCount = 4;

        private const string ProgressStorageKey = "leiting_level_progress_v1";
        private const string RankZoneId = "default";
        private const int NumericRankDataType = 0;
        private const int NumericRankPriority = 0;

        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static LevelProgressDocument cachedDocument;
        private static LevelRun currentRun;
        private static LevelProgressRecord lastCompletedRecord;

        public static LevelProgressRecord LastCompletedRecord => CloneRecord(lastCompletedRecord);

        public static void BeginLevel(int levelNumber)
        {
            var targets = CalculateLevelTargets(levelNumber);
            currentRun = new LevelRun(levelNumber, targets.enemyCount, targets.starCount);
            lastCompletedRecord = null;
        }

        public static void RecordEnemySpawned()
        {
            EnsureCurrentRun().spawnedEnemyCount++;
        }

        public static void RecordEnemyKilled()
        {
            EnsureCurrentRun().enemyKillCount++;
        }

        public static void RecordStarSpawned(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            EnsureCurrentRun().spawnedStarCount += amount;
        }

        public static void RecordStarsCollected(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            EnsureCurrentRun().starCount += amount;
        }

        public static void RecordPlayerHit()
        {
            EnsureCurrentRun().wasHit = true;
        }

        public static LevelProgressRecord CompleteLevel(int levelNumber, int score)
        {
            var run = EnsureCurrentRun(levelNumber);
            run.score = Mathf.Max(0, score);
            run.totalEnemyCount = Mathf.Max(run.configEnemyCount, run.spawnedEnemyCount);
            run.totalStarCount = Mathf.Max(run.configStarCount, run.spawnedStarCount);

            var document = LoadDocument();
            var record = FindOrCreateRecord(document, levelNumber);
            record.levelNumber = levelNumber;
            record.score = run.score;
            record.enemyKillCount = Mathf.Max(0, run.enemyKillCount);
            record.starCount = Mathf.Max(0, run.starCount);
            record.wasHit = run.wasHit;
            record.totalEnemyCount = Mathf.Max(0, run.totalEnemyCount);
            record.totalStarCount = Mathf.Max(0, run.totalStarCount);
            record.achievementMask |= CalculateAchievementMask(run);
            record.updatedAtUnixSeconds = GetUnixTimestampSeconds();

            document.records.Sort((left, right) => left.levelNumber.CompareTo(right.levelNumber));
            SaveDocument(document);

            lastCompletedRecord = CloneRecord(record);
            SubmitLeaderboardScore(CalculateTotalScore(document));

            if (currentRun == run)
            {
                currentRun = null;
            }

            return CloneRecord(record);
        }

        public static LevelProgressRecord GetRecord(int levelNumber)
        {
            return CloneRecord(FindRecord(LoadDocument(), levelNumber));
        }

        public static int GetAchievementCount(int levelNumber)
        {
            var record = FindRecord(LoadDocument(), levelNumber);
            return record != null ? record.EarnedAchievementCount : 0;
        }

        public static bool HasAchievement(int levelNumber, LevelAchievementType achievementType)
        {
            var record = FindRecord(LoadDocument(), levelNumber);
            return record != null && record.HasAchievement(achievementType);
        }

        public static int GetTotalScore()
        {
            return CalculateTotalScore(LoadDocument());
        }

        private static LevelRun EnsureCurrentRun()
        {
            return EnsureCurrentRun(GameManager.Instance != null ? GameManager.Instance.CurrentLevelNumber : 1);
        }

        private static LevelRun EnsureCurrentRun(int levelNumber)
        {
            if (currentRun == null || currentRun.levelNumber != levelNumber)
            {
                BeginLevel(levelNumber);
            }

            return currentRun;
        }

        private static int CalculateAchievementMask(LevelRun run)
        {
            var mask = 0;

            if (HasReachedPercent(run.enemyKillCount, run.totalEnemyCount, 70))
            {
                mask |= 1 << (int)LevelAchievementType.Destroy70PercentEnemies;
            }

            if (run.totalEnemyCount > 0 && run.enemyKillCount >= run.totalEnemyCount)
            {
                mask |= 1 << (int)LevelAchievementType.DestroyAllEnemies;
            }

            if (HasReachedPercent(run.starCount, run.totalStarCount, 80))
            {
                mask |= 1 << (int)LevelAchievementType.Collect80PercentStars;
            }

            if (!run.wasHit)
            {
                mask |= 1 << (int)LevelAchievementType.NoHit;
            }

            return mask;
        }

        private static bool HasReachedPercent(int value, int total, int percent)
        {
            if (total <= 0)
            {
                return false;
            }

            return Mathf.Max(0, value) >= Mathf.CeilToInt(total * Mathf.Clamp(percent, 0, 100) / 100f);
        }

        private static int CalculateTotalScore(LevelProgressDocument document)
        {
            var total = 0;
            if (document?.records == null)
            {
                return total;
            }

            foreach (var record in document.records)
            {
                if (record != null)
                {
                    total += Mathf.Max(0, record.score);
                }
            }

            return total;
        }

        private static LevelTargets CalculateLevelTargets(int levelNumber)
        {
            var targets = new LevelTargets();
            var configManager = GetLoadedConfigManager();
            if (configManager == null)
            {
                return targets;
            }

            var dropOnceItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var wave in configManager.GetWavesForLevel(levelNumber))
            {
                if (wave?.spawns == null)
                {
                    continue;
                }

                foreach (var spawn in wave.spawns)
                {
                    if (spawn == null || string.IsNullOrEmpty(spawn.enemyId))
                    {
                        continue;
                    }

                    var enemyCount = Mathf.Max(1, spawn.count);
                    targets.enemyCount += enemyCount;

                    var enemyConfig = configManager.GetEnemy(spawn.enemyId);
                    AddEnemyStarTargets(configManager, enemyConfig, enemyCount, dropOnceItemIds, targets);
                }
            }

            return targets;
        }

        private static void AddEnemyStarTargets(
            ConfigManager configManager,
            EnemyConfig enemyConfig,
            int enemyCount,
            HashSet<string> dropOnceItemIds,
            LevelTargets targets)
        {
            if (enemyConfig?.drops == null)
            {
                return;
            }

            foreach (var drop in enemyConfig.drops)
            {
                if (drop == null || string.IsNullOrEmpty(drop.itemId) || drop.count <= 0)
                {
                    continue;
                }

                if (drop.dropOnce && !dropOnceItemIds.Add(drop.itemId))
                {
                    continue;
                }

                if (!TryGetStarValue(configManager, drop.itemId, out var starValue))
                {
                    continue;
                }

                var enemyMultiplier = drop.dropOnce ? 1 : Mathf.Max(1, enemyCount);
                targets.starCount += Mathf.Max(1, drop.count) * starValue * enemyMultiplier;
            }
        }

        private static bool TryGetStarValue(ConfigManager configManager, string itemId, out int starValue)
        {
            starValue = 0;
            var pickupConfig = configManager != null ? configManager.GetPickupItem(itemId) : null;
            var isStar = pickupConfig != null
                ? IsStarPickup(pickupConfig)
                : string.Equals(itemId, "star", StringComparison.OrdinalIgnoreCase);

            if (!isStar)
            {
                return false;
            }

            starValue = Mathf.Max(1, pickupConfig != null ? pickupConfig.starValue : 1);
            return true;
        }

        public static bool IsStarPickup(PickupItemConfig pickupConfig)
        {
            return pickupConfig != null
                && (string.Equals(pickupConfig.itemType, "Star", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(pickupConfig.id, "star", StringComparison.OrdinalIgnoreCase));
        }

        public static int GetPickupStarValue(PickupItemConfig pickupConfig)
        {
            return Mathf.Max(1, pickupConfig != null ? pickupConfig.starValue : 1);
        }

        private static ConfigManager GetLoadedConfigManager()
        {
            var configManager = ConfigManager.Instance;
            if (configManager != null && !configManager.IsLoaded)
            {
                configManager.LoadDefaultConfig();
            }

            return configManager != null && configManager.IsLoaded ? configManager : null;
        }

        private static LevelProgressDocument LoadDocument()
        {
            if (cachedDocument != null)
            {
                EnsureDocumentList(cachedDocument);
                return cachedDocument;
            }

            var json = GameStorage.GetString(ProgressStorageKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    cachedDocument = JsonUtility.FromJson<LevelProgressDocument>(json);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[Progress] Failed to parse level progress: {exception.Message}");
                }
            }

            if (cachedDocument == null)
            {
                cachedDocument = new LevelProgressDocument();
            }

            EnsureDocumentList(cachedDocument);
            return cachedDocument;
        }

        private static void SaveDocument(LevelProgressDocument document)
        {
            cachedDocument = document ?? new LevelProgressDocument();
            EnsureDocumentList(cachedDocument);
            GameStorage.SetString(ProgressStorageKey, JsonUtility.ToJson(cachedDocument));
            GameStorage.Save();
        }

        private static void EnsureDocumentList(LevelProgressDocument document)
        {
            if (document.records == null)
            {
                document.records = new List<LevelProgressRecord>();
            }
        }

        private static LevelProgressRecord FindOrCreateRecord(LevelProgressDocument document, int levelNumber)
        {
            var record = FindRecord(document, levelNumber);
            if (record != null)
            {
                return record;
            }

            record = new LevelProgressRecord
            {
                levelNumber = levelNumber
            };
            document.records.Add(record);
            return record;
        }

        private static LevelProgressRecord FindRecord(LevelProgressDocument document, int levelNumber)
        {
            if (document?.records == null)
            {
                return null;
            }

            foreach (var record in document.records)
            {
                if (record != null && record.levelNumber == levelNumber)
                {
                    return record;
                }
            }

            return null;
        }

        private static LevelProgressRecord CloneRecord(LevelProgressRecord source)
        {
            if (source == null)
            {
                return null;
            }

            return new LevelProgressRecord
            {
                levelNumber = source.levelNumber,
                score = source.score,
                enemyKillCount = source.enemyKillCount,
                starCount = source.starCount,
                wasHit = source.wasHit,
                totalEnemyCount = source.totalEnemyCount,
                totalStarCount = source.totalStarCount,
                achievementMask = source.achievementMask,
                updatedAtUnixSeconds = source.updatedAtUnixSeconds
            };
        }

        private static long GetUnixTimestampSeconds()
        {
            return (long)(DateTime.UtcNow - UnixEpoch).TotalSeconds;
        }

        private static void SubmitLeaderboardScore(int totalScore)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            DouyinAccountService.EnsureLogin((success, message) =>
            {
                if (!success)
                {
                    Debug.LogWarning($"[Progress] Douyin rank save skipped: login failed: {message}");
                    return;
                }

                SubmitLeaderboardScoreAfterLogin(totalScore);
            });
#else
            Debug.Log($"[Progress] Leaderboard total score ready: {totalScore}");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private static void SubmitLeaderboardScoreAfterLogin(int totalScore)
        {
            try
            {
                var rankData = new JsonData();
                rankData["dataType"] = NumericRankDataType;
                rankData["value"] = Mathf.Max(0, totalScore).ToString();
                rankData["priority"] = NumericRankPriority;
                rankData["zoneId"] = RankZoneId;
                TT.SetImRankData(rankData, (success, message) =>
                {
                    if (!success)
                    {
                        Debug.LogWarning($"[Progress] Douyin rank save failed: {message}");
                    }
                });
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Progress] Douyin rank save failed: {exception.Message}");
            }
        }
#endif

        [Serializable]
        private sealed class LevelProgressDocument
        {
            public List<LevelProgressRecord> records = new List<LevelProgressRecord>();
        }

        private sealed class LevelRun
        {
            public readonly int levelNumber;
            public readonly int configEnemyCount;
            public readonly int configStarCount;
            public int spawnedEnemyCount;
            public int spawnedStarCount;
            public int enemyKillCount;
            public int starCount;
            public bool wasHit;
            public int score;
            public int totalEnemyCount;
            public int totalStarCount;

            public LevelRun(int levelNumber, int configEnemyCount, int configStarCount)
            {
                this.levelNumber = levelNumber;
                this.configEnemyCount = Mathf.Max(0, configEnemyCount);
                this.configStarCount = Mathf.Max(0, configStarCount);
                totalEnemyCount = this.configEnemyCount;
                totalStarCount = this.configStarCount;
            }
        }

        private sealed class LevelTargets
        {
            public int enemyCount;
            public int starCount;
        }
    }
}
