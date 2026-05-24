using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using LeiTing.Config;
using LeiTing.Pickups;
using LeiTing.Player;
using LeiTing.Storage;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LeiTing.Core
{
    public class GameManager : MonoSingleton<GameManager>
    {
        private const int DefaultMaxLevelCount = 12;
        private const string LatestUnlockedLevelKey = "leiting_latest_unlocked_level";
#if UNITY_EDITOR
        private const string EditorRequestedLevelKey = "LeiTing.Editor.RequestedLevelNumber";
#endif

        private static int requestedLevelNumber = 1;
        private static bool hasRequestedBattleOptions;
        private static bool requestedInvincibleMode;

        [SerializeField] private GameState currentState = GameState.Boot;
        [SerializeField] private int score;
        [SerializeField] private int currentLevelNumber = 1;
        [Header("调试选项")]
        [InspectorName("无敌模式")]
        [SerializeField] private bool invincibleMode;

        private Coroutine pendingVictoryRoutine;

        public GameState CurrentState => currentState;
        public int Score => score;
        public int CurrentLevelNumber => currentLevelNumber;
        public int MaxLevelCount => ResolveMaxLevelCount();
        public int MaxUnlockedLevelNumber => GetMaxUnlockedLevel(MaxLevelCount);
        public bool HasNextLevel => currentLevelNumber < MaxLevelCount;
        public string CurrentLevelDisplayName => ResolveCurrentLevelDisplayName();
        public string CurrentLevelBossId => ResolveCurrentLevelBossId();
        public string CurrentLevelBossDisplayName => ResolveCurrentLevelBossDisplayName();
        public static int RequestedLevelNumber => ResolveRequestedLevelNumber();
        public static bool InvincibleModeEnabled => Instance != null && Instance.invincibleMode;

        public void Initialize()
        {
            ApplyRequestedBattleOptions();
            CancelPendingVictory();
            currentLevelNumber = Mathf.Clamp(ResolveRequestedLevelNumber(), 1, MaxLevelCount);
            currentState = GameState.Ready;
            score = 0;
        }

        public void StartGame()
        {
            CancelPendingVictory();
            currentState = GameState.Playing;
        }

        public void AddScore(int amount)
        {
            score += Mathf.Max(0, amount);
        }

        public void WinGame()
        {
            if (currentState == GameState.Victory || currentState == GameState.Defeat)
            {
                return;
            }

            if (pendingVictoryRoutine != null)
            {
                return;
            }

            if (currentState == GameState.Playing && TryDelayVictoryForPickups())
            {
                return;
            }

            currentState = GameState.Victory;
            UnlockNextLevel();
        }

        public void LoseGame()
        {
            CancelPendingVictory();
            currentState = GameState.Defeat;
        }

        public void RestartCurrentScene()
        {
            LoadLevel(currentLevelNumber);
        }

        public void LoadNextLevel()
        {
            if (!HasNextLevel)
            {
                return;
            }

            LoadLevel(currentLevelNumber + 1);
        }

        public void LoadLevel(int levelNumber)
        {
            var requestedLevelNumber = Mathf.Clamp(levelNumber, 1, MaxLevelCount);
            RequestLevel(requestedLevelNumber);
            GameBootstrap.PlayLevelBgm(requestedLevelNumber);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public static void RequestLevel(int levelNumber)
        {
            requestedLevelNumber = Mathf.Max(1, levelNumber);
#if UNITY_EDITOR
            EditorPrefs.SetInt(EditorRequestedLevelKey, requestedLevelNumber);
#endif
        }

        public static void CaptureBattleOptions()
        {
            if (Instance == null)
            {
                return;
            }

            requestedInvincibleMode = Instance.invincibleMode;
            hasRequestedBattleOptions = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetBattleOptions()
        {
            requestedInvincibleMode = false;
            hasRequestedBattleOptions = false;
        }

        private void ApplyRequestedBattleOptions()
        {
            if (hasRequestedBattleOptions && GameSceneManager.IsBattleSceneName(SceneManager.GetActiveScene().name))
            {
                invincibleMode = requestedInvincibleMode;
            }
        }

        private static int ResolveRequestedLevelNumber()
        {
#if UNITY_EDITOR
            requestedLevelNumber = Mathf.Max(1, EditorPrefs.GetInt(EditorRequestedLevelKey, requestedLevelNumber));
#endif
            return requestedLevelNumber;
        }

        private int ResolveMaxLevelCount()
        {
            var levels = ConfigManager.Instance != null && ConfigManager.Instance.IsLoaded && ConfigManager.Instance.Config != null
                ? ConfigManager.Instance.Config.levels
                : null;

            return levels != null && levels.Count > 0 ? levels.Count : DefaultMaxLevelCount;
        }

        private LevelConfig ResolveCurrentLevelConfig()
        {
            return ConfigManager.Instance != null && ConfigManager.Instance.IsLoaded
                ? ConfigManager.Instance.GetLevel(currentLevelNumber)
                : null;
        }

        private string ResolveCurrentLevelDisplayName()
        {
            var levelConfig = ResolveCurrentLevelConfig();
            return levelConfig != null && !string.IsNullOrEmpty(levelConfig.displayName)
                ? levelConfig.displayName
                : $"第 {currentLevelNumber} 关";
        }

        private string ResolveCurrentLevelBossId()
        {
            return ConfigManager.Instance != null && ConfigManager.Instance.IsLoaded
                ? ConfigManager.Instance.GetBossEnemyIdForLevel(currentLevelNumber) ?? string.Empty
                : string.Empty;
        }

        private string ResolveCurrentLevelBossDisplayName()
        {
            var enemyConfig = ConfigManager.Instance != null && ConfigManager.Instance.IsLoaded
                ? ConfigManager.Instance.GetEnemy(CurrentLevelBossId)
                : null;

            return enemyConfig != null && !string.IsNullOrEmpty(enemyConfig.displayName)
                ? enemyConfig.displayName
                : "BOSS";
        }

        private bool TryDelayVictoryForPickups()
        {
            var pickupManager = PickupManager.Instance;
            if (pickupManager == null || !pickupManager.HasActivePickups())
            {
                return false;
            }

            var player = FindObjectOfType<PlayerController>();
            if (player == null)
            {
                return false;
            }

            pendingVictoryRoutine = StartCoroutine(CompleteVictoryAfterPickups(pickupManager, player));
            return true;
        }

        private IEnumerator CompleteVictoryAfterPickups(PickupManager pickupManager, PlayerController player)
        {
            pickupManager.AttractAllPickupsToPlayer(player);

            while (currentState == GameState.Playing && pickupManager != null && pickupManager.HasActivePickups())
            {
                if (player == null)
                {
                    player = FindObjectOfType<PlayerController>();
                }

                if (player != null)
                {
                    pickupManager.AttractAllPickupsToPlayer(player);
                }

                yield return null;
            }

            pendingVictoryRoutine = null;

            if (currentState == GameState.Playing)
            {
                currentState = GameState.Victory;
                UnlockNextLevel();
            }
        }

        private void CancelPendingVictory()
        {
            if (pendingVictoryRoutine == null)
            {
                return;
            }

            StopCoroutine(pendingVictoryRoutine);
            pendingVictoryRoutine = null;
        }

        public static int GetMaxUnlockedLevel(int maxLevelCount)
        {
            return Mathf.Clamp(GameStorage.GetInt(LatestUnlockedLevelKey, 1), 1, Mathf.Max(1, maxLevelCount));
        }

        private void UnlockNextLevel()
        {
            var nextUnlockedLevel = Mathf.Min(currentLevelNumber + 1, MaxLevelCount);
            if (nextUnlockedLevel <= MaxUnlockedLevelNumber)
            {
                return;
            }

            GameStorage.SetInt(LatestUnlockedLevelKey, nextUnlockedLevel);
            GameStorage.Save();
        }
    }
}
