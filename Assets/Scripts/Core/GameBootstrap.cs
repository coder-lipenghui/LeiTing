using LeiTing.Config;
using LeiTing.Audio;
using LeiTing.Pickups;
using LeiTing.Player;
#if UNITY_WEBGL && !UNITY_EDITOR
using LeiTing.Platform;
#endif
using LeiTing.Stage;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LeiTing.Core
{
    [DefaultExecutionOrder(100)]
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private bool startGameOnAwake = true;
        [SerializeField] private bool waitForFirstBattleInput = true;
        [SerializeField] private bool createPlayerIfMissing = true;
        [SerializeField] private Vector3 defaultPlayerPosition = new Vector3(0f, -3.5f, 0f);
        [SerializeField] private Vector2Int designResolution = new Vector2Int(1080, 1920);
        [SerializeField] private float pixelsPerUnit = 100f;

        private void Awake()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            DouyinAccountService.LoginOnGameEnter();
#endif

            if (ConfigManager.Instance != null)
            {
                ConfigManager.Instance.LoadDefaultConfig();
            }

            EnsureDesignCamera();
            EnsurePickupManager();
            EnsurePlayerReady();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.Initialize();
                EnsureBackgroundReady();

                var isBattleScene = GameSceneManager.IsBattleSceneName(SceneManager.GetActiveScene().name);
                if (isBattleScene)
                {
                    EnsureReadyCloudLayer();
                    PlayCurrentLevelBgm();
                    BattleTimeController.GetOrCreate().ResetForReady();
                }

                if (startGameOnAwake && (!isBattleScene || !waitForFirstBattleInput))
                {
                    GameManager.Instance.StartGame();
                }
            }
        }

        private void EnsurePlayerReady()
        {
            var player = FindObjectOfType<PlayerController>();

            if (player == null && createPlayerIfMissing)
            {
                var playerObject = CreateConfiguredPlayerObject();
                var root = GameObject.Find("GameRoot");

                if (root != null)
                {
                    playerObject.transform.SetParent(root.transform);
                }

                playerObject.transform.position = defaultPlayerPosition;
                var playerLayer = LayerMask.NameToLayer("Player");
                if (playerLayer >= 0)
                {
                    playerObject.layer = playerLayer;
                }

                player = playerObject.AddComponent<PlayerController>();
            }

            if (player != null && ConfigManager.Instance != null && ConfigManager.Instance.IsLoaded)
            {
                player.ApplyConfig(ConfigManager.Instance.Config.player);
            }
        }

        private void EnsurePickupManager()
        {
            PickupManager.GetOrCreate();
        }

        private GameObject CreateConfiguredPlayerObject()
        {
            var prefabPath = ConfigManager.Instance != null && ConfigManager.Instance.IsLoaded && ConfigManager.Instance.Config.player != null
                ? ConfigManager.Instance.Config.player.prefabPath
                : string.Empty;
            GameObject prefab = null;

#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(prefabPath))
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }
#endif

            prefab = prefab != null ? prefab : RuntimeAssetCatalog.LoadPrefab(prefabPath);

            if (prefab != null)
            {
#if UNITY_EDITOR
                return (GameObject)PrefabUtility.InstantiatePrefab(prefab);
#else
                return Instantiate(prefab);
#endif
            }

            return new GameObject("Player");
        }

        private void EnsureDesignCamera()
        {
            var camera = Camera.main;
            if (camera == null || pixelsPerUnit <= 0f || designResolution.y <= 0)
            {
                return;
            }

            camera.orthographic = true;
            camera.orthographicSize = designResolution.y * 0.5f / pixelsPerUnit;
        }

        private void EnsureBackgroundReady()
        {
            var scroller = FindObjectOfType<BackgroundScroller>();

            if (scroller == null)
            {
                var backgroundObject = GameObject.Find("ScrollingBackground") ?? GameObject.Find("background-01");
                if (backgroundObject == null)
                {
                    backgroundObject = new GameObject("ScrollingBackground");
                }

                scroller = backgroundObject.AddComponent<BackgroundScroller>();
            }

            var bgLayer = GameObject.Find("BgLayer");
            if (bgLayer != null)
            {
                scroller.transform.SetParent(bgLayer.transform);
            }

            scroller.transform.localPosition = Vector3.zero;
            scroller.transform.localScale = Vector3.one;

            var levelConfig = GetCurrentLevelConfig();
            if (levelConfig != null)
            {
                scroller.Configure(LoadBackgroundSprite(levelConfig.backgroundSpritePath), levelConfig.backgroundScrollSpeed);
            }
        }

        private void EnsureReadyCloudLayer()
        {
            ReadyCloudLayer.GetOrCreate();
        }

        private static LevelConfig GetCurrentLevelConfig()
        {
            return ConfigManager.Instance != null && ConfigManager.Instance.IsLoaded && GameManager.Instance != null
                ? ConfigManager.Instance.GetLevel(GameManager.Instance.CurrentLevelNumber)
                : null;
        }

        private static Sprite LoadBackgroundSprite(string spritePath)
        {
            if (string.IsNullOrEmpty(spritePath))
            {
                return null;
            }

#if UNITY_EDITOR
            var editorSprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (editorSprite != null)
            {
                return editorSprite;
            }
#endif

            return RuntimeAssetCatalog.LoadSprite(spritePath);
        }

        internal static void PlayCurrentLevelBgm()
        {
            if (GameManager.Instance != null)
            {
                PlayLevelBgm(GameManager.Instance.CurrentLevelNumber);
            }
        }

        internal static void PlayLevelBgm(int levelNumber)
        {
            var levelConfig = ConfigManager.Instance != null && ConfigManager.Instance.IsLoaded
                ? ConfigManager.Instance.GetLevel(levelNumber)
                : null;
            if (levelConfig != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBgm(levelConfig.bgmPath);
            }
        }
    }
}
