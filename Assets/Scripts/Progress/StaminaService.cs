using System;
using System.Globalization;
using LeiTing.Storage;
using UnityEngine;

namespace LeiTing.Progress
{
    public static class StaminaService
    {
        public const int MaxStamina = 5;
        public const int BattleCost = 1;
        public const float RecoveryIntervalSeconds = 300f;

        private const string StaminaKey = "leiting_stamina_value_v1";
        private const string LastRecoveryUnixKey = "leiting_stamina_last_recovery_unix_v1";

        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static int CurrentStamina
        {
            get
            {
                RefreshRecovery();
                return GetStoredStamina();
            }
        }

        public static bool HasEnough(int amount = BattleCost)
        {
            return CurrentStamina >= Mathf.Max(1, amount);
        }

        public static bool TryConsume(int amount = BattleCost)
        {
            RefreshRecovery();

            amount = Mathf.Max(1, amount);
            var current = GetStoredStamina();
            if (current < amount)
            {
                return false;
            }

            current = Mathf.Clamp(current - amount, 0, MaxStamina);
            SetStoredStamina(current);
            if (current < MaxStamina)
            {
                SetLastRecoveryUnix(GetNowUnixSeconds());
            }

            GameStorage.Save();
            return true;
        }

        public static float SecondsUntilNextRecovery()
        {
            RefreshRecovery();

            if (GetStoredStamina() >= MaxStamina)
            {
                return 0f;
            }

            var elapsed = Mathf.Max(0f, (float)(GetNowUnixSeconds() - GetLastRecoveryUnix()));
            return Mathf.Max(0f, RecoveryIntervalSeconds - elapsed);
        }

        private static void RefreshRecovery()
        {
            EnsureInitialized();

            var current = GetStoredStamina();
            if (current >= MaxStamina)
            {
                return;
            }

            var now = GetNowUnixSeconds();
            var lastRecovery = GetLastRecoveryUnix();
            var elapsed = Mathf.Max(0f, (float)(now - lastRecovery));
            var recovered = Mathf.FloorToInt(elapsed / RecoveryIntervalSeconds);
            if (recovered <= 0)
            {
                return;
            }

            current = Mathf.Clamp(current + recovered, 0, MaxStamina);
            SetStoredStamina(current);
            SetLastRecoveryUnix(current >= MaxStamina
                ? now
                : lastRecovery + recovered * RecoveryIntervalSeconds);
            GameStorage.Save();
        }

        private static void EnsureInitialized()
        {
            var changed = false;
            if (!GameStorage.HasKey(StaminaKey))
            {
                GameStorage.SetInt(StaminaKey, MaxStamina);
                changed = true;
            }

            if (string.IsNullOrEmpty(GameStorage.GetString(LastRecoveryUnixKey, string.Empty)))
            {
                SetLastRecoveryUnix(GetNowUnixSeconds());
                changed = true;
            }

            if (changed)
            {
                GameStorage.Save();
            }
        }

        private static int GetStoredStamina()
        {
            return Mathf.Clamp(GameStorage.GetInt(StaminaKey, MaxStamina), 0, MaxStamina);
        }

        private static void SetStoredStamina(int value)
        {
            GameStorage.SetInt(StaminaKey, Mathf.Clamp(value, 0, MaxStamina));
        }

        private static double GetLastRecoveryUnix()
        {
            var text = GameStorage.GetString(LastRecoveryUnixKey, string.Empty);
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : GetNowUnixSeconds();
        }

        private static void SetLastRecoveryUnix(double value)
        {
            GameStorage.SetString(LastRecoveryUnixKey, value.ToString("F3", CultureInfo.InvariantCulture));
        }

        private static double GetNowUnixSeconds()
        {
            return (DateTime.UtcNow - UnixEpoch).TotalSeconds;
        }
    }
}
