using System;
using System.Globalization;
using LeiTing.Bullets;
using LeiTing.Audio;
using LeiTing.Config;
using LeiTing.Core;
using LeiTing.Effects;
using LeiTing.Enemy.Movement;
using LeiTing.Missiles;
using LeiTing.Pickups;
using LeiTing.Player;
using LeiTing.UI;
using UnityEngine;

namespace LeiTing.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class EnemyController : MonoBehaviour
    {
        private const float DespawnY = -6.4f;
        private const float EntryStopY = 2.6f;

        private static Material hitFlashMaterial;

        [SerializeField] private EnemyConfig config;
        [SerializeField] private int currentHp;

        private Rigidbody2D body;
        private CircleCollider2D hitbox;
        private SpriteRenderer spriteRenderer;
        private SpriteRenderer flashRenderer;
        private ActorMounts mounts;
        private Color originalColor = Color.white;
        private Vector3 spawnPosition;
        private Vector3 flashBaseLocalScale = Vector3.one;
        private string attackPatternId;
        private string movementPath;
        private float pathAmplitude;
        private float pathSpeed;
        private float holdDuration;
        private float holdStartedAt;
        private float aliveTime;
        private float nextAttackTime;
        private float flashUntil;
        private OrbitMovement orbitMovement;
        private bool usesOrbitMovement;
        private bool configuredRotateToPath;
        private float configuredRotationOffset = -90f;
        private bool hasFiredEntryShot;
        private bool useChildHitboxes;
        private bool isDead;

        public void Initialize(EnemyConfig enemyConfig, Vector2 position)
        {
            Initialize(enemyConfig, position, null);
        }

        public void Initialize(EnemyConfig enemyConfig, Vector2 position, WaveSpawnConfig spawnConfig)
        {
            EnsureComponents();

            config = enemyConfig;
            currentHp = Mathf.Max(1, config != null ? config.hp : 1);
            spawnPosition = position;
            attackPatternId = spawnConfig != null && !string.IsNullOrEmpty(spawnConfig.attackPatternId)
                ? spawnConfig.attackPatternId
                : config?.bulletPatternId;
            movementPath = spawnConfig != null ? spawnConfig.movementPath : string.Empty;
            pathAmplitude = spawnConfig != null ? spawnConfig.pathAmplitude : 0f;
            pathSpeed = spawnConfig != null ? spawnConfig.pathSpeed : 0f;
            holdDuration = spawnConfig != null ? spawnConfig.holdDuration : 0f;
            holdStartedAt = -1f;
            transform.position = position;
            aliveTime = 0f;
            nextAttackTime = Time.time + GetAttackInterval();
            flashUntil = 0f;
            hasFiredEntryShot = false;
            isDead = false;
            gameObject.SetActive(true);

            ApplyLayer();
            ApplyVisual();
            ConfigureRootHitbox();
            ConfigureMovementBehavior(spawnConfig);
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
                UpdateFlash();
                return;
            }

            aliveTime += Time.deltaTime;
            UpdateMovement();
            UpdateAttack();
            UpdateFlash();

            if (!usesOrbitMovement && transform.position.y < DespawnY)
            {
                Destroy(gameObject);
            }
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
            hitbox.radius = 0.32f;

            spriteRenderer = spriteRenderer != null ? spriteRenderer : ResolveSpriteRenderer();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            if (flashRenderer == null)
            {
                var flashObject = new GameObject("HitFlash");
                flashObject.transform.SetParent(transform, false);
                flashRenderer = flashObject.AddComponent<SpriteRenderer>();
                flashRenderer.color = new Color(1f, 1f, 1f, 0f);
                flashRenderer.sharedMaterial = GetHitFlashMaterial();
                flashRenderer.sortingOrder = 16;
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
            spriteRenderer.sprite = spriteRenderer.sprite != null ? spriteRenderer.sprite : LoadEnemySprite() ?? CreateFallbackEnemySprite();
            spriteRenderer.flipY = false;
            spriteRenderer.sortingOrder = 15;
            originalColor = Color.white;
            spriteRenderer.color = originalColor;

            if (flashRenderer != null)
            {
                flashRenderer.sprite = spriteRenderer.sprite;
                flashRenderer.flipY = false;
                flashRenderer.color = new Color(1f, 1f, 1f, 0f);
                flashRenderer.sharedMaterial = GetHitFlashMaterial();
                SyncFlashRendererTransform();
            }
        }

        private Sprite LoadEnemySprite()
        {
            const string enemySpritePath = "Assets/Art/Animations/Enemies/enemy-01.png";

#if UNITY_EDITOR
            var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(enemySpritePath);
            if (sprite != null)
            {
                return sprite;
            }
#endif
            return RuntimeAssetCatalog.LoadSprite(enemySpritePath)
                ?? Resources.Load<Sprite>("Enemies/enemy-01");
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

        private void UpdateMovement()
        {
            if (usesOrbitMovement)
            {
                if (orbitMovement != null && orbitMovement.IsActive)
                {
                    orbitMovement.Tick(Time.deltaTime);
                }

                return;
            }

            if (!string.IsNullOrEmpty(movementPath))
            {
                UpdateConfiguredMovement();
                return;
            }

            var position = transform.position;
            var speed = GetMoveSpeed();

            if (IsEnemyType("enemy_b") || IsEnemyType("enemy_e"))
            {
                if (position.y > EntryStopY)
                {
                    position += Vector3.down * speed * Time.deltaTime;
                }
                else if (aliveTime > 3.2f)
                {
                    position += Vector3.down * speed * 1.25f * Time.deltaTime;
                }
            }
            else if (IsEnemyType("enemy_c"))
            {
                position.y -= speed * Time.deltaTime;
                position.x = spawnPosition.x + Mathf.Sin(aliveTime * 3.2f) * 1.1f;
            }
            else
            {
                position += Vector3.down * speed * Time.deltaTime;
            }

            transform.position = position;
        }

        private void ConfigureMovementBehavior(WaveSpawnConfig spawnConfig)
        {
            usesOrbitMovement = false;
            configuredRotateToPath = false;
            configuredRotationOffset = -90f;

            if (!IsOrbitMovementPath(movementPath))
            {
                ConfigureConfiguredMovementRotation();

                if (orbitMovement != null)
                {
                    orbitMovement.AutoUpdate = false;
                    orbitMovement.enabled = false;
                }

                return;
            }

            orbitMovement = orbitMovement != null ? orbitMovement : GetComponent<OrbitMovement>();
            if (orbitMovement == null)
            {
                orbitMovement = gameObject.AddComponent<OrbitMovement>();
            }

            orbitMovement.enabled = true;
            orbitMovement.AutoUpdate = false;
            orbitMovement.Initialize(BuildOrbitMovementConfig(spawnConfig), spawnPosition, GetMoveSpeed());
            usesOrbitMovement = true;
        }

        private OrbitMovementConfig BuildOrbitMovementConfig(WaveSpawnConfig spawnConfig)
        {
            if (spawnConfig != null && spawnConfig.orbitMovement != null)
            {
                var configured = spawnConfig.orbitMovement.Clone();
                ApplyInlineOrbitParameters(configured, movementPath);
                return NormalizeOrbitMovementConfig(configured);
            }

            var radius = spawnConfig != null && spawnConfig.pathAmplitude > 0f ? spawnConfig.pathAmplitude : 1.2f;
            var fallback = new OrbitMovementConfig
            {
                centerX = spawnConfig != null ? spawnConfig.startPosition.x : spawnPosition.x,
                centerY = EntryStopY,
                radiusX = radius,
                radiusY = radius * 0.65f,
                angularSpeed = spawnConfig != null && spawnConfig.pathSpeed > 0f ? spawnConfig.pathSpeed : 120f,
                startAngle = 90f,
                orbitDuration = spawnConfig != null && spawnConfig.holdDuration > 0f ? spawnConfig.holdDuration : 3f,
                enterSpeed = GetMoveSpeed(),
                exitSpeed = GetMoveSpeed() * 1.25f,
                exitDirection = OrbitExitDirection.Down,
                rotateToPath = false
            };

            ApplyInlineOrbitParameters(fallback, movementPath);
            return NormalizeOrbitMovementConfig(fallback);
        }

        private OrbitMovementConfig NormalizeOrbitMovementConfig(OrbitMovementConfig orbitConfig)
        {
            orbitConfig.radiusX = orbitConfig.radiusX > 0f ? orbitConfig.radiusX : 1.2f;
            orbitConfig.radiusY = orbitConfig.radiusY > 0f ? orbitConfig.radiusY : orbitConfig.radiusX;
            orbitConfig.angularSpeed = orbitConfig.angularSpeed > 0f ? orbitConfig.angularSpeed : GetMoveSpeed() * 60f;
            orbitConfig.exitSpeed = orbitConfig.exitSpeed > 0f ? orbitConfig.exitSpeed : GetMoveSpeed() * 1.25f;
            return orbitConfig;
        }

        private void ConfigureConfiguredMovementRotation()
        {
            ForEachInlineMovementParameter(movementPath, (key, value) =>
            {
                switch (key.ToLowerInvariant())
                {
                    case "rotate":
                    case "rotatetopath":
                        SetBool(value, result => configuredRotateToPath = result);
                        break;
                    case "rotationoffset":
                        SetFloat(value, result => configuredRotationOffset = result);
                        break;
                }
            });
        }

        private void UpdateConfiguredMovement()
        {
            var previousPosition = transform.position;
            var position = transform.position;
            var speed = GetMoveSpeed();
            var normalizedPath = GetMovementPathName(movementPath);

            if (IsMovementPath(normalizedPath, "Hold") || IsMovementPath(normalizedPath, "StopAndLeave"))
            {
                if (position.y > EntryStopY)
                {
                    position += Vector3.down * speed * Time.deltaTime;
                    if (position.y <= EntryStopY)
                    {
                        position.y = EntryStopY;
                        holdStartedAt = aliveTime;
                    }
                }
                else if (holdStartedAt < 0f)
                {
                    holdStartedAt = aliveTime;
                }
                else if (IsMovementPath(normalizedPath, "StopAndLeave") && aliveTime - holdStartedAt > Mathf.Max(0.5f, holdDuration))
                {
                    position += Vector3.down * speed * 1.25f * Time.deltaTime;
                }

                transform.position = position;
                ApplyConfiguredPathRotation(previousPosition, position);
                return;
            }

            position += Vector3.down * speed * Time.deltaTime;

            if (IsMovementPath(normalizedPath, "Sine"))
            {
                var amplitude = pathAmplitude > 0f ? pathAmplitude : 1.0f;
                var frequency = pathSpeed > 0f ? pathSpeed : 3.0f;
                position.x = spawnPosition.x + Mathf.Sin(aliveTime * frequency) * amplitude;
            }
            else if (IsMovementPath(normalizedPath, "DriftLeft") || IsMovementPath(normalizedPath, "DriftRight"))
            {
                var direction = IsMovementPath(normalizedPath, "DriftLeft") ? -1f : 1f;
                var horizontalSpeed = pathSpeed > 0f ? pathSpeed : 0.45f;
                position.x += direction * horizontalSpeed * Time.deltaTime;
            }

            transform.position = position;
            ApplyConfiguredPathRotation(previousPosition, position);
        }

        private void ApplyConfiguredPathRotation(Vector2 previousPosition, Vector2 currentPosition)
        {
            if (!configuredRotateToPath)
            {
                return;
            }

            var delta = currentPosition - previousPosition;
            if (delta.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle + configuredRotationOffset);
        }

        private void UpdateAttack()
        {
            if (string.IsNullOrEmpty(config?.bulletId) && string.IsNullOrEmpty(attackPatternId))
            {
                return;
            }

            if (IsEnemyType("enemy_a"))
            {
                if (!hasFiredEntryShot && aliveTime > 0.65f)
                {
                    hasFiredEntryShot = true;
                    FireConfiguredPattern();
                }

                return;
            }

            if (Time.time < nextAttackTime)
            {
                return;
            }

            nextAttackTime = Time.time + GetAttackInterval();

            FireConfiguredPattern();
        }

        private void FireConfiguredPattern()
        {
            if (!string.IsNullOrEmpty(attackPatternId))
            {
                if (TryFireBulletPattern(attackPatternId) || TryFireMissilePattern(attackPatternId))
                {
                    return;
                }
            }

            FireSingleAtPlayer();
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

        private void FireSingleAtPlayer()
        {
            var bulletConfig = ResolveBulletConfig();
            if (bulletConfig == null || BulletManager.Instance == null)
            {
                return;
            }

            var direction = ResolveDirectionToPlayer();
            BulletManager.Instance.Fire(bulletConfig, transform.position + Vector3.down * 0.38f, direction);
        }

        private void FireSpreadDown()
        {
            var bulletConfig = ResolveBulletConfig();
            if (bulletConfig == null || BulletManager.Instance == null)
            {
                return;
            }

            for (var index = 0; index < 3; index++)
            {
                var angle = -18f + 18f * index;
                var direction = Rotate(Vector2.down, angle);
                BulletManager.Instance.Fire(bulletConfig, transform.position + Vector3.down * 0.32f, direction);
            }
        }

        private BulletConfig ResolveBulletConfig()
        {
            return ConfigManager.Instance != null && ConfigManager.Instance.IsLoaded ? ConfigManager.Instance.GetBullet(config.bulletId) : null;
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

        private Vector2 ResolveDirectionToPlayer()
        {
            var player = FindObjectOfType<PlayerController>();
            if (player == null)
            {
                return Vector2.down;
            }

            var direction = (Vector2)(player.transform.position - transform.position);
            return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.down;
        }

        public void TakeDamage(int damage)
        {
            if (isDead || damage <= 0)
            {
                return;
            }

            currentHp = Mathf.Max(0, currentHp - damage);
            flashUntil = Time.time + 0.06f;

            if (currentHp <= 0)
            {
                Die();
            }
        }

        public void KillInstantly()
        {
            if (isDead)
            {
                return;
            }

            currentHp = 0;
            Die();
        }

        private void Die()
        {
            if (isDead)
            {
                return;
            }

            isDead = true;
            ExplosionEffect.Spawn(transform.position, IsEnemyType("enemy_b") ? 1.0f : 0.7f);

            if (GameManager.Instance != null && config != null)
            {
                GameManager.Instance.AddScore(config.score);
            }

            if (config != null)
            {
                PickupManager.GetOrCreate().SpawnDrops(config, transform.position);
            }

            if (UIManager.Instance != null && config != null)
            {
                UIManager.Instance.ShowScorePopup(transform.position, config.score);
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayEnemyDestroyed();
            }

            Destroy(gameObject);
        }

        private void UpdateFlash()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            var isFlashing = Time.time < flashUntil;
            spriteRenderer.color = isFlashing ? Color.white : originalColor;

            if (flashRenderer != null)
            {
                flashRenderer.sprite = spriteRenderer.sprite;
                flashRenderer.color = isFlashing ? new Color(1f, 1f, 1f, 0.72f) : new Color(1f, 1f, 1f, 0f);
                flashRenderer.transform.localScale = flashBaseLocalScale;
            }
        }

        private bool IsEnemyType(string enemyId)
        {
            return string.Equals(config?.id, enemyId, System.StringComparison.OrdinalIgnoreCase);
        }

        private static Material GetHitFlashMaterial()
        {
            if (hitFlashMaterial != null)
            {
                return hitFlashMaterial;
            }

            var shader = Shader.Find("LeiTing/SpriteSilhouette");
            if (shader == null)
            {
                return null;
            }

            hitFlashMaterial = new Material(shader)
            {
                name = "EnemyHitFlashMaterial"
            };
            return hitFlashMaterial;
        }

        private static bool IsMovementPath(string path, string expected)
        {
            return string.Equals(path, expected, System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsOrbitMovementPath(string path)
        {
            var pathName = GetMovementPathName(path);
            return IsMovementPath(pathName, "Orbit")
                || IsMovementPath(pathName, "OrbitMovement")
                || IsMovementPath(pathName, "EnemyOrbitMove");
        }

        private static string GetMovementPathName(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            var normalized = path.Trim();
            var parameterStart = normalized.IndexOfAny(new[] { ':', '(' });
            return parameterStart >= 0 ? normalized.Substring(0, parameterStart).Trim() : normalized;
        }

        private static void ApplyInlineOrbitParameters(OrbitMovementConfig orbitConfig, string path)
        {
            if (orbitConfig == null || string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            ForEachInlineMovementParameter(path, (key, value) => ApplyInlineOrbitParameter(orbitConfig, key, value));
        }

        private static void ForEachInlineMovementParameter(string path, Action<string, string> apply)
        {
            if (string.IsNullOrWhiteSpace(path) || apply == null)
            {
                return;
            }

            var parameterStart = path.IndexOfAny(new[] { ':', '(' });
            if (parameterStart < 0)
            {
                return;
            }

            var marker = path[parameterStart];
            var body = path.Substring(parameterStart + 1).Trim();
            if (marker == '(' && body.EndsWith(")", StringComparison.Ordinal))
            {
                body = body.Substring(0, body.Length - 1);
            }
            else if (body.StartsWith("(", StringComparison.Ordinal) && body.EndsWith(")", StringComparison.Ordinal))
            {
                body = body.Substring(1, body.Length - 2);
            }

            var pairs = body.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var separator = pair.IndexOf('=');
                if (separator <= 0 || separator >= pair.Length - 1)
                {
                    continue;
                }

                var key = pair.Substring(0, separator).Trim();
                var value = pair.Substring(separator + 1).Trim();
                apply(key, value);
            }
        }

        private static void ApplyInlineOrbitParameter(OrbitMovementConfig orbitConfig, string key, string value)
        {
            switch (key.ToLowerInvariant())
            {
                case "centerx":
                    SetFloat(value, result => orbitConfig.centerX = result);
                    break;
                case "centery":
                    SetFloat(value, result => orbitConfig.centerY = result);
                    break;
                case "radiusx":
                    SetFloat(value, result => orbitConfig.radiusX = result);
                    break;
                case "radiusy":
                    SetFloat(value, result => orbitConfig.radiusY = result);
                    break;
                case "angularspeed":
                    SetFloat(value, result => orbitConfig.angularSpeed = result);
                    break;
                case "clockwise":
                    SetBool(value, result => orbitConfig.clockwise = result);
                    break;
                case "startangle":
                    SetFloat(value, result => orbitConfig.startAngle = result);
                    break;
                case "orbitduration":
                case "duration":
                    SetFloat(value, result => orbitConfig.orbitDuration = result);
                    break;
                case "loopcount":
                    SetFloat(value, result => orbitConfig.loopCount = result);
                    break;
                case "centermovespeedy":
                case "centermovespeed":
                    SetFloat(value, result => orbitConfig.centerMoveSpeedY = result);
                    break;
                case "enterduration":
                    SetFloat(value, result => orbitConfig.enterDuration = result);
                    break;
                case "enterspeed":
                    SetFloat(value, result => orbitConfig.enterSpeed = result);
                    break;
                case "easeoutenter":
                case "easeout":
                    SetBool(value, result => orbitConfig.easeOutEnter = result);
                    break;
                case "exitdirection":
                    SetExitDirection(value, result => orbitConfig.exitDirection = result);
                    break;
                case "exitspeed":
                    SetFloat(value, result => orbitConfig.exitSpeed = result);
                    break;
                case "destroyonexitcomplete":
                    SetBool(value, result => orbitConfig.destroyOnExitComplete = result);
                    break;
                case "exitdespawnpadding":
                case "exitpadding":
                    SetFloat(value, result => orbitConfig.exitDespawnPadding = result);
                    break;
                case "rotate":
                case "rotatetopath":
                    SetBool(value, result => orbitConfig.rotateToPath = result);
                    break;
                case "rotationoffset":
                    SetFloat(value, result => orbitConfig.rotationOffset = result);
                    break;
            }
        }

        private static void SetFloat(string value, Action<float> apply)
        {
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            {
                apply(result);
            }
        }

        private static void SetBool(string value, Action<bool> apply)
        {
            if (bool.TryParse(value, out var result))
            {
                apply(result);
                return;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericResult))
            {
                apply(numericResult != 0);
            }
        }

        private static void SetExitDirection(string value, Action<OrbitExitDirection> apply)
        {
            if (Enum.TryParse(value, true, out OrbitExitDirection result))
            {
                apply(result);
            }
        }

        private float GetMoveSpeed()
        {
            return Mathf.Max(0.1f, config != null ? config.moveSpeed : 2f);
        }

        private float GetAttackInterval()
        {
            return Mathf.Max(0.1f, config != null ? config.attackInterval : 1.5f);
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

        private void SyncFlashRendererTransform()
        {
            if (flashRenderer == null || spriteRenderer == null)
            {
                return;
            }

            var flashTransform = flashRenderer.transform;
            var visualTransform = spriteRenderer.transform;
            flashTransform.localPosition = visualTransform == transform ? Vector3.zero : visualTransform.localPosition;
            flashTransform.localRotation = visualTransform == transform ? Quaternion.identity : visualTransform.localRotation;
            flashBaseLocalScale = visualTransform == transform ? Vector3.one : visualTransform.localScale;
            flashTransform.localScale = flashBaseLocalScale;
        }

        private static Vector2 Rotate(Vector2 vector, float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            var sin = Mathf.Sin(radians);
            var cos = Mathf.Cos(radians);
            return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos).normalized;
        }

        private static Sprite CreateFallbackEnemySprite()
        {
            const int size = 18;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;

            var clear = new Color(0f, 0f, 0f, 0f);
            var body = new Color(1f, 0.32f, 0.18f, 1f);
            var wing = new Color(0.75f, 0.08f, 0.2f, 1f);

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    texture.SetPixel(x, y, clear);
                }
            }

            for (var y = 2; y < size - 1; y++)
            {
                var half = y < 10 ? (10 - y) / 2 + 1 : y / 4;
                for (var x = 8 - half; x <= 9 + half; x++)
                {
                    texture.SetPixel(x, y, body);
                }
            }

            for (var y = 7; y < 13; y++)
            {
                texture.SetPixel(3, y, wing);
                texture.SetPixel(4, y, wing);
                texture.SetPixel(13, y, wing);
                texture.SetPixel(14, y, wing);
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
