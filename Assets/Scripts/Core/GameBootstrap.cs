using LeiTing.Config;
using LeiTing.Pickups;
using LeiTing.Player;
using LeiTing.Stage;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LeiTing.Core
{
    [DefaultExecutionOrder(100)]
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private bool startGameOnAwake = true;
        [SerializeField] private bool createPlayerIfMissing = true;
        [SerializeField] private Vector3 defaultPlayerPosition = new Vector3(0f, -3.5f, 0f);
        [SerializeField] private Vector2Int designResolution = new Vector2Int(1080, 1920);
        [SerializeField] private float pixelsPerUnit = 100f;

        private void Awake()
        {
            if (ConfigManager.Instance != null)
            {
                ConfigManager.Instance.LoadDefaultConfig();
            }

            EnsureDesignCamera();
            EnsurePickupManager();
            EnsurePlayerReady();
            EnsureBackgroundReady();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.Initialize();

                if (startGameOnAwake)
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

            if (ConfigManager.Instance != null && ConfigManager.Instance.IsLoaded && ConfigManager.Instance.Config.background != null)
            {
                scroller.Configure(null, ConfigManager.Instance.Config.background.scrollSpeed);
            }
        }
    }
}
