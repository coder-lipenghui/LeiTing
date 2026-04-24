using LeiTing.Config;
using LeiTing.Core;
using UnityEngine;

namespace LeiTing.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerController : MonoBehaviour
    {
        private const float SnapDistance = 0.02f;

        [SerializeField] private PlayerConfig config;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private LayerMask damageSourceLayers;
        [SerializeField] private float fallbackMoveSpeed = 6f;
        [SerializeField] private float fallbackInvincibleTime = 1.5f;
        [SerializeField] private float hitboxRadius = 0.18f;
        [SerializeField] private int contactDamage = 1;
        [SerializeField] private float flashInterval = 0.08f;

        private Rigidbody2D body;
        private CircleCollider2D hitbox;
        private SpriteRenderer spriteRenderer;
        private Vector3 targetPosition;
        private int currentHp;
        private float invincibleUntil;
        private Color originalColor = Color.white;

        public int CurrentHp => currentHp;
        public bool IsInvincible => Time.time < invincibleUntil;

        public void ApplyConfig(PlayerConfig playerConfig)
        {
            config = playerConfig;
            currentHp = Mathf.Max(1, config != null ? config.hp : currentHp);
        }

        public bool TakeDamage(int damage)
        {
            if (damage <= 0 || IsInvincible || currentHp <= 0)
            {
                return false;
            }

            currentHp = Mathf.Max(0, currentHp - damage);
            BeginInvincible();

            if (currentHp <= 0 && GameManager.Instance != null)
            {
                GameManager.Instance.LoseGame();
            }

            return true;
        }

        public void BeginInvincible()
        {
            invincibleUntil = Time.time + GetInvincibleTime();
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            hitbox = GetComponent<CircleCollider2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            gameplayCamera = gameplayCamera != null ? gameplayCamera : Camera.main;

            ConfigurePhysics();
            EnsureFallbackSprite();

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
                currentHp = Mathf.Max(1, config != null ? config.hp : 1);
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

            MoveTowardsTarget();
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
            if (CanTakeContactDamageFrom(other))
            {
                TakeDamage(contactDamage);
            }
        }

        private bool TryGetPointerWorldPosition(out Vector3 worldPosition)
        {
            var screenPosition = Vector3.zero;
            var hasPointer = false;

            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                hasPointer = touch.phase != TouchPhase.Canceled && touch.phase != TouchPhase.Ended;
                screenPosition = touch.position;
            }
            else
            {
                hasPointer = Input.mousePresent;
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

        private void MoveTowardsTarget()
        {
            var current = transform.position;
            var delta = targetPosition - current;

            if (delta.sqrMagnitude <= SnapDistance * SnapDistance)
            {
                SetPosition(targetPosition);
                return;
            }

            var next = Vector3.MoveTowards(current, targetPosition, GetMoveSpeed() * Time.deltaTime);
            SetPosition(next);
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
            var radius = Mathf.Max(0f, hitboxRadius) * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);

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

            hitbox.isTrigger = true;
            hitbox.radius = Mathf.Max(0.01f, hitboxRadius);
        }

        private void EnsureFallbackSprite()
        {
            if (spriteRenderer.sprite == null)
            {
                spriteRenderer.sprite = CreateFallbackPlayerSprite();
            }

            spriteRenderer.sortingOrder = 10;
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

        private float GetInvincibleTime()
        {
            return Mathf.Max(0f, config != null && config.invincibleTime > 0f ? config.invincibleTime : fallbackInvincibleTime);
        }
    }
}
