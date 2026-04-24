using System.Linq;
using LeiTing.Core;
using UnityEngine;

namespace LeiTing.Config
{
    public class ConfigManager : MonoSingleton<ConfigManager>
    {
        private const string DefaultConfigPath = "Configs/GameConfig";

        [SerializeField] private TextAsset configJson;
        [SerializeField] private GameConfig config;

        public GameConfig Config => config;
        public bool IsLoaded => config != null;

        public void LoadDefaultConfig()
        {
            var source = configJson != null ? configJson : Resources.Load<TextAsset>(DefaultConfigPath);

            if (source == null)
            {
                Debug.LogError($"Config not found at Resources/{DefaultConfigPath}.json");
                return;
            }

            LoadFromJson(source.text);
        }

        public void LoadFromJson(string json)
        {
            config = JsonUtility.FromJson<GameConfig>(json);

            if (config == null)
            {
                Debug.LogError("GameConfig parse failed.");
            }
        }

        public EnemyConfig GetEnemy(string id)
        {
            return config?.enemies.FirstOrDefault(item => item.id == id);
        }

        public BulletConfig GetBullet(string id)
        {
            return config?.bullets.FirstOrDefault(item => item.id == id);
        }

        public BossSkillConfig GetBossSkill(string id)
        {
            return config?.bossSkills.FirstOrDefault(item => item.id == id);
        }
    }
}
