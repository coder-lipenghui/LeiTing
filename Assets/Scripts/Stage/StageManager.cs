using System;
using System.Collections.Generic;
using System.Globalization;
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
        private const string SpawnTrophyEventPrefix = "spawn_trophy";
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

            if (IsSpawnTrophyEvent(stageEvent.id))
            {
                PickupManager.GetOrCreate().SpawnPickup(TrophyItemId, ResolveTrophySpawnPosition(stageEvent.id));
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

        private static bool IsSpawnTrophyEvent(string eventId)
        {
            return !string.IsNullOrWhiteSpace(eventId)
                && eventId.Trim().StartsWith(SpawnTrophyEventPrefix, StringComparison.OrdinalIgnoreCase);
        }

        private static Vector3 ResolveTrophySpawnPosition(string eventId)
        {
            var position = Vector3.zero;
            ForEachInlineEventParameter(eventId, (key, value) =>
            {
                switch (key.ToLowerInvariant())
                {
                    case "x":
                        SetFloat(value, result => position.x = result);
                        break;
                    case "y":
                        SetFloat(value, result => position.y = result);
                        break;
                    case "z":
                        SetFloat(value, result => position.z = result);
                        break;
                }
            });

            return position;
        }

        private static void ForEachInlineEventParameter(string eventId, Action<string, string> apply)
        {
            if (string.IsNullOrWhiteSpace(eventId) || apply == null)
            {
                return;
            }

            var parameterStart = eventId.IndexOfAny(new[] { ':', '(' });
            if (parameterStart < 0)
            {
                return;
            }

            var marker = eventId[parameterStart];
            var body = eventId.Substring(parameterStart + 1).Trim();
            if (marker == '(' && body.EndsWith(")", StringComparison.Ordinal))
            {
                body = body.Substring(0, body.Length - 1);
            }
            else if (body.StartsWith("(", StringComparison.Ordinal) && body.EndsWith(")", StringComparison.Ordinal))
            {
                body = body.Substring(1, body.Length - 2);
            }

            var pairs = body.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var separator = pair.IndexOf('=');
                if (separator <= 0 || separator >= pair.Length - 1)
                {
                    continue;
                }

                var key = pair.Substring(0, separator).Trim();
                var value = pair.Substring(separator + 1).Trim();
                apply(key, value);
            }
        }

        private static void SetFloat(string value, Action<float> apply)
        {
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            {
                apply(result);
            }
        }
    }
}
