using System.Collections;
using LeiTing.Audio;
using LeiTing.Bullets;
using LeiTing.Config;
using LeiTing.Core;
using LeiTing.Effects;
using LeiTing.Missiles;
using LeiTing.Pickups;
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

        [SerializeField] private EnemyConfig config;
        [SerializeField] private int currentHp;
        [SerializeField] private int maxHp;

        private BossPhaseConfig[] phases;
        private Rigidbody2D body;
        private CircleCollider2D hitbox;
        private SpriteRenderer spriteRenderer;
        private ActorMounts mounts;
        private Color originalColor = Color.white;
        private Vector3 anchorPosition;
        private float aliveTime;
        private float nextAttackTime;
        private int currentPhaseIndex = -1;
        private int volleyCursor;
        private bool isEntering;
        private bool isFiringBurst;
        private bool useChildHitboxes;
        private bool isDead;

        public int CurrentHp => currentHp;
        public int MaxHp => maxHp;

        public void Initialize(EnemyConfig enemyConfig, Vector2 position)
        {
            EnsureComponents();

            config = enemyConfig;
            maxHp = Mathf.Max(1, config != null ? config.hp : 1);
            currentHp = maxHp;
            phases = ResolveBossPhases();

            transform.position = position;
            anchorPosition = new Vector3(0f, EntryTargetY, 0f);
            aliveTime = 0f;
            nextAttackTime = Time.time + 1.4f;
            currentPhaseIndex = -1;
            volleyCursor = 0;
            isEntering = true;
            isFiringBurst = false;
            isDead = false;

            ApplyLayer();
            ApplyVisual();
            ConfigureRootHitbox();
            UpdateBossUi();

            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowBossPhaseNotice("WARNING\nBOSS APPROACHING");
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

            aliveTime += Time.deltaTime;

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
            spriteRenderer.flipY = true;
            spriteRenderer.sortingOrder = 25;
            originalColor = hasConfiguredSprite ? spriteRenderer.color : Color.white;
            spriteRenderer.color = originalColor;
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
            currentPhaseIndex = Mathf.Clamp(phaseIndex, 0, Mathf.Max(0, GetPhaseCount() - 1));
            nextAttackTime = Time.time + (isInitial ? 0.65f : 1.1f);
            volleyCursor = 0;

            if (!isInitial && BulletManager.Instance != null)
            {
                BulletManager.Instance.ClearEnemyBullets();
            }

            if (!isInitial && MissileManager.Instance != null)
            {
                MissileManager.Instance.ClearEnemyMissiles();
            }

            ExplosionEffect.Spawn(transform.position, isInitial ? 1.1f : 1.55f);

            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowBossPhaseNotice(GetCurrentPhaseName());
            }
        }

        private void UpdateMovement()
        {
            var phase = GetCurrentPhase();
            var range = phase != null ? phase.movementRange : new Vector2(1.2f, 0.15f);
            var speed = Mathf.Max(0.1f, phase != null ? phase.movementSpeed : 1.0f);
            var position = anchorPosition;
            position.x += Mathf.Sin(aliveTime * speed) * Mathf.Max(0f, range.x);
            position.y += Mathf.Sin(aliveTime * speed * 0.55f) * Mathf.Max(0f, range.y);
            transform.position = position;
        }

        private void UpdateAttack()
        {
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

            if (TryFireBulletPattern(patternId) || TryFireMissilePattern(patternId))
            {
                return;
            }

            FireFallbackShot();
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
            hitbox.enabled = false;

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

            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowBossPhaseNotice("BOSS DESTROYED");
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

                UIManager.Instance.HideBossHud();
            }

            var shouldWinGame = EnemyManager.Instance == null || EnemyManager.Instance.NotifyBossDefeated(config != null ? config.id : string.Empty);
            if (shouldWinGame && GameManager.Instance != null)
            {
                GameManager.Instance.WinGame();
            }

            Destroy(gameObject);
        }

        private void UpdateBossUi()
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.UpdateBossHud(config != null ? config.displayName : "BOSS", currentHp, maxHp, GetCurrentPhaseName());
            }
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
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(bossSpritePath);
            if (sprite != null)
            {
                return sprite;
            }
#endif
            return RuntimeAssetCatalog.LoadSprite(bossSpritePath)
                ?? Resources.Load<Sprite>("Enemies/BOSS-1");
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
