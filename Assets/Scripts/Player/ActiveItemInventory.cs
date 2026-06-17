using System;
using System.Collections.Generic;
using LeiTing.Storage;
using UnityEngine;

namespace LeiTing.Player
{
    public static class ActiveItemInventory
    {
        private const string CountKeyPrefix = "leiting_active_item_count_";

        private static readonly HashSet<ActiveItemKind> usedThisBattle =
            new HashSet<ActiveItemKind>();

        private static readonly HashSet<ActiveItemKind> adRewardedThisBattle =
            new HashSet<ActiveItemKind>();

        public static event Action InventoryChanged;

        public static void BeginBattle()
        {
            usedThisBattle.Clear();
            adRewardedThisBattle.Clear();
            NotifyChanged();
        }

        public static int GetCount(ActiveItemKind kind)
        {
            return Mathf.Max(0, GameStorage.GetInt(GetCountKey(kind), 0));
        }

        public static bool WasUsedThisBattle(ActiveItemKind kind)
        {
            return usedThisBattle.Contains(kind);
        }

        public static bool CanUse(ActiveItemKind kind)
        {
            return !WasUsedThisBattle(kind) && GetCount(kind) > 0;
        }

        public static bool TryConsumeForBattle(ActiveItemKind kind)
        {
            if (!CanUse(kind))
            {
                return false;
            }

            SetCount(kind, GetCount(kind) - 1);
            usedThisBattle.Add(kind);
            NotifyChanged();
            return true;
        }

        public static bool CanClaimAdRewardThisBattle(ActiveItemKind kind)
        {
            return !adRewardedThisBattle.Contains(kind);
        }

        public static bool TryClaimAdReward(ActiveItemKind kind)
        {
            if (!CanClaimAdRewardThisBattle(kind))
            {
                return false;
            }

            adRewardedThisBattle.Add(kind);
            SetCount(kind, GetCount(kind) + 1);
            NotifyChanged();
            return true;
        }

        public static void Add(ActiveItemKind kind, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            SetCount(kind, GetCount(kind) + amount);
            NotifyChanged();
        }

        private static void SetCount(ActiveItemKind kind, int value)
        {
            GameStorage.SetInt(GetCountKey(kind), Mathf.Max(0, value));
            GameStorage.Save();
        }

        private static string GetCountKey(ActiveItemKind kind)
        {
            return CountKeyPrefix + kind.ToString().ToLowerInvariant();
        }

        private static void NotifyChanged()
        {
            InventoryChanged?.Invoke();
        }
    }
}
