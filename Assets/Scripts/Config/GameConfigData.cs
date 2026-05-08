using System;
using System.Collections.Generic;
using LeiTing.Enemy.Movement;
using UnityEngine;

namespace LeiTing.Config
{
    [Serializable]
    public class GameConfig
    {
        public PlayerConfig player;
        public List<EnemyConfig> enemies = new List<EnemyConfig>();
        public List<BulletConfig> bullets = new List<BulletConfig>();
        public List<MissileConfig> missiles = new List<MissileConfig>();
        public List<PickupItemConfig> pickupItems = new List<PickupItemConfig>();
        public List<LevelConfig> levels = new List<LevelConfig>();
        public List<BulletPatternConfig> bulletPatterns = new List<BulletPatternConfig>();
        public List<MissilePatternConfig> missilePatterns = new List<MissilePatternConfig>();
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
        public int stars;
        public int coins;
        public float moveSpeed;
        public float invincibleTime;
        public float pickupAttractRange;
        public float pickupAttractSpeed;
        public float visualScale;
        public float hitboxRadius;
        public Vector2 hitboxOffset;
        public string defaultBulletId;
        public float fireInterval;
    }

    [Serializable]
    public class LevelConfig
    {
        public string id;
        public string displayName;
        public string backgroundSpritePath;
        public float backgroundScrollSpeed;
        public string bgmPath;
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
        public List<DropConfig> drops = new List<DropConfig>();
    }

    [Serializable]
    public class DropConfig
    {
        public string itemId;
        public int count;
        public bool dropOnce;
    }

    [Serializable]
    public class PickupItemConfig
    {
        public string id;
        public string displayName;
        public string itemType;
        public string spritePath;
        public int starValue;
        public int coinValue;
        public int healValue;
        public float shieldDuration;
        public float lifetime;
        public float driftSpeed;
        public float pickupRadius;
        public float visualScale;
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
        public Color glowColor;
        public float glowRange;
        public int projectileCount;
        public float spreadAngle;
        public float muzzleSpacing;
        public int pierceCount;
        public float laserLength;
    }

    [Serializable]
    public class MissileConfig
    {
        public string id;
        public int missileId;
        public string name;
        public int behaviorType;
        public float speed;
        public float maxSpeed;
        public float acceleration;
        public float lifeTime;
        public int damage;
        public float radius;
        public float turnSpeed;
        public float trackTime;
        public float lockDelay;
        public float warningTime;
        public float explodeTime;
        public float explodeRadius;
        public float splitTime;
        public int splitCount;
        public float splitAngle;
        public string childMissileId;
        public bool canBeDestroyed;
        public int hp;
        public bool isLoopTrack;
        public string prefabPath;
        public string bodyRes;
        public string flyAnim;
        public string tailType;
        public string tailRes;
        public string tailColor;
        public string warningRes;
        public string lockEffectRes;
        public string explodeEffectRes;
        public string hitEffectRes;
        public string destroyEffectRes;
        public string effectRes;
        public string soundRes;
        public string soundLaunch;
        public string soundLock;
        public string soundExplode;
        public float waveAmplitude;
        public float waveFrequency;
        public float releaseInterval;
        public float triggerRadius;
        public float returnDelay;
    }

    [Serializable]
    public class WaveConfig
    {
        public string id;
        public string levelId;
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
        public OrbitMovementConfig orbitMovement;
    }

    [Serializable]
    public class StageEventConfig
    {
        public string id;
        public string levelId;
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
        public int bulletCountPerBurst;
        public float angleStep;
        public float spreadAngle;
        public float bulletSpeed;
        public float bulletLifetime;
        public bool rotate;
        public float rotationSpeed;
        public float rotateStepDegrees;
        public bool clockwise;
        public bool aimAtPlayer;
        public int burstCount;
        public float fireInterval;
        public float duration;
    }

    [Serializable]
    public class MissilePatternConfig
    {
        public string id;
        public string patternType;
        public string missileId;
        public string firePointGroup;
        public Vector2 firePointOffset;
        public float baseAngle;
        public int missileCount;
        public float angleStep;
        public float spreadAngle;
        public float missileSpeed;
        public float missileLifetime;
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
