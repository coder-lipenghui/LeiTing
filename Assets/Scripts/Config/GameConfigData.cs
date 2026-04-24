using System;
using System.Collections.Generic;
using UnityEngine;

namespace LeiTing.Config
{
    [Serializable]
    public class GameConfig
    {
        public PlayerConfig player;
        public List<EnemyConfig> enemies = new List<EnemyConfig>();
        public List<BulletConfig> bullets = new List<BulletConfig>();
        public List<WaveConfig> waves = new List<WaveConfig>();
        public List<BossSkillConfig> bossSkills = new List<BossSkillConfig>();
    }

    [Serializable]
    public class PlayerConfig
    {
        public string id;
        public int hp;
        public float moveSpeed;
        public float invincibleTime;
        public string defaultBulletId;
        public float fireInterval;
    }

    [Serializable]
    public class EnemyConfig
    {
        public string id;
        public string displayName;
        public int hp;
        public float moveSpeed;
        public float attackInterval;
        public string bulletId;
        public int score;
    }

    [Serializable]
    public class BulletConfig
    {
        public string id;
        public string owner;
        public int damage;
        public float speed;
        public float lifetime;
        public Vector2 size;
    }

    [Serializable]
    public class WaveConfig
    {
        public string id;
        public float startTime;
        public List<WaveSpawnConfig> spawns = new List<WaveSpawnConfig>();
    }

    [Serializable]
    public class WaveSpawnConfig
    {
        public string enemyId;
        public int count;
        public float interval;
        public Vector2 startPosition;
    }

    [Serializable]
    public class BossSkillConfig
    {
        public string id;
        public string bossId;
        public string bulletId;
        public float triggerHpPercent;
        public float cooldown;
        public int bulletCount;
        public float spreadAngle;
    }
}
