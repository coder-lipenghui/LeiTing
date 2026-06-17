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
        private const string DefaultRewardedVideoAdUnitId = "4lqn5ekt68ogfij68i";
        private const int RewardAdTimeoutMilliseconds = 90000;

        [SerializeField] private string rewardedVideoAdUnitId = DefaultRewardedVideoAdUnitId;

        private Task<bool> activeRewardAdTask;

#if UNITY_WEBGL && !UNITY_EDITOR
        private TTRewardedVideoAd activeRewardAd;
#endif

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

        public async Task<bool> ShowRewardAd(string source = null)
        {
            var sourceTag = GetSourceTag(source);
            if (activeRewardAdTask != null && !activeRewardAdTask.IsCompleted)
            {
                Debug.LogWarning($"[AdManager] Reward ad request ignored because another ad is active. source={sourceTag}");
                return false;
            }

            Debug.LogWarning(
                $"[AdManager] Reward ad requested. source={sourceTag}, branch={GetRuntimeBranch()}, adUnitId={MaskAdUnitId(ResolveRewardedVideoAdUnitId())}");
            activeRewardAdTask = ShowRewardAdWithTimeout(sourceTag);
            try
            {
                var completed = await activeRewardAdTask;
                Debug.LogWarning($"[AdManager] Reward ad completed. source={sourceTag}, success={completed}");
                return completed;
            }
            finally
            {
                activeRewardAdTask = null;
            }
        }

        private async Task<bool> ShowRewardAdWithTimeout(string source)
        {
            var adTask = ShowRewardAdInternal(source);
            var timeoutTask = Task.Delay(RewardAdTimeoutMilliseconds);
            var completedTask = await Task.WhenAny(adTask, timeoutTask);
            if (completedTask != adTask)
            {
                Debug.LogWarning(
                    $"[AdManager] Reward ad timed out waiting for SDK callback. source={source}, timeoutMs={RewardAdTimeoutMilliseconds}");
#if UNITY_WEBGL && !UNITY_EDITOR
                DestroyActiveRewardAd(source, "timeout");
#endif
                return false;
            }

            return await adTask;
        }

        private Task<bool> ShowRewardAdInternal(string source)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return ShowDouyinRewardAd(source);
#else
            Debug.LogWarning($"[AdManager] Reward video ad simulated outside Douyin WebGL. source={source}");
            return Task.FromResult(true);
#endif
        }

        private string ResolveRewardedVideoAdUnitId()
        {
            var configuredId = rewardedVideoAdUnitId != null ? rewardedVideoAdUnitId.Trim() : string.Empty;
            return !string.IsNullOrEmpty(configuredId) ? configuredId : DefaultRewardedVideoAdUnitId;
        }

        private static string GetSourceTag(string source)
        {
            return string.IsNullOrEmpty(source) ? "Unknown" : source;
        }

        private static string GetRuntimeBranch()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return "DouyinWebGL";
#else
            return "NonDouyinOrEditor";
#endif
        }

        private static string MaskAdUnitId(string adUnitId)
        {
            if (string.IsNullOrEmpty(adUnitId))
            {
                return "<empty>";
            }

            return adUnitId.Length <= 6
                ? adUnitId
                : $"{adUnitId.Substring(0, 3)}...{adUnitId.Substring(adUnitId.Length - 4)}";
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private Task<bool> ShowDouyinRewardAd(string source)
        {
            var completion = new TaskCompletionSource<bool>();
            var adUnitId = ResolveRewardedVideoAdUnitId();
            if (string.IsNullOrEmpty(adUnitId))
            {
                Debug.LogWarning($"[AdManager] Reward video ad unit id is empty. source={source}");
                completion.TrySetResult(false);
                return completion.Task;
            }

            try
            {
                DestroyActiveRewardAd(source, "replace");

                var settled = false;
                var param = new CreateRewardedVideoAdParam { AdUnitId = adUnitId };
                Debug.LogWarning($"[AdManager] Calling official TT.CreateRewardedVideoAd(param). source={source}, adUnitId={MaskAdUnitId(adUnitId)}");
                var rewardAd = TT.CreateRewardedVideoAd(param);
                activeRewardAd = rewardAd;

                if (rewardAd == null)
                {
                    Debug.LogWarning($"[AdManager] TT.CreateRewardedVideoAd returned null. source={source}");
                    completion.TrySetResult(false);
                    return completion.Task;
                }

                Action<bool, string> finish = (success, reason) =>
                {
                    if (settled)
                    {
                        return;
                    }

                    settled = true;
                    Debug.LogWarning($"[AdManager] Reward video finished. source={source}, success={success}, reason={reason}");
                    DestroyActiveRewardAd(source, reason);
                    completion.TrySetResult(success);
                };

                rewardAd.OnLoad += () =>
                {
                    Debug.LogWarning($"[AdManager] Reward video loaded. source={source}");
                };

                rewardAd.OnError += (errorCode, errorMessage) =>
                {
                    Debug.LogWarning($"[AdManager] Reward video error. source={source}, code={errorCode}, message={errorMessage}");
                    finish(false, $"error:{errorCode}");
                };

                rewardAd.OnClose += (isEnded, count) =>
                {
                    Debug.LogWarning($"[AdManager] Reward video closed. source={source}, isEnded={isEnded}, count={count}");
                    finish(isEnded, isEnded ? "complete" : "not-complete");
                };

                Debug.LogWarning($"[AdManager] Calling rewardAd.Show(). source={source}");
                rewardAd.Show();
                Debug.LogWarning($"[AdManager] rewardAd.Show() returned; waiting for OnClose/OnError. source={source}");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[AdManager] Reward video exception. source={source}, message={exception.Message}");
                DestroyActiveRewardAd(source, "exception");
                completion.TrySetResult(false);
            }

            return completion.Task;
        }

        private void DestroyActiveRewardAd(string source, string reason)
        {
            if (activeRewardAd == null)
            {
                return;
            }

            try
            {
                activeRewardAd.Destroy();
                Debug.LogWarning($"[AdManager] Reward video destroyed. source={source}, reason={reason}");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[AdManager] Reward video destroy exception. source={source}, reason={reason}, message={exception.Message}");
            }
            finally
            {
                activeRewardAd = null;
            }
        }
#endif
    }
}
