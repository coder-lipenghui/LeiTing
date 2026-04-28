using UnityEngine;
using UnityEngine.SceneManagement;
using LeiTing.Config;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LeiTing.Core
{
    public class GameManager : MonoSingleton<GameManager>
    {
        private const int DefaultMaxLevelCount = 12;
        private const string BossIdPrefix = "boss_";
#if UNITY_EDITOR
        private const string EditorRequestedLevelKey = "LeiTing.Editor.RequestedLevelNumber";
#endif

        private static int requestedLevelNumber = 1;

        [SerializeField] private GameState currentState = GameState.Boot;
        [SerializeField] private int score;
        [SerializeField] private int currentLevelNumber = 1;

        public GameState CurrentState => currentState;
        public int Score => score;
        public int CurrentLevelNumber => currentLevelNumber;
        public int MaxLevelCount => ResolveMaxLevelCount();
        public bool HasNextLevel => currentLevelNumber < MaxLevelCount;
        public string CurrentLevelDisplayName => ResolveCurrentLevelDisplayName();
        public string CurrentLevelBossId => ResolveCurrentLevelBossId();
        public string CurrentLevelBossDisplayName => ResolveCurrentLevelBossDisplayName();
        public static int RequestedLevelNumber => ResolveRequestedLevelNumber();

        public void Initialize()
        {
            currentLevelNumber = Mathf.Clamp(ResolveRequestedLevelNumber(), 1, MaxLevelCount);
            currentState = GameState.Ready;
            score = 0;
        }

        public void StartGame()
        {
            currentState = GameState.Playing;
        }

        public void AddScore(int amount)
        {
            score += Mathf.Max(0, amount);
        }

        public void WinGame()
        {
            currentState = GameState.Victory;
        }

        public void LoseGame()
        {
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
            RequestLevel(Mathf.Clamp(levelNumber, 1, MaxLevelCount));
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public static void RequestLevel(int levelNumber)
        {
            requestedLevelNumber = Mathf.Max(1, levelNumber);
#if UNITY_EDITOR
            EditorPrefs.SetInt(EditorRequestedLevelKey, requestedLevelNumber);
#endif
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
            var levelConfig = ResolveCurrentLevelConfig();
            return levelConfig != null && !string.IsNullOrEmpty(levelConfig.bossId)
                ? levelConfig.bossId
                : $"{BossIdPrefix}{currentLevelNumber:00}";
        }

        private string ResolveCurrentLevelBossDisplayName()
        {
            var enemyConfig = ConfigManager.Instance != null && ConfigManager.Instance.IsLoaded
                ? ConfigManager.Instance.GetEnemy(CurrentLevelBossId)
                : null;

            return enemyConfig != null && !string.IsNullOrEmpty(enemyConfig.displayName)
                ? enemyConfig.displayName
                : $"BOSS {currentLevelNumber}";
        }
    }
}
