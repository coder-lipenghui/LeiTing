using LeiTing.Config;
using UnityEngine;

namespace LeiTing.Player
{
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private PlayerConfig config;

        public void ApplyConfig(PlayerConfig playerConfig)
        {
            config = playerConfig;
        }
    }
}
