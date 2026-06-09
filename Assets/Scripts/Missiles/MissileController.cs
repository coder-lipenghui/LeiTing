using System;
using System.Collections;
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
        private static Material smokeTrailMaterial;
        private static Texture2D smokeTrailTexture;
        private static readonly Dictionary<string, Sprite> configuredSprites = new Dictionary<string, Sprite>();

        private MissileManager manager;
        private MissileConfig config;
        private Rigidbody2D body;
        private CircleCollider2D hitbox;
        private Transform visualRoot;
        private MissileVisualEffects visualEffects;
        private Transform lightTrailRoot;
        private Transform smokeTrailRoot;
        private SpriteRenderer bodyRenderer;
        private TrailRenderer rootTrailRenderer;
        private TrailRenderer trailRenderer;
        private ParticleSystem smokeTrail;
        private ParticleSystemRenderer smokeTrailRenderer;
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
        private bool isWaitingForPool;
        private bool hasSplit;
        private bool hasReturned;
        private bool hasExploded;
        private Coroutine recycleCoroutine;

        public int Damage => Mathf.Max(1, config != null ? config.damage : 1);
        public bool CanBeDestroyed => config != null && config.canBeDestroyed;
        public MissileState State => state;

        public void Activate(MissileConfig missileConfig, Vector2 fireDirection, MissileManager owningManager, bool skipLockDelay = false)
        {
            if (missileConfig == null)
            {
                return;
            }

            EnsureComponents();

            if (recycleCoroutine != null)
            {
                StopCoroutine(recycleCoroutine);
                recycleCoroutine = null;
            }

            config = missileConfig;
            manager = owningManager;
            target = FindObjectOfType<PlayerController>()?.transform;
            direction = fireDirection.sqrMagnitude > 0.0001f ? fireDirection.normalized : Vector2.down;
            lockedDirection = ResolveDirectionToTarget(direction);
            returnDirection = direction;
            speed = Mathf.Max(0f, config.speed);
            var shouldSkipLock = skipLockDelay && GetBehaviorType() == MissileBehaviorType.LockAndDash;
            if (shouldSkipLock)
            {
                direction = lockedDirection.sqrMagnitude > 0.0001f ? lockedDirection.normalized : direction;
                speed = ResolveDashSpeed();
            }

            age = 0f;
            stateAge = 0f;
            lastWaveOffset = 0f;
            releaseTimer = 0f;
            currentHp = Mathf.Max(1, config.hp);
            hasSplit = false;
            hasReturned = false;
            hasExploded = false;
            isWaitingForPool = false;
            isActiveMissile = true;

            ApplyLayer();
            ApplyCollider();
            ApplyVisual();
            ConfigureTrail();

            SetState(ResolveInitialState(shouldSkipLock));
            UpdateWarningVisuals();

            gameObject.SetActive(true);
            PlayTrail();
        }

        public void DeactivateForPool()
        {
            if (recycleCoroutine != null)
            {
                StopCoroutine(recycleCoroutine);
                recycleCoroutine = null;
            }

            isActiveMissile = false;
            isWaitingForPool = false;
            state = MissileState.Dead;
            HideWarnings();

            if (hitbox != null)
            {
                hitbox.enabled = false;
            }

            if (bodyRenderer != null)
            {
                bodyRenderer.enabled = false;
            }

            if (visualEffects != null)
            {
                visualEffects.StopAndClear();
            }

            if (trailRenderer != null)
            {
                trailRenderer.Clear();
            }

            if (rootTrailRenderer != null)
            {
                rootTrailRenderer.Clear();
            }

            if (smokeTrail != null)
            {
                smokeTrail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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

            rootTrailRenderer = rootTrailRenderer != null ? rootTrailRenderer : GetComponent<TrailRenderer>();
            if (rootTrailRenderer != null)
            {
                rootTrailRenderer.enabled = false;
                rootTrailRenderer.Clear();
            }

            visualEffects = visualEffects != null ? visualEffects : GetComponent<MissileVisualEffects>();
            if (visualEffects == null)
            {
                visualEffects = gameObject.AddComponent<MissileVisualEffects>();
            }

            visualEffects.EnsureEffectObjects();
            lightTrailRoot = visualEffects.LightTrailRoot;
            trailRenderer = visualEffects.LightTrail;
            smokeTrailRoot = visualEffects.SmokeTrailRoot;
            smokeTrail = visualEffects.SmokeTrail;
            smokeTrailRenderer = visualEffects.SmokeTrailRenderer;

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

            if (visualEffects != null)
            {
                visualEffects.SetLayer(layer);
            }

            if (lightTrailRoot != null)
            {
                lightTrailRoot.gameObject.layer = layer;
            }

            if (smokeTrailRoot != null)
            {
                smokeTrailRoot.gameObject.layer = layer;
            }

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
            bodyRenderer.sprite = LoadConfiguredSprite(config.bodyRes) ?? GetFallbackSprite();
            bodyRenderer.enabled = true;
            baseColor = Color.white;
            bodyRenderer.color = Color.white;
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
            if (visualEffects != null)
            {
                visualEffects.Apply(new MissileVisualEffectContext
                {
                    Radius = hitbox != null ? hitbox.radius : 0.16f,
                    CanBeDestroyed = CanBeDestroyed,
                    TailColor = ResolveTrailColor(),
                    TailType = config != null ? config.tailType : string.Empty,
                    Time = Time.time
                });

                lightTrailRoot = visualEffects.LightTrailRoot;
                trailRenderer = visualEffects.LightTrail;
                smokeTrailRoot = visualEffects.SmokeTrailRoot;
                smokeTrail = visualEffects.SmokeTrail;
                smokeTrailRenderer = visualEffects.SmokeTrailRenderer;
                return;
            }

            var trailStyle = ResolveTrailStyle();
            var tailColor = ResolveTrailColor();

            ConfigureLightTrail(trailStyle == MissileTrailStyle.Light, tailColor);
            ConfigureSmokeTrail(trailStyle == MissileTrailStyle.Smoke);
        }

        private void ConfigureLightTrail(bool enabled, Color tailColor)
        {
            if (trailRenderer == null)
            {
                return;
            }

            trailRenderer.enabled = enabled;
            lightTrailRoot.localPosition = Vector3.down * Mathf.Max(0.05f, hitbox.radius * 0.85f);
            lightTrailRoot.localRotation = Quaternion.identity;
            lightTrailRoot.localScale = Vector3.one;
            trailRenderer.time = CanBeDestroyed ? 0.42f : 0.28f;
            trailRenderer.startWidth = Mathf.Max(0.04f, hitbox.radius * 0.82f);
            trailRenderer.endWidth = 0.01f;
            trailRenderer.startColor = new Color(tailColor.r, tailColor.g, tailColor.b, 0.7f);
            trailRenderer.endColor = new Color(tailColor.r, tailColor.g, tailColor.b, 0f);
            trailRenderer.material = GetDefaultMaterial();
            trailRenderer.sortingOrder = 21;
            trailRenderer.Clear();
        }

        private void ConfigureSmokeTrail(bool enabled)
        {
            if (smokeTrail == null)
            {
                return;
            }

            smokeTrail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            smokeTrailRoot.localPosition = Vector3.down * Mathf.Max(0.05f, hitbox.radius * 0.8f);
            smokeTrailRoot.localRotation = Quaternion.identity;
            smokeTrailRoot.localScale = Vector3.one;

            var main = smokeTrail.main;
            main.loop = true;
            main.playOnAwake = false;
            main.prewarm = false;
            main.duration = 1f;
            main.startDelay = 0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(CanBeDestroyed ? 0.78f : 0.58f, CanBeDestroyed ? 1.08f : 0.82f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.16f);
            main.startSize = new ParticleSystem.MinMaxCurve(hitbox.radius * 1.45f, hitbox.radius * (CanBeDestroyed ? 2.7f : 2.25f));
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.72f), new Color(0.88f, 0.92f, 0.96f, 0.58f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = CanBeDestroyed ? 96 : 64;

            var emission = smokeTrail.emission;
            emission.enabled = enabled;
            emission.rateOverTime = enabled ? new ParticleSystem.MinMaxCurve(CanBeDestroyed ? 28f : 20f) : new ParticleSystem.MinMaxCurve(0f);

            var shape = smokeTrail.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Mathf.Max(0.015f, hitbox.radius * 0.45f);
            shape.radiusThickness = 1f;

            var velocity = smokeTrail.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.28f, -0.08f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var colorOverLifetime = smokeTrail.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var colorGradient = new Gradient();
            colorGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 1f, 1f), 0f),
                    new GradientColorKey(new Color(0.88f, 0.92f, 0.96f), 0.65f),
                    new GradientColorKey(new Color(0.76f, 0.8f, 0.84f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.76f, 0f),
                    new GradientAlphaKey(0.4f, 0.48f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(colorGradient);

            var sizeOverLifetime = smokeTrail.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.58f, 0.62f),
                new Keyframe(1f, 0.08f)));

            var noise = smokeTrail.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(hitbox.radius * 0.18f, hitbox.radius * 0.34f);
            noise.frequency = 1.6f;
            noise.scrollSpeed = 0.25f;

            if (smokeTrailRenderer != null)
            {
                smokeTrailRenderer.enabled = enabled;
                smokeTrailRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                smokeTrailRenderer.material = GetSmokeTrailMaterial();
                smokeTrailRenderer.sortingOrder = 20;
            }
        }

        private void PlayTrail()
        {
            if (visualEffects != null)
            {
                visualEffects.Play();
                return;
            }

            if (trailRenderer != null)
            {
                trailRenderer.Clear();
            }

            if (smokeTrail == null)
            {
                return;
            }

            if (ResolveTrailStyle() == MissileTrailStyle.Smoke)
            {
                smokeTrail.Play(true);
            }
            else
            {
                smokeTrail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private MissileState ResolveInitialState(bool skipLockDelay = false)
        {
            var behaviorType = GetBehaviorType();
            if (behaviorType == MissileBehaviorType.WeakHoming || behaviorType == MissileBehaviorType.StrongHoming)
            {
                return MissileState.Tracking;
            }

            if (behaviorType == MissileBehaviorType.LockAndDash)
            {
                return skipLockDelay ? MissileState.Dashing : MissileState.Locking;
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
                    speed = ResolveDashSpeed();
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

        private float ResolveDashSpeed()
        {
            return config.maxSpeed > 0f ? Mathf.Max(speed, config.maxSpeed) : Mathf.Max(speed, config.speed * 2.4f);
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

            bodyRenderer.color = baseColor;

            if (visualEffects != null)
            {
                visualEffects.UpdateDynamic(Time.time);
            }
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

        public void RecycleToPool()
        {
            if (!isActiveMissile || isWaitingForPool)
            {
                return;
            }

            isActiveMissile = false;
            isWaitingForPool = true;
            state = MissileState.Dead;
            HideWarnings();

            if (hitbox != null)
            {
                hitbox.enabled = false;
            }

            if (bodyRenderer != null)
            {
                bodyRenderer.enabled = false;
            }

            StopTrailForRecycle();

            var releaseDuration = visualEffects != null ? visualEffects.ReleaseDuration : 0f;
            if (gameObject.activeInHierarchy && releaseDuration > 0.01f)
            {
                recycleCoroutine = StartCoroutine(CompleteRecycleAfterDelay(releaseDuration));
                return;
            }

            CompleteRecycleNow();
        }

        private IEnumerator CompleteRecycleAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            recycleCoroutine = null;
            CompleteRecycleNow();
        }

        private void CompleteRecycleNow()
        {
            isWaitingForPool = false;

            if (manager != null)
            {
                manager.CompleteRecycle(this);
                return;
            }

            DeactivateForPool();
        }

        private void StopTrailForRecycle()
        {
            if (visualEffects != null)
            {
                visualEffects.StopTrail();
            }

            if (rootTrailRenderer != null)
            {
                rootTrailRenderer.emitting = false;
            }

            if (trailRenderer != null)
            {
                trailRenderer.emitting = false;
            }

            if (smokeTrail != null && visualEffects == null)
            {
                smokeTrail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void Recycle()
        {
            RecycleToPool();
        }

        private Color ResolveTrailColor()
        {
            if (!string.IsNullOrEmpty(config.tailColor) && ColorUtility.TryParseHtmlString(config.tailColor, out var parsed))
            {
                return parsed;
            }

            return Color.white;
        }

        private MissileTrailStyle ResolveTrailStyle()
        {
            if (config == null || string.IsNullOrEmpty(config.tailType))
            {
                return MissileTrailStyle.Light;
            }

            if (string.Equals(config.tailType, "smoke", StringComparison.OrdinalIgnoreCase)
                || string.Equals(config.tailType, "exhaust", StringComparison.OrdinalIgnoreCase)
                || string.Equals(config.tailType, "fire_smoke", StringComparison.OrdinalIgnoreCase))
            {
                return MissileTrailStyle.Smoke;
            }

            return MissileTrailStyle.Light;
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

            return RuntimeAssetCatalog.LoadSprite(spritePath)
                ?? Resources.Load<Sprite>(NormalizeResourcesPath(spritePath));
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
                defaultMaterial = SpriteMaterialUtility.CreateSpriteMaterial("Missile Sprite Material");
            }

            return defaultMaterial;
        }

        private static Material GetSmokeTrailMaterial()
        {
            if (smokeTrailMaterial == null)
            {
                smokeTrailMaterial = SpriteMaterialUtility.CreateSpriteMaterial("Missile Smoke Trail Material", GetSmokeTrailTexture());
                if (smokeTrailMaterial != null && smokeTrailMaterial.HasProperty("_Color"))
                {
                    smokeTrailMaterial.SetColor("_Color", Color.white);
                }
            }

            return smokeTrailMaterial;
        }

        private static Texture2D GetSmokeTrailTexture()
        {
            if (smokeTrailTexture == null)
            {
                smokeTrailTexture = CreateSmokeTrailTexture();
            }

            return smokeTrailTexture;
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

        private static Texture2D CreateSmokeTrailTexture()
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
                    var alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2.1f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return texture;
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

        private enum MissileTrailStyle
        {
            Light,
            Smoke
        }
    }
}
