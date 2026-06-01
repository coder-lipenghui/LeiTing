using LeiTing.Audio;
using LeiTing.Config;
using LeiTing.Core;
using LeiTing.Player;
using LeiTing.Progress;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LeiTing.Pickups
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class PickupItemController : MonoBehaviour
    {
        private const float DespawnViewportMargin = 0.08f;
        private const float GlowSpriteSize = 1f;
        private const float DefaultGlowRange = 0.28f;
        private const float MagnetGlowRange = 0.34f;
        private const float TreasureGlowRange = 0.3f;
        private const float SpecialGlowRange = 0.36f;
        private const float GlowPulseScale = 0.1f;
        private const float GlowPulseAlpha = 0.18f;
        private const float TreasureScaleVariance = 0.08f;
        private const float SpecialWobblePeriod = 1.85f;
        private const float SpecialWobbleScale = 0.13f;
        private const float SpecialWobbleYOffset = 0.03f;
        private const string CoinPickupSoundPath = "Assets/Art/Sound/SFX/Item/coin.wav";
        private const string StarPickupSoundPath = "Assets/Art/Sound/SFX/Item/star.wav";
        private const string SpecialPickupSoundPath = "Assets/Art/Sound/SFX/Item/SFX_Item_Pickup_Special_01.wav";

        private static readonly Color MagnetGlowColor = new Color(0.12f, 0.58f, 1f, 0.54f);
        private static readonly Color TreasureGlowColor = new Color(1f, 0.72f, 0.08f, 0.58f);
        private static readonly Color SpecialGlowColor = new Color(0.42f, 0.96f, 1f, 0.56f);
        private static readonly Color DefaultGlowColor = new Color(1f, 1f, 1f, 0.42f);
        private static Sprite glowSprite;
        private static Material defaultSpriteMaterial;

        [SerializeField] private PickupItemConfig config;

        private Rigidbody2D body;
        private CircleCollider2D pickupCollider;
        private SpriteRenderer spriteRenderer;
        private Transform glowRoot;
        private SpriteRenderer glowRenderer;
        private Transform leftVisualRoot;
        private Transform rightVisualRoot;
        private SpriteRenderer leftVisualRenderer;
        private SpriteRenderer rightVisualRenderer;
        private Camera gameplayCamera;
        private PlayerController forcedAttractTarget;
        private Color glowBaseColor;
        private Vector3 glowBaseScale = Vector3.one;
        private float spawnTime;
        private bool isCollected;
        private bool glowConfigured;
        private bool specialSplitVisual;

        public void Initialize(PickupItemConfig pickupConfig)
        {
            EnsureComponents();

            config = pickupConfig;
            spawnTime = Time.time;
            isCollected = false;
            forcedAttractTarget = null;
            gameObject.name = config != null && !string.IsNullOrEmpty(config.id) ? config.id : "Pickup";
            ApplyVisual();
            ApplyCollider();
        }

        public bool IsStarPickup => IsItemType("Star") || IsItemId("star");
        private bool IsCoinPickup => IsItemType("Coin") || IsItemId("coin");
        private bool IsSpecialPickup => !IsStarPickup && !IsCoinPickup;
        public bool IsStarOrCoinPickup => IsStarPickup || IsCoinPickup;
        public bool IsCollected => isCollected;

        public void BeginForcedAttract(PlayerController player)
        {
            if (isCollected || player == null)
            {
                return;
            }

            forcedAttractTarget = player;
        }

        private void Awake()
        {
            EnsureComponents();
        }

        private void Update()
        {
            if (isCollected)
            {
                return;
            }

            UpdatePickupVisuals();

            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            {
                return;
            }

            var player = FindObjectOfType<PlayerController>();
            if (forcedAttractTarget != null)
            {
                if (TryAttractToPlayer(forcedAttractTarget, true))
                {
                    return;
                }
            }

            if (player != null && TryAttractToPlayer(player))
            {
                return;
            }

            DriftDown();
            CheckLifetime();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isCollected || other == null)
            {
                return;
            }

            var player = other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                Collect(player);
            }
        }

        private void EnsureComponents()
        {
            body = body != null ? body : GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;

            pickupCollider = pickupCollider != null ? pickupCollider : GetComponent<CircleCollider2D>();
            pickupCollider.isTrigger = true;

            gameplayCamera = gameplayCamera != null ? gameplayCamera : Camera.main;

            spriteRenderer = spriteRenderer != null ? spriteRenderer : GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            if (glowRoot == null)
            {
                var glow = transform.Find("Glow");
                if (glow == null)
                {
                    glow = new GameObject("Glow").transform;
                    glow.SetParent(transform, false);
                    glow.localPosition = Vector3.zero;
                    glow.localRotation = Quaternion.identity;
                }

                glowRoot = glow;
            }

            glowRenderer = glowRenderer != null ? glowRenderer : glowRoot.GetComponent<SpriteRenderer>();
            if (glowRenderer == null)
            {
                glowRenderer = glowRoot.gameObject.AddComponent<SpriteRenderer>();
            }

            EnsureSplitVisualRenderers();
        }

        private bool TryAttractToPlayer(PlayerController player, bool forceAttract = false)
        {
            var toPlayer = player.transform.position - transform.position;
            var distance = toPlayer.magnitude;

            if (distance <= GetCollectDistance(player))
            {
                Collect(player);
                return true;
            }

            if (!forceAttract && distance > player.PickupAttractRange)
            {
                return false;
            }

            var speed = Mathf.Max(0.01f, player.PickupAttractSpeed);
            var nextPosition = Vector3.MoveTowards(transform.position, player.transform.position, speed * Time.deltaTime);
            MoveTo(nextPosition);
            return true;
        }

        private void DriftDown()
        {
            var speed = Mathf.Max(0f, config != null && config.driftSpeed > 0f ? config.driftSpeed : 1.1f);
            MoveTo(transform.position + Vector3.down * speed * Time.deltaTime);

            if (IsBelowScreen())
            {
                Destroy(gameObject);
            }
        }

        private void MoveTo(Vector3 position)
        {
            if (body != null)
            {
                body.position = position;
            }

            transform.position = position;
        }

        private void CheckLifetime()
        {
            var lifetime = config != null && config.lifetime > 0f ? config.lifetime : 12f;
            if (Time.time - spawnTime >= lifetime)
            {
                Destroy(gameObject);
            }
        }

        private bool IsBelowScreen()
        {
            gameplayCamera = gameplayCamera != null ? gameplayCamera : Camera.main;
            if (gameplayCamera == null)
            {
                return transform.position.y < -6.4f;
            }

            var viewportPosition = gameplayCamera.WorldToViewportPoint(transform.position);
            return viewportPosition.y < -DespawnViewportMargin;
        }

        private void Collect(PlayerController player)
        {
            if (isCollected || player == null)
            {
                return;
            }

            isCollected = true;

            if (IsStarPickup)
            {
                var starValue = GetStarValue();
                player.AddStars(starValue);
                LevelProgressService.RecordStarsCollected(starValue);
                PlayPickupSound(StarPickupSoundPath);
            }
            else if (IsCoinPickup)
            {
                player.AddCoins(GetCoinValue());
                PlayPickupSound(CoinPickupSoundPath);
            }
            else
            {
                if (IsItemType("Magnet") || IsItemId("magnet"))
                {
                    PickupManager.GetOrCreate().AttractAllStarsToPlayer(player);
                }
                else if (IsItemType("Bomb") || IsItemId("boom"))
                {
                    PickupManager.GetOrCreate().KillAllMinions();
                }
                else if (IsItemType("Heal") || IsItemId("hp"))
                {
                    player.Heal(GetHealValue());
                }
                else if (IsItemType("Shield") || IsItemId("shield"))
                {
                    player.ActivateInvincibleShield(GetShieldDuration());
                }

                PlayPickupSound(SpecialPickupSoundPath);
            }

            Destroy(gameObject);
        }

        private static void PlayPickupSound(string soundPath)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySfx(soundPath);
            }
        }

        private bool IsItemType(string itemType)
        {
            return config != null && string.Equals(config.itemType, itemType, System.StringComparison.OrdinalIgnoreCase);
        }

        private bool IsItemId(string itemId)
        {
            return config != null && string.Equals(config.id, itemId, System.StringComparison.OrdinalIgnoreCase);
        }

        private int GetStarValue()
        {
            return Mathf.Max(1, config != null ? config.starValue : 1);
        }

        private int GetCoinValue()
        {
            return Mathf.Max(1, config != null ? config.coinValue : 1);
        }

        private int GetHealValue()
        {
            return Mathf.Max(1, config != null ? config.healValue : 1);
        }

        private float GetShieldDuration()
        {
            return Mathf.Max(0.1f, config != null ? config.shieldDuration : 5f);
        }

        private float GetCollectDistance(PlayerController player)
        {
            var pickupRadius = config != null && config.pickupRadius > 0f ? config.pickupRadius : 0.22f;
            return Mathf.Max(0.05f, pickupRadius + player.HitboxRadius);
        }

        private void ApplyCollider()
        {
            if (pickupCollider == null)
            {
                return;
            }

            pickupCollider.radius = config != null && config.pickupRadius > 0f ? config.pickupRadius : 0.22f;
            pickupCollider.offset = Vector2.zero;
        }

        private void ApplyVisual()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            spriteRenderer.sprite = LoadPickupSprite() ?? CreateFallbackStarSprite();
            spriteRenderer.sortingOrder = 18;
            spriteRenderer.color = Color.white;

            var scale = config != null && config.visualScale > 0f ? config.visualScale : 0.62f;
            if (ShouldRandomizeTreasureScale())
            {
                scale *= Random.Range(1f - TreasureScaleVariance, 1f + TreasureScaleVariance);
            }

            transform.localScale = new Vector3(scale, scale, 1f);
            ConfigureSplitVisual(spriteRenderer.sprite);
            ApplyGlowVisual();
        }

        private bool ShouldRandomizeTreasureScale()
        {
            return IsStarPickup || IsCoinPickup;
        }

        private void ApplyGlowVisual()
        {
            if (glowRenderer == null)
            {
                return;
            }

            if (IsItemType("Magnet") || IsItemId("magnet"))
            {
                ConfigureGlow(MagnetGlowColor, MagnetGlowRange);
                return;
            }

            if (IsStarPickup || IsCoinPickup)
            {
                ConfigureGlow(TreasureGlowColor, TreasureGlowRange);
                return;
            }

            if (IsSpecialPickup)
            {
                ConfigureGlow(SpecialGlowColor, SpecialGlowRange);
                return;
            }

            ConfigureGlow(DefaultGlowColor, DefaultGlowRange);
        }

        private void ConfigureGlow(Color color, float glowRange)
        {
            if (spriteRenderer == null || glowRenderer == null)
            {
                return;
            }

            var spriteSize = spriteRenderer.sprite != null ? spriteRenderer.sprite.bounds.size : Vector3.one * 0.35f;
            var glowSize = Mathf.Max(spriteSize.x, spriteSize.y) + glowRange;

            glowRenderer.enabled = true;
            glowRenderer.sprite = GetGlowSprite();
            glowRenderer.color = color;
            glowRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
            glowRenderer.sharedMaterial = GetDefaultSpriteMaterial();
            glowRoot.localPosition = Vector3.zero;
            glowRoot.localRotation = Quaternion.identity;
            glowBaseColor = color;
            glowBaseScale = new Vector3(glowSize / GlowSpriteSize, glowSize / GlowSpriteSize, 1f);
            glowRoot.localScale = glowBaseScale;
            glowConfigured = true;
        }

        private void EnsureSplitVisualRenderers()
        {
            if (leftVisualRoot == null)
            {
                leftVisualRoot = EnsureChildTransform("VisualLeft");
            }

            if (rightVisualRoot == null)
            {
                rightVisualRoot = EnsureChildTransform("VisualRight");
            }

            leftVisualRenderer = leftVisualRenderer != null ? leftVisualRenderer : leftVisualRoot.GetComponent<SpriteRenderer>();
            if (leftVisualRenderer == null)
            {
                leftVisualRenderer = leftVisualRoot.gameObject.AddComponent<SpriteRenderer>();
            }

            rightVisualRenderer = rightVisualRenderer != null ? rightVisualRenderer : rightVisualRoot.GetComponent<SpriteRenderer>();
            if (rightVisualRenderer == null)
            {
                rightVisualRenderer = rightVisualRoot.gameObject.AddComponent<SpriteRenderer>();
            }

            leftVisualRenderer.enabled = false;
            rightVisualRenderer.enabled = false;
        }

        private Transform EnsureChildTransform(string childName)
        {
            var child = transform.Find(childName);
            if (child == null)
            {
                child = new GameObject(childName).transform;
                child.SetParent(transform, false);
            }

            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            return child;
        }

        private void ConfigureSplitVisual(Sprite sprite)
        {
            EnsureSplitVisualRenderers();

            specialSplitVisual = IsSpecialPickup && sprite != null;
            spriteRenderer.enabled = !specialSplitVisual;
            leftVisualRenderer.enabled = specialSplitVisual;
            rightVisualRenderer.enabled = specialSplitVisual;

            if (!specialSplitVisual)
            {
                return;
            }

            leftVisualRenderer.sprite = CreateHalfSprite(sprite, true);
            rightVisualRenderer.sprite = CreateHalfSprite(sprite, false);
            leftVisualRenderer.sortingOrder = spriteRenderer.sortingOrder;
            rightVisualRenderer.sortingOrder = spriteRenderer.sortingOrder;
            leftVisualRenderer.color = spriteRenderer.color;
            rightVisualRenderer.color = spriteRenderer.color;
            leftVisualRenderer.sharedMaterial = spriteRenderer.sharedMaterial != null
                ? spriteRenderer.sharedMaterial
                : GetDefaultSpriteMaterial();
            rightVisualRenderer.sharedMaterial = leftVisualRenderer.sharedMaterial;
        }

        private static Sprite CreateHalfSprite(Sprite source, bool leftHalf)
        {
            var rect = source.rect;
            var halfWidth = Mathf.Max(1f, rect.width * 0.5f);
            var halfRect = leftHalf
                ? new Rect(rect.x, rect.y, halfWidth, rect.height)
                : new Rect(rect.x + halfWidth, rect.y, rect.width - halfWidth, rect.height);
            var pivot = leftHalf ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
            return Sprite.Create(source.texture, halfRect, pivot, source.pixelsPerUnit);
        }

        private void UpdatePickupVisuals()
        {
            UpdateGlowPulse();
            UpdateSpecialWobble();
        }

        private void UpdateGlowPulse()
        {
            if (!glowConfigured || glowRenderer == null || glowRoot == null || !glowRenderer.enabled)
            {
                return;
            }

            var wave = (Mathf.Sin((Time.time - spawnTime) * Mathf.PI * 2f / 1.55f) + 1f) * 0.5f;
            glowRoot.localScale = glowBaseScale * (1f + GlowPulseScale * wave);
            glowRenderer.color = new Color(
                glowBaseColor.r,
                glowBaseColor.g,
                glowBaseColor.b,
                Mathf.Clamp01(glowBaseColor.a + GlowPulseAlpha * wave));
        }

        private void UpdateSpecialWobble()
        {
            if (!specialSplitVisual || leftVisualRoot == null || rightVisualRoot == null)
            {
                return;
            }

            var wave = Mathf.Sin((Time.time - spawnTime) * Mathf.PI * 2f / SpecialWobblePeriod);
            var leftScale = 1f + wave * SpecialWobbleScale;
            var rightScale = 1f - wave * SpecialWobbleScale;
            leftVisualRoot.localScale = new Vector3(leftScale, 1f + wave * SpecialWobbleYOffset, 1f);
            rightVisualRoot.localScale = new Vector3(rightScale, 1f - wave * SpecialWobbleYOffset, 1f);
        }

        private Sprite LoadPickupSprite()
        {
            if (config == null || string.IsNullOrEmpty(config.spritePath))
            {
                return null;
            }

#if UNITY_EDITOR
            if (config.spritePath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
            {
                var editorSprite = AssetDatabase.LoadAssetAtPath<Sprite>(config.spritePath);
                if (editorSprite != null)
                {
                    return editorSprite;
                }
            }
#endif

            return RuntimeAssetCatalog.LoadSprite(config.spritePath)
                ?? Resources.Load<Sprite>(NormalizeResourcesPath(config.spritePath));
        }

        private static string NormalizeResourcesPath(string assetPath)
        {
            const string resourcesSegment = "/Resources/";
            var normalized = assetPath.Replace("\\", "/");
            var resourcesIndex = normalized.IndexOf(resourcesSegment, System.StringComparison.OrdinalIgnoreCase);

            if (resourcesIndex >= 0)
            {
                normalized = normalized.Substring(resourcesIndex + resourcesSegment.Length);
            }

            var extensionIndex = normalized.LastIndexOf(".", System.StringComparison.Ordinal);
            if (extensionIndex >= 0)
            {
                normalized = normalized.Substring(0, extensionIndex);
            }

            return normalized;
        }

        private static Sprite CreateFallbackStarSprite()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;

            var clear = new Color(0f, 0f, 0f, 0f);
            var fill = new Color(1f, 0.86f, 0.18f, 1f);
            var shine = new Color(1f, 1f, 0.72f, 1f);
            var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    texture.SetPixel(x, y, clear);
                }
            }

            var points = new Vector2[10];
            for (var index = 0; index < points.Length; index++)
            {
                var radius = index % 2 == 0 ? 14f : 6.2f;
                var angle = Mathf.Deg2Rad * (-90f + index * 36f);
                points[index] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    if (IsPointInPolygon(new Vector2(x, y), points))
                    {
                        texture.SetPixel(x, y, y > center.y ? shine : fill);
                    }
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite GetGlowSprite()
        {
            if (glowSprite == null)
            {
                glowSprite = CreateGlowSprite();
            }

            return glowSprite;
        }

        private static Material GetDefaultSpriteMaterial()
        {
            if (defaultSpriteMaterial == null)
            {
                defaultSpriteMaterial = SpriteMaterialUtility.CreateSpriteMaterial("Pickup Sprite Material");
            }

            return defaultSpriteMaterial;
        }

        private static Sprite CreateGlowSprite()
        {
            const int size = 96;
            const float radius = size * 0.5f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x + 0.5f - radius;
                    var dy = y + 0.5f - radius;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy) / radius;
                    var alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2.35f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
        {
            var inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                var crosses = (polygon[i].y > point.y) != (polygon[j].y > point.y);
                if (crosses)
                {
                    var x = (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x;
                    if (point.x < x)
                    {
                        inside = !inside;
                    }
                }
            }

            return inside;
        }
    }
}
