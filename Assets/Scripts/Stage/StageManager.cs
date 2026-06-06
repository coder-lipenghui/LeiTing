using System.Collections.Generic;
using LeiTing.Bullets;
using LeiTing.Config;
using LeiTing.Core;
using LeiTing.Missiles;
using LeiTing.Pickups;
using LeiTing.UI;
using UnityEngine;

namespace LeiTing.Stage
{
    public class StageManager : MonoSingleton<StageManager>
    {
        private const string SpawnTrophyEventId = "spawn_trophy_level_01";
        private const string TrophyItemId = "trophy";

        private readonly HashSet<string> triggeredEvents = new HashSet<string>();
        private float stageTime;

        public float StageTime => stageTime;

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
            UpdateStageEvents(ConfigManager.Instance);
        }

        private void UpdateStageEvents(ConfigManager configManager)
        {
            if (configManager == null || GameManager.Instance == null)
            {
                return;
            }

            foreach (var stageEvent in configManager.GetStageEventsForLevel(GameManager.Instance.CurrentLevelNumber))
            {
                if (stageEvent == null || triggeredEvents.Contains(stageEvent.id) || stageTime < stageEvent.startTime)
                {
                    continue;
                }

                triggeredEvents.Add(stageEvent.id);
                TriggerStageEvent(stageEvent);
            }
        }

        private static void TriggerStageEvent(StageEventConfig stageEvent)
        {
            if (stageEvent.clearEnemyBullets && BulletManager.Instance != null)
            {
                BulletManager.Instance.ClearEnemyBullets();
            }

            if (stageEvent.clearEnemyBullets && MissileManager.Instance != null)
            {
                MissileManager.Instance.ClearEnemyMissiles();
            }

            if (string.Equals(stageEvent.id, SpawnTrophyEventId, System.StringComparison.OrdinalIgnoreCase))
            {
                PickupManager.GetOrCreate().SpawnPickup(TrophyItemId, Vector3.zero);
            }

            var message = ResolveStageMessage(stageEvent.message);
            if (!string.IsNullOrEmpty(message) && UIManager.Instance != null)
            {
                UIManager.Instance.ShowBossPhaseNotice(message);
            }
        }

        private static string ResolveStageMessage(string message)
        {
            if (string.IsNullOrEmpty(message) || GameManager.Instance == null)
            {
                return message;
            }

            return message
                .Replace("{LEVEL}", GameManager.Instance.CurrentLevelNumber.ToString())
                .Replace("{MAX_LEVEL}", GameManager.Instance.MaxLevelCount.ToString())
                .Replace("{BOSS_ID}", GameManager.Instance.CurrentLevelBossId)
                .Replace("{BOSS}", GameManager.Instance.CurrentLevelBossDisplayName);
        }
    }
}
