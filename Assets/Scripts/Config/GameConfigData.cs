using System;
using System.Collections.Generic;
using UnityEngine;

namespace LeiTing.Config
{
    [Serializable]
    public class GameConfig
    {
        public PlayerConfig player;
        public BackgroundConfig background;
        public List<EnemyConfig> enemies = new List<EnemyConfig>();
        public List<BulletConfig> bullets = new List<BulletConfig>();
        public List<BulletPatternConfig> bulletPatterns = new List<BulletPatternConfig>();
        public List<WaveConfig> waves = new List<WaveConfig>();
        public List<StageEventConfig> stageEvents = new List<StageEventConfig>();
        public List<BossPhaseConfig> bossPhases = new List<BossPhaseConfig>();
        public List<BossSkillConfig> bossSkills = new List<BossSkillConfig>();
    }

    [Serializable]
    public class PlayerConfig
    {
        public string id;
        public string displayName;
        public string prefabPath;
        public int hp;
        public int shield;
        public float moveSpeed;
        public float invincibleTime;
        public float visualScale;
        public float hitboxRadius;
        public Vector2 hitboxOffset;
        public string defaultBulletId;
        public float fireInterval;
    }

    [Serializable]
    public class BackgroundConfig
    {
        public string id;
        public string spritePath;
        public float scrollSpeed;
    }

    [Serializable]
    public class EnemyConfig
    {
        public string id;
        public string displayName;
        public string prefabPath;
        public int hp;
        public float moveSpeed;
        public float attackInterval;
        public string bulletId;
        public string bulletPatternId;
        public bool hitScaleFeedback;
        public int score;
    }

    [Serializable]
    public class BulletConfig
    {
        public string id;
        public string owner;
        public string firePattern;
        public string spritePath;
        public int damage;
        public float speed;
        public float lifetime;
        public Vector2 size;
        public int projectileCount;
        public float spreadAngle;
        public float muzzleSpacing;
        public int pierceCount;
        public float laserLength;
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
        public string attackPatternId;
        public string movementPath;
        public float pathAmplitude;
        public float pathSpeed;
        public float holdDuration;
    }

    [Serializable]
    public class StageEventConfig
    {
        public string id;
        public float startTime;
        public string message;
        public bool clearEnemyBullets;
    }

    [Serializable]
    public class BulletPatternConfig
    {
        public string id;
        public string patternType;
        public string bulletId;
        public string firePointGroup;
        public Vector2 firePointOffset;
        public float baseAngle;
        public int bulletCount;
        public float angleStep;
        public float spreadAngle;
        public float bulletSpeed;
        public float bulletLifetime;
        public bool rotate;
        public float rotationSpeed;
        public bool aimAtPlayer;
        public int burstCount;
        public float fireInterval;
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

    [Serializable]
    public class BossPhaseConfig
    {
        public string id;
        public string bossId;
        public string displayName;
        public float triggerHpPercent;
        public float attackInterval;
        public int burstCount;
        public float burstInterval;
        public Vector2 movementRange;
        public float movementSpeed;
        public List<string> bulletPatternIds = new List<string>();
    }
}
