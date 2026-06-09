using System;
using System.Collections.Generic;
using System.Globalization;
using LeiTing.Bullets;
using LeiTing.Audio;
using LeiTing.Config;
using LeiTing.Core;
using LeiTing.Effects;
using LeiTing.Enemy.Movement;
using LeiTing.Missiles;
using LeiTing.Pickups;
using LeiTing.Player;
using LeiTing.Progress;
using LeiTing.UI;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace LeiTing.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class EnemyController : MonoBehaviour
    {
        private const float DespawnY = -6.4f;
        private const float EntryStopY = 2.6f;

        private static Material hitFlashMaterial;

        private sealed class ScheduledPattern
        {
            public string patternId;
            public float interval;
            public float nextFireTime;
            public float endFireTime = float.PositiveInfinity;
        }

        [SerializeField] private EnemyConfig config;
        [SerializeField] private int currentHp;

        private Rigidbody2D body;
        private CircleCollider2D hitbox;
        private SpriteRenderer spriteRenderer;
        private SpriteRenderer flashRenderer;
        private AircraftWingTrailEffect wingTrailEffect;
        private ActorMounts mounts;
        private readonly List<ScheduledPattern> scheduledPatterns = new List<ScheduledPattern>();
        private readonly List<Vector2> configuredCurvePoints = new List<Vector2>();
        private Color originalColor = Color.white;
        private Vector3 spawnPosition;
        private Vector3 flashBaseLocalScale = Vector3.one;
        private string attackPatternId;
        private string movementPath;
        private float pathAmplitude;
        private float pathSpeed;
        private float holdDuration;
        private float holdStartedAt;
        private float aliveTime;
        private float nextAttackTime;
        private float flashUntil;
        private OrbitMovement orbitMovement;
        private bool usesOrbitMovement;
        private bool usesConfiguredCurvePath;
        private bool configuredCurveDestroyOnComplete = true;
        private bool configuredCurveCompleted;
        private bool configuredCurveUsesSpline;
        private SplineContainer configuredSplineContainer;
        private Spline configuredInlineSpline;
        private string configuredSplinePathId;
        private string configuredSplineId;
        private int configuredSplineIndex = -1;
        private Vector2 configuredSplineOffset;
        private bool configuredSplineMirrorX;
        private bool configuredSplineMirrorY;
        private float configuredSplinePauseAtT = -1f;
        private float configuredSplineEntryDuration;
        private float configuredSplinePauseDuration;
        private float configuredSplineExitDuration;
        private bool configuredRotateToPath;
        private bool configuredFireOnce;
        private float configuredCurveDuration = 4f;
        private float configuredRotationOffset = -90f;
        private float configuredFireOnceDelay = 0.65f;
        private bool hasFiredEntryShot;
        private bool hasFiredConfiguredOnce;
        private bool useChildHitboxes;
        private bool isDead;

        public void Initialize(EnemyConfig enemyConfig, Vector2 position)
        {
            Initialize(enemyConfig, position, null);
        }

        public void Initialize(EnemyConfig enemyConfig, Vector2 position, WaveSpawnConfig spawnConfig)
        {
            EnsureComponents();

            config = enemyConfig;
            currentHp = Mathf.Max(1, config != null ? config.hp : 1);
            spawnPosition = position;
            attackPatternId = spawnConfig != null && !string.IsNullOrEmpty(spawnConfig.attackPatternId)
                ? spawnConfig.attackPatternId
                : config?.bulletPatternId;
            movementPath = spawnConfig != null ? spawnConfig.movementPath : string.Empty;
            pathAmplitude = spawnConfig != null ? spawnConfig.pathAmplitude : 0f;
            pathSpeed = spawnConfig != null ? spawnConfig.pathSpeed : 0f;
            holdDuration = spawnConfig != null ? spawnConfig.holdDuration : 0f;
            holdStartedAt = -1f;
            transform.position = position;
            wingTrailEffect?.ResetTrailsForTeleport();
            aliveTime = 0f;
            nextAttackTime = Time.time + GetAttackInterval();
            flashUntil = 0f;
            hasFiredEntryShot = false;
            hasFiredConfiguredOnce = false;
            ConfigureScheduledPatterns(attackPatternId);
            isDead = false;
            gameObject.SetActive(true);

            ApplyLayer();
            ApplyVisual();
            ConfigureRootHitbox();
            ConfigureMovementBehavior(spawnConfig);
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
                UpdateFlash();
                return;
            }

            aliveTime += Time.deltaTime;
            UpdateMovement();
            UpdateAttack();
            UpdateFlash();

            if (!usesOrbitMovement && !usesConfiguredCurvePath && transform.position.y < DespawnY)
            {
                Destroy(gameObject);
            }
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
            hitbox.radius = 0.32f;

            spriteRenderer = spriteRenderer != null ? spriteRenderer : ResolveSpriteRenderer();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            if (flashRenderer == null)
            {
                var flashObject = new GameObject("HitFlash");
                flashObject.transform.SetParent(transform, false);
                flashRenderer = flashObject.AddComponent<SpriteRenderer>();
                flashRenderer.color = new Color(1f, 1f, 1f, 0f);
                flashRenderer.sharedMaterial = GetHitFlashMaterial();
                flashRenderer.sortingOrder = 16;
            }

            mounts = mounts != null ? mounts : GetComponent<ActorMounts>();
            if (mounts == null)
            {
                mounts = gameObject.AddComponent<ActorMounts>();
            }

            wingTrailEffect = wingTrailEffect != null ? wingTrailEffect : GetComponent<AircraftWingTrailEffect>();
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
            spriteRenderer.sprite = spriteRenderer.sprite != null ? spriteRenderer.sprite : LoadEnemySprite() ?? CreateFallbackEnemySprite();
            spriteRenderer.flipY = false;
            spriteRenderer.sortingOrder = 15;
            SpriteMaterialUtility.EnsureUsableSpriteMaterial(spriteRenderer);
            originalColor = Color.white;
            spriteRenderer.color = originalColor;

            if (flashRenderer != null)
            {
                flashRenderer.sprite = spriteRenderer.sprite;
                flashRenderer.flipY = false;
                flashRenderer.color = new Color(1f, 1f, 1f, 0f);
                flashRenderer.sharedMaterial = GetHitFlashMaterial();
                SyncFlashRendererTransform();
            }
        }

        private Sprite LoadEnemySprite()
        {
            const string enemySpritePath = "Assets/Art/Animations/Enemies/enemy-01.png";

#if UNITY_EDITOR
            if (RuntimeRemoteResourceManager.CanUseEditorLocalAssets)
            {
                var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(enemySpritePath);
                if (sprite != null)
                {
                    return sprite;
                }
            }
#endif
            return RuntimeAssetCatalog.LoadSprite(enemySpritePath)
                ?? Resources.Load<Sprite>("Enemies/enemy-01");
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

        private void UpdateMovement()
        {
            if (usesOrbitMovement)
            {
                if (orbitMovement != null && orbitMovement.IsActive)
                {
                    orbitMovement.Tick(Time.deltaTime);
                }

                return;
            }

            if (!string.IsNullOrEmpty(movementPath))
            {
                UpdateConfiguredMovement();
                return;
            }

            var position = transform.position;
            var speed = GetMoveSpeed();

            if (IsEnemyType("enemy_b") || IsEnemyType("enemy_e"))
            {
                if (position.y > EntryStopY)
                {
                    position += Vector3.down * speed * Time.deltaTime;
                }
                else if (aliveTime > 3.2f)
                {
                    position += Vector3.down * speed * 1.25f * Time.deltaTime;
                }
            }
            else if (IsEnemyType("enemy_c"))
            {
                position.y -= speed * Time.deltaTime;
                position.x = spawnPosition.x + Mathf.Sin(aliveTime * 3.2f) * 1.1f;
            }
            else
            {
                position += Vector3.down * speed * Time.deltaTime;
            }

            transform.position = position;
        }

        private void ConfigureMovementBehavior(WaveSpawnConfig spawnConfig)
        {
            usesOrbitMovement = false;
            usesConfiguredCurvePath = false;
            configuredCurveCompleted = false;
            configuredCurveDestroyOnComplete = true;
            configuredCurveUsesSpline = false;
            configuredCurveDuration = 4f;
            configuredSplineContainer = null;
            configuredInlineSpline = null;
            configuredSplinePathId = string.Empty;
            configuredSplineId = string.Empty;
            configuredSplineIndex = -1;
            configuredSplineOffset = Vector2.zero;
            configuredSplineMirrorX = false;
            configuredSplineMirrorY = false;
            configuredSplinePauseAtT = -1f;
            configuredSplineEntryDuration = 0f;
            configuredSplinePauseDuration = 0f;
            configuredSplineExitDuration = 0f;
            configuredCurvePoints.Clear();
            configuredRotateToPath = false;
            configuredFireOnce = false;
            configuredRotationOffset = -90f;
            configuredFireOnceDelay = 0.65f;

            if (IsCurveMovementPath(movementPath))
            {
                ConfigureConfiguredCurveMovement();

                if (orbitMovement != null)
                {
                    orbitMovement.AutoUpdate = false;
                    orbitMovement.enabled = false;
                }

                return;
            }

            if (!IsOrbitMovementPath(movementPath))
            {
                ConfigureConfiguredMovementRotation();

                if (orbitMovement != null)
                {
                    orbitMovement.AutoUpdate = false;
                    orbitMovement.enabled = false;
                }

                return;
            }

            orbitMovement = orbitMovement != null ? orbitMovement : GetComponent<OrbitMovement>();
            if (orbitMovement == null)
            {
                orbitMovement = gameObject.AddComponent<OrbitMovement>();
            }

            orbitMovement.enabled = true;
            orbitMovement.AutoUpdate = false;
            orbitMovement.Initialize(BuildOrbitMovementConfig(spawnConfig), spawnPosition, GetMoveSpeed());
            usesOrbitMovement = true;
        }

        private void ConfigureConfiguredCurveMovement()
        {
            usesConfiguredCurvePath = true;
            configuredCurveUsesSpline = IsMovementPath(GetMovementPathName(movementPath), "Spline");
            configuredCurveDuration = pathSpeed > 0f ? pathSpeed : 4f;
            if (!configuredCurveUsesSpline)
            {
                configuredCurvePoints.Add(spawnPosition);
            }
            ConfigureConfiguredMovementRotation();

            ForEachInlineMovementParameter(movementPath, (key, value) =>
            {
                switch (key.ToLowerInvariant())
                {
                    case "points":
                        SetConfiguredCurvePoints(value);
                        break;
                    case "path":
                        if (configuredCurveUsesSpline)
                        {
                            configuredSplinePathId = value;
                        }
                        else
                        {
                            SetConfiguredCurvePoints(value);
                        }
                        break;
                    case "spline":
                        configuredSplineId = value;
                        break;
                    case "splineid":
                        configuredSplineId = value;
                        break;
                    case "splineindex":
                    case "index":
                        SetInt(value, result => configuredSplineIndex = Mathf.Max(0, result));
                        break;
                    case "offset":
                        SetVector2(value, result => configuredSplineOffset = result);
                        break;
                    case "offsetx":
                        SetFloat(value, result => configuredSplineOffset.x = result);
                        break;
                    case "offsety":
                        SetFloat(value, result => configuredSplineOffset.y = result);
                        break;
                    case "mirrorx":
                        SetBool(value, result => configuredSplineMirrorX = result);
                        break;
                    case "mirrory":
                        SetBool(value, result => configuredSplineMirrorY = result);
                        break;
                    case "duration":
                        SetFloat(value, result => configuredCurveDuration = Mathf.Max(0.05f, result));
                        break;
                    case "entryduration":
                        SetFloat(value, result => configuredSplineEntryDuration = Mathf.Max(0f, result));
                        break;
                    case "pauseduration":
                    case "splinepauseduration":
                    case "splineholdduration":
                        SetFloat(value, result => configuredSplinePauseDuration = Mathf.Max(0f, result));
                        break;
                    case "exitduration":
                        SetFloat(value, result => configuredSplineExitDuration = Mathf.Max(0f, result));
                        break;
                    case "pauseatt":
                    case "holdatt":
                    case "pauset":
                    case "holdt":
                        SetFloat(value, result => configuredSplinePauseAtT = Mathf.Clamp01(result));
                        break;
                    case "destroy":
                    case "destroyoncomplete":
                        SetBool(value, result => configuredCurveDestroyOnComplete = result);
                        break;
                    case "mode":
                        configuredCurveUsesSpline = string.Equals(value, "Spline", StringComparison.OrdinalIgnoreCase);
                        break;
                }
            });

            configuredSplineContainer = ResolveConfiguredSplineContainer();
            var hasSceneSpline = configuredSplineContainer != null;
            if (hasSceneSpline)
            {
                var splineStartPosition = EvaluateConfiguredCurvePosition(0f);
                spawnPosition = splineStartPosition;
                transform.position = splineStartPosition;
                wingTrailEffect?.ResetTrailsForTeleport();
            }

            if (configuredCurveUsesSpline && !hasSceneSpline)
            {
                Debug.LogWarning(
                    $"Unable to resolve enemy spline path '{configuredSplinePathId}' with spline '{configuredSplineId}'.",
                    this);
            }

            if (!hasSceneSpline && !configuredCurveUsesSpline && configuredCurvePoints.Count == 0)
            {
                configuredCurvePoints.Add(spawnPosition);
            }

            if (!hasSceneSpline
                && !configuredCurveUsesSpline
                && (configuredCurvePoints[0] - (Vector2)spawnPosition).sqrMagnitude > 0.0001f)
            {
                configuredCurvePoints.Insert(0, spawnPosition);
            }

            if (!hasSceneSpline && !configuredCurveUsesSpline && configuredCurvePoints.Count < 2)
            {
                configuredCurvePoints.Add((Vector2)spawnPosition + Vector2.down);
            }

            configuredInlineSpline = null;
        }

        private OrbitMovementConfig BuildOrbitMovementConfig(WaveSpawnConfig spawnConfig)
        {
            if (spawnConfig != null && spawnConfig.orbitMovement != null)
            {
                var configured = spawnConfig.orbitMovement.Clone();
                ApplyInlineOrbitParameters(configured, movementPath);
                return NormalizeOrbitMovementConfig(configured);
            }

            var radius = spawnConfig != null && spawnConfig.pathAmplitude > 0f ? spawnConfig.pathAmplitude : 1.2f;
            var fallback = new OrbitMovementConfig
            {
                centerX = spawnConfig != null ? spawnConfig.startPosition.x : spawnPosition.x,
                centerY = EntryStopY,
                radiusX = radius,
                radiusY = radius * 0.65f,
                angularSpeed = spawnConfig != null && spawnConfig.pathSpeed > 0f ? spawnConfig.pathSpeed : 120f,
                startAngle = 90f,
                orbitDuration = spawnConfig != null && spawnConfig.holdDuration > 0f ? spawnConfig.holdDuration : 3f,
                enterSpeed = GetMoveSpeed(),
                exitSpeed = GetMoveSpeed() * 1.25f,
                exitDirection = OrbitExitDirection.Down,
                rotateToPath = false
            };

            ApplyInlineOrbitParameters(fallback, movementPath);
            return NormalizeOrbitMovementConfig(fallback);
        }

        private OrbitMovementConfig NormalizeOrbitMovementConfig(OrbitMovementConfig orbitConfig)
        {
            orbitConfig.radiusX = orbitConfig.radiusX > 0f ? orbitConfig.radiusX : 1.2f;
            orbitConfig.radiusY = orbitConfig.radiusY > 0f ? orbitConfig.radiusY : orbitConfig.radiusX;
            orbitConfig.angularSpeed = orbitConfig.angularSpeed > 0f ? orbitConfig.angularSpeed : GetMoveSpeed() * 60f;
            orbitConfig.exitSpeed = orbitConfig.exitSpeed > 0f ? orbitConfig.exitSpeed : GetMoveSpeed() * 1.25f;
            return orbitConfig;
        }

        private void ConfigureConfiguredMovementRotation()
        {
            ForEachInlineMovementParameter(movementPath, (key, value) =>
            {
                switch (key.ToLowerInvariant())
                {
                    case "rotate":
                    case "rotatetopath":
                        SetBool(value, result => configuredRotateToPath = result);
                        break;
                    case "rotationoffset":
                        SetFloat(value, result => configuredRotationOffset = result);
                        break;
                    case "fireonce":
                    case "entryshotonce":
                        SetBool(value, result => configuredFireOnce = result);
                        break;
                    case "fireoncedelay":
                    case "attackdelay":
                        SetFloat(value, result => configuredFireOnceDelay = Mathf.Max(0f, result));
                        break;
                }
            });
        }

        private void UpdateConfiguredMovement()
        {
            var previousPosition = transform.position;

            if (usesConfiguredCurvePath)
            {
                UpdateConfiguredCurveMovement(previousPosition);
                return;
            }

            var position = transform.position;
            var speed = GetMoveSpeed();
            var normalizedPath = GetMovementPathName(movementPath);

            if (IsMovementPath(normalizedPath, "Hold") || IsMovementPath(normalizedPath, "StopAndLeave"))
            {
                if (position.y > EntryStopY)
                {
                    position += Vector3.down * speed * Time.deltaTime;
                    if (position.y <= EntryStopY)
                    {
                        position.y = EntryStopY;
                        holdStartedAt = aliveTime;
                    }
                }
                else if (holdStartedAt < 0f)
                {
                    holdStartedAt = aliveTime;
                }
                else if (IsMovementPath(normalizedPath, "StopAndLeave") && aliveTime - holdStartedAt > Mathf.Max(0.5f, holdDuration))
                {
                    position += Vector3.down * speed * 1.25f * Time.deltaTime;
                }

                transform.position = position;
                ApplyConfiguredPathRotation(previousPosition, position);
                return;
            }

            position += Vector3.down * speed * Time.deltaTime;

            if (IsMovementPath(normalizedPath, "Sine"))
            {
                var amplitude = pathAmplitude > 0f ? pathAmplitude : 1.0f;
                var frequency = pathSpeed > 0f ? pathSpeed : 3.0f;
                position.x = spawnPosition.x + Mathf.Sin(aliveTime * frequency) * amplitude;
            }
            else if (IsMovementPath(normalizedPath, "DriftLeft") || IsMovementPath(normalizedPath, "DriftRight"))
            {
                var direction = IsMovementPath(normalizedPath, "DriftLeft") ? -1f : 1f;
                var horizontalSpeed = pathSpeed > 0f ? pathSpeed : 0.45f;
                position.x += direction * horizontalSpeed * Time.deltaTime;
            }

            transform.position = position;
            ApplyConfiguredPathRotation(previousPosition, position);
        }

        private void UpdateConfiguredCurveMovement(Vector2 previousPosition)
        {
            if (configuredCurveCompleted || !HasConfiguredCurveSource())
            {
                return;
            }

            var t = EvaluateConfiguredCurveTime();
            var position = EvaluateConfiguredCurvePosition(t);
            transform.position = position;
            ApplyConfiguredPathRotation(previousPosition, position);

            if (!IsConfiguredCurveComplete())
            {
                return;
            }

            configuredCurveCompleted = true;
            if (configuredCurveDestroyOnComplete)
            {
                Destroy(gameObject);
            }
        }

        private SplineContainer ResolveConfiguredSplineContainer()
        {
            if (!configuredCurveUsesSpline
                || string.IsNullOrWhiteSpace(configuredSplinePathId)
                || string.IsNullOrWhiteSpace(configuredSplineId))
            {
                return null;
            }

            return EnemySplinePath.TryResolve(configuredSplinePathId, configuredSplineId, configuredSplineIndex, out var container, out var splineIndex)
                ? ResolveConfiguredSplineContainer(container, splineIndex)
                : null;
        }

        private SplineContainer ResolveConfiguredSplineContainer(SplineContainer container, int splineIndex)
        {
            configuredSplineIndex = splineIndex;
            return container;
        }

        private bool HasConfiguredCurveSource()
        {
            return configuredSplineContainer != null || configuredInlineSpline != null || configuredCurvePoints.Count > 0;
        }

        private float EvaluateConfiguredCurveTime()
        {
            if (!configuredCurveUsesSpline || configuredSplinePauseDuration <= 0f)
            {
                return configuredCurveDuration <= 0f ? 1f : Mathf.Clamp01(aliveTime / configuredCurveDuration);
            }

            var pauseAtT = configuredSplinePauseAtT >= 0f ? configuredSplinePauseAtT : 0.5f;
            var entryDuration = configuredSplineEntryDuration > 0f ? configuredSplineEntryDuration : Mathf.Max(0.05f, configuredCurveDuration * pauseAtT);
            var exitDuration = configuredSplineExitDuration > 0f ? configuredSplineExitDuration : Mathf.Max(0.05f, configuredCurveDuration * (1f - pauseAtT));
            var pauseStart = entryDuration;
            var pauseEnd = pauseStart + configuredSplinePauseDuration;

            if (aliveTime < pauseStart)
            {
                return Mathf.Lerp(0f, pauseAtT, Mathf.Clamp01(aliveTime / entryDuration));
            }

            if (aliveTime < pauseEnd)
            {
                return pauseAtT;
            }

            return Mathf.Lerp(pauseAtT, 1f, Mathf.Clamp01((aliveTime - pauseEnd) / exitDuration));
        }

        private bool IsConfiguredCurveComplete()
        {
            if (!configuredCurveUsesSpline || configuredSplinePauseDuration <= 0f)
            {
                return configuredCurveDuration <= 0f || aliveTime >= configuredCurveDuration;
            }

            var pauseAtT = configuredSplinePauseAtT >= 0f ? configuredSplinePauseAtT : 0.5f;
            var entryDuration = configuredSplineEntryDuration > 0f ? configuredSplineEntryDuration : Mathf.Max(0.05f, configuredCurveDuration * pauseAtT);
            var exitDuration = configuredSplineExitDuration > 0f ? configuredSplineExitDuration : Mathf.Max(0.05f, configuredCurveDuration * (1f - pauseAtT));
            return aliveTime >= entryDuration + configuredSplinePauseDuration + exitDuration;
        }

        private Vector2 EvaluateConfiguredCurvePosition(float t)
        {
            if (configuredSplineContainer != null)
            {
                var position = configuredSplineContainer.EvaluatePosition(configuredSplineIndex, t);
                return ApplyConfiguredSplineTransform(new Vector2(position.x, position.y));
            }

            if (configuredInlineSpline != null)
            {
                var position = SplineUtility.EvaluatePosition(configuredInlineSpline, t);
                return ApplyConfiguredSplineTransform(new Vector2(position.x, position.y));
            }

            var fallbackPosition = configuredCurveUsesSpline
                ? EvaluateSplinePoint(configuredCurvePoints, t)
                : EvaluateBezierPoint(configuredCurvePoints, t);
            return ApplyConfiguredSplineTransform(fallbackPosition);
        }

        private Vector2 ApplyConfiguredSplineTransform(Vector2 position)
        {
            if (configuredSplineMirrorX)
            {
                position.x = -position.x;
            }

            if (configuredSplineMirrorY)
            {
                position.y = -position.y;
            }

            return position + configuredSplineOffset;
        }

        private void ApplyConfiguredPathRotation(Vector2 previousPosition, Vector2 currentPosition)
        {
            if (!configuredRotateToPath)
            {
                return;
            }

            var delta = currentPosition - previousPosition;
            if (delta.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle + configuredRotationOffset);
        }

        private void UpdateAttack()
        {
            if (string.IsNullOrEmpty(config?.bulletId) && string.IsNullOrEmpty(attackPatternId))
            {
                return;
            }

            if (scheduledPatterns.Count > 0)
            {
                UpdateScheduledPatterns();
                return;
            }

            if (configuredFireOnce)
            {
                if (!hasFiredConfiguredOnce && aliveTime >= configuredFireOnceDelay)
                {
                    hasFiredConfiguredOnce = true;
                    FireConfiguredPattern();
                }

                return;
            }

            if (IsEnemyType("enemy_a"))
            {
                if (!hasFiredEntryShot && aliveTime > 0.65f)
                {
                    hasFiredEntryShot = true;
                    FireConfiguredPattern();
                }

                return;
            }

            if (Time.time < nextAttackTime)
            {
                return;
            }

            nextAttackTime = Time.time + GetAttackInterval();

            FireConfiguredPattern();
        }

        private void FireConfiguredPattern()
        {
            if (!string.IsNullOrEmpty(attackPatternId))
            {
                FirePatternId(attackPatternId);
                return;
            }

            FireSingleAtPlayer();
        }

        private void FirePatternId(string patternId)
        {
            if (TryFireBulletPattern(patternId) || TryFireMissilePattern(patternId))
            {
                return;
            }

            FireSingleAtPlayer();
        }

        private void ConfigureScheduledPatterns(string patternSpec)
        {
            scheduledPatterns.Clear();
            if (string.IsNullOrWhiteSpace(patternSpec))
            {
                return;
            }

            var entries = patternSpec.Split(new[] { '|', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawEntry in entries)
            {
                if (!TryParseScheduledPattern(rawEntry.Trim(), out var patternId, out var interval, out var offset, out var duration))
                {
                    continue;
                }

                var startTime = Time.time + offset;
                scheduledPatterns.Add(new ScheduledPattern
                {
                    patternId = patternId,
                    interval = interval,
                    nextFireTime = startTime,
                    endFireTime = duration > 0f ? startTime + duration : float.PositiveInfinity
                });
            }
        }

        private void UpdateScheduledPatterns()
        {
            for (var index = 0; index < scheduledPatterns.Count; index++)
            {
                var scheduled = scheduledPatterns[index];
                if (Time.time < scheduled.nextFireTime)
                {
                    continue;
                }

                if (scheduled.nextFireTime > scheduled.endFireTime)
                {
                    continue;
                }

                FirePatternId(scheduled.patternId);
                scheduled.nextFireTime += scheduled.interval;
            }
        }

        private static bool TryParseScheduledPattern(string rawPatternId, out string patternId, out float interval, out float offset, out float duration)
        {
            patternId = rawPatternId;
            interval = 0f;
            offset = 0f;
            duration = 0f;

            if (string.IsNullOrWhiteSpace(rawPatternId))
            {
                return false;
            }

            var separatorIndex = rawPatternId.IndexOf('@');
            if (separatorIndex < 0 || separatorIndex >= rawPatternId.Length - 1)
            {
                return false;
            }

            patternId = rawPatternId.Substring(0, separatorIndex);
            var scheduleSpec = rawPatternId.Substring(separatorIndex + 1);
            var durationIndex = scheduleSpec.IndexOf('~');
            if (durationIndex >= 0)
            {
                var durationText = durationIndex < scheduleSpec.Length - 1 ? scheduleSpec.Substring(durationIndex + 1) : string.Empty;
                scheduleSpec = scheduleSpec.Substring(0, durationIndex);
                if (!float.TryParse(durationText, NumberStyles.Float, CultureInfo.InvariantCulture, out duration))
                {
                    duration = 0f;
                }

                duration = Mathf.Max(0f, duration);
            }

            var offsetIndex = scheduleSpec.IndexOf('+');
            var intervalText = offsetIndex >= 0 ? scheduleSpec.Substring(0, offsetIndex) : scheduleSpec;
            var offsetText = offsetIndex >= 0 ? scheduleSpec.Substring(offsetIndex + 1) : "0";

            if (!float.TryParse(intervalText, NumberStyles.Float, CultureInfo.InvariantCulture, out interval))
            {
                return false;
            }

            if (!float.TryParse(offsetText, NumberStyles.Float, CultureInfo.InvariantCulture, out offset))
            {
                offset = 0f;
            }

            interval = Mathf.Max(0.05f, interval);
            offset = Mathf.Max(0f, offset);
            return !string.IsNullOrWhiteSpace(patternId);
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

        private void FireSingleAtPlayer()
        {
            var bulletConfig = ResolveBulletConfig();
            if (bulletConfig == null || BulletManager.Instance == null)
            {
                return;
            }

            var direction = ResolveDirectionToPlayer();
            BulletManager.Instance.Fire(bulletConfig, transform.position + Vector3.down * 0.38f, direction);
        }

        private void FireSpreadDown()
        {
            var bulletConfig = ResolveBulletConfig();
            if (bulletConfig == null || BulletManager.Instance == null)
            {
                return;
            }

            for (var index = 0; index < 3; index++)
            {
                var angle = -18f + 18f * index;
                var direction = Rotate(Vector2.down, angle);
                BulletManager.Instance.Fire(bulletConfig, transform.position + Vector3.down * 0.32f, direction);
            }
        }

        private BulletConfig ResolveBulletConfig()
        {
            return ConfigManager.Instance != null && ConfigManager.Instance.IsLoaded ? ConfigManager.Instance.GetBullet(config.bulletId) : null;
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

        private Vector2 ResolveDirectionToPlayer()
        {
            var player = FindObjectOfType<PlayerController>();
            if (player == null)
            {
                return Vector2.down;
            }

            var direction = (Vector2)(player.transform.position - transform.position);
            return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.down;
        }

        public void TakeDamage(int damage)
        {
            if (isDead || damage <= 0)
            {
                return;
            }

            currentHp = Mathf.Max(0, currentHp - damage);
            flashUntil = Time.time + 0.06f;

            if (AudioManager.Instance != null && config != null)
            {
                AudioManager.Instance.PlaySfx(config.hitSoundPath);
            }

            if (currentHp <= 0)
            {
                Die();
            }
        }

        public void KillInstantly()
        {
            if (isDead)
            {
                return;
            }

            currentHp = 0;
            Die();
        }

        private void Die()
        {
            if (isDead)
            {
                return;
            }

            isDead = true;
            ExplosionEffect.Spawn(transform.position, IsEnemyType("enemy_b") ? 1.0f : 0.7f);
            LevelProgressService.RecordEnemyKilled();

            if (GameManager.Instance != null && config != null)
            {
                GameManager.Instance.AddScore(config.score);
            }

            if (config != null)
            {
                PickupManager.GetOrCreate().SpawnDrops(config, transform.position);
            }

            if (UIManager.Instance != null && config != null)
            {
                UIManager.Instance.ShowScorePopup(transform.position, config.score);
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayEnemyDestroyed();
            }

            Destroy(gameObject);
        }

        private void UpdateFlash()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            var isFlashing = Time.time < flashUntil;
            spriteRenderer.color = isFlashing ? Color.white : originalColor;

            if (flashRenderer != null)
            {
                flashRenderer.sprite = spriteRenderer.sprite;
                flashRenderer.color = isFlashing ? new Color(1f, 1f, 1f, 0.72f) : new Color(1f, 1f, 1f, 0f);
                flashRenderer.transform.localScale = flashBaseLocalScale;
            }
        }

        private bool IsEnemyType(string enemyId)
        {
            return string.Equals(config?.id, enemyId, System.StringComparison.OrdinalIgnoreCase);
        }

        private static Material GetHitFlashMaterial()
        {
            if (hitFlashMaterial != null)
            {
                return hitFlashMaterial;
            }

            var shader = Shader.Find("LeiTing/SpriteSilhouette");
            if (shader == null)
            {
                return null;
            }

            hitFlashMaterial = new Material(shader)
            {
                name = "EnemyHitFlashMaterial"
            };
            return hitFlashMaterial;
        }

        private static bool IsMovementPath(string path, string expected)
        {
            return string.Equals(path, expected, System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsOrbitMovementPath(string path)
        {
            var pathName = GetMovementPathName(path);
            return IsMovementPath(pathName, "Orbit")
                || IsMovementPath(pathName, "OrbitMovement")
                || IsMovementPath(pathName, "EnemyOrbitMove");
        }

        private static bool IsCurveMovementPath(string path)
        {
            var pathName = GetMovementPathName(path);
            return IsMovementPath(pathName, "Bezier")
                || IsMovementPath(pathName, "Curve")
                || IsMovementPath(pathName, "Path")
                || IsMovementPath(pathName, "Spline");
        }

        private static string GetMovementPathName(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            var normalized = path.Trim();
            var parameterStart = normalized.IndexOfAny(new[] { ':', '(' });
            return parameterStart >= 0 ? normalized.Substring(0, parameterStart).Trim() : normalized;
        }

        private static void ApplyInlineOrbitParameters(OrbitMovementConfig orbitConfig, string path)
        {
            if (orbitConfig == null || string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            ForEachInlineMovementParameter(path, (key, value) => ApplyInlineOrbitParameter(orbitConfig, key, value));
        }

        private static void ForEachInlineMovementParameter(string path, Action<string, string> apply)
        {
            if (string.IsNullOrWhiteSpace(path) || apply == null)
            {
                return;
            }

            var parameterStart = path.IndexOfAny(new[] { ':', '(' });
            if (parameterStart < 0)
            {
                return;
            }

            var marker = path[parameterStart];
            var body = path.Substring(parameterStart + 1).Trim();
            if (marker == '(' && body.EndsWith(")", StringComparison.Ordinal))
            {
                body = body.Substring(0, body.Length - 1);
            }
            else if (body.StartsWith("(", StringComparison.Ordinal) && body.EndsWith(")", StringComparison.Ordinal))
            {
                body = body.Substring(1, body.Length - 2);
            }

            var pairs = body.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var separator = pair.IndexOf('=');
                if (separator <= 0 || separator >= pair.Length - 1)
                {
                    continue;
                }

                var key = pair.Substring(0, separator).Trim();
                var value = pair.Substring(separator + 1).Trim();
                apply(key, value);
            }
        }

        private static void ApplyInlineOrbitParameter(OrbitMovementConfig orbitConfig, string key, string value)
        {
            switch (key.ToLowerInvariant())
            {
                case "centerx":
                    SetFloat(value, result => orbitConfig.centerX = result);
                    break;
                case "centery":
                    SetFloat(value, result => orbitConfig.centerY = result);
                    break;
                case "radiusx":
                    SetFloat(value, result => orbitConfig.radiusX = result);
                    break;
                case "radiusy":
                    SetFloat(value, result => orbitConfig.radiusY = result);
                    break;
                case "angularspeed":
                    SetFloat(value, result => orbitConfig.angularSpeed = result);
                    break;
                case "clockwise":
                    SetBool(value, result => orbitConfig.clockwise = result);
                    break;
                case "startangle":
                    SetFloat(value, result => orbitConfig.startAngle = result);
                    break;
                case "orbitduration":
                case "duration":
                    SetFloat(value, result => orbitConfig.orbitDuration = result);
                    break;
                case "loopcount":
                    SetFloat(value, result => orbitConfig.loopCount = result);
                    break;
                case "centermovespeedy":
                case "centermovespeed":
                    SetFloat(value, result => orbitConfig.centerMoveSpeedY = result);
                    break;
                case "enterduration":
                    SetFloat(value, result => orbitConfig.enterDuration = result);
                    break;
                case "enterspeed":
                    SetFloat(value, result => orbitConfig.enterSpeed = result);
                    break;
                case "easeoutenter":
                case "easeout":
                    SetBool(value, result => orbitConfig.easeOutEnter = result);
                    break;
                case "exitdirection":
                    SetExitDirection(value, result => orbitConfig.exitDirection = result);
                    break;
                case "exitspeed":
                    SetFloat(value, result => orbitConfig.exitSpeed = result);
                    break;
                case "destroyonexitcomplete":
                    SetBool(value, result => orbitConfig.destroyOnExitComplete = result);
                    break;
                case "exitdespawnpadding":
                case "exitpadding":
                    SetFloat(value, result => orbitConfig.exitDespawnPadding = result);
                    break;
                case "rotate":
                case "rotatetopath":
                    SetBool(value, result => orbitConfig.rotateToPath = result);
                    break;
                case "rotationoffset":
                    SetFloat(value, result => orbitConfig.rotationOffset = result);
                    break;
            }
        }

        private void SetConfiguredCurvePoints(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var parsedPoints = new List<Vector2>();
            var pointValues = value.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var pointValue in pointValues)
            {
                if (TryParseCurvePoint(pointValue, out var point))
                {
                    parsedPoints.Add(point);
                }
            }

            if (parsedPoints.Count == 0)
            {
                return;
            }

            configuredCurvePoints.Clear();
            configuredCurvePoints.AddRange(parsedPoints);
        }

        private static bool TryParseCurvePoint(string value, out Vector2 point)
        {
            point = Vector2.zero;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var components = value.Trim().Split(new[] { '/', '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (components.Length < 2
                || !float.TryParse(components[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                || !float.TryParse(components[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                return false;
            }

            point = new Vector2(x, y);
            return true;
        }

        private static Vector2 EvaluateBezierPoint(IReadOnlyList<Vector2> points, float t)
        {
            if (points == null || points.Count == 0)
            {
                return Vector2.zero;
            }

            if (points.Count == 1)
            {
                return points[0];
            }

            var workingPoints = new Vector2[points.Count];
            for (var index = 0; index < points.Count; index++)
            {
                workingPoints[index] = points[index];
            }

            for (var level = points.Count - 1; level > 0; level--)
            {
                for (var index = 0; index < level; index++)
                {
                    workingPoints[index] = Vector2.LerpUnclamped(workingPoints[index], workingPoints[index + 1], t);
                }
            }

            return workingPoints[0];
        }

        private static Vector2 EvaluateSplinePoint(IReadOnlyList<Vector2> points, float t)
        {
            if (points == null || points.Count == 0)
            {
                return Vector2.zero;
            }

            if (points.Count == 1)
            {
                return points[0];
            }

            var segmentCount = points.Count - 1;
            var scaledT = Mathf.Clamp01(t) * segmentCount;
            var segment = Mathf.Min(Mathf.FloorToInt(scaledT), segmentCount - 1);
            var localT = scaledT - segment;

            var p0 = points[Mathf.Max(segment - 1, 0)];
            var p1 = points[segment];
            var p2 = points[segment + 1];
            var p3 = points[Mathf.Min(segment + 2, points.Count - 1)];

            var localT2 = localT * localT;
            var localT3 = localT2 * localT;
            return 0.5f * (
                (2f * p1)
                + (-p0 + p2) * localT
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * localT2
                + (-p0 + 3f * p1 - 3f * p2 + p3) * localT3);
        }

        private static Spline BuildInlineSpline(IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count == 0)
            {
                return null;
            }

            var knotPositions = new List<float3>(points.Count);
            foreach (var point in points)
            {
                knotPositions.Add(new float3(point.x, point.y, 0f));
            }

            return new Spline(knotPositions, TangentMode.AutoSmooth);
        }

        private static void SetFloat(string value, Action<float> apply)
        {
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            {
                apply(result);
            }
        }

        private static void SetInt(string value, Action<int> apply)
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            {
                apply(result);
            }
        }

        private static void SetVector2(string value, Action<Vector2> apply)
        {
            if (TryParseCurvePoint(value, out var result))
            {
                apply(result);
            }
        }

        private static void SetBool(string value, Action<bool> apply)
        {
            if (bool.TryParse(value, out var result))
            {
                apply(result);
                return;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericResult))
            {
                apply(numericResult != 0);
            }
        }

        private static void SetExitDirection(string value, Action<OrbitExitDirection> apply)
        {
            if (Enum.TryParse(value, true, out OrbitExitDirection result))
            {
                apply(result);
            }
        }

        private float GetMoveSpeed()
        {
            return Mathf.Max(0.1f, config != null ? config.moveSpeed : 2f);
        }

        private float GetAttackInterval()
        {
            return Mathf.Max(0.1f, config != null ? config.attackInterval : 1.5f);
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

        private void SyncFlashRendererTransform()
        {
            if (flashRenderer == null || spriteRenderer == null)
            {
                return;
            }

            var flashTransform = flashRenderer.transform;
            var visualTransform = spriteRenderer.transform;
            flashTransform.localPosition = visualTransform == transform ? Vector3.zero : visualTransform.localPosition;
            flashTransform.localRotation = visualTransform == transform ? Quaternion.identity : visualTransform.localRotation;
            flashBaseLocalScale = visualTransform == transform ? Vector3.one : visualTransform.localScale;
            flashTransform.localScale = flashBaseLocalScale;
        }

        private static Vector2 Rotate(Vector2 vector, float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            var sin = Mathf.Sin(radians);
            var cos = Mathf.Cos(radians);
            return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos).normalized;
        }

        private static Sprite CreateFallbackEnemySprite()
        {
            const int size = 18;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;

            var clear = new Color(0f, 0f, 0f, 0f);
            var body = new Color(1f, 0.32f, 0.18f, 1f);
            var wing = new Color(0.75f, 0.08f, 0.2f, 1f);

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    texture.SetPixel(x, y, clear);
                }
            }

            for (var y = 2; y < size - 1; y++)
            {
                var half = y < 10 ? (10 - y) / 2 + 1 : y / 4;
                for (var x = 8 - half; x <= 9 + half; x++)
                {
                    texture.SetPixel(x, y, body);
                }
            }

            for (var y = 7; y < 13; y++)
            {
                texture.SetPixel(3, y, wing);
                texture.SetPixel(4, y, wing);
                texture.SetPixel(13, y, wing);
                texture.SetPixel(14, y, wing);
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
