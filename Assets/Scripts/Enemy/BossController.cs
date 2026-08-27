using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using LeiTing.Audio;
using LeiTing.Bullets;
using LeiTing.Config;
using LeiTing.Core;
using LeiTing.Effects;
using LeiTing.Missiles;
using LeiTing.Pickups;
using LeiTing.Player;
using LeiTing.Progress;
using LeiTing.UI;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LeiTing.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class BossController : MonoBehaviour
    {
        private const float EntryTargetY = 2.95f;
        private const float EntrySpeed = 1.65f;
        private const float DefaultAttackInterval = 1.8f;
        private const string EntryWarningSoundPath = "Assets/Art/Sound/SFX/Enemy/SFX_Boss_Attack_Warning_01.wav";
        private const string Level2MidBossId = "boss_level_02_mid_01";
        private const string Level2FinalBossId = "boss_02";
        private const string Level2FinalBossWindmillPatternId = "boss_02_p2_center_windmill";
        private const string Level2FinalBossFastHomingPatternId = "boss_02_p2_side_fast_homing";
        private const string Level2FinalBossLaserPatternId = "boss_02_p3_side_laser";
        private const float BossWindmillMissileResumeDelay = 0.25f;
        private const float BossLaserTrackingDuration = 2f;
        private const float BossLaserChargeDuration = 0.5f;
        private const float BossLaserFireDuration = 0.8f;
        private const float BossLaserWarningLength = 14f;

        private static Material bossLaserWarningMaterial;

        [SerializeField] private EnemyConfig config;
        [SerializeField] private int currentHp;
        [SerializeField] private int maxHp;

        private BossPhaseConfig[] phases;
        private readonly List<ScheduledPattern> scheduledPatterns = new List<ScheduledPattern>();
        private readonly List<GameObject> bossLaserWarningObjects = new List<GameObject>();
        private Rigidbody2D body;
        private CircleCollider2D hitbox;
        private SpriteRenderer spriteRenderer;
        private ActorMounts mounts;
        private Color originalColor = Color.white;
        private Vector3 anchorPosition;
        private float phaseMovementTime;
        private float nextAttackTime;
        private int currentPhaseIndex = -1;
        private int volleyCursor;
        private bool isEntering;
        private bool isFiringBurst;
        private bool useChildHitboxes;
        private bool isDead;
        private bool pausesBattleTimeline;
        private float movementLockedUntil;
        private Coroutine bossLaserSequence;

        public int CurrentHp => currentHp;
        public int MaxHp => maxHp;

        public void Initialize(EnemyConfig enemyConfig, Vector2 position)
        {
            Initialize(enemyConfig, position, true);
        }

        public void Initialize(EnemyConfig enemyConfig, Vector2 position, bool pauseBattleTimeline)
        {
            EnsureComponents();

            config = enemyConfig;
            pausesBattleTimeline = pauseBattleTimeline;
            maxHp = Mathf.Max(1, config != null ? config.hp : 1);
            currentHp = maxHp;
            phases = ResolveBossPhases();

            transform.position = position;
            anchorPosition = new Vector3(0f, EntryTargetY, 0f);
            phaseMovementTime = 0f;
            nextAttackTime = Time.time + 1.4f;
            currentPhaseIndex = -1;
            volleyCursor = 0;
            isEntering = true;
            isFiringBurst = false;
            isDead = false;
            movementLockedUntil = 0f;
            bossLaserSequence = null;
            scheduledPatterns.Clear();
            ClearBossLaserWarnings();

            ApplyLayer();
            ApplyVisual();
            ConfigureRootHitbox();
            UpdateBossUi();

            if (UIManager.Instance != null && ShouldShowBossPresentation())
            {
                UIManager.Instance.ShowBossPhaseNotice("警告\n首领来袭");
            }
            else if (UIManager.Instance != null)
            {
                UIManager.Instance.HideBossHud();
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySfx(EntryWarningSoundPath);
            }
        }

        private void Awake()
        {
            EnsureComponents();
            ApplyLayer();
            ApplyVisual();
            ConfigureRootHitbox();
        }

        private void Update()
        {
            if (isDead || GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Playing)
            {
                return;
            }

            if (isEntering)
            {
                UpdateEntry();
                UpdateBossUi();
                return;
            }

            UpdatePhase();
            UpdateMovement();
            UpdateAttack();
            UpdateBossUi();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isDead || useChildHitboxes || other == null)
            {
                return;
            }

            var bullet = other.GetComponent<BulletProjectile>();
            if (bullet != null && string.Equals(bullet.Owner, "Player", System.StringComparison.OrdinalIgnoreCase))
            {
                TakeDamage(bullet.Damage);
            }
        }

        private void EnsureComponents()
        {
            body = body != null ? body : GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            hitbox = hitbox != null ? hitbox : GetComponent<CircleCollider2D>();
            hitbox.isTrigger = true;
            hitbox.radius = 0.72f;

            spriteRenderer = spriteRenderer != null ? spriteRenderer : ResolveSpriteRenderer();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            mounts = mounts != null ? mounts : GetComponent<ActorMounts>();
            if (mounts == null)
            {
                mounts = gameObject.AddComponent<ActorMounts>();
            }
        }

        private void ApplyLayer()
        {
            var layer = LayerMask.NameToLayer("Enemy");
            if (layer >= 0)
            {
                gameObject.layer = layer;
            }
        }

        private void ApplyVisual()
        {
            var hasConfiguredSprite = spriteRenderer.sprite != null;
            spriteRenderer.sprite = hasConfiguredSprite ? spriteRenderer.sprite : LoadBossSprite() ?? CreateFallbackBossSprite();
            spriteRenderer.flipY = ShouldFlipBossVisual();
            spriteRenderer.sortingOrder = 25;
            originalColor = hasConfiguredSprite ? spriteRenderer.color : Color.white;
            spriteRenderer.color = originalColor;
        }

        private bool ShouldFlipBossVisual()
        {
            return config == null
                || string.IsNullOrEmpty(config.prefabPath)
                || config.prefabPath.IndexOf("/enemy_", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private void UpdateEntry()
        {
            var position = transform.position;
            position.y = Mathf.MoveTowards(position.y, EntryTargetY, EntrySpeed * Time.deltaTime);
            position.x = Mathf.Lerp(position.x, 0f, Time.deltaTime * 2.5f);
            transform.position = position;

            if (Mathf.Abs(position.y - EntryTargetY) > 0.01f)
            {
                return;
            }

            isEntering = false;
            anchorPosition = transform.position;
            nextAttackTime = Time.time + 0.65f;
            SetPhase(0, true);
        }

        private void UpdatePhase()
        {
            var nextPhase = ResolvePhaseIndex();
            if (nextPhase != currentPhaseIndex)
            {
                SetPhase(nextPhase, false);
            }
        }

        private int ResolvePhaseIndex()
        {
            if (phases == null || phases.Length == 0 || maxHp <= 0)
            {
                return 0;
            }

            var hpPercent = currentHp / (float)maxHp;
            var nextPhase = 0;
            for (var index = 0; index < phases.Length; index++)
            {
                if (hpPercent <= phases[index].triggerHpPercent)
                {
                    nextPhase = index;
                }
            }

            return nextPhase;
        }

        private void SetPhase(int phaseIndex, bool isInitial)
        {
            CancelBossLaserSequence();
            currentPhaseIndex = Mathf.Clamp(phaseIndex, 0, Mathf.Max(0, GetPhaseCount() - 1));
            anchorPosition = transform.position;
            phaseMovementTime = 0f;
            nextAttackTime = Time.time + (isInitial ? 0.65f : 1.1f);
            volleyCursor = 0;
            ConfigureScheduledPatterns(GetCurrentPhase());

            if (!isInitial && BulletManager.Instance != null)
            {
                BulletManager.Instance.ClearEnemyBullets();
            }

            if (!isInitial && MissileManager.Instance != null)
            {
                MissileManager.Instance.ClearEnemyMissiles();
            }

            ExplosionEffect.Spawn(transform.position, isInitial ? 1.1f : 1.55f);

            if (UIManager.Instance != null && ShouldShowBossPresentation())
            {
                UIManager.Instance.ShowBossPhaseNotice(GetCurrentPhaseName());
            }
        }

        private void UpdateMovement()
        {
            if (Time.time < movementLockedUntil)
            {
                return;
            }

            var phase = GetCurrentPhase();
            var range = phase != null ? phase.movementRange : new Vector2(1.2f, 0.15f);
            var speed = Mathf.Max(0.1f, phase != null ? phase.movementSpeed : 1.0f);
            phaseMovementTime += Time.deltaTime;
            var position = anchorPosition;
            position.x += Mathf.Sin(phaseMovementTime * speed) * Mathf.Max(0f, range.x);
            position.y += Mathf.Sin(phaseMovementTime * speed * 0.55f) * Mathf.Max(0f, range.y);
            transform.position = position;
        }

        private void UpdateAttack()
        {
            if (scheduledPatterns.Count > 0)
            {
                UpdateScheduledPatterns();
                return;
            }

            if (Time.time < nextAttackTime || isFiringBurst)
            {
                return;
            }

            var phase = GetCurrentPhase();
            nextAttackTime = Time.time + GetAttackInterval(phase);
            StartCoroutine(FirePhaseBurst(phase));
        }

        private IEnumerator FirePhaseBurst(BossPhaseConfig phase)
        {
            isFiringBurst = true;

            var burstCount = Mathf.Max(1, phase != null ? phase.burstCount : 1);
            var burstInterval = Mathf.Max(0.02f, phase != null ? phase.burstInterval : 0.08f);

            for (var index = 0; index < burstCount; index++)
            {
                if (isDead || GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Playing)
                {
                    break;
                }

                FireNextPattern(phase);

                if (index < burstCount - 1)
                {
                    yield return new WaitForSeconds(burstInterval);
                }
            }

            isFiringBurst = false;
        }

        private void FireNextPattern(BossPhaseConfig phase)
        {
            if (phase == null || phase.bulletPatternIds == null || phase.bulletPatternIds.Count == 0)
            {
                FireFallbackShot();
                return;
            }

            var patternId = phase.bulletPatternIds[volleyCursor % phase.bulletPatternIds.Count];
            volleyCursor++;

            FirePatternId(patternId);
        }

        private void FirePatternId(string patternId)
        {
            if (TryStartBoss02LaserSequence(patternId))
            {
                return;
            }

            if (TryFireBulletPattern(patternId) || TryFireMissilePattern(patternId))
            {
                return;
            }

            FireFallbackShot();
        }

        private void ConfigureScheduledPatterns(BossPhaseConfig phase)
        {
            scheduledPatterns.Clear();
            if (phase?.bulletPatternIds == null)
            {
                return;
            }

            foreach (var rawPatternId in phase.bulletPatternIds)
            {
                if (!TryParseScheduledPattern(rawPatternId, out var patternId, out var interval, out var offset))
                {
                    continue;
                }

                scheduledPatterns.Add(new ScheduledPattern
                {
                    patternId = patternId,
                    interval = interval,
                    nextFireTime = Time.time + offset
                });
            }
        }

        private void UpdateScheduledPatterns()
        {
            for (var index = 0; index < scheduledPatterns.Count; index++)
            {
                var scheduled = scheduledPatterns[index];
                if (Time.time < scheduled.nextFireTime)
                {
                    continue;
                }

                if (ShouldDeferLevel2FinalBossFastHoming(scheduled.patternId))
                {
                    scheduled.nextFireTime = movementLockedUntil + BossWindmillMissileResumeDelay;
                    continue;
                }

                FirePatternId(scheduled.patternId);
                scheduled.nextFireTime = Time.time + scheduled.interval;
            }
        }

        private bool ShouldDeferLevel2FinalBossFastHoming(string patternId)
        {
            return Time.time < movementLockedUntil
                && config != null
                && string.Equals(config.id, Level2FinalBossId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(patternId, Level2FinalBossFastHomingPatternId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseScheduledPattern(string rawPatternId, out string patternId, out float interval, out float offset)
        {
            patternId = rawPatternId;
            interval = 0f;
            offset = 0f;

            if (string.IsNullOrWhiteSpace(rawPatternId))
            {
                return false;
            }

            var separatorIndex = rawPatternId.IndexOf('@');
            if (separatorIndex < 0 || separatorIndex >= rawPatternId.Length - 1)
            {
                return false;
            }

            patternId = rawPatternId.Substring(0, separatorIndex);
            var scheduleSpec = rawPatternId.Substring(separatorIndex + 1);
            var offsetIndex = scheduleSpec.IndexOf('+');
            var intervalText = offsetIndex >= 0 ? scheduleSpec.Substring(0, offsetIndex) : scheduleSpec;
            var offsetText = offsetIndex >= 0 ? scheduleSpec.Substring(offsetIndex + 1) : "0";

            if (!float.TryParse(intervalText, NumberStyles.Float, CultureInfo.InvariantCulture, out interval))
            {
                return false;
            }

            if (!float.TryParse(offsetText, NumberStyles.Float, CultureInfo.InvariantCulture, out offset))
            {
                offset = 0f;
            }

            interval = Mathf.Max(0.05f, interval);
            offset = Mathf.Max(0f, offset);
            return !string.IsNullOrWhiteSpace(patternId);
        }

        private sealed class ScheduledPattern
        {
            public string patternId;
            public float interval;
            public float nextFireTime;
        }

        private bool TryStartBoss02LaserSequence(string patternId)
        {
            if (!string.Equals(patternId, Level2FinalBossLaserPatternId, StringComparison.OrdinalIgnoreCase)
                || config == null
                || !string.Equals(config.id, Level2FinalBossId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (bossLaserSequence == null)
            {
                bossLaserSequence = StartCoroutine(FireBoss02LaserSequence(patternId));
            }

            return true;
        }

        private IEnumerator FireBoss02LaserSequence(string patternId)
        {
            var configManager = ConfigManager.Instance;
            var bulletManager = BulletManager.Instance;
            var pattern = configManager != null && configManager.IsLoaded
                ? configManager.GetBulletPattern(patternId)
                : null;
            var sourceBullet = pattern != null ? configManager.GetBullet(pattern.bulletId) : null;
            var firePoints = pattern != null ? GetFirePoints(pattern.firePointGroup) : null;

            if (bulletManager == null || pattern == null || sourceBullet == null || firePoints == null || firePoints.Length == 0)
            {
                bossLaserSequence = null;
                yield break;
            }

            var runtimeBullet = CreateRuntimeBulletConfig(sourceBullet, pattern, BossLaserFireDuration);
            var warningLines = new LineRenderer[firePoints.Length];
            var lockedDirections = new Vector2[firePoints.Length];
            var warningLength = Mathf.Max(BossLaserWarningLength, sourceBullet.laserLength);

            for (var index = 0; index < firePoints.Length; index++)
            {
                warningLines[index] = CreateBossLaserWarningLine(index);
                lockedDirections[index] = Vector2.down;
            }

            var elapsed = 0f;
            while (elapsed < BossLaserTrackingDuration)
            {
                if (!CanContinueBossLaserSequence())
                {
                    ClearBossLaserWarnings();
                    bossLaserSequence = null;
                    yield break;
                }

                var player = FindObjectOfType<PlayerController>();
                for (var index = 0; index < firePoints.Length; index++)
                {
                    var firePoint = firePoints[index];
                    if (firePoint == null)
                    {
                        continue;
                    }

                    var origin = (Vector2)firePoint.position + pattern.firePointOffset;
                    lockedDirections[index] = ResolveBossLaserDirection(origin, player, lockedDirections[index]);
                    UpdateBossLaserWarningLine(warningLines[index], origin, lockedDirections[index], warningLength, false);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < BossLaserChargeDuration)
            {
                if (!CanContinueBossLaserSequence())
                {
                    ClearBossLaserWarnings();
                    bossLaserSequence = null;
                    yield break;
                }

                for (var index = 0; index < firePoints.Length; index++)
                {
                    var firePoint = firePoints[index];
                    if (firePoint == null)
                    {
                        continue;
                    }

                    var origin = (Vector2)firePoint.position + pattern.firePointOffset;
                    UpdateBossLaserWarningLine(warningLines[index], origin, lockedDirections[index], warningLength, true);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            ClearBossLaserWarnings();
            movementLockedUntil = Time.time + BossLaserFireDuration;

            for (var index = 0; index < firePoints.Length; index++)
            {
                var firePoint = firePoints[index];
                if (firePoint == null)
                {
                    continue;
                }

                bulletManager.Fire(runtimeBullet, firePoint.position, lockedDirections[index], firePoint);
            }

            elapsed = 0f;
            while (elapsed < BossLaserFireDuration && CanContinueBossLaserSequence())
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            movementLockedUntil = 0f;
            bossLaserSequence = null;
        }

        private static BulletConfig CreateRuntimeBulletConfig(BulletConfig source, BulletPatternConfig pattern, float lifetime)
        {
            return new BulletConfig
            {
                id = source.id,
                owner = source.owner,
                firePattern = source.firePattern,
                spritePath = source.spritePath,
                damage = source.damage,
                speed = pattern.bulletSpeed > 0f ? pattern.bulletSpeed : source.speed,
                lifetime = lifetime,
                size = source.size,
                glowColor = source.glowColor,
                glowRange = source.glowRange,
                projectileCount = source.projectileCount,
                spreadAngle = source.spreadAngle,
                muzzleSpacing = source.muzzleSpacing,
                pierceCount = source.pierceCount,
                laserLength = source.laserLength
            };
        }

        private bool CanContinueBossLaserSequence()
        {
            if (isDead || currentPhaseIndex < 0)
            {
                return false;
            }

            var phase = GetCurrentPhase();
            if (phase == null || !string.Equals(phase.id, "boss_02_phase_03", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var gameManager = GameManager.Instance;
            return gameManager == null
                || gameManager.CurrentState == GameState.Playing
                || gameManager.CurrentState == GameState.Paused;
        }

        private LineRenderer CreateBossLaserWarningLine(int index)
        {
            var warningObject = new GameObject($"BossLaserTracking_{index + 1}");
            warningObject.transform.SetParent(transform, false);
            var layer = LayerMask.NameToLayer("EnemyBullet");
            if (layer >= 0)
            {
                warningObject.layer = layer;
            }

            var line = warningObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.sharedMaterial = GetBossLaserWarningMaterial();
            line.sortingOrder = 24;
            line.numCapVertices = 3;
            line.enabled = true;
            bossLaserWarningObjects.Add(warningObject);
            return line;
        }

        private static void UpdateBossLaserWarningLine(
            LineRenderer line,
            Vector2 origin,
            Vector2 direction,
            float length,
            bool charging)
        {
            if (line == null)
            {
                return;
            }

            var pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (charging ? 24f : 12f));
            var normalizedDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.down;
            var width = charging ? Mathf.Lerp(0.035f, 0.065f, pulse) : Mathf.Lerp(0.018f, 0.032f, pulse);
            var alpha = charging ? Mathf.Lerp(0.72f, 1f, pulse) : Mathf.Lerp(0.42f, 0.82f, pulse);
            var color = charging
                ? new Color(1f, 0.34f, 0.04f, alpha)
                : new Color(1f, 0.04f, 0.02f, alpha);

            line.startWidth = width;
            line.endWidth = width * 0.58f;
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, color.a * 0.42f);
            line.SetPosition(0, origin);
            line.SetPosition(1, origin + normalizedDirection * length);
        }

        private static Vector2 ResolveBossLaserDirection(Vector2 origin, PlayerController player, Vector2 fallback)
        {
            if (player == null)
            {
                return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector2.down;
            }

            var direction = (Vector2)player.transform.position - origin;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.down;
        }

        private static Material GetBossLaserWarningMaterial()
        {
            if (bossLaserWarningMaterial == null)
            {
                bossLaserWarningMaterial = SpriteMaterialUtility.CreateSpriteMaterial("Boss Laser Tracking Material");
            }

            return bossLaserWarningMaterial;
        }

        private void CancelBossLaserSequence()
        {
            if (bossLaserSequence != null)
            {
                StopCoroutine(bossLaserSequence);
                bossLaserSequence = null;
            }

            movementLockedUntil = 0f;
            ClearBossLaserWarnings();
        }

        private void ClearBossLaserWarnings()
        {
            for (var index = 0; index < bossLaserWarningObjects.Count; index++)
            {
                if (bossLaserWarningObjects[index] != null)
                {
                    Destroy(bossLaserWarningObjects[index]);
                }
            }

            bossLaserWarningObjects.Clear();
        }

        private bool TryFireBulletPattern(string patternId)
        {
            var pattern = ConfigManager.Instance != null && ConfigManager.Instance.IsLoaded
                ? ConfigManager.Instance.GetBulletPattern(patternId)
                : null;
            if (pattern == null)
            {
                return false;
            }

            var patternManager = EnsureBulletPatternManager();
            if (patternManager == null)
            {
                return false;
            }

            LockMovementForLevel2FinalBossWindmill(pattern);

            var firePoints = GetFirePoints(pattern.firePointGroup);
            if (firePoints != null && firePoints.Length > 0)
            {
                foreach (var firePoint in firePoints)
                {
                    patternManager.FirePattern(pattern, firePoint);
                }

                return true;
            }

            patternManager.FirePattern(pattern, transform);
            return true;
        }

        private void LockMovementForLevel2FinalBossWindmill(BulletPatternConfig pattern)
        {
            if (config == null
                || pattern == null
                || !string.Equals(config.id, Level2FinalBossId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(pattern.id, Level2FinalBossWindmillPatternId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var duration = pattern.duration > 0f
                ? pattern.duration
                : Mathf.Max(1, pattern.burstCount) * Mathf.Max(0.01f, pattern.fireInterval);
            movementLockedUntil = Mathf.Max(movementLockedUntil, Time.time + duration);
        }

        private bool TryFireMissilePattern(string patternId)
        {
            var pattern = ConfigManager.Instance != null && ConfigManager.Instance.IsLoaded
                ? ConfigManager.Instance.GetMissilePattern(patternId)
                : null;
            if (pattern == null)
            {
                return false;
            }

            var patternManager = EnsureMissilePatternManager();
            if (patternManager == null)
            {
                return false;
            }

            var firePoints = GetFirePoints(pattern.firePointGroup);
            if (firePoints != null && firePoints.Length > 0)
            {
                foreach (var firePoint in firePoints)
                {
                    patternManager.FirePattern(pattern, firePoint);
                }

                return true;
            }

            patternManager.FirePattern(pattern, transform);
            return true;
        }

        private void FireFallbackShot()
        {
            if (BulletManager.Instance == null || ConfigManager.Instance == null || !ConfigManager.Instance.IsLoaded)
            {
                return;
            }

            var bulletConfig = ConfigManager.Instance.GetBullet(config != null ? config.bulletId : string.Empty);
            if (bulletConfig != null)
            {
                BulletManager.Instance.Fire(bulletConfig, transform.position + Vector3.down * 0.62f, Vector2.down);
            }
        }

        public void TakeDamage(int damage)
        {
            if (isDead || damage <= 0)
            {
                return;
            }

            currentHp = Mathf.Max(0, currentHp - damage);
            UpdateBossUi();

            if (AudioManager.Instance != null && config != null)
            {
                AudioManager.Instance.PlaySfx(config.hitSoundPath);
            }

            if (currentHp <= 0)
            {
                StartCoroutine(DefeatSequence());
            }
        }

        private IEnumerator DefeatSequence()
        {
            if (isDead)
            {
                yield break;
            }

            isDead = true;
            CancelBossLaserSequence();
            hitbox.enabled = false;
            LevelProgressService.RecordEnemyKilled();

            if (config != null)
            {
                PickupManager.GetOrCreate().SpawnDrops(config, transform.position);
            }

            if (BulletManager.Instance != null)
            {
                BulletManager.Instance.ClearEnemyBullets();
            }

            if (MissileManager.Instance != null)
            {
                MissileManager.Instance.ClearEnemyMissiles();
            }

            if (UIManager.Instance != null && ShouldShowBossPresentation())
            {
                UIManager.Instance.ShowBossPhaseNotice("首领已击破");
            }

            const int explosionCount = 9;
            for (var index = 0; index < explosionCount; index++)
            {
                var angle = index * 137.5f * Mathf.Deg2Rad;
                var radius = 0.25f + 0.16f * index;
                var offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                ExplosionEffect.Spawn(transform.position + offset, 1.0f + index * 0.12f);

                if (AudioManager.Instance != null && index % 3 == 0)
                {
                    AudioManager.Instance.PlayEnemyDestroyed();
                }

                yield return new WaitForSeconds(0.09f);
            }

            if (GameManager.Instance != null && config != null)
            {
                GameManager.Instance.AddScore(config.score);
            }

            if (UIManager.Instance != null)
            {
                if (config != null)
                {
                    UIManager.Instance.ShowScorePopup(transform.position, config.score);
                }

                if (ShouldShowBossPresentation())
                {
                    UIManager.Instance.HideBossHud();
                }
            }

            var shouldWinGame = EnemyManager.Instance == null
                || EnemyManager.Instance.NotifyBossDefeated(
                    config != null ? config.id : string.Empty,
                    pausesBattleTimeline);
            if (shouldWinGame && GameManager.Instance != null)
            {
                GameManager.Instance.WinGame();
            }

            Destroy(gameObject);
        }

        private void UpdateBossUi()
        {
            if (UIManager.Instance != null && ShouldShowBossPresentation())
            {
                UIManager.Instance.UpdateBossHud(config != null ? config.displayName : "首领", currentHp, maxHp, GetCurrentPhaseName());
            }
        }

        private bool ShouldShowBossPresentation()
        {
            return config == null
                || !string.Equals(config.id, Level2MidBossId, StringComparison.OrdinalIgnoreCase);
        }

        private BossPhaseConfig GetCurrentPhase()
        {
            if (phases == null || phases.Length == 0)
            {
                return null;
            }

            return phases[Mathf.Clamp(currentPhaseIndex, 0, phases.Length - 1)];
        }

        private string GetCurrentPhaseName()
        {
            var phase = GetCurrentPhase();
            if (phase != null && !string.IsNullOrEmpty(phase.displayName))
            {
                return phase.displayName;
            }

            return $"PHASE {Mathf.Max(1, currentPhaseIndex + 1)}";
        }

        private int GetPhaseCount()
        {
            return phases != null && phases.Length > 0 ? phases.Length : 1;
        }

        private float GetAttackInterval(BossPhaseConfig phase)
        {
            if (phase != null && phase.attackInterval > 0f)
            {
                return phase.attackInterval;
            }

            return config != null && config.attackInterval > 0f ? config.attackInterval : DefaultAttackInterval;
        }

        private BossPhaseConfig[] ResolveBossPhases()
        {
            if (ConfigManager.Instance == null || !ConfigManager.Instance.IsLoaded || config == null)
            {
                return null;
            }

            var resolvedPhases = ConfigManager.Instance.GetBossPhases(config.id);
            if (resolvedPhases != null && resolvedPhases.Length > 0)
            {
                return resolvedPhases;
            }

            return ConfigManager.Instance.GetBossPhases("boss_01");
        }

        private Transform[] GetFirePoints(string groupName)
        {
            return mounts != null ? mounts.GetFirePoints(groupName) : null;
        }

        private void ConfigureRootHitbox()
        {
            useChildHitboxes = GetComponentsInChildren<ActorHitbox>().Length > 0;
            if (hitbox != null && useChildHitboxes)
            {
                hitbox.enabled = false;
            }
        }

        private BulletPatternManager EnsureBulletPatternManager()
        {
            if (BulletPatternManager.Instance != null)
            {
                return BulletPatternManager.Instance;
            }

            var managers = GameObject.Find("Managers") ?? new GameObject("Managers");
            return managers.GetComponent<BulletPatternManager>() ?? managers.AddComponent<BulletPatternManager>();
        }

        private MissilePatternManager EnsureMissilePatternManager()
        {
            if (MissilePatternManager.Instance != null)
            {
                return MissilePatternManager.Instance;
            }

            var managers = GameObject.Find("Managers") ?? new GameObject("Managers");
            return managers.GetComponent<MissilePatternManager>() ?? managers.AddComponent<MissilePatternManager>();
        }

        private Sprite LoadBossSprite()
        {
            const string bossSpritePath = "Assets/Art/Animations/Enemies/BOSS-1.png";

#if UNITY_EDITOR
            if (RuntimeRemoteResourceManager.CanUseEditorLocalAssets)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(bossSpritePath);
                if (sprite != null)
                {
                    return sprite;
                }
            }
#endif
            var catalogSprite = RuntimeAssetCatalog.LoadSprite(bossSpritePath);
            if (catalogSprite != null || !RuntimeRemoteResourceManager.CanUseResourcesFallback)
            {
                return catalogSprite;
            }

            return Resources.Load<Sprite>("Enemies/BOSS-1");
        }

        private SpriteRenderer ResolveSpriteRenderer()
        {
            var visual = transform.Find("Visual");
            if (visual != null)
            {
                return visual.GetComponent<SpriteRenderer>() ?? visual.gameObject.AddComponent<SpriteRenderer>();
            }

            return GetComponent<SpriteRenderer>();
        }

        private static Sprite CreateFallbackBossSprite()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;

            var clear = new Color(0f, 0f, 0f, 0f);
            var body = new Color(0.74f, 0.18f, 0.95f, 1f);
            var core = new Color(1f, 0.82f, 0.24f, 1f);
            var wing = new Color(0.22f, 0.78f, 1f, 1f);

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    texture.SetPixel(x, y, clear);
                }
            }

            for (var y = 4; y < 29; y++)
            {
                var half = y < 17 ? 5 + y / 4 : 13 - (y - 17) / 3;
                for (var x = 15 - half; x <= 16 + half; x++)
                {
                    texture.SetPixel(x, y, body);
                }
            }

            for (var y = 10; y < 23; y++)
            {
                for (var x = 2; x < 9; x++)
                {
                    texture.SetPixel(x, y, wing);
                    texture.SetPixel(size - 1 - x, y, wing);
                }
            }

            for (var y = 9; y < 22; y++)
            {
                texture.SetPixel(15, y, core);
                texture.SetPixel(16, y, core);
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
