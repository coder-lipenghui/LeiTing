using System;
using System.Threading.Tasks;
using LeiTing.Core;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using TTSDK;
#endif

namespace LeiTing.UI
{
    public class AdManager : MonoSingleton<AdManager>
    {
        [SerializeField] private string rewardedVideoAdUnitId = string.Empty;

        private Task<bool> activeRewardAdTask;

        public static AdManager GetOrCreate()
        {
            if (Instance != null)
            {
                return Instance;
            }

            var existing = FindObjectOfType<AdManager>();
            if (existing != null)
            {
                return existing;
            }

            var managerObject = new GameObject("AdManager");
            return managerObject.AddComponent<AdManager>();
        }

        protected override void Awake()
        {
            base.Awake();

            if (Instance == this)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        public async Task<bool> ShowRewardAd()
        {
            if (activeRewardAdTask != null && !activeRewardAdTask.IsCompleted)
            {
                return false;
            }

            activeRewardAdTask = ShowRewardAdInternal();
            try
            {
                return await activeRewardAdTask;
            }
            finally
            {
                activeRewardAdTask = null;
            }
        }

        private Task<bool> ShowRewardAdInternal()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return ShowDouyinRewardAd();
#else
            Debug.Log("[AdManager] Reward video ad simulated outside Douyin WebGL.");
            return Task.FromResult(true);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private Task<bool> ShowDouyinRewardAd()
        {
            var completion = new TaskCompletionSource<bool>();
            if (string.IsNullOrEmpty(rewardedVideoAdUnitId != null ? rewardedVideoAdUnitId.Trim() : string.Empty))
            {
                Debug.LogWarning("[AdManager] Reward video ad unit id is empty.");
                completion.TrySetResult(false);
                return completion.Task;
            }

            try
            {
                TT.CreateRewardedVideoAd(
                    rewardedVideoAdUnitId,
                    (isComplete, rewardAmount) =>
                    {
                        Debug.Log($"[AdManager] Reward video closed. complete={isComplete}, reward={rewardAmount}");
                        completion.TrySetResult(isComplete);
                    },
                    (errorCode, errorMessage) =>
                    {
                        Debug.LogWarning($"[AdManager] Reward video failed: code={errorCode}, message={errorMessage}");
                        completion.TrySetResult(false);
                    },
                    false,
                    null,
                    0,
                    false);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[AdManager] Reward video exception: {exception.Message}");
                completion.TrySetResult(false);
            }

            return completion.Task;
        }
#endif
    }
}
