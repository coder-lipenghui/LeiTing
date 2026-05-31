using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_WEBGL && !UNITY_EDITOR
using TTSDK;
#endif

namespace LeiTing.Platform
{
    public static class DouyinAccountService
    {
        private static readonly List<Action<bool, string>> PendingCallbacks = new List<Action<bool, string>>();

        private static bool loginSucceeded;
        private static bool loginInProgress;

        public static bool IsLoginReady => loginSucceeded;

        public static void LoginOnGameEnter()
        {
            EnsureLogin(null, true);
        }

        public static void EnsureLogin(Action<bool, string> callback = null, bool forceLogin = true)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (loginSucceeded)
            {
                callback?.Invoke(true, string.Empty);
                return;
            }

            if (callback != null)
            {
                PendingCallbacks.Add(callback);
            }

            if (loginInProgress)
            {
                return;
            }

            loginInProgress = true;

            try
            {
                TT.Login(
                    (code, anonymousCode, isLogin) =>
                    {
                        loginInProgress = false;
                        loginSucceeded = true;
                        Debug.Log($"[DouyinAccount] Login success. isLogin={isLogin}, hasCode={!string.IsNullOrEmpty(code)}, hasAnonymousCode={!string.IsNullOrEmpty(anonymousCode)}");
                        CompletePendingCallbacks(true, string.Empty);
                    },
                    errMsg =>
                    {
                        loginInProgress = false;
                        loginSucceeded = false;
                        var message = string.IsNullOrEmpty(errMsg) ? "unknown error" : errMsg;
                        Debug.LogWarning($"[DouyinAccount] Login failed: {message}");
                        CompletePendingCallbacks(false, message);
                    },
                    forceLogin);
            }
            catch (Exception exception)
            {
                loginInProgress = false;
                loginSucceeded = false;
                Debug.LogWarning($"[DouyinAccount] Login failed: {exception.Message}");
                CompletePendingCallbacks(false, exception.Message);
            }
#else
            loginSucceeded = true;
            callback?.Invoke(true, string.Empty);
#endif
        }

        private static void CompletePendingCallbacks(bool success, string message)
        {
            if (PendingCallbacks.Count == 0)
            {
                return;
            }

            var callbacks = PendingCallbacks.ToArray();
            PendingCallbacks.Clear();

            foreach (var callback in callbacks)
            {
                try
                {
                    callback?.Invoke(success, message);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }
    }
}
