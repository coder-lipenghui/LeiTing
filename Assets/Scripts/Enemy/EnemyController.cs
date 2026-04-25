using LeiTing.Bullets;
using LeiTing.Audio;
using LeiTing.Config;
using LeiTing.Core;
using LeiTing.Effects;
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

        [SerializeField] private EnemyConfig config;
        [SerializeField] private int currentHp;

        private Rigidbody2D body;
        private CircleCollider2D hitbox;
        private SpriteRenderer spriteRenderer;
        private SpriteRenderer flashRenderer;
        private ActorMounts mounts;
        private Color originalColor = Color.white;
        private Vector3 spawnPosition;
        private Vector3 baseScale = Vector3.one;
        private string attackPatternId;
        private string movementPath;
        private float pathAmplitude;
        private float pathSpeed;
        private float holdDuration;
        private float holdStartedAt;
        private float aliveTime;
        private float nextAttackTime;
        private float flashUntil;
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

            if (transform.position.y < DespawnY)
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
            var hasPrefabVisual = transform.Find("Visual") != null;
            spriteRenderer.sprite = spriteRenderer.sprite != null ? spriteRenderer.sprite : LoadEnemySprite() ?? CreateFallbackEnemySprite();
            spriteRenderer.flipY = true;
            spriteRenderer.sortingOrder = 15;
            originalColor = GetEnemyColor();
            spriteRenderer.color = originalColor;

            var scale = IsEnemyType("enemy_b") || IsEnemyType("enemy_e") ? 0.82f : IsEnemyType("enemy_c") ? 0.68f : 0.58f;
            baseScale = hasPrefabVisual ? Vector3.one : Vector3.one * scale;
            transform.localScale = baseScale;

            if (flashRenderer != null)
            {
                flashRenderer.sprite = spriteRenderer.sprite;
                flashRenderer.flipY = true;
                flashRenderer.color = new Color(1f, 1f, 1f, 0f);
            }
        }

        private Sprite LoadEnemySprite()
        {
#if UNITY_EDITOR
            var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Animations/Enemies/enemy-01.png");
            if (sprite != null)
            {
                return sprite;
            }
#endif
            return Resources.Load<Sprite>("Enemies/enemy-01");
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

        private void UpdateConfiguredMovement()
        {
            var position = transform.position;
            var speed = GetMoveSpeed();
            var normalizedPath = movementPath.Trim();

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
                var patternManager = EnsureBulletPatternManager();
                if (patternManager != null)
                {
                    var pattern = ConfigManager.Instance != null && ConfigManager.Instance.IsLoaded
                        ? ConfigManager.Instance.GetBulletPattern(attackPatternId)
                        : null;
                    var firePoints = pattern != null ? GetFirePoints(pattern.firePointGroup) : null;
                    if (firePoints != null && firePoints.Length > 0)
                    {
                        foreach (var firePoint in firePoints)
                        {
                            patternManager.FirePattern(pattern, firePoint.position);
                        }

                        return;
                    }

                    patternManager.FirePattern(attackPatternId, transform.position);
                    return;
                }
            }

            FireSingleAtPlayer();
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
            if (damage <= 0)
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
            transform.localScale = isFlashing ? baseScale * 1.08f : baseScale;

            if (flashRenderer != null)
            {
                flashRenderer.sprite = spriteRenderer.sprite;
                flashRenderer.color = isFlashing ? new Color(1f, 1f, 1f, 0.72f) : new Color(1f, 1f, 1f, 0f);
                flashRenderer.transform.localScale = isFlashing ? Vector3.one * 1.08f : Vector3.one;
            }
        }

        private bool IsEnemyType(string enemyId)
        {
            return string.Equals(config?.id, enemyId, System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMovementPath(string path, string expected)
        {
            return string.Equals(path, expected, System.StringComparison.OrdinalIgnoreCase);
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

        private Color GetEnemyColor()
        {
            if (IsEnemyType("enemy_b"))
            {
                return new Color(1f, 0.58f, 0.22f, 1f);
            }

            if (IsEnemyType("enemy_c"))
            {
                return new Color(0.95f, 0.32f, 1f, 1f);
            }

            if (IsEnemyType("enemy_d"))
            {
                return new Color(0.45f, 0.95f, 1f, 1f);
            }

            if (IsEnemyType("enemy_e"))
            {
                return new Color(1f, 0.22f, 0.32f, 1f);
            }

            return new Color(1f, 1f, 1f, 1f);
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
