using System;
using System.Collections.Generic;
using LeiTing.Config;
using LeiTing.Core;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LeiTing.Bullets
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class BulletProjectile : MonoBehaviour
    {
        private const float SpritePixelsPerUnit = 100f;
        private const float BaseSpriteWidth = 0.12f;
        private const float BaseSpriteHeight = 0.32f;
        private const float GlowSpriteSize = 1f;
        private const float EnemyBulletMinGlowRange = 0.16f;
        private const float EnemyBulletMinGlowAlpha = 0.5f;
        private const float LaserGlowMinWidth = 0.72f;
        private const float LaserGlowLengthPadding = 0.35f;
        private const float LaserViewportOvershoot = 2f;
        private const string GlowTrailOption = "GlowTrail";
        private const string ProceduralLaserShaderName = "LeiTing/ProceduralLaser";

        private static readonly int LaserCoreColorId = Shader.PropertyToID("_CoreColor");
        private static readonly int LaserBeamColorId = Shader.PropertyToID("_BeamColor");
        private static readonly int LaserGlowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int LaserPulseSpeedId = Shader.PropertyToID("_PulseSpeed");

        private static Sprite playerSprite;
        private static Sprite enemySprite;
        private static Sprite laserSprite;
        private static Sprite glowSprite;
        private static Material defaultSpriteMaterial;
        private static Material laserMaterial;
        private static readonly Dictionary<string, Sprite> configuredSprites = new Dictionary<string, Sprite>();

        private BulletManager manager;
        private Transform followTarget;
        private Rigidbody2D body;
        private BoxCollider2D boxCollider;
        private Transform visualRoot;
        private Transform glowRoot;
        private Transform glowTrailRoot;
        private SpriteRenderer spriteRenderer;
        private SpriteRenderer glowRenderer;
        private TrailRenderer glowTrailRenderer;
        private MaterialPropertyBlock laserPropertyBlock;
        private Vector2 direction = Vector2.up;
        private Vector2 linearOrigin;
        private Vector2 lateralDirection;
        private float speed;
        private float visualSpinSpeed;
        private float projectileAge;
        private float lifetimeRemaining;
        private float laserWidth;
        private float configuredLaserLength;
        private float laserLength;
        private Color laserGlowColor = new Color(0.08f, 0.68f, 1f, 0.45f);
        private float swayAmplitude;
        private float swayFrequency;
        private float swayPhase;
        private float previousSwayOffset;
        private int remainingPierceHits;
        private bool isLaser;
        private bool useSwayMovement;
        private bool isActiveProjectile;

        public string Owner { get; private set; }
        public int Damage { get; private set; }

        public void RegisterExternalHit()
        {
            if (!isActiveProjectile)
            {
                return;
            }

            if (CanPierceHit())
            {
                return;
            }

            Recycle();
        }

        public void Activate(BulletConfig bulletConfig, Vector2 fireDirection, BulletManager owningManager, Transform sourceFollowTarget = null)
        {
            if (bulletConfig == null)
            {
                return;
            }

            EnsureComponents();

            manager = owningManager;
            followTarget = sourceFollowTarget;
            Owner = string.IsNullOrEmpty(bulletConfig.owner) ? "Player" : bulletConfig.owner;
            Damage = Mathf.Max(1, bulletConfig.damage);
            speed = Mathf.Max(0f, bulletConfig.speed);
            ConfigureMotionPattern(bulletConfig.firePattern);
            lifetimeRemaining = Mathf.Max(0.05f, bulletConfig.lifetime);
            direction = fireDirection.sqrMagnitude > 0.0001f ? fireDirection.normalized : Vector2.up;
            lateralDirection = new Vector2(-direction.y, direction.x);
            linearOrigin = transform.position;
            projectileAge = 0f;
            previousSwayOffset = 0f;
            remainingPierceHits = bulletConfig.pierceCount;
            isLaser = IsPattern(bulletConfig.firePattern, "Laser");

            ApplyLayer();
            ApplyVisual(bulletConfig);
            ApplyTransformForDirection(bulletConfig);

            isActiveProjectile = true;
            gameObject.SetActive(true);
        }

        public void DeactivateForPool()
        {
            isActiveProjectile = false;
            followTarget = null;
            DisableGlowTrail();
            gameObject.SetActive(false);
        }

        private void Awake()
        {
            EnsureComponents();
        }

        private void Update()
        {
            if (!isActiveProjectile)
            {
                return;
            }

            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            {
                return;
            }

            var delta = Time.deltaTime;
            if (isLaser)
            {
                UpdateLaserTransform();
            }
            else
            {
                if (useSwayMovement)
                {
                    UpdateSwayMovement(delta);
                }
                else
                {
                    transform.position += (Vector3)(direction * speed * delta);
                }

                UpdateVisualSpin(delta);
            }

            lifetimeRemaining -= delta;

            if (lifetimeRemaining <= 0f)
            {
                Recycle();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!isActiveProjectile || other == null)
            {
                return;
            }

            if (ShouldRecycleOnHit(other))
            {
                if (CanPierceHit())
                {
                    return;
                }

                Recycle();
            }
        }

        private void EnsureComponents()
        {
            body = body != null ? body : GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            boxCollider = boxCollider != null ? boxCollider : GetComponent<BoxCollider2D>();
            boxCollider.isTrigger = true;

            if (visualRoot == null)
            {
                var visual = transform.Find("Visual");
                if (visual == null)
                {
                    visual = new GameObject("Visual").transform;
                    visual.SetParent(transform);
                    visual.localPosition = Vector3.zero;
                    visual.localRotation = Quaternion.identity;
                }

                visualRoot = visual;
            }

            spriteRenderer = spriteRenderer != null ? spriteRenderer : visualRoot.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = visualRoot.gameObject.AddComponent<SpriteRenderer>();
            }

            if (glowRoot == null)
            {
                var glow = transform.Find("Glow");
                if (glow == null)
                {
                    glow = new GameObject("Glow").transform;
                    glow.SetParent(transform);
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

        private void ApplyLayer()
        {
            var layerName = IsPlayerOwned() ? "PlayerBullet" : "EnemyBullet";
            var layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0)
            {
                gameObject.layer = layer;
                visualRoot.gameObject.layer = layer;
                glowRoot.gameObject.layer = layer;
                if (glowTrailRoot != null)
                {
                    glowTrailRoot.gameObject.layer = layer;
                }
            }
        }

        private void ApplyVisual(BulletConfig bulletConfig)
        {
            var size = bulletConfig.size;
            if (size.x <= 0f)
            {
                size.x = BaseSpriteWidth;
            }

            if (size.y <= 0f)
            {
                size.y = BaseSpriteHeight;
            }

            if (IsPattern(bulletConfig.firePattern, "Laser"))
            {
                size.y = Mathf.Max(size.y, bulletConfig.laserLength > 0f ? bulletConfig.laserLength : 4.8f);
            }

            laserWidth = size.x;
            configuredLaserLength = size.y;
            laserLength = configuredLaserLength;
            laserGlowColor = ResolveLaserColor(bulletConfig.glowColor);
            boxCollider.size = size;
            boxCollider.offset = Vector2.zero;

            spriteRenderer.sprite = isLaser ? GetLaserSprite() : LoadConfiguredSprite(bulletConfig.spritePath) ?? GetFallbackSprite();
            spriteRenderer.sortingOrder = IsPlayerOwned() ? 30 : 20;
            spriteRenderer.sharedMaterial = isLaser ? GetLaserMaterial() : GetDefaultSpriteMaterial();
            spriteRenderer.color = Color.white;
            ApplyLaserMaterialProperties();
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localScale = ResolveVisualScale(spriteRenderer.sprite, size);
            ApplyGlowVisual(bulletConfig, size);
            ConfigureGlowTrail(bulletConfig, size);
        }

        private void ApplyGlowVisual(BulletConfig bulletConfig, Vector2 size)
        {
            if (isLaser)
            {
                ConfigureLaserGlow(size.y);
                return;
            }

            if (IsPlayerOwned())
            {
                glowRenderer.enabled = false;
                return;
            }

            var glowColor = ResolveGlowColor(bulletConfig.glowColor);
            var glowRange = ResolveEnemyGlowRange(bulletConfig, size);
            var glowSize = new Vector2(size.x + glowRange * 2f, size.y + glowRange * 2f);

            glowRenderer.enabled = true;
            glowRenderer.sprite = GetGlowSprite();
            glowRenderer.color = glowColor;
            glowRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
            glowRenderer.sharedMaterial = GetDefaultSpriteMaterial();
            glowRoot.localPosition = Vector3.zero;
            glowRoot.localRotation = Quaternion.identity;
            glowRoot.localScale = new Vector3(glowSize.x / GlowSpriteSize, glowSize.y / GlowSpriteSize, 1f);
        }

        private void ApplyLaserMaterialProperties()
        {
            if (!isLaser)
            {
                spriteRenderer.SetPropertyBlock(null);
                return;
            }

            laserPropertyBlock = laserPropertyBlock ?? new MaterialPropertyBlock();
            laserPropertyBlock.Clear();

            var coreColor = IsPlayerOwned()
                ? Color.white
                : new Color(1f, 0.97f, 0.72f, 1f);
            var beamColor = Color.Lerp(laserGlowColor, coreColor, IsPlayerOwned() ? 0.56f : 0.4f);
            beamColor.a = 1f;
            var outerGlowColor = laserGlowColor;
            outerGlowColor.a = Mathf.Max(0.45f, outerGlowColor.a);

            laserPropertyBlock.SetColor(LaserCoreColorId, coreColor);
            laserPropertyBlock.SetColor(LaserBeamColorId, beamColor);
            laserPropertyBlock.SetColor(LaserGlowColorId, outerGlowColor);
            laserPropertyBlock.SetFloat(LaserPulseSpeedId, IsPlayerOwned() ? 18f : 23f);
            spriteRenderer.SetPropertyBlock(laserPropertyBlock);
        }

        private void ConfigureGlowTrail(BulletConfig bulletConfig, Vector2 size)
        {
            if (!ShouldUseGlowTrail(bulletConfig))
            {
                DisableGlowTrail();
                return;
            }

            var trailRenderer = EnsureGlowTrailRenderer();
            if (trailRenderer == null)
            {
                return;
            }

            var glowColor = ResolveGlowColor(bulletConfig.glowColor);
            var startColor = new Color(glowColor.r, glowColor.g, glowColor.b, Mathf.Clamp01(glowColor.a * 0.9f));
            var endColor = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);

            trailRenderer.enabled = true;
            trailRenderer.emitting = true;
            trailRenderer.Clear();
            trailRenderer.sharedMaterial = GetDefaultSpriteMaterial();
            trailRenderer.time = 0.22f;
            trailRenderer.minVertexDistance = 0.02f;
            trailRenderer.startWidth = Mathf.Max(0.04f, size.x * 0.95f);
            trailRenderer.endWidth = 0.015f;
            trailRenderer.startColor = startColor;
            trailRenderer.endColor = endColor;
            trailRenderer.numCornerVertices = 2;
            trailRenderer.numCapVertices = 2;
            trailRenderer.alignment = LineAlignment.View;
            trailRenderer.textureMode = LineTextureMode.Stretch;
            trailRenderer.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder - 2 : 18;
        }

        private bool ShouldUseGlowTrail(BulletConfig bulletConfig)
        {
            return bulletConfig != null
                && !IsPlayerOwned()
                && !isLaser
                && HasPatternOption(bulletConfig.firePattern, GlowTrailOption);
        }

        private TrailRenderer EnsureGlowTrailRenderer()
        {
            if (glowTrailRoot == null)
            {
                var trail = transform.Find("GlowTrail");
                if (trail == null)
                {
                    trail = new GameObject("GlowTrail").transform;
                    trail.SetParent(transform, false);
                }

                trail.localPosition = Vector3.zero;
                trail.localRotation = Quaternion.identity;
                trail.localScale = Vector3.one;
                glowTrailRoot = trail;
            }

            glowTrailRoot.gameObject.layer = gameObject.layer;

            glowTrailRenderer = glowTrailRenderer != null ? glowTrailRenderer : glowTrailRoot.GetComponent<TrailRenderer>();
            if (glowTrailRenderer == null)
            {
                glowTrailRenderer = glowTrailRoot.gameObject.AddComponent<TrailRenderer>();
                glowTrailRenderer.autodestruct = false;
            }

            return glowTrailRenderer;
        }

        private void DisableGlowTrail()
        {
            if (glowTrailRenderer == null)
            {
                return;
            }

            glowTrailRenderer.emitting = false;
            glowTrailRenderer.Clear();
            glowTrailRenderer.enabled = false;
        }

        private void ApplyTransformForDirection(BulletConfig bulletConfig)
        {
            transform.up = direction;

            if (isLaser)
            {
                UpdateLaserTransform();
            }
        }

        private void UpdateLaserTransform()
        {
            var origin = followTarget != null ? (Vector2)followTarget.position : (Vector2)transform.position - direction * laserLength * 0.5f;
            var length = ResolveLaserLengthBeyondViewport(origin);

            transform.up = direction;
            transform.position = origin + direction * length * 0.5f;
            boxCollider.size = new Vector2(Mathf.Max(BaseSpriteWidth, laserWidth), length);
            boxCollider.offset = Vector2.zero;
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localScale = new Vector3(Mathf.Max(BaseSpriteWidth, laserWidth) / BaseSpriteWidth, length / BaseSpriteHeight, 1f);
            UpdateLaserGlowTransform(length);
            laserLength = length;
        }

        private void UpdateVisualSpin(float delta)
        {
            if (Mathf.Approximately(visualSpinSpeed, 0f) || visualRoot == null)
            {
                return;
            }

            visualRoot.Rotate(0f, 0f, visualSpinSpeed * delta, Space.Self);
        }

        private void UpdateSwayMovement(float delta)
        {
            projectileAge += delta;
            linearOrigin += direction * speed * delta;

            var swayOffset = Mathf.Sin(projectileAge * swayFrequency * Mathf.PI * 2f + swayPhase) * swayAmplitude;
            transform.position = linearOrigin + lateralDirection * swayOffset;

            if (delta > 0f)
            {
                var lateralSpeed = (swayOffset - previousSwayOffset) / delta;
                var velocity = direction * speed + lateralDirection * lateralSpeed;
                if (velocity.sqrMagnitude > 0.0001f)
                {
                    transform.up = velocity.normalized;
                }
            }

            previousSwayOffset = swayOffset;
        }

        private void ConfigureLaserGlow(float length)
        {
            if (glowRenderer == null)
            {
                return;
            }

            glowRenderer.enabled = true;
            glowRenderer.sprite = GetGlowSprite();
            glowRenderer.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder - 1 : 29;
            glowRenderer.sharedMaterial = GetDefaultSpriteMaterial();
            glowRoot.localPosition = Vector3.zero;
            glowRoot.localRotation = Quaternion.identity;
            UpdateLaserGlowTransform(length);
        }

        private void UpdateLaserGlowTransform(float length)
        {
            if (glowRenderer == null || glowRoot == null)
            {
                return;
            }

            var glowWidth = Mathf.Max(LaserGlowMinWidth, laserWidth * 3.2f);
            var glowLength = Mathf.Max(BaseSpriteHeight, length + LaserGlowLengthPadding);
            var pulse = 0.3f + Mathf.PingPong(Time.time * 5.2f, 0.14f);

            glowRenderer.enabled = true;
            glowRenderer.color = new Color(
                laserGlowColor.r,
                laserGlowColor.g,
                laserGlowColor.b,
                pulse * Mathf.Clamp01(laserGlowColor.a / 0.45f));
            glowRoot.localPosition = Vector3.zero;
            glowRoot.localRotation = Quaternion.identity;
            glowRoot.localScale = new Vector3(glowWidth / GlowSpriteSize, glowLength / GlowSpriteSize, 1f);
        }

        private static Vector3 ResolveVisualScale(Sprite sprite, Vector2 targetSize)
        {
            var spriteSize = sprite != null ? (Vector2)sprite.bounds.size : new Vector2(BaseSpriteWidth, BaseSpriteHeight);
            var width = spriteSize.x > 0f ? spriteSize.x : BaseSpriteWidth;
            var height = spriteSize.y > 0f ? spriteSize.y : BaseSpriteHeight;
            return new Vector3(targetSize.x / width, targetSize.y / height, 1f);
        }

        private float ResolveLaserLengthBeyondViewport(Vector2 origin)
        {
            var configuredLength = Mathf.Max(BaseSpriteHeight, configuredLaserLength);
            var camera = Camera.main;
            if (camera == null || direction.sqrMagnitude <= 0.0001f)
            {
                return configuredLength;
            }

            var originWorld = new Vector3(origin.x, origin.y, transform.position.z);
            var normalizedDirection = direction.normalized;
            var aheadWorld = originWorld + new Vector3(normalizedDirection.x, normalizedDirection.y, 0f);
            var originViewport = camera.WorldToViewportPoint(originWorld);
            var aheadViewport = camera.WorldToViewportPoint(aheadWorld);
            var viewportDirection = new Vector2(
                aheadViewport.x - originViewport.x,
                aheadViewport.y - originViewport.y);
            var distanceToEdge = float.PositiveInfinity;

            if (Mathf.Abs(viewportDirection.x) > 0.0001f)
            {
                var xBoundary = viewportDirection.x > 0f ? 1f : 0f;
                var xDistance = (xBoundary - originViewport.x) / viewportDirection.x;
                if (xDistance > 0f)
                {
                    distanceToEdge = Mathf.Min(distanceToEdge, xDistance);
                }
            }

            if (Mathf.Abs(viewportDirection.y) > 0.0001f)
            {
                var yBoundary = viewportDirection.y > 0f ? 1f : 0f;
                var yDistance = (yBoundary - originViewport.y) / viewportDirection.y;
                if (yDistance > 0f)
                {
                    distanceToEdge = Mathf.Min(distanceToEdge, yDistance);
                }
            }

            if (float.IsInfinity(distanceToEdge) || float.IsNaN(distanceToEdge))
            {
                return configuredLength;
            }

            return Mathf.Max(configuredLength, distanceToEdge + LaserViewportOvershoot);
        }

        private bool ShouldRecycleOnHit(Collider2D other)
        {
            var targetLayer = LayerMask.LayerToName(other.gameObject.layer);

            if (IsPlayerOwned())
            {
                return targetLayer == "Enemy";
            }

            return targetLayer == "Player";
        }

        private bool IsPlayerOwned()
        {
            return string.Equals(Owner, "Player", StringComparison.OrdinalIgnoreCase);
        }

        private Sprite GetFallbackSprite()
        {
            if (isLaser)
            {
                return GetLaserSprite();
            }

            return IsPlayerOwned() ? GetPlayerSprite() : GetEnemySprite();
        }

        private bool CanPierceHit()
        {
            if (isLaser || remainingPierceHits < 0)
            {
                return true;
            }

            if (remainingPierceHits <= 0)
            {
                return false;
            }

            remainingPierceHits--;
            return true;
        }

        private static bool IsPattern(string pattern, string expected)
        {
            return string.Equals(pattern, expected, StringComparison.OrdinalIgnoreCase);
        }

        private void ConfigureMotionPattern(string firePattern)
        {
            visualSpinSpeed = ResolveVisualSpinSpeed(firePattern);
            useSwayMovement = IsMotionPattern(firePattern, "Sway") || IsMotionPattern(firePattern, "Sine");
            swayAmplitude = 0.28f;
            swayFrequency = 1.6f;
            swayPhase = 0f;

            if (!useSwayMovement)
            {
                return;
            }

            var values = GetPatternValues(firePattern);
            TryReadFloat(values, 0, result => swayAmplitude = Mathf.Max(0f, result));
            TryReadFloat(values, 1, result => swayFrequency = Mathf.Max(0.05f, result));
            TryReadFloat(values, 2, result => visualSpinSpeed = result);
            TryReadFloat(values, 3, result => swayPhase = result * Mathf.Deg2Rad);
        }

        private static float ResolveVisualSpinSpeed(string firePattern)
        {
            if (string.IsNullOrWhiteSpace(firePattern)
                || !IsMotionPattern(firePattern, "Spin"))
            {
                return 0f;
            }

            var values = GetPatternValues(firePattern);
            if (TryGetFloat(values, 0, out var configuredSpeed))
            {
                return configuredSpeed;
            }

            return 70f;
        }

        private static bool IsMotionPattern(string firePattern, string expected)
        {
            if (string.IsNullOrWhiteSpace(firePattern))
            {
                return false;
            }

            var separatorIndex = firePattern.IndexOf(':');
            var patternName = separatorIndex >= 0 ? firePattern.Substring(0, separatorIndex) : firePattern;
            return string.Equals(patternName.Trim(), expected, StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasPatternOption(string firePattern, string option)
        {
            if (string.IsNullOrWhiteSpace(firePattern) || string.IsNullOrWhiteSpace(option))
            {
                return false;
            }

            var values = GetPatternValues(firePattern);
            for (var index = 0; index < values.Length; index++)
            {
                if (string.Equals(values[index].Trim(), option, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string[] GetPatternValues(string firePattern)
        {
            if (string.IsNullOrWhiteSpace(firePattern))
            {
                return Array.Empty<string>();
            }

            var separatorIndex = firePattern.IndexOf(':');
            if (separatorIndex < 0 || separatorIndex >= firePattern.Length - 1)
            {
                return Array.Empty<string>();
            }

            return firePattern.Substring(separatorIndex + 1)
                .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static void TryReadFloat(string[] values, int index, Action<float> apply)
        {
            if (TryGetFloat(values, index, out var result))
            {
                apply(result);
            }
        }

        private static bool TryGetFloat(string[] values, int index, out float result)
        {
            result = 0f;
            return values != null
                && index >= 0
                && index < values.Length
                && float.TryParse(
                    values[index],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out result);
        }

        private static Sprite LoadConfiguredSprite(string spritePath)
        {
            if (string.IsNullOrEmpty(spritePath))
            {
                return null;
            }

            if (configuredSprites.TryGetValue(spritePath, out var cachedSprite))
            {
                return cachedSprite;
            }

            var sprite = LoadSpriteAsset(spritePath);
            configuredSprites[spritePath] = sprite;
            return sprite;
        }

        private static Sprite LoadSpriteAsset(string spritePath)
        {
#if UNITY_EDITOR
            if (RuntimeRemoteResourceManager.CanUseEditorLocalAssets
                && spritePath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                var editorSprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (editorSprite != null)
                {
                    return editorSprite;
                }
            }
#endif

            var catalogSprite = RuntimeAssetCatalog.LoadSprite(spritePath);
            if (catalogSprite != null || !RuntimeRemoteResourceManager.CanUseResourcesFallback)
            {
                return catalogSprite;
            }

            return Resources.Load<Sprite>(NormalizeResourcesPath(spritePath));
        }

        private static string NormalizeResourcesPath(string spritePath)
        {
            const string resourcesSegment = "/Resources/";
            var normalized = spritePath.Replace("\\", "/");
            var resourcesIndex = normalized.IndexOf(resourcesSegment, StringComparison.OrdinalIgnoreCase);

            if (resourcesIndex >= 0)
            {
                normalized = normalized.Substring(resourcesIndex + resourcesSegment.Length);
            }

            var extensionIndex = normalized.LastIndexOf(".", StringComparison.Ordinal);
            if (extensionIndex >= 0)
            {
                normalized = normalized.Substring(0, extensionIndex);
            }

            return normalized;
        }

        private void Recycle()
        {
            if (!isActiveProjectile)
            {
                return;
            }

            if (manager != null)
            {
                manager.Recycle(this);
                return;
            }

            DeactivateForPool();
        }

        private static Sprite GetPlayerSprite()
        {
            if (playerSprite == null)
            {
                playerSprite = CreateBulletSprite(new Color(0.36f, 0.92f, 1f, 1f), new Color(1f, 1f, 1f, 1f));
            }

            return playerSprite;
        }

        private static Sprite GetEnemySprite()
        {
            if (enemySprite == null)
            {
                enemySprite = CreateBulletSprite(new Color(1f, 0.32f, 0.18f, 1f), new Color(1f, 0.86f, 0.2f, 1f));
            }

            return enemySprite;
        }

        private static Sprite GetLaserSprite()
        {
            if (laserSprite == null)
            {
                laserSprite = CreateLaserSprite();
            }

            return laserSprite;
        }

        private static Sprite GetGlowSprite()
        {
            if (glowSprite == null)
            {
                glowSprite = CreateGlowSprite();
            }

            return glowSprite;
        }

        private static Color ResolveGlowColor(Color configuredColor)
        {
            var resolvedColor = configuredColor.a > 0f
                ? configuredColor
                : new Color(1f, 0.32f, 0.42f, 0.58f);
            Color.RGBToHSV(resolvedColor, out var hue, out var saturation, out var value);
            var vividColor = Color.HSVToRGB(
                hue,
                Mathf.Max(0.62f, saturation),
                Mathf.Max(0.9f, value));
            vividColor.a = Mathf.Clamp01(Mathf.Max(EnemyBulletMinGlowAlpha, resolvedColor.a));
            return vividColor;
        }

        private static float ResolveEnemyGlowRange(BulletConfig bulletConfig, Vector2 size)
        {
            var configuredRange = bulletConfig != null ? Mathf.Max(0f, bulletConfig.glowRange) : 0f;
            var sizeBasedRange = Mathf.Max(size.x, size.y) * 0.55f;
            return Mathf.Max(EnemyBulletMinGlowRange, Mathf.Max(configuredRange, sizeBasedRange));
        }

        private static Color ResolveLaserColor(Color configuredColor)
        {
            if (configuredColor.a > 0f)
            {
                return configuredColor;
            }

            return new Color(0.08f, 0.68f, 1f, 0.45f);
        }

        private static Material GetDefaultSpriteMaterial()
        {
            if (defaultSpriteMaterial == null)
            {
                defaultSpriteMaterial = SpriteMaterialUtility.CreateSpriteMaterial("Bullet Sprite Material");
            }

            return defaultSpriteMaterial;
        }

        private static Material GetLaserMaterial()
        {
            if (laserMaterial == null)
            {
                var shader = Shader.Find(ProceduralLaserShaderName);
                if (shader != null && shader.isSupported)
                {
                    laserMaterial = new Material(shader)
                    {
                        name = "Bullet Procedural Laser Material",
                        hideFlags = HideFlags.DontSave
                    };
                }
                else
                {
                    laserMaterial = SpriteMaterialUtility.CreateSpriteMaterial("Bullet Laser Material");
                }
            }

            return laserMaterial;
        }

        private static Sprite CreateBulletSprite(Color bodyColor, Color coreColor)
        {
            const int width = 12;
            const int height = 32;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;

            var clear = new Color(0f, 0f, 0f, 0f);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var normalizedX = (x + 0.5f) / width * 2f - 1f;
                    var cap = y < width / 2 ? y / (float)(width / 2) : (height - 1 - y) / (float)(width / 2);
                    var halfWidth = Mathf.Lerp(0.15f, 0.72f, Mathf.Clamp01(cap));
                    var color = Mathf.Abs(normalizedX) <= halfWidth ? bodyColor : clear;

                    if (Mathf.Abs(normalizedX) <= halfWidth * 0.35f && y > 3 && y < height - 4)
                    {
                        color = coreColor;
                    }

                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), SpritePixelsPerUnit);
        }

        private static Sprite CreateLaserSprite()
        {
            const int width = 12;
            const int height = 32;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            var glowColor = new Color(1f, 1f, 1f, 0.28f);
            var beamColor = new Color(1f, 1f, 1f, 0.92f);
            var coreColor = Color.white;

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var horizontal = Mathf.Abs((x + 0.5f) / width * 2f - 1f);
                    var core = 1f - SmoothStep(0.12f, 0.26f, horizontal);
                    var beam = 1f - SmoothStep(0.38f, 0.64f, horizontal);
                    var glow = 1f - SmoothStep(0.68f, 1f, horizontal);
                    var color = Color.Lerp(glowColor, beamColor, beam);
                    color = Color.Lerp(color, coreColor, core);
                    color.a = Mathf.Clamp01(Mathf.Max(glow * 0.34f, Mathf.Max(beam * 0.82f, core)));
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), SpritePixelsPerUnit);
        }

        private static float SmoothStep(float edge0, float edge1, float value)
        {
            var t = Mathf.Clamp01((value - edge0) / Mathf.Max(0.0001f, edge1 - edge0));
            return t * t * (3f - 2f * t);
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
    }
}
