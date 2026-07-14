using System.Collections.Generic;
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
        private const float TrophyGlowRange = 0.58f;
        private const float TrophyOuterGlowRange = 0.95f;
        private const float GlowPulseScale = 0.1f;
        private const float GlowPulseAlpha = 0.18f;
        private const float TrophyOuterGlowPulseScale = 0.18f;
        private const float TrophyOuterGlowPulseAlpha = 0.16f;
        private const float TreasureScaleVariance = 0.08f;
        private const float SpecialWobblePeriod = 1.85f;
        private const float SpecialWobbleScale = 0.13f;
        private const float SpecialWobbleYOffset = 0.03f;
        private const float TrophyHoldDuration = 2f;
        private const float TrophyProgressPadding = 0.18f;
        private const float TrophyProgressLineWidth = 0.035f;
        private const float TrophyConnectionLineWidth = 0.025f;
        private const int TrophyProgressSegments = 64;
        private const string CoinPickupSoundPath = "Assets/Art/Sound/SFX/Item/coin.wav";
        private const string StarPickupSoundPath = "Assets/Art/Sound/SFX/Item/star.wav";
        private const string SpecialPickupSoundPath = "Assets/Art/Sound/SFX/Item/SFX_Item_Pickup_Special_01.wav";

        private static readonly Color MagnetGlowColor = new Color(0.12f, 0.58f, 1f, 0.54f);
        private static readonly Color TreasureGlowColor = new Color(1f, 0.72f, 0.08f, 0.58f);
        private static readonly Color SpecialGlowColor = new Color(0.42f, 0.96f, 1f, 0.56f);
        private static readonly Color TrophyGlowColor = new Color(1f, 0.86f, 0.12f, 0.82f);
        private static readonly Color TrophyOuterGlowColor = new Color(1f, 0.58f, 0.04f, 0.42f);
        private static readonly Color TrophyProgressBackColor = new Color(1f, 1f, 1f, 0.22f);
        private static readonly Color TrophyProgressColor = new Color(1f, 0.83f, 0.18f, 0.95f);
        private static readonly Color TrophyConnectionColor = new Color(1f, 0.83f, 0.18f, 0.62f);
        private static readonly Color DefaultGlowColor = new Color(1f, 1f, 1f, 0.42f);
        private static Sprite glowSprite;
        private static Sprite fallbackStarSprite;
        private static Material defaultSpriteMaterial;
        private static PlayerController cachedPlayer;
        private static readonly Dictionary<string, Sprite> configuredSprites = new Dictionary<string, Sprite>();

        [SerializeField] private PickupItemConfig config;

        private PickupManager manager;
        private Rigidbody2D body;
        private CircleCollider2D pickupCollider;
        private SpriteRenderer spriteRenderer;
        private Transform glowRoot;
        private SpriteRenderer glowRenderer;
        private Transform leftVisualRoot;
        private Transform rightVisualRoot;
        private Transform trophyProgressRoot;
        private Transform trophyOuterGlowRoot;
        private SpriteRenderer leftVisualRenderer;
        private SpriteRenderer rightVisualRenderer;
        private SpriteRenderer trophyOuterGlowRenderer;
        private LineRenderer trophyProgressBackRenderer;
        private LineRenderer trophyProgressRenderer;
        private LineRenderer trophyConnectionRenderer;
        private Camera gameplayCamera;
        private PlayerController forcedAttractTarget;
        private Color glowBaseColor;
        private Color trophyOuterGlowBaseColor;
        private Vector3 glowBaseScale = Vector3.one;
        private Vector3 trophyOuterGlowBaseScale = Vector3.one;
        private float spawnTime;
        private float trophyHoldElapsed;
        private bool isCollected;
        private bool glowConfigured;
        private bool trophyOuterGlowConfigured;
        private bool specialSplitVisual;

        public void Initialize(PickupItemConfig pickupConfig, PickupManager owningManager = null)
        {
            EnsureComponents();

            manager = owningManager;
            config = pickupConfig;
            spawnTime = Time.time;
            isCollected = false;
            forcedAttractTarget = null;
            trophyHoldElapsed = 0f;
            gameObject.name = config != null && !string.IsNullOrEmpty(config.id) ? config.id : "Pickup";
            ApplyVisual();
            ApplyCollider();
            HideTrophyHoldVisuals();
        }

        public bool IsStarPickup => IsItemType("Star") || IsItemId("star");
        private bool IsCoinPickup => IsItemType("Coin") || IsItemId("coin");
        private bool IsTrophyPickup => IsItemType("Trophy") || IsItemId("trophy");
        private bool IsSpecialPickup => !IsStarPickup && !IsCoinPickup;
        public bool IsStarOrCoinPickup => IsStarPickup || IsCoinPickup;
        public bool IsCollected => isCollected;

        public void BeginForcedAttract(PlayerController player)
        {
            if (isCollected || player == null || IsTrophyPickup)
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

            var player = ResolvePlayer();
            if (IsTrophyPickup)
            {
                DriftDown();
                if (isCollected)
                {
                    return;
                }

                UpdateTrophyHold(player);
                return;
            }

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
            if (!isCollected)
            {
                CheckLifetime();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isCollected || other == null)
            {
                return;
            }

            var player = other.GetComponentInParent<PlayerController>();
            if (player != null && !IsTrophyPickup)
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
            pickupCollider.enabled = true;

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
            var speed = ResolveDriftSpeed();
            MoveTo(transform.position + Vector3.down * speed * Time.deltaTime);

            if (IsBelowScreen())
            {
                RecycleOrDestroy();
            }
        }

        private float ResolveDriftSpeed()
        {
            if (IsTrophyPickup)
            {
                var levelConfig = ConfigManager.Instance != null && ConfigManager.Instance.IsLoaded && GameManager.Instance != null
                    ? ConfigManager.Instance.GetLevel(GameManager.Instance.CurrentLevelNumber)
                    : null;

                if (levelConfig != null && levelConfig.backgroundScrollSpeed > 0f)
                {
                    return levelConfig.backgroundScrollSpeed;
                }
            }

            return Mathf.Max(0f, config != null && config.driftSpeed > 0f ? config.driftSpeed : 1.1f);
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
                RecycleOrDestroy();
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
                else if (IsItemType("WeaponUp") || IsItemId("weaponup"))
                {
                    var shooter = player.GetComponent<PlayerShooter>();
                    if (shooter != null)
                    {
                        shooter.IncreaseAttackPower();
                    }
                }

                PlayPickupSound(SpecialPickupSoundPath);
            }

            RecycleOrDestroy();
        }

        public void DeactivateForPool()
        {
            isCollected = true;
            forcedAttractTarget = null;
            manager = null;
            glowConfigured = false;
            trophyOuterGlowConfigured = false;
            specialSplitVisual = false;
            HideTrophyHoldVisuals();
            SetSplitVisualsEnabled(false);

            if (pickupCollider != null)
            {
                pickupCollider.enabled = false;
            }

            if (body != null)
            {
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            gameObject.SetActive(false);
        }

        private void RecycleOrDestroy()
        {
            isCollected = true;
            if (manager != null)
            {
                manager.Recycle(this);
                return;
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

            DisableTrophyOuterGlow();

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

            if (IsTrophyPickup)
            {
                ConfigureTrophyGlow();
                return;
            }

            if (IsSpecialPickup)
            {
                ConfigureGlow(SpecialGlowColor, SpecialGlowRange);
                return;
            }

            ConfigureGlow(DefaultGlowColor, DefaultGlowRange);
        }

        private void ConfigureTrophyGlow()
        {
            ConfigureGlow(TrophyGlowColor, TrophyGlowRange);
            ConfigureTrophyOuterGlow();
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

        private void ConfigureTrophyOuterGlow()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            if (trophyOuterGlowRoot == null)
            {
                trophyOuterGlowRoot = EnsureChildTransform("TrophyOuterGlow");
            }

            trophyOuterGlowRenderer = trophyOuterGlowRenderer != null
                ? trophyOuterGlowRenderer
                : trophyOuterGlowRoot.GetComponent<SpriteRenderer>();
            if (trophyOuterGlowRenderer == null)
            {
                trophyOuterGlowRenderer = trophyOuterGlowRoot.gameObject.AddComponent<SpriteRenderer>();
            }

            var spriteSize = spriteRenderer.sprite != null ? spriteRenderer.sprite.bounds.size : Vector3.one * 0.35f;
            var glowSize = Mathf.Max(spriteSize.x, spriteSize.y) + TrophyOuterGlowRange;

            trophyOuterGlowRenderer.enabled = true;
            trophyOuterGlowRenderer.sprite = GetGlowSprite();
            trophyOuterGlowRenderer.color = TrophyOuterGlowColor;
            trophyOuterGlowRenderer.sortingOrder = spriteRenderer.sortingOrder - 2;
            trophyOuterGlowRenderer.sharedMaterial = GetDefaultSpriteMaterial();
            trophyOuterGlowRoot.localPosition = Vector3.zero;
            trophyOuterGlowRoot.localRotation = Quaternion.identity;
            trophyOuterGlowBaseColor = TrophyOuterGlowColor;
            trophyOuterGlowBaseScale = new Vector3(glowSize / GlowSpriteSize, glowSize / GlowSpriteSize, 1f);
            trophyOuterGlowRoot.localScale = trophyOuterGlowBaseScale;
            trophyOuterGlowConfigured = true;
        }

        private void DisableTrophyOuterGlow()
        {
            trophyOuterGlowConfigured = false;
            if (trophyOuterGlowRenderer != null)
            {
                trophyOuterGlowRenderer.enabled = false;
            }
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
            specialSplitVisual = IsSpecialPickup && !IsTrophyPickup && sprite != null;
            spriteRenderer.enabled = !specialSplitVisual;

            if (!specialSplitVisual)
            {
                SetSplitVisualsEnabled(false);
                return;
            }

            EnsureSplitVisualRenderers();
            SetSplitVisualsEnabled(true);
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

        private void SetSplitVisualsEnabled(bool visible)
        {
            if (leftVisualRenderer != null)
            {
                leftVisualRenderer.enabled = visible;
            }

            if (rightVisualRenderer != null)
            {
                rightVisualRenderer.enabled = visible;
            }
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

        private void UpdateTrophyHold(PlayerController player)
        {
            if (player == null)
            {
                SuspendTrophyHold();
                return;
            }

            var distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance > GetCollectDistance(player))
            {
                SuspendTrophyHold();
                return;
            }

            trophyHoldElapsed = Mathf.Min(TrophyHoldDuration, trophyHoldElapsed + Time.deltaTime);
            UpdateTrophyHoldVisuals(player, Mathf.Clamp01((TrophyHoldDuration - trophyHoldElapsed) / TrophyHoldDuration));

            if (trophyHoldElapsed >= TrophyHoldDuration)
            {
                Collect(player);
            }
        }

        private void SuspendTrophyHold()
        {
            HideTrophyHoldVisuals();
        }

        private void EnsureTrophyHoldVisuals()
        {
            if (trophyProgressRoot == null)
            {
                trophyProgressRoot = EnsureChildTransform("TrophyHoldProgress");
            }

            trophyProgressBackRenderer = trophyProgressBackRenderer != null
                ? trophyProgressBackRenderer
                : EnsureLineRenderer(trophyProgressRoot, "BackRing", TrophyProgressBackColor, TrophyProgressLineWidth, false, spriteRenderer.sortingOrder + 2);
            trophyProgressRenderer = trophyProgressRenderer != null
                ? trophyProgressRenderer
                : EnsureLineRenderer(trophyProgressRoot, "RemainingRing", TrophyProgressColor, TrophyProgressLineWidth, false, spriteRenderer.sortingOrder + 3);
            trophyConnectionRenderer = trophyConnectionRenderer != null
                ? trophyConnectionRenderer
                : EnsureLineRenderer(transform, "TrophyConnection", TrophyConnectionColor, TrophyConnectionLineWidth, true, spriteRenderer.sortingOrder + 1);
        }

        private LineRenderer EnsureLineRenderer(
            Transform parent,
            string childName,
            Color color,
            float width,
            bool useWorldSpace,
            int sortingOrder)
        {
            var lineRoot = parent.Find(childName);
            if (lineRoot == null)
            {
                lineRoot = new GameObject(childName).transform;
                lineRoot.SetParent(parent, false);
            }

            lineRoot.localPosition = Vector3.zero;
            lineRoot.localRotation = Quaternion.identity;
            lineRoot.localScale = Vector3.one;

            var lineRenderer = lineRoot.GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                lineRenderer = lineRoot.gameObject.AddComponent<LineRenderer>();
            }

            lineRenderer.sharedMaterial = GetDefaultSpriteMaterial();
            lineRenderer.useWorldSpace = useWorldSpace;
            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            lineRenderer.numCapVertices = 4;
            lineRenderer.numCornerVertices = 4;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.sortingOrder = sortingOrder;
            lineRenderer.enabled = false;
            return lineRenderer;
        }

        private void UpdateTrophyHoldVisuals(PlayerController player, float remainingFraction)
        {
            EnsureTrophyHoldVisuals();

            var radius = GetTrophyProgressRadius();
            UpdateCircularLine(trophyProgressBackRenderer, radius, 1f);
            UpdateCircularLine(trophyProgressRenderer, radius, remainingFraction);

            if (trophyConnectionRenderer != null && player != null)
            {
                trophyConnectionRenderer.enabled = true;
                trophyConnectionRenderer.positionCount = 2;
                trophyConnectionRenderer.SetPosition(0, transform.position);
                trophyConnectionRenderer.SetPosition(1, player.transform.position);
            }
        }

        private void HideTrophyHoldVisuals()
        {
            SetLineVisible(trophyProgressBackRenderer, false);
            SetLineVisible(trophyProgressRenderer, false);
            SetLineVisible(trophyConnectionRenderer, false);
        }

        private static void SetLineVisible(LineRenderer lineRenderer, bool visible)
        {
            if (lineRenderer != null)
            {
                lineRenderer.enabled = visible;
            }
        }

        private float GetTrophyProgressRadius()
        {
            var spriteSize = spriteRenderer != null && spriteRenderer.sprite != null
                ? spriteRenderer.sprite.bounds.size
                : Vector3.one * 0.5f;
            return Mathf.Max(spriteSize.x, spriteSize.y) * 0.5f + TrophyProgressPadding;
        }

        private static void UpdateCircularLine(LineRenderer lineRenderer, float radius, float fraction)
        {
            if (lineRenderer == null)
            {
                return;
            }

            fraction = Mathf.Clamp01(fraction);
            if (fraction <= 0.001f)
            {
                lineRenderer.enabled = false;
                return;
            }

            lineRenderer.enabled = true;
            var segmentCount = Mathf.Max(3, Mathf.CeilToInt(TrophyProgressSegments * fraction));
            lineRenderer.positionCount = segmentCount + 1;

            const float startAngle = Mathf.PI * 0.5f;
            var arc = Mathf.PI * 2f * fraction;
            for (var index = 0; index <= segmentCount; index++)
            {
                var t = index / (float)segmentCount;
                var angle = startAngle - arc * t;
                lineRenderer.SetPosition(index, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }
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

            UpdateTrophyOuterGlowPulse();
        }

        private void UpdateTrophyOuterGlowPulse()
        {
            if (!trophyOuterGlowConfigured
                || trophyOuterGlowRenderer == null
                || trophyOuterGlowRoot == null
                || !trophyOuterGlowRenderer.enabled)
            {
                return;
            }

            var wave = (Mathf.Sin((Time.time - spawnTime) * Mathf.PI * 2f / 1.9f + 0.7f) + 1f) * 0.5f;
            trophyOuterGlowRoot.localScale = trophyOuterGlowBaseScale * (1f + TrophyOuterGlowPulseScale * wave);
            trophyOuterGlowRenderer.color = new Color(
                trophyOuterGlowBaseColor.r,
                trophyOuterGlowBaseColor.g,
                trophyOuterGlowBaseColor.b,
                Mathf.Clamp01(trophyOuterGlowBaseColor.a + TrophyOuterGlowPulseAlpha * wave));
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

            if (configuredSprites.TryGetValue(config.spritePath, out var cachedSprite))
            {
                return cachedSprite;
            }

            Sprite sprite = null;

#if UNITY_EDITOR
            if (RuntimeRemoteResourceManager.CanUseEditorLocalAssets)
            {
                var editorSpritePath = ResolveEditorSpritePath(config.spritePath);
                if (!string.IsNullOrEmpty(editorSpritePath))
                {
                    var editorSprite = AssetDatabase.LoadAssetAtPath<Sprite>(editorSpritePath);
                    if (editorSprite != null)
                    {
                        sprite = editorSprite;
                    }
                }
            }
#endif

            if (sprite == null)
            {
                sprite = RuntimeAssetCatalog.LoadSprite(config.spritePath);
            }

            if (sprite == null && RuntimeRemoteResourceManager.CanUseResourcesFallback)
            {
                sprite = Resources.Load<Sprite>(NormalizeResourcesPath(config.spritePath));
            }

            configuredSprites[config.spritePath] = sprite;
            return sprite;
        }

#if UNITY_EDITOR
        private static string ResolveEditorSpritePath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return string.Empty;
            }

            var normalized = assetPath.Replace("\\", "/").Trim();
            if (normalized.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            if (normalized.StartsWith("Sprites/", System.StringComparison.OrdinalIgnoreCase))
            {
                var resolvedPath = $"Assets/Art/{normalized}";
                return HasExtension(resolvedPath) ? resolvedPath : $"{resolvedPath}.png";
            }

            return string.Empty;
        }

        private static bool HasExtension(string path)
        {
            var extensionIndex = path.LastIndexOf(".", System.StringComparison.Ordinal);
            var slashIndex = path.LastIndexOf("/", System.StringComparison.Ordinal);
            return extensionIndex > slashIndex;
        }
#endif

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
            if (fallbackStarSprite == null)
            {
                fallbackStarSprite = BuildFallbackStarSprite();
            }

            return fallbackStarSprite;
        }

        private static Sprite BuildFallbackStarSprite()
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

        private static PlayerController ResolvePlayer()
        {
            if (cachedPlayer == null)
            {
                cachedPlayer = FindObjectOfType<PlayerController>();
            }

            return cachedPlayer;
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
