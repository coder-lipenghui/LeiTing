using System.Collections.Generic;
using LeiTing.Bullets;
using LeiTing.Config;
using LeiTing.Core;
using LeiTing.Missiles;
using LeiTing.UI;
using UnityEngine;

namespace LeiTing.Stage
{
    public class StageManager : MonoSingleton<StageManager>
    {
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
            UpdateStageEvents(ConfigManager.Instance.Config);
        }

        private void UpdateStageEvents(GameConfig gameConfig)
        {
            if (gameConfig.stageEvents == null)
            {
                return;
            }

            foreach (var stageEvent in gameConfig.stageEvents)
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

            if (!string.IsNullOrEmpty(stageEvent.message) && UIManager.Instance != null)
            {
                UIManager.Instance.ShowBossPhaseNotice(stageEvent.message);
            }
        }
    }
}
