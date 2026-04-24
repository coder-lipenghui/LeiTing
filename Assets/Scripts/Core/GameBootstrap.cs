using LeiTing.Config;
using LeiTing.Player;
using UnityEngine;

namespace LeiTing.Core
{
    [DefaultExecutionOrder(100)]
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private bool startGameOnAwake = true;
        [SerializeField] private bool createPlayerIfMissing = true;
        [SerializeField] private Vector3 defaultPlayerPosition = new Vector3(0f, -3.5f, 0f);

        private void Awake()
        {
            if (ConfigManager.Instance != null)
            {
                ConfigManager.Instance.LoadDefaultConfig();
            }

            EnsurePlayerReady();

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
                var playerObject = new GameObject("Player");
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
    }
}
