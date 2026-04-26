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

        private static Sprite playerSprite;
        private static Sprite enemySprite;
        private static Sprite laserSprite;
        private static Material defaultSpriteMaterial;
        private static Material laserMaterial;
        private static readonly Dictionary<string, Sprite> configuredSprites = new Dictionary<string, Sprite>();

        private BulletManager manager;
        private Rigidbody2D body;
        private BoxCollider2D boxCollider;
        private Transform visualRoot;
        private SpriteRenderer spriteRenderer;
        private Vector2 direction = Vector2.up;
        private float speed;
        private float lifetimeRemaining;
        private int remainingPierceHits;
        private bool isLaser;
        private bool isActiveProjectile;

        public string Owner { get; private set; }
        public int Damage { get; private set; }

        public void Activate(BulletConfig bulletConfig, Vector2 fireDirection, BulletManager owningManager)
        {
            if (bulletConfig == null)
            {
                return;
            }

            EnsureComponents();

            manager = owningManager;
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
            if (!isLaser)
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
        }

        private void ApplyLayer()
        {
            var layerName = IsPlayerOwned() ? "PlayerBullet" : "EnemyBullet";
            var layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0)
            {
                gameObject.layer = layer;
                visualRoot.gameObject.layer = layer;
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

            boxCollider.size = size;
            boxCollider.offset = Vector2.zero;

            spriteRenderer.sprite = isLaser ? GetLaserSprite() : LoadConfiguredSprite(bulletConfig.spritePath) ?? GetFallbackSprite();
            spriteRenderer.sortingOrder = IsPlayerOwned() ? 30 : 20;
            spriteRenderer.sharedMaterial = isLaser ? GetLaserMaterial() : GetDefaultSpriteMaterial();
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localScale = new Vector3(size.x / BaseSpriteWidth, size.y / BaseSpriteHeight, 1f);
        }

        private void ApplyTransformForDirection(BulletConfig bulletConfig)
        {
            transform.up = direction;

            if (isLaser)
            {
                var length = bulletConfig.laserLength > 0f ? bulletConfig.laserLength : Mathf.Max(BaseSpriteHeight, bulletConfig.size.y);
                transform.position += (Vector3)(direction * length * 0.5f);
            }
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
    }
}
