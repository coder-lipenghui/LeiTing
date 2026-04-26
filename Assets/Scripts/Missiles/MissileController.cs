using System;
using System.Collections.Generic;
using LeiTing.Bullets;
using LeiTing.Config;
using LeiTing.Core;
using LeiTing.Effects;
using LeiTing.Player;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LeiTing.Missiles
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class MissileController : MonoBehaviour
    {
        private const float SpritePixelsPerUnit = 100f;
        private const float BaseSpriteSize = 0.32f;

        private static Sprite fallbackMissileSprite;
        private static Sprite fallbackHeavyMissileSprite;
        private static Sprite warningCircleSprite;
        private static Material defaultMaterial;
        private static readonly Dictionary<string, Sprite> configuredSprites = new Dictionary<string, Sprite>();

        private MissileManager manager;
        private MissileConfig config;
        private Rigidbody2D body;
        private CircleCollider2D hitbox;
        private Transform visualRoot;
        private SpriteRenderer bodyRenderer;
        private TrailRenderer trailRenderer;
        private SpriteRenderer warningCircleRenderer;
        private LineRenderer warningLineRenderer;
        private Transform target;
        private MissileState state = MissileState.Idle;
        private Color baseColor = Color.white;
        private Vector2 direction = Vector2.down;
        private Vector2 lockedDirection = Vector2.down;
        private Vector2 returnDirection = Vector2.down;
        private float speed;
        private float age;
        private float stateAge;
        private float lastWaveOffset;
        private float releaseTimer;
        private int currentHp;
        private bool isActiveMissile;
        private bool hasSplit;
        private bool hasReturned;
        private bool hasExploded;

        public int Damage => Mathf.Max(1, config != null ? config.damage : 1);
        public bool CanBeDestroyed => config != null && config.canBeDestroyed;
        public MissileState State => state;

        public void Activate(MissileConfig missileConfig, Vector2 fireDirection, MissileManager owningManager)
        {
            if (missileConfig == null)
            {
                return;
            }

            EnsureComponents();

            config = missileConfig;
            manager = owningManager;
            target = FindObjectOfType<PlayerController>()?.transform;
            direction = fireDirection.sqrMagnitude > 0.0001f ? fireDirection.normalized : Vector2.down;
            lockedDirection = ResolveDirectionToTarget(direction);
            returnDirection = direction;
            speed = Mathf.Max(0f, config.speed);
            age = 0f;
            stateAge = 0f;
            lastWaveOffset = 0f;
            releaseTimer = 0f;
            currentHp = Mathf.Max(1, config.hp);
            hasSplit = false;
            hasReturned = false;
            hasExploded = false;
            isActiveMissile = true;

            ApplyLayer();
            ApplyCollider();
            ApplyVisual();
            ConfigureTrail();

            SetState(ResolveInitialState());
            UpdateWarningVisuals();

            gameObject.SetActive(true);
        }

        public void DeactivateForPool()
        {
            isActiveMissile = false;
            state = MissileState.Dead;
            HideWarnings();

            if (trailRenderer != null)
            {
                trailRenderer.Clear();
            }

            gameObject.SetActive(false);
        }

        private void Awake()
        {
            EnsureComponents();
        }

        private void Update()
        {
            if (!isActiveMissile)
            {
                return;
            }

            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            {
                return;
            }

            var delta = Time.deltaTime;
            age += delta;
            stateAge += delta;

            UpdateBehavior(delta);
            UpdateWarningVisuals();
            UpdateVisualState();

            if (age >= GetLifetime())
            {
                if (ShouldExplodeOnLifetime())
                {
                    Detonate(true);
                    return;
                }

                Recycle();
                return;
            }

            if (age > 0.35f && IsOutsideCameraBounds())
            {
                Recycle();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!isActiveMissile || other == null)
            {
                return;
            }

            var bullet = other.GetComponent<BulletProjectile>();
            if (bullet != null && string.Equals(bullet.Owner, "Player", StringComparison.OrdinalIgnoreCase))
            {
                HandlePlayerBulletHit(bullet);
                return;
            }

            var player = other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                HandlePlayerHit(player);
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

            bodyRenderer = bodyRenderer != null ? bodyRenderer : visualRoot.GetComponent<SpriteRenderer>();
            if (bodyRenderer == null)
            {
                bodyRenderer = visualRoot.gameObject.AddComponent<SpriteRenderer>();
            }

            trailRenderer = trailRenderer != null ? trailRenderer : GetComponent<TrailRenderer>();
            if (trailRenderer == null)
            {
                trailRenderer = gameObject.AddComponent<TrailRenderer>();
            }

            EnsureWarningLine();
            EnsureWarningCircle();
        }

        private void EnsureWarningLine()
        {
            if (warningLineRenderer != null)
            {
                return;
            }

            var warningLine = transform.Find("WarningLine");
            if (warningLine == null)
            {
                warningLine = new GameObject("WarningLine").transform;
                warningLine.SetParent(transform);
            }

            warningLine.localPosition = Vector3.zero;
            warningLine.localRotation = Quaternion.identity;
            warningLine.localScale = Vector3.one;

            warningLineRenderer = warningLine.GetComponent<LineRenderer>();
            if (warningLineRenderer == null)
            {
                warningLineRenderer = warningLine.gameObject.AddComponent<LineRenderer>();
            }

            warningLineRenderer.positionCount = 2;
            warningLineRenderer.startWidth = 0.045f;
            warningLineRenderer.endWidth = 0.018f;
            warningLineRenderer.startColor = new Color(1f, 0.1f, 0.08f, 0.86f);
            warningLineRenderer.endColor = new Color(1f, 0.2f, 0.06f, 0.1f);
            warningLineRenderer.material = GetDefaultMaterial();
            warningLineRenderer.sortingOrder = 19;
            warningLineRenderer.enabled = false;
        }

        private void EnsureWarningCircle()
        {
            if (warningCircleRenderer != null)
            {
                return;
            }

            var warningCircle = transform.Find("WarningCircle");
            if (warningCircle == null)
            {
                warningCircle = new GameObject("WarningCircle").transform;
                warningCircle.SetParent(transform);
            }

            warningCircle.localPosition = Vector3.zero;
            warningCircle.localRotation = Quaternion.identity;

            warningCircleRenderer = warningCircle.GetComponent<SpriteRenderer>();
            if (warningCircleRenderer == null)
            {
                warningCircleRenderer = warningCircle.gameObject.AddComponent<SpriteRenderer>();
            }

            warningCircleRenderer.sprite = GetWarningCircleSprite();
            warningCircleRenderer.color = new Color(1f, 0.12f, 0.05f, 0.35f);
            warningCircleRenderer.sortingOrder = 18;
            warningCircleRenderer.enabled = false;
        }

        private void ApplyLayer()
        {
            var layer = LayerMask.NameToLayer("EnemyMissile");
            if (layer < 0)
            {
                layer = LayerMask.NameToLayer("EnemyBullet");
            }

            if (layer < 0)
            {
                return;
            }

            gameObject.layer = layer;
            visualRoot.gameObject.layer = layer;

            if (warningLineRenderer != null)
            {
                warningLineRenderer.gameObject.layer = layer;
            }

            if (warningCircleRenderer != null)
            {
                warningCircleRenderer.gameObject.layer = layer;
            }
        }

        private void ApplyCollider()
        {
            hitbox.radius = Mathf.Max(0.04f, config.radius > 0f ? config.radius : 0.16f);
            hitbox.offset = Vector2.zero;
            hitbox.enabled = true;
        }

        private void ApplyVisual()
        {
            baseColor = ResolveBodyColor();
            bodyRenderer.sprite = LoadConfiguredSprite(config.bodyRes) ?? GetFallbackSprite();
            bodyRenderer.color = baseColor;
            bodyRenderer.sortingOrder = CanBeDestroyed ? 24 : 22;
            bodyRenderer.sharedMaterial = GetDefaultMaterial();

            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
            var diameter = Mathf.Max(0.12f, hitbox.radius * 2.1f);
            var scale = Mathf.Max(0.7f, diameter / BaseSpriteSize);
            visualRoot.localScale = Vector3.one * scale;
            transform.up = direction;
        }

        private void ConfigureTrail()
        {
            var tailColor = ResolveTrailColor();
            trailRenderer.time = CanBeDestroyed ? 0.32f : 0.22f;
            trailRenderer.startWidth = Mathf.Max(0.04f, hitbox.radius * 0.78f);
            trailRenderer.endWidth = 0.01f;
            trailRenderer.startColor = new Color(tailColor.r, tailColor.g, tailColor.b, 0.62f);
            trailRenderer.endColor = new Color(tailColor.r, tailColor.g, tailColor.b, 0f);
            trailRenderer.material = GetDefaultMaterial();
            trailRenderer.sortingOrder = 21;
            trailRenderer.Clear();
        }

        private MissileState ResolveInitialState()
        {
            var behaviorType = GetBehaviorType();
            if (behaviorType == MissileBehaviorType.WeakHoming || behaviorType == MissileBehaviorType.StrongHoming)
            {
                return MissileState.Tracking;
            }

            if (behaviorType == MissileBehaviorType.LockAndDash)
            {
                return MissileState.Locking;
            }

            return MissileState.Flying;
        }

        private void UpdateBehavior(float delta)
        {
            switch (GetBehaviorType())
            {
                case MissileBehaviorType.Accelerate:
                    UpdateAccelerate(delta);
                    break;
                case MissileBehaviorType.WeakHoming:
                    UpdateHoming(delta, false);
                    break;
                case MissileBehaviorType.StrongHoming:
                    UpdateHoming(delta, true);
                    break;
                case MissileBehaviorType.LockAndDash:
                    UpdateLockAndDash(delta);
                    break;
                case MissileBehaviorType.Curve:
                    UpdateCurve(delta);
                    break;
                case MissileBehaviorType.Wave:
                    UpdateWave(delta);
                    break;
                case MissileBehaviorType.Split:
                    UpdateSplit(delta);
                    break;
                case MissileBehaviorType.Explode:
                    UpdateExplode(delta);
                    break;
                case MissileBehaviorType.Carrier:
                    UpdateCarrier(delta);
                    break;
                case MissileBehaviorType.Mine:
                    UpdateMine(delta);
                    break;
                case MissileBehaviorType.Return:
                    UpdateReturn(delta);
                    break;
                default:
                    UpdateStraight(delta);
                    break;
            }
        }

        private void UpdateStraight(float delta)
        {
            Accelerate(delta);
            Move(direction * speed * delta);
        }

        private void UpdateAccelerate(float delta)
        {
            Accelerate(delta);
            Move(direction * speed * delta);
        }

        private void UpdateHoming(float delta, bool strong)
        {
            var trackTime = Mathf.Max(0f, config.trackTime);
            if (config.isLoopTrack || trackTime <= 0f || stateAge <= trackTime)
            {
                var targetDirection = ResolveDirectionToTarget(direction);
                var turnSpeed = config.turnSpeed > 0f ? config.turnSpeed : strong ? 150f : 70f;
                direction = RotateTowards(direction, targetDirection, turnSpeed * delta);
            }
            else if (state == MissileState.Tracking)
            {
                SetState(MissileState.Flying);
            }

            Accelerate(delta);
            Move(direction * speed * delta);
        }

        private void UpdateLockAndDash(float delta)
        {
            if (state == MissileState.Locking)
            {
                Move(direction * speed * 0.25f * delta);

                if (stateAge >= Mathf.Max(0.05f, config.lockDelay))
                {
                    direction = lockedDirection.sqrMagnitude > 0.0001f ? lockedDirection.normalized : direction;
                    speed = config.maxSpeed > 0f ? Mathf.Max(speed, config.maxSpeed) : Mathf.Max(speed, config.speed * 2.4f);
                    SetState(MissileState.Dashing);
                }

                return;
            }

            Accelerate(delta);
            Move(direction * speed * delta);
        }

        private void UpdateCurve(float delta)
        {
            var curveRate = config.turnSpeed > 0f ? config.turnSpeed : 55f;
            direction = Rotate(direction, curveRate * delta);
            Accelerate(delta);
            Move(direction * speed * delta);
        }

        private void UpdateWave(float delta)
        {
            Accelerate(delta);

            var amplitude = config.waveAmplitude > 0f ? config.waveAmplitude : 0.42f;
            var frequency = config.waveFrequency > 0f ? config.waveFrequency : 5.2f;
            var side = new Vector2(-direction.y, direction.x);
            var waveOffset = Mathf.Sin(age * frequency) * amplitude;
            var offsetDelta = waveOffset - lastWaveOffset;
            lastWaveOffset = waveOffset;

            Move(direction * speed * delta + side * offsetDelta);
        }

        private void UpdateSplit(float delta)
        {
            Accelerate(delta);
            Move(direction * speed * delta);

            if (!hasSplit && age >= Mathf.Max(0.05f, config.splitTime))
            {
                SplitNow();
            }
        }

        private void UpdateExplode(float delta)
        {
            Accelerate(delta);
            Move(direction * speed * delta);

            var explodeTime = GetExplodeTime();
            if (explodeTime > 0f && age >= explodeTime)
            {
                Detonate(true);
            }
        }

        private void UpdateCarrier(float delta)
        {
            Accelerate(delta);
            Move(direction * speed * delta);

            var interval = config.releaseInterval > 0f ? config.releaseInterval : Mathf.Max(0.25f, config.splitTime);
            releaseTimer += delta;
            if (releaseTimer >= interval)
            {
                releaseTimer = 0f;
                ReleaseChildren(Mathf.Max(1, config.splitCount), Mathf.Max(0f, config.splitAngle));
            }
        }

        private void UpdateMine(float delta)
        {
            var armTime = config.lockDelay > 0f ? config.lockDelay : Mathf.Max(0.2f, config.splitTime);
            if (age < armTime)
            {
                Accelerate(delta);
                Move(direction * speed * delta);
                return;
            }

            if (state != MissileState.Warning)
            {
                speed = 0f;
                SetState(MissileState.Warning);
            }

            var triggerRadius = GetTriggerRadius();
            var player = ResolvePlayer();
            if (player != null && Vector2.Distance(transform.position, player.transform.position) <= triggerRadius)
            {
                Detonate(true);
                return;
            }

            var explodeTime = GetExplodeTime();
            if (explodeTime > 0f && age >= explodeTime)
            {
                Detonate(true);
            }
        }

        private void UpdateReturn(float delta)
        {
            var returnDelay = config.returnDelay > 0f ? config.returnDelay : Mathf.Max(0.3f, config.trackTime);
            if (!hasReturned && age >= returnDelay)
            {
                hasReturned = true;
                direction = -returnDirection;
            }

            Accelerate(delta);
            Move(direction * speed * delta);
        }

        private void Move(Vector2 displacement)
        {
            transform.position += (Vector3)displacement;

            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.up = direction;
            }
        }

        private void Accelerate(float delta)
        {
            if (config.acceleration <= 0f)
            {
                return;
            }

            var maxSpeed = config.maxSpeed > 0f ? config.maxSpeed : speed + config.acceleration * delta;
            speed = Mathf.Min(maxSpeed, speed + config.acceleration * delta);
        }

        private void SplitNow()
        {
            if (hasSplit)
            {
                return;
            }

            hasSplit = true;
            SetState(MissileState.Splitting);
            ReleaseChildren(Mathf.Max(1, config.splitCount), Mathf.Max(0f, config.splitAngle));
            ExplosionEffect.Spawn(transform.position, 0.55f);
            Recycle();
        }

        private void ReleaseChildren(int count, float spreadAngle)
        {
            if (ConfigManager.Instance == null || !ConfigManager.Instance.IsLoaded || MissileManager.Instance == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(config.childMissileId))
            {
                return;
            }

            var childConfig = ConfigManager.Instance.GetMissile(config.childMissileId);
            if (childConfig == null)
            {
                return;
            }

            if (spreadAngle <= 0f || spreadAngle >= 359.9f)
            {
                var step = 360f / count;
                for (var index = 0; index < count; index++)
                {
                    MissileManager.Instance.Fire(childConfig, transform.position, DirectionFromAngle(step * index));
                }

                return;
            }

            var baseAngle = AngleFromDirection(direction);
            var stepAngle = count > 1 ? spreadAngle / (count - 1) : 0f;
            var startAngle = baseAngle - spreadAngle * 0.5f;
            for (var index = 0; index < count; index++)
            {
                MissileManager.Instance.Fire(childConfig, transform.position, DirectionFromAngle(startAngle + stepAngle * index));
            }
        }

        private void HandlePlayerBulletHit(BulletProjectile bullet)
        {
            if (!CanBeDestroyed)
            {
                return;
            }

            currentHp = Mathf.Max(0, currentHp - Mathf.Max(1, bullet.Damage));
            bullet.RegisterExternalHit();
            bodyRenderer.color = Color.white;

            if (currentHp <= 0)
            {
                ExplosionEffect.Spawn(transform.position, Mathf.Max(0.65f, hitbox.radius * 2.2f));
                Recycle();
            }
        }

        private void HandlePlayerHit(PlayerController player)
        {
            if (player == null || hasExploded)
            {
                return;
            }

            if (GetBehaviorType() == MissileBehaviorType.Explode || config.explodeRadius > hitbox.radius * 1.6f)
            {
                Detonate(true);
                return;
            }

            player.TakeDamage(Damage);
            ExplosionEffect.Spawn(transform.position, Mathf.Max(0.45f, hitbox.radius * 2f));
            Recycle();
        }

        private void Detonate(bool damagePlayers)
        {
            if (hasExploded)
            {
                return;
            }

            hasExploded = true;
            SetState(MissileState.Exploding);
            HideWarnings();

            var radius = GetExplosionRadius();
            ExplosionEffect.Spawn(transform.position, Mathf.Max(0.65f, radius * 1.15f));

            if (damagePlayers)
            {
                DamagePlayersInRadius(radius);
            }

            Recycle();
        }

        private void DamagePlayersInRadius(float radius)
        {
            var colliders = Physics2D.OverlapCircleAll(transform.position, Mathf.Max(0.01f, radius));
            var damagedPlayers = new HashSet<PlayerController>();

            foreach (var collider in colliders)
            {
                var player = collider != null ? collider.GetComponentInParent<PlayerController>() : null;
                if (player == null || damagedPlayers.Contains(player))
                {
                    continue;
                }

                damagedPlayers.Add(player);
                player.TakeDamage(Damage);
            }
        }

        private void UpdateWarningVisuals()
        {
            if (state == MissileState.Locking)
            {
                UpdateWarningLine();
                SetWarningCircleVisible(false);
                return;
            }

            var shouldShowCircle = ShouldShowExplosionWarning();
            SetWarningCircleVisible(shouldShowCircle);

            if (warningLineRenderer != null)
            {
                warningLineRenderer.enabled = false;
            }
        }

        private void UpdateWarningLine()
        {
            if (warningLineRenderer == null)
            {
                return;
            }

            warningLineRenderer.enabled = true;
            var start = transform.position;
            var end = start + (Vector3)(lockedDirection.normalized * 8f);
            warningLineRenderer.SetPosition(0, start);
            warningLineRenderer.SetPosition(1, end);
        }

        private void SetWarningCircleVisible(bool visible)
        {
            if (warningCircleRenderer == null)
            {
                return;
            }

            warningCircleRenderer.enabled = visible;
            if (!visible)
            {
                return;
            }

            var diameter = GetExplosionRadius() * 2f;
            var spriteWorldSize = 64f / SpritePixelsPerUnit;
            warningCircleRenderer.transform.localScale = Vector3.one * Mathf.Max(0.1f, diameter / spriteWorldSize);
            var pulse = 0.25f + Mathf.PingPong(Time.time * 4.5f, 0.2f);
            warningCircleRenderer.color = new Color(1f, 0.12f, 0.05f, pulse);
        }

        private void HideWarnings()
        {
            if (warningLineRenderer != null)
            {
                warningLineRenderer.enabled = false;
            }

            if (warningCircleRenderer != null)
            {
                warningCircleRenderer.enabled = false;
            }
        }

        private void UpdateVisualState()
        {
            if (bodyRenderer == null)
            {
                return;
            }

            var color = baseColor;

            if (CanBeDestroyed && currentHp <= Mathf.Max(1, config.hp) * 0.35f)
            {
                color = Color.Lerp(baseColor, new Color(0.42f, 0.42f, 0.42f, 1f), 0.35f);
            }

            if (state == MissileState.Locking || state == MissileState.Warning || ShouldShowExplosionWarning())
            {
                var pulse = Mathf.PingPong(Time.time * 7f, 1f);
                color = Color.Lerp(color, Color.white, pulse * 0.55f);
            }

            bodyRenderer.color = color;
        }

        private bool ShouldShowExplosionWarning()
        {
            var behavior = GetBehaviorType();
            if (behavior != MissileBehaviorType.Explode && behavior != MissileBehaviorType.Mine)
            {
                return false;
            }

            var warningTime = Mathf.Max(0f, config.warningTime);
            if (warningTime <= 0f)
            {
                return state == MissileState.Warning;
            }

            var explodeTime = GetExplodeTime();
            return state == MissileState.Warning || explodeTime > 0f && explodeTime - age <= warningTime;
        }

        private bool ShouldExplodeOnLifetime()
        {
            var behavior = GetBehaviorType();
            return behavior == MissileBehaviorType.Explode || behavior == MissileBehaviorType.Mine || config.explodeRadius > 0f;
        }

        private MissileBehaviorType GetBehaviorType()
        {
            if (config == null || !Enum.IsDefined(typeof(MissileBehaviorType), config.behaviorType))
            {
                return MissileBehaviorType.Straight;
            }

            return (MissileBehaviorType)config.behaviorType;
        }

        private float GetLifetime()
        {
            return Mathf.Max(0.05f, config != null && config.lifeTime > 0f ? config.lifeTime : 5f);
        }

        private float GetExplodeTime()
        {
            if (config == null)
            {
                return 0f;
            }

            return config.explodeTime > 0f ? config.explodeTime : GetLifetime();
        }

        private float GetExplosionRadius()
        {
            return Mathf.Max(hitbox != null ? hitbox.radius : 0.16f, config != null && config.explodeRadius > 0f ? config.explodeRadius : 0.42f);
        }

        private float GetTriggerRadius()
        {
            return Mathf.Max(GetExplosionRadius(), config != null && config.triggerRadius > 0f ? config.triggerRadius : 0.8f);
        }

        private Vector2 ResolveDirectionToTarget(Vector2 fallback)
        {
            var player = ResolvePlayer();
            if (player == null)
            {
                return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector2.down;
            }

            var targetDirection = (Vector2)(player.transform.position - transform.position);
            return targetDirection.sqrMagnitude > 0.0001f ? targetDirection.normalized : fallback.normalized;
        }

        private PlayerController ResolvePlayer()
        {
            if (target != null)
            {
                return target.GetComponent<PlayerController>();
            }

            var player = FindObjectOfType<PlayerController>();
            target = player != null ? player.transform : null;
            return player;
        }

        private void SetState(MissileState nextState)
        {
            if (state == nextState)
            {
                return;
            }

            state = nextState;
            stateAge = 0f;
        }

        private bool IsOutsideCameraBounds()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return false;
            }

            var viewport = camera.WorldToViewportPoint(transform.position);
            return viewport.x < -0.2f || viewport.x > 1.2f || viewport.y < -0.2f || viewport.y > 1.2f;
        }

        private void Recycle()
        {
            if (!isActiveMissile)
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

        private Color ResolveBodyColor()
        {
            if (CanBeDestroyed)
            {
                return new Color(1f, 0.48f, 0.12f, 1f);
            }

            switch (GetBehaviorType())
            {
                case MissileBehaviorType.WeakHoming:
                case MissileBehaviorType.StrongHoming:
                    return new Color(1f, 0.12f, 0.36f, 1f);
                case MissileBehaviorType.LockAndDash:
                    return new Color(1f, 0.92f, 0.95f, 1f);
                case MissileBehaviorType.Split:
                case MissileBehaviorType.Carrier:
                    return new Color(0.95f, 0.2f, 1f, 1f);
                case MissileBehaviorType.Explode:
                case MissileBehaviorType.Mine:
                    return new Color(1f, 0.2f, 0.08f, 1f);
                case MissileBehaviorType.Wave:
                case MissileBehaviorType.Curve:
                    return new Color(0.28f, 0.9f, 1f, 1f);
                default:
                    return new Color(1f, 0.76f, 0.16f, 1f);
            }
        }

        private Color ResolveTrailColor()
        {
            if (!string.IsNullOrEmpty(config.tailColor) && ColorUtility.TryParseHtmlString(config.tailColor, out var parsed))
            {
                return parsed;
            }

            return ResolveBodyColor();
        }

        private Sprite GetFallbackSprite()
        {
            if (CanBeDestroyed)
            {
                if (fallbackHeavyMissileSprite == null)
                {
                    fallbackHeavyMissileSprite = CreateMissileSprite(true);
                }

                return fallbackHeavyMissileSprite;
            }

            if (fallbackMissileSprite == null)
            {
                fallbackMissileSprite = CreateMissileSprite(false);
            }

            return fallbackMissileSprite;
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

        private static Material GetDefaultMaterial()
        {
            if (defaultMaterial == null)
            {
                defaultMaterial = new Material(Shader.Find("Sprites/Default"));
            }

            return defaultMaterial;
        }

        private static Sprite GetWarningCircleSprite()
        {
            if (warningCircleSprite == null)
            {
                warningCircleSprite = CreateWarningCircleSprite();
            }

            return warningCircleSprite;
        }

        private static Sprite CreateMissileSprite(bool heavy)
        {
            var width = heavy ? 24 : 16;
            var height = heavy ? 44 : 32;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;

            var clear = new Color(0f, 0f, 0f, 0f);
            var body = Color.white;
            var shade = new Color(0.58f, 0.58f, 0.62f, 1f);
            var core = new Color(1f, 0.88f, 0.18f, 1f);
            var flame = new Color(1f, 0.22f, 0.04f, 1f);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    texture.SetPixel(x, y, clear);
                }
            }

            var center = (width - 1) * 0.5f;
            for (var y = 4; y < height - 2; y++)
            {
                var t = y / (float)(height - 1);
                var halfWidth = Mathf.Lerp(width * 0.18f, width * 0.36f, Mathf.Sin(t * Mathf.PI));
                if (y > height - 9)
                {
                    halfWidth *= 0.72f;
                }

                for (var x = 0; x < width; x++)
                {
                    if (Mathf.Abs(x - center) <= halfWidth)
                    {
                        texture.SetPixel(x, y, x < center ? shade : body);
                    }
                }
            }

            for (var y = 0; y < 5; y++)
            {
                var halfWidth = Mathf.Max(1f, width * 0.18f - y * 0.18f);
                for (var x = 0; x < width; x++)
                {
                    if (Mathf.Abs(x - center) <= halfWidth)
                    {
                        texture.SetPixel(x, y, core);
                    }
                }
            }

            for (var y = height - 8; y < height; y++)
            {
                var halfWidth = Mathf.Lerp(width * 0.08f, width * 0.28f, (y - (height - 8)) / 8f);
                for (var x = 0; x < width; x++)
                {
                    if (Mathf.Abs(x - center) <= halfWidth)
                    {
                        texture.SetPixel(x, y, flame);
                    }
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), SpritePixelsPerUnit);
        }

        private static Sprite CreateWarningCircleSprite()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            var clear = new Color(0f, 0f, 0f, 0f);
            var ring = Color.white;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x, y), center) / (size * 0.5f);
                    var isRing = distance > 0.82f && distance < 0.96f;
                    texture.SetPixel(x, y, isRing ? ring : clear);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), SpritePixelsPerUnit);
        }

        private static Vector2 RotateTowards(Vector2 current, Vector2 target, float maxDegreesDelta)
        {
            if (current.sqrMagnitude <= 0.0001f)
            {
                return target.sqrMagnitude > 0.0001f ? target.normalized : Vector2.down;
            }

            if (target.sqrMagnitude <= 0.0001f)
            {
                return current.normalized;
            }

            var angle = Vector2.SignedAngle(current, target);
            var clamped = Mathf.Clamp(angle, -Mathf.Abs(maxDegreesDelta), Mathf.Abs(maxDegreesDelta));
            return Rotate(current, clamped);
        }

        private static Vector2 Rotate(Vector2 vector, float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            var sin = Mathf.Sin(radians);
            var cos = Mathf.Cos(radians);
            return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos).normalized;
        }

        private static Vector2 DirectionFromAngle(float angle)
        {
            var radians = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
        }

        private static float AngleFromDirection(Vector2 vector)
        {
            return Mathf.Atan2(vector.y, vector.x) * Mathf.Rad2Deg;
        }
    }
}
