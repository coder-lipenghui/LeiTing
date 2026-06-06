using System.Collections.Generic;
using LeiTing.Config;
using LeiTing.Core;
using LeiTing.Effects;
using LeiTing.Audio;
using LeiTing.Enemy;
using LeiTing.Missiles;
using LeiTing.Progress;
using LeiTing.Stage;
using LeiTing.UI;
using TTSDK;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LeiTing.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        private enum TouchInputStrategy
        {
            DouyinEventsPreferred = 0,
            SdkPollingOnly = 1,
            UnityLegacyOnly = 2
        }

        private enum PointerTrackingMode
        {
            PreserveInitialOffset = 0,
            FollowFinger = 1
        }

        private enum ActivePointerSource
        {
            None = 0,
            DouyinEvent = 1,
            SdkTouchPolling = 2,
            UnityTouch = 3,
            SdkMouse = 4,
            UnityMouse = 5
        }

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
        [Header("Pointer Input")]
        [SerializeField] private TouchInputStrategy touchInputStrategy = TouchInputStrategy.DouyinEventsPreferred;
        [SerializeField] private PointerTrackingMode pointerTrackingMode = PointerTrackingMode.PreserveInitialOffset;
        [SerializeField] private bool invertDouyinTouchY;
        [SerializeField] private bool logPointerInputDiagnostics;

        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private Vector3 targetPosition;
        private Vector3 dragPointerStartWorldPosition;
        private Vector3 dragPlayerStartPosition;
        private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
        private bool isPointerDragging;
        private int activeTouchFingerId = -1;
        private ActivePointerSource activePointerSource;
        private float nextPointerDiagnosticTime;
        private int currentHp;
        private int maxHp;
        private int currentShield;
        private int currentStars;
        private int currentCoins;
        private float invincibleUntil;
        private bool isDead;
        private Color originalColor = Color.white;
        private static bool hasLoggedDamageVibrationFailure;
        private static bool hasLoggedSdkInputReadFailure;
        private static bool hasLoggedDouyinTouchRegistrationFailure;

#if UNITY_WEBGL && !UNITY_EDITOR
        private bool areDouyinTouchEventsRegistered;
        private bool hasActiveDouyinTouch;
        private int activeDouyinTouchIdentifier = -1;
        private Vector2 activeDouyinTouchScreenPosition;
