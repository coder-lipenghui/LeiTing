using LeiTing.Audio;
using LeiTing.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LeiTing.Core
{
    public class GameSceneManager : MonoSingleton<GameSceneManager>
    {
        public const string DefaultLobbySceneName = "SampleScene";
        public const string DefaultBattleSceneName = "BattleScene";

        [SerializeField] private string lobbySceneName = DefaultLobbySceneName;
        [SerializeField] private string battleSceneName = DefaultBattleSceneName;

        public static GameSceneManager GetOrCreate()
        {
            if (Instance != null)
            {
                return Instance;
            }

            var existing = FindObjectOfType<GameSceneManager>();
            if (existing != null)
            {
                return existing;
            }

            var managerObject = new GameObject("GameSceneManager");
            return managerObject.AddComponent<GameSceneManager>();
        }

        protected override void Awake()
        {
            base.Awake();

            if (Instance == this)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        public static bool IsBattleSceneName(string sceneName)
        {
            return string.Equals(sceneName, DefaultBattleSceneName, System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsLobbySceneName(string sceneName)
        {
            return string.Equals(sceneName, DefaultLobbySceneName, System.StringComparison.OrdinalIgnoreCase);
        }

        public void EnterBattle()
        {
            EnterBattle(GameManager.RequestedLevelNumber);
        }

        public void EnterBattle(int levelNumber)
        {
            var requestedLevelNumber = Mathf.Max(1, levelNumber);
            GameManager.CaptureBattleOptions();
            GameManager.RequestLevel(requestedLevelNumber);
            AudioManager.Instance?.StopBgm();
            GameBootstrap.PlayLevelBgm(requestedLevelNumber);

            if (CanLoadScene(battleSceneName))
            {
                SceneManager.LoadScene(battleSceneName);
                return;
            }

            Debug.LogWarning($"Battle scene '{battleSceneName}' is not in Build Settings. Starting battle in the active scene.");
            StartBattleInCurrentScene();
        }

        public void EnterLobby()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayMenuBgm();
            }

            if (CanLoadScene(lobbySceneName))
            {
                SceneManager.LoadScene(lobbySceneName);
                return;
            }

            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowMainUI(true);
                UIManager.Instance.Init();
            }
        }

        private static bool CanLoadScene(string sceneName)
        {
            return !string.IsNullOrEmpty(sceneName)
                && (Application.CanStreamedLevelBeLoaded(sceneName)
                    || Application.CanStreamedLevelBeLoaded("Assets/Scenes/" + sceneName + ".unity"));
        }

        private static void StartBattleInCurrentScene()
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowMainUI(false);
                UIManager.Instance.EnsureBattleHud();
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.Initialize();
                GameBootstrap.PlayCurrentLevelBgm();
                GameManager.Instance.StartGame();
            }
        }
    }
}
