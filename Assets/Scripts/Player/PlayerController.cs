using LeiTing.Config;
using LeiTing.Core;
using LeiTing.Effects;
using LeiTing.Audio;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LeiTing.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private PlayerConfig config;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private PlayerHitbox playerHitbox;
        [SerializeField] private PlayerShooter playerShooter;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private LayerMask damageSourceLayers;
        [SerializeField] private float fallbackMoveSpeed = 6f;
        [SerializeField] private int fallbackShield = 1;
        [SerializeField] private float fallbackInvincibleTime = 1.5f;
        [SerializeField] private float fallbackPickupAttractRange = 2.2f;
        [SerializeField] private float fallbackPickupAttractSpeed = 8f;
        [SerializeField] private float hitboxRadius = 0.18f;
        [SerializeField] private Vector2 hitboxOffset;
        [SerializeField] private int contactDamage = 1;
        [SerializeField] private float flashInterval = 0.08f;

        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private Vector3 targetPosition;
        private int currentHp;
        private int maxHp;
        private int currentShield;
        private int currentStars;
        private int currentCoins;
        private float invincibleUntil;
        private bool isDead;
        private Color originalColor = Color.white;

        public int CurrentHp => currentHp;
        public int MaxHp => maxHp;
        public int CurrentShield => currentShield;
        public int CurrentStars => currentStars;
        public int CurrentCoins => currentCoins;
        public bool IsInvincible => Time.time < invincibleUntil;
        public float MoveSpeed => GetMoveSpeed();
        public float HitboxRadius => GetHitboxRadius();
        public float PickupAttractRange => GetPickupAttractRange();
        public float PickupAttractSpeed => GetPickupAttractSpeed();

        public void ApplyConfig(PlayerConfig playerConfig)
        {
            config = playerConfig;
            maxHp = Mathf.Max(1, config != null ? config.hp : currentHp);
            currentHp = maxHp;
            currentShield = Mathf.Max(0, config != null ? config.shield : currentShield);
            currentStars = Mathf.Max(0, config != null ? config.stars : currentStars);
            currentCoins = Mathf.Max(0, config != null ? config.coins : currentCoins);
            if (playerShooter != null)
            {
                playerShooter.ApplyConfig(config);
            }

            ApplyAircraftConfig();
        }

        public bool TakeDamage(int damage)
        {
            if (damage <= 0 || IsInvincible || currentHp <= 0 || isDead)
            {
                return false;
            }

            var remainingDamage = damage;
            if (currentShield > 0)
            {
                var absorbed = Mathf.Min(currentShield, remainingDamage);
                currentShield -= absorbed;
                remainingDamage -= absorbed;
            }

            if (remainingDamage > 0)
            {
                currentHp = Mathf.Max(0, currentHp - remainingDamage);
            }

            BeginInvincible();

            if (currentHp <= 0)
            {
                Die();
            }

            return true;
        }

        public void AddStars(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            currentStars += amount;
        }

        public void AddCoins(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            currentCoins += amount;
        }

        public void Heal(int amount)
        {
            if (amount <= 0 || isDead)
            {
                return;
            }

            currentHp = Mathf.Min(GetMaxHp(), currentHp + amount);
        }

        public void ActivateInvincibleShield(float duration)
        {
            if (duration <= 0f || isDead)
            {
                return;
            }

            invincibleUntil = Mathf.Max(invincibleUntil, Time.time + duration);
        }

        public void BeginInvincible()
        {
            invincibleUntil = Mathf.Max(invincibleUntil, Time.time + GetInvincibleTime());
        }

        public void HandleHitboxTrigger(Collider2D other)
        {
            if (CanTakeContactDamageFrom(other))
            {
                TakeDamage(contactDamage);
            }
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            gameplayCamera = gameplayCamera != null ? gameplayCamera : Camera.main;

            EnsureVisual();
            EnsureHitbox();
            EnsureShooter();
            ConfigurePhysics();
            EnsureFallbackSprite();
            ApplyAircraftConfig();

            originalColor = spriteRenderer.color;
            targetPosition = transform.position;

            if (damageSourceLayers.value == 0)
            {
                damageSourceLayers = LayerMask.GetMask("Enemy", "EnemyBullet");
            }
        }

        private void Start()
        {
            if (!HasRuntimeConfig() && ConfigManager.Instance != null && ConfigManager.Instance.IsLoaded)
            {
                ApplyConfig(ConfigManager.Instance.Config.player);
            }

            if (currentHp <= 0)
            {
                maxHp = Mathf.Max(1, config != null ? config.hp : 1);
                currentHp = maxHp;
            }

            if (config == null && currentShield <= 0)
            {
                currentShield = Mathf.Max(0, fallbackShield);
            }
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            {
                UpdateInvincibleVisual();
                return;
            }

            if (TryGetPointerWorldPosition(out var pointerWorldPosition))
            {
                targetPosition = ClampToCameraBounds(pointerWorldPosition);
            }

            SetPosition(targetPosition);
            UpdateInvincibleVisual();
        }

        private void OnDisable()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            HandleHitboxTrigger(other);
        }

        private void Die()
        {
            if (isDead)
            {
                return;
            }

            isDead = true;
            currentHp = 0;
            currentShield = 0;
            ExplosionEffect.Spawn(transform.position, 1.2f);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayPlayerDestroyed();
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.LoseGame();
            }

            gameObject.SetActive(false);
        }

        private bool TryGetPointerWorldPosition(out Vector3 worldPosition)
        {
            var screenPosition = Vector3.zero;
            var hasPointer = false;

            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                hasPointer = touch.phase != TouchPhase.Canceled
                    && touch.phase != TouchPhase.Ended
                    && !IsPointerOverUi(touch.fingerId);
                screenPosition = touch.position;
            }
            else
            {
                hasPointer = Input.mousePresent && Input.GetMouseButton(0) && !IsPointerOverUi();
                screenPosition = Input.mousePosition;
            }

            if (!hasPointer || gameplayCamera == null)
            {
                worldPosition = targetPosition;
                return false;
            }

            screenPosition.z = Mathf.Abs(gameplayCamera.transform.position.z - transform.position.z);
            worldPosition = gameplayCamera.ScreenToWorldPoint(screenPosition);
            worldPosition.z = transform.position.z;
            return true;
        }

        private bool IsPointerOverUi(int pointerId = -1)
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            return pointerId >= 0 ? EventSystem.current.IsPointerOverGameObject(pointerId) : EventSystem.current.IsPointerOverGameObject();
        }

        private void SetPosition(Vector3 position)
        {
            if (body != null)
            {
                body.position = new Vector2(position.x, position.y);
            }
            else
            {
                transform.position = position;
            }
        }

        private Vector3 ClampToCameraBounds(Vector3 worldPosition)
        {
            if (gameplayCamera == null)
            {
                return worldPosition;
            }

            var distance = Mathf.Abs(gameplayCamera.transform.position.z - transform.position.z);
            var min = gameplayCamera.ViewportToWorldPoint(new Vector3(0f, 0f, distance));
            var max = gameplayCamera.ViewportToWorldPoint(new Vector3(1f, 1f, distance));
            var radius = GetHitboxRadius() * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);

            worldPosition.x = Mathf.Clamp(worldPosition.x, min.x + radius, max.x - radius);
            worldPosition.y = Mathf.Clamp(worldPosition.y, min.y + radius, max.y - radius);
            worldPosition.z = transform.position.z;

            return worldPosition;
        }

        private bool CanTakeContactDamageFrom(Collider2D other)
        {
            if (other == null || other.attachedRigidbody == body || contactDamage <= 0)
            {
                return false;
            }

            return (damageSourceLayers.value & (1 << other.gameObject.layer)) != 0;
        }

        private void ConfigurePhysics()
        {
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        private void EnsureFallbackSprite()
        {
            if (spriteRenderer != null && spriteRenderer.sprite == null)
            {
                spriteRenderer.sprite = CreateFallbackPlayerSprite();
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.sortingOrder = 10;
            }
        }

        private Sprite CreateFallbackPlayerSprite()
        {
            const int size = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;

            var clear = new Color(0f, 0f, 0f, 0f);
            var bodyColor = new Color(0.28f, 0.82f, 1f, 1f);
            var wingColor = new Color(0.1f, 0.42f, 0.9f, 1f);
            var cockpitColor = new Color(1f, 1f, 1f, 1f);

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    texture.SetPixel(x, y, clear);
                }
            }

            for (var y = 1; y < size - 1; y++)
            {
                var halfWidth = y < 8 ? y / 2 : (15 - y) / 2 + 2;
                for (var x = 7 - halfWidth; x <= 8 + halfWidth; x++)
                {
                    texture.SetPixel(x, y, bodyColor);
                }
            }

            for (var y = 3; y <= 7; y++)
            {
                texture.SetPixel(3, y, wingColor);
                texture.SetPixel(4, y, wingColor);
                texture.SetPixel(11, y, wingColor);
                texture.SetPixel(12, y, wingColor);
            }

            texture.SetPixel(7, 10, cockpitColor);
            texture.SetPixel(8, 10, cockpitColor);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private void UpdateInvincibleVisual()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            if (!IsInvincible)
            {
                spriteRenderer.color = originalColor;
                return;
            }

            var interval = Mathf.Max(0.01f, flashInterval);
            var visible = Mathf.FloorToInt(Time.time / interval) % 2 == 0;
            spriteRenderer.color = visible ? originalColor : new Color(originalColor.r, originalColor.g, originalColor.b, 0.35f);
        }

        private bool HasRuntimeConfig()
        {
            return config != null && !string.IsNullOrEmpty(config.id);
        }

        private float GetMoveSpeed()
        {
            return Mathf.Max(0f, config != null && config.moveSpeed > 0f ? config.moveSpeed : fallbackMoveSpeed);
        }

        private int GetMaxHp()
        {
            if (maxHp <= 0)
            {
                maxHp = Mathf.Max(1, config != null ? config.hp : currentHp);
            }

            return maxHp;
        }

        private float GetInvincibleTime()
        {
            return Mathf.Max(0f, config != null && config.invincibleTime > 0f ? config.invincibleTime : fallbackInvincibleTime);
        }

        private float GetPickupAttractRange()
        {
            return Mathf.Max(0f, config != null && config.pickupAttractRange > 0f ? config.pickupAttractRange : fallbackPickupAttractRange);
        }

        private float GetPickupAttractSpeed()
        {
            return Mathf.Max(0.01f, config != null && config.pickupAttractSpeed > 0f ? config.pickupAttractSpeed : fallbackPickupAttractSpeed);
        }

        private void EnsureVisual()
        {
            if (visualRoot == null)
            {
                var visual = transform.Find("Visual");

                if (visual == null)
                {
                    var visualObject = new GameObject("Visual");
                    visual = visualObject.transform;
                    visual.SetParent(transform);
                    visual.localPosition = Vector3.zero;
                    visual.localRotation = Quaternion.identity;
                    visual.localScale = Vector3.one;
                }

                visualRoot = visual;
            }

            spriteRenderer = visualRoot.GetComponent<SpriteRenderer>();

            if (spriteRenderer == null)
            {
                spriteRenderer = visualRoot.gameObject.AddComponent<SpriteRenderer>();
            }
        }

        private void EnsureHitbox()
        {
            if (playerHitbox == null)
            {
                playerHitbox = GetComponentInChildren<PlayerHitbox>();
            }

            if (playerHitbox == null)
            {
                var hitbox = transform.Find("Hitbox");

                if (hitbox == null)
                {
                    var hitboxObject = new GameObject("Hitbox");
                    hitbox = hitboxObject.transform;
                    hitbox.SetParent(transform);
                    hitbox.localRotation = Quaternion.identity;
                    hitbox.localScale = Vector3.one;
                }

                playerHitbox = hitbox.GetComponent<PlayerHitbox>();

                if (playerHitbox == null)
                {
                    playerHitbox = hitbox.gameObject.AddComponent<PlayerHitbox>();
                }
            }
        }

        private void ApplyAircraftConfig()
        {
            if (visualRoot != null)
            {
                var scale = config != null && config.visualScale > 0f ? config.visualScale : 1f;
                visualRoot.localScale = new Vector3(scale, scale, 1f);
            }

            if (config != null)
            {
                hitboxRadius = config.hitboxRadius > 0f ? config.hitboxRadius : hitboxRadius;
                hitboxOffset = config.hitboxOffset;
            }

            if (playerHitbox != null)
            {
                playerHitbox.Configure(this, GetHitboxRadius(), hitboxOffset);
            }

            if (playerShooter != null)
            {
                playerShooter.ApplyConfig(config);
            }
        }

        private float GetHitboxRadius()
        {
            return Mathf.Max(0.01f, config != null && config.hitboxRadius > 0f ? config.hitboxRadius : hitboxRadius);
        }

        private void EnsureShooter()
        {
            if (playerShooter == null)
            {
                playerShooter = GetComponent<PlayerShooter>();
            }

            if (playerShooter == null)
            {
                playerShooter = gameObject.AddComponent<PlayerShooter>();
            }
        }
    }
}
