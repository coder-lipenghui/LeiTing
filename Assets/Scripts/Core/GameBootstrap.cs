using LeiTing.Config;
using UnityEngine;

namespace LeiTing.Core
{
    [DefaultExecutionOrder(100)]
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private bool startGameOnAwake = true;

        private void Awake()
        {
            if (ConfigManager.Instance != null)
            {
                ConfigManager.Instance.LoadDefaultConfig();
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.Initialize();

                if (startGameOnAwake)
                {
                    GameManager.Instance.StartGame();
                }
            }
        }
    }
}
