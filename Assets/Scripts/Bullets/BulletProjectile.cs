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
        private SpriteRenderer spriteRenderer;
        private SpriteRenderer glowRenderer;
        private Vector2 direction = Vector2.up;
        private float speed;
        private float lifetimeRemaining;
        private float laserWidth;
        private float laserLength;
        private int remainingPierceHits;
        private bool isLaser;
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
            lifetimeRemaining = Mathf.Max(0.05f, bulletConfig.lifetime);
            direction = fireDirection.sqrMagnitude > 0.0001f ? fireDirection.normalized : Vector2.up;
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
                transform.position += (Vector3)(direction * speed * delta);
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
            laserLength = size.y;
            boxCollider.size = size;
            boxCollider.offset = Vector2.zero;

            spriteRenderer.sprite = isLaser ? GetLaserSprite() : LoadConfiguredSprite(bulletConfig.spritePath) ?? GetFallbackSprite();
            spriteRenderer.sortingOrder = IsPlayerOwned() ? 30 : 20;
            spriteRenderer.sharedMaterial = isLaser ? GetLaserMaterial() : GetDefaultSpriteMaterial();
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localScale = ResolveVisualScale(spriteRenderer.sprite, size);
            ApplyGlowVisual(bulletConfig, size);
        }

        private void ApplyGlowVisual(BulletConfig bulletConfig, Vector2 size)
        {
            var glowRange = Mathf.Max(0f, bulletConfig.glowRange);
            if (IsPlayerOwned() || isLaser || glowRange <= 0f)
            {
                glowRenderer.enabled = false;
                return;
            }

            var glowColor = ResolveGlowColor(bulletConfig.glowColor);
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
            var length = ResolveLaserLengthToScreenTop(origin);

            transform.up = direction;
            transform.position = origin + direction * length * 0.5f;
            boxCollider.size = new Vector2(Mathf.Max(BaseSpriteWidth, laserWidth), length);
            boxCollider.offset = Vector2.zero;
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localScale = new Vector3(Mathf.Max(BaseSpriteWidth, laserWidth) / BaseSpriteWidth, length / BaseSpriteHeight, 1f);
            glowRenderer.enabled = false;
            laserLength = length;
        }

        private static Vector3 ResolveVisualScale(Sprite sprite, Vector2 targetSize)
        {
            var spriteSize = sprite != null ? (Vector2)sprite.bounds.size : new Vector2(BaseSpriteWidth, BaseSpriteHeight);
            var width = spriteSize.x > 0f ? spriteSize.x : BaseSpriteWidth;
            var height = spriteSize.y > 0f ? spriteSize.y : BaseSpriteHeight;
            return new Vector3(targetSize.x / width, targetSize.y / height, 1f);
        }

        private float ResolveLaserLengthToScreenTop(Vector2 origin)
        {
            var configuredLength = Mathf.Max(BaseSpriteHeight, laserLength);
            var camera = Camera.main;
            if (camera == null || direction.y <= 0.01f)
            {
                return configuredLength;
            }

            var distance = Mathf.Abs(camera.transform.position.z - transform.position.z);
            var top = camera.ViewportToWorldPoint(new Vector3(0.5f, 1f, distance));
            var lengthToTop = (top.y - origin.y) / direction.y;
            return Mathf.Max(BaseSpriteHeight, lengthToTop + 0.25f);
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
            if (spritePath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            }
#endif

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
            if (configuredColor.a > 0f)
            {
                return configuredColor;
            }

            return new Color(1f, 0.48f, 0.12f, 0.58f);
        }

        private static Material GetDefaultSpriteMaterial()
        {
            if (defaultSpriteMaterial == null)
            {
                defaultSpriteMaterial = new Material(Shader.Find("Sprites/Default"));
            }

            return defaultSpriteMaterial;
        }

        private static Material GetLaserMaterial()
        {
            if (laserMaterial == null)
            {
                var shader = Shader.Find("LeiTing/ProceduralLaser") ?? Shader.Find("Sprites/Default");
                laserMaterial = new Material(shader);
                laserMaterial.SetColor("_CoreColor", Color.white);
                laserMaterial.SetColor("_BeamColor", new Color(0.25f, 0.92f, 1f, 0.95f));
                laserMaterial.SetColor("_GlowColor", new Color(0.08f, 0.55f, 1f, 0.42f));
                laserMaterial.SetFloat("_PulseSpeed", 18f);
                laserMaterial.SetFloat("_GlowWidth", 0.96f);
                laserMaterial.SetFloat("_BodyWidth", 0.48f);
                laserMaterial.SetFloat("_CoreWidth", 0.14f);
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
            texture.filterMode = FilterMode.Point;

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    texture.SetPixel(x, y, Color.white);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), SpritePixelsPerUnit);
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