#endif

        public int CurrentHp => currentHp;
        public int MaxHp => maxHp;
        public int CurrentShield => currentShield;
        public int CurrentStars => currentStars;
        public int CurrentCoins => currentCoins;
        private bool HasTimedInvincibility => Time.time < invincibleUntil;
        public bool IsInvincible => GameManager.InvincibleModeEnabled || HasTimedInvincibility;
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

            LevelProgressService.RecordPlayerHit();
            BeginInvincible();
            PlayDamageVibration();

            if (currentHp <= 0)
            {
                Die();
            }

            return true;
        }

        private static void PlayDamageVibration()
        {
            if (!GameSettingManager.VibrationEnabled)
            {
                return;
            }

            try
            {
                TT.VibrateShort(new VibrateShortParam());
            }
            catch (System.Exception exception)
            {
                if (!hasLoggedDamageVibrationFailure)
                {
                    hasLoggedDamageVibrationFailure = true;
                    Debug.LogWarning($"Douyin vibration failed: {exception.Message}");
                }
            }
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
            UnityEngine.Input.simulateMouseWithTouches = true;

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

        private void OnEnable()
        {
            RegisterDouyinTouchEvents();
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
            var gameState = GameManager.Instance != null ? GameManager.Instance.CurrentState : GameState.Playing;
            if (!CanProcessGameplayInput(gameState))
            {
                EndPointerDrag(false);
                UpdateInvincibleVisual();
                return;
            }

            if (TryGetDragTargetPosition(out var dragTargetPosition))
            {
                targetPosition = ClampToCameraBounds(dragTargetPosition);
            }

            SetPosition(targetPosition);
            UpdateInvincibleVisual();
        }

        private void OnDisable()
        {
            UnregisterDouyinTouchEvents();
            EndPointerDrag(false);

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

        private bool TryGetDragTargetPosition(out Vector3 worldPosition)
        {
            worldPosition = targetPosition;

            if (gameplayCamera == null)
            {
                EndPointerDrag();
                return false;
            }

            if (TryGetDouyinEventDragTargetPosition(out worldPosition))
            {
                return true;
            }

            if (TryGetSdkTouchDragTargetPosition(out worldPosition))
            {
                return true;
            }

            if (TryGetUnityTouchDragTargetPosition(out worldPosition))
            {
                return true;
            }

            return TryGetMouseDragTargetPosition(out worldPosition);
        }

        private bool TryGetDouyinEventDragTargetPosition(out Vector3 worldPosition)
        {
            worldPosition = targetPosition;

#if UNITY_WEBGL && !UNITY_EDITOR
            if (touchInputStrategy != TouchInputStrategy.DouyinEventsPreferred || !hasActiveDouyinTouch)
            {
                if (activePointerSource == ActivePointerSource.DouyinEvent)
                {
                    EndPointerDrag();
                }

                return false;
            }

            if (activePointerSource != ActivePointerSource.None && activePointerSource != ActivePointerSource.DouyinEvent)
            {
                EndPointerDrag();
            }

            return TryUpdatePointerDrag(
                activeDouyinTouchScreenPosition,
                activeDouyinTouchIdentifier,
                ActivePointerSource.DouyinEvent,
                out worldPosition);
#else
            return false;
#endif
        }

        private bool TryGetSdkTouchDragTargetPosition(out Vector3 worldPosition)
        {
            worldPosition = targetPosition;

#if UNITY_WEBGL && STARK_UNITY_INPUT_OVERRIDE
            if (touchInputStrategy == TouchInputStrategy.UnityLegacyOnly)
            {
                return false;
            }

            try
            {
                if (isPointerDragging && activePointerSource == ActivePointerSource.SdkTouchPolling)
                {
                    for (var i = 0; i < global::Input.touchCount; i++)
                    {
                        var activeTouch = global::Input.GetTouch(i);
                        if (activeTouch.fingerId != activeTouchFingerId)
                        {
                            continue;
                        }

                        if (activeTouch.phase == TouchPhase.Canceled || activeTouch.phase == TouchPhase.Ended)
                        {
                            EndPointerDrag();
                            return false;
                        }

                        return TryUpdatePointerDrag(
                            activeTouch.position,
                            activeTouch.fingerId,
                            ActivePointerSource.SdkTouchPolling,
                            out worldPosition);
                    }

                    EndPointerDrag();
                    return false;
                }

                if (isPointerDragging)
                {
                    return false;
                }

                for (var i = 0; i < global::Input.touchCount; i++)
                {
                    var touch = global::Input.GetTouch(i);
                    if (touch.phase == TouchPhase.Canceled || touch.phase == TouchPhase.Ended)
                    {
                        continue;
                    }

                    return TryUpdatePointerDrag(
                        touch.position,
                        touch.fingerId,
                        ActivePointerSource.SdkTouchPolling,
                        out worldPosition);
                }
            }
            catch (System.Exception exception)
            {
                LogSdkInputReadFailure(exception);
                if (activePointerSource == ActivePointerSource.SdkTouchPolling)
                {
                    EndPointerDrag();
                }
            }

            return false;
#else
            return false;
#endif
        }

        private bool TryGetUnityTouchDragTargetPosition(out Vector3 worldPosition)
        {
            worldPosition = targetPosition;

            if (isPointerDragging && activePointerSource == ActivePointerSource.UnityTouch)
            {
                for (var i = 0; i < UnityEngine.Input.touchCount; i++)
                {
                    var activeTouch = UnityEngine.Input.GetTouch(i);
                    if (activeTouch.fingerId != activeTouchFingerId)
                    {
                        continue;
                    }

                    if (activeTouch.phase == TouchPhase.Canceled || activeTouch.phase == TouchPhase.Ended)
                    {
                        EndPointerDrag();
                        return false;
                    }

                    return TryUpdatePointerDrag(
                        activeTouch.position,
                        activeTouch.fingerId,
                        ActivePointerSource.UnityTouch,
                        out worldPosition);
                }

                EndPointerDrag();
                return false;
            }

            if (isPointerDragging)
            {
                return false;
            }

            for (var i = 0; i < UnityEngine.Input.touchCount; i++)
            {
                var touch = UnityEngine.Input.GetTouch(i);
                if (touch.phase == TouchPhase.Canceled || touch.phase == TouchPhase.Ended)
                {
                    continue;
                }

                return TryUpdatePointerDrag(
                    touch.position,
                    touch.fingerId,
                    ActivePointerSource.UnityTouch,
                    out worldPosition);
            }

            return false;
        }

        private bool TryGetMouseDragTargetPosition(out Vector3 worldPosition)
        {
            worldPosition = targetPosition;

#if UNITY_WEBGL && STARK_UNITY_INPUT_OVERRIDE
            if (touchInputStrategy != TouchInputStrategy.UnityLegacyOnly)
            {
                try
                {
                    if (TryUpdateMouseDrag(
                        global::Input.mousePosition,
                        global::Input.GetMouseButton(0),
                        ActivePointerSource.SdkMouse,
                        out worldPosition))
                    {
                        return true;
                    }
                }
                catch (System.Exception exception)
                {
                    LogSdkInputReadFailure(exception);
                    if (activePointerSource == ActivePointerSource.SdkMouse)
                    {
                        EndPointerDrag();
                    }
                }
            }
#endif

            return TryUpdateMouseDrag(
                UnityEngine.Input.mousePosition,
                UnityEngine.Input.GetMouseButton(0),
                ActivePointerSource.UnityMouse,
                out worldPosition);
        }

        private bool TryUpdateMouseDrag(
            Vector3 screenPosition,
            bool isButtonHeld,
            ActivePointerSource source,
            out Vector3 worldPosition)
        {
            worldPosition = targetPosition;

            if (!isButtonHeld)
            {
                if (activePointerSource == source)
                {
                    EndPointerDrag();
                }

                return false;
            }

            if (isPointerDragging && activePointerSource != source)
            {
                return false;
            }

            return TryUpdatePointerDrag(screenPosition, -1, source, out worldPosition);
        }

        private bool TryUpdatePointerDrag(
            Vector3 screenPosition,
            int touchFingerId,
            ActivePointerSource source,
            out Vector3 worldPosition)
        {
            worldPosition = targetPosition;

            if (!isPointerDragging)
            {
                if (IsPointerOverUi(screenPosition, touchFingerId))
                {
                    return false;
                }

                BeginPointerDrag(screenPosition, touchFingerId, source);
            }

            if (activePointerSource != source)
            {
                return false;
            }

            worldPosition = CalculatePointerTargetPosition(screenPosition);
            if (logPointerInputDiagnostics && Time.unscaledTime >= nextPointerDiagnosticTime)
            {
                nextPointerDiagnosticTime = Time.unscaledTime + 0.5f;
                Debug.Log($"[PlayerInput] Move source={source}, pointer={screenPosition}, target={worldPosition}.");
            }

            return true;
        }

        private void BeginPointerDrag(Vector3 screenPosition, int touchFingerId, ActivePointerSource source)
        {
            dragPointerStartWorldPosition = ScreenToWorldPosition(screenPosition);
            dragPlayerStartPosition = GetCurrentPosition();
            targetPosition = dragPlayerStartPosition;
            activeTouchFingerId = touchFingerId;
            activePointerSource = source;
            isPointerDragging = true;
            nextPointerDiagnosticTime = 0f;
            if (BattleTimeController.Instance != null)
            {
                BattleTimeController.Instance.NotifyPointerDown(this);
            }

            if (logPointerInputDiagnostics)
            {
                Debug.Log(
                    $"[PlayerInput] Begin source={source}, strategy={touchInputStrategy}, tracking={pointerTrackingMode}, "
                    + $"invertDouyinY={invertDouyinTouchY}, pointer={screenPosition}, screen={Screen.width}x{Screen.height}.");
            }
        }

        private void EndPointerDrag(bool notifyBattleTime = true)
        {
            if (logPointerInputDiagnostics && isPointerDragging)
            {
                Debug.Log($"[PlayerInput] End source={activePointerSource}.");
            }

            if (notifyBattleTime && isPointerDragging && BattleTimeController.Instance != null)
            {
                BattleTimeController.Instance.NotifyPointerUp(this);
            }

            isPointerDragging = false;
            activeTouchFingerId = -1;
            activePointerSource = ActivePointerSource.None;
        }

        private Vector3 CalculatePointerTargetPosition(Vector3 screenPosition)
        {
            var pointerPosition = ScreenToWorldPosition(screenPosition);
            if (pointerTrackingMode == PointerTrackingMode.FollowFinger)
            {
                return pointerPosition;
            }

            var worldPosition = dragPlayerStartPosition + (pointerPosition - dragPointerStartWorldPosition);
            worldPosition.z = transform.position.z;
            return worldPosition;
        }

        private Vector3 ScreenToWorldPosition(Vector3 screenPosition)
        {
            screenPosition.z = Mathf.Abs(gameplayCamera.transform.position.z - transform.position.z);
            var worldPosition = gameplayCamera.ScreenToWorldPoint(screenPosition);
            worldPosition.z = transform.position.z;
            return worldPosition;
        }

        private Vector3 GetCurrentPosition()
        {
            if (body != null)
            {
                return new Vector3(body.position.x, body.position.y, transform.position.z);
            }

            return transform.position;
        }

        private bool IsPointerOverUi(Vector2 screenPosition, int pointerId = -1)
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            var eventData = new PointerEventData(EventSystem.current)
            {
                position = screenPosition,
                pointerId = pointerId
            };

            uiRaycastResults.Clear();
            EventSystem.current.RaycastAll(eventData, uiRaycastResults);
            foreach (var hit in uiRaycastResults)
            {
                var hitObject = hit.gameObject;
                if (hitObject.GetComponentInParent<Selectable>() != null
                    || hitObject.GetComponentInParent<ScrollRect>() != null
                    || hitObject.GetComponentInParent<BasePopup>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static void LogSdkInputReadFailure(System.Exception exception)
        {
            if (hasLoggedSdkInputReadFailure)
            {
                return;
            }

            hasLoggedSdkInputReadFailure = true;
            Debug.LogWarning($"[PlayerInput] TTSDK polling input unavailable; falling back to other sources. {exception.Message}");
        }

        private void RegisterDouyinTouchEvents()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (touchInputStrategy != TouchInputStrategy.DouyinEventsPreferred || areDouyinTouchEventsRegistered)
            {
                return;
            }

            try
            {
                TT.OnTouchStart(OnDouyinTouchStart);
                TT.OnTouchMove(OnDouyinTouchMove);
                TT.OnTouchEnd(OnDouyinTouchEnd);
                TT.OnTouchCancel(OnDouyinTouchCancel);
                areDouyinTouchEventsRegistered = true;
            }
            catch (System.Exception exception)
            {
                if (!hasLoggedDouyinTouchRegistrationFailure)
                {
                    hasLoggedDouyinTouchRegistrationFailure = true;
                    Debug.LogWarning($"[PlayerInput] TT touch event registration failed; polling fallbacks remain active. {exception.Message}");
                }
            }
#endif
        }

        private void UnregisterDouyinTouchEvents()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!areDouyinTouchEventsRegistered)
            {
                return;
            }

            TT.OffTouchStart(OnDouyinTouchStart);
            TT.OffTouchMove(OnDouyinTouchMove);
            TT.OffTouchEnd(OnDouyinTouchEnd);
            TT.OffTouchCancel(OnDouyinTouchCancel);
            areDouyinTouchEventsRegistered = false;
            hasActiveDouyinTouch = false;
            activeDouyinTouchIdentifier = -1;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private void OnDouyinTouchStart(TTTouchEvent touchEvent)
        {
            if (hasActiveDouyinTouch || touchEvent.changedTouches == null)
            {
                return;
            }

            foreach (var touch in touchEvent.changedTouches)
            {
                activeDouyinTouchIdentifier = touch.identifier;
                activeDouyinTouchScreenPosition = ConvertDouyinTouchPosition(touch);
                hasActiveDouyinTouch = true;
                break;
            }
        }

        private void OnDouyinTouchMove(TTTouchEvent touchEvent)
        {
            if (touchEvent.changedTouches == null)
            {
                return;
            }

            foreach (var touch in touchEvent.changedTouches)
            {
                if (!hasActiveDouyinTouch)
                {
                    activeDouyinTouchIdentifier = touch.identifier;
                    hasActiveDouyinTouch = true;
                }

                if (touch.identifier != activeDouyinTouchIdentifier)
                {
                    continue;
                }

                activeDouyinTouchScreenPosition = ConvertDouyinTouchPosition(touch);
                break;
            }
        }

        private void OnDouyinTouchEnd(TTTouchEvent touchEvent)
        {
            CompleteDouyinTouch(touchEvent);
        }

        private void OnDouyinTouchCancel(TTTouchEvent touchEvent)
        {
            CompleteDouyinTouch(touchEvent);
        }

        private void CompleteDouyinTouch(TTTouchEvent touchEvent)
        {
            if (!hasActiveDouyinTouch || touchEvent.changedTouches == null)
            {
                return;
            }

            foreach (var touch in touchEvent.changedTouches)
            {
                if (touch.identifier != activeDouyinTouchIdentifier)
                {
                    continue;
                }

                hasActiveDouyinTouch = false;
                activeDouyinTouchIdentifier = -1;
                return;
            }
        }

        private Vector2 ConvertDouyinTouchPosition(TTTouch touch)
        {
            // On the tested Douyin WebGL runtime the callback already moves upward in Unity screen space.
            // Keep an opt-in flip for host or SDK variants that expose a top-origin Y value.
            var y = invertDouyinTouchY ? Screen.height - touch.screenY : touch.screenY;
            return new Vector2(touch.screenX, y);
        }
#endif

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

            if (other.GetComponentInParent<MissileController>() != null)
            {
                return false;
            }

            if (other.GetComponentInParent<EnemyController>() != null
                || other.GetComponentInParent<BossController>() != null)
            {
                return true;
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
                SpriteMaterialUtility.EnsureUsableSpriteMaterial(spriteRenderer);
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

            if (!HasTimedInvincibility)
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

        private static bool CanProcessGameplayInput(GameState state)
        {
            return state == GameState.Ready || state == GameState.Playing;
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
