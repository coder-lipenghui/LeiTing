using LeiTing.Core;
using LeiTing.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LeiTing.Stage
{
    [DisallowMultipleComponent]
    public class BattleTimeController : MonoSingleton<BattleTimeController>
    {
        private const float StartPromptDelay = 2f;
        private const float StartPromptCycleDuration = 0.92f;
        private const float StartPromptMinScale = 0.45f;
        private const float StartPromptMaxScale = 1.9f;
        private const float BulletTimeScale = 0.25f;
        private const float BulletTimeMaskAlpha = 0.38f;
        private const int StartPromptSortingOrder = 42;
        private const int BulletTimeMaskSortingOrder = 120;

        private static Sprite startPromptSprite;
        private static Sprite maskSprite;

        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private PlayerController player;

        private Transform startPromptRoot;
        private SpriteRenderer startPromptRenderer;
        private Transform bulletTimeMaskRoot;
        private SpriteRenderer bulletTimeMaskRenderer;
        private float readyStartedAt;
        private float defaultFixedDeltaTime;
        private bool battleStarted;
        private bool isPointerHeld;
        private bool isBulletTimeActive;
        private bool isBattleControlActive;

        public bool BattleStarted => battleStarted;
        public bool IsBulletTimeActive => isBulletTimeActive;

        public static BattleTimeController GetOrCreate()
        {
            if (Instance != null)
            {
                return Instance;
            }

            var existing = FindObjectOfType<BattleTimeController>();
            if (existing != null)
            {
                return existing;
            }

            var managers = GameObject.Find("Managers");
            if (managers == null)
            {
                managers = new GameObject("Managers");
            }

            return managers.GetComponent<BattleTimeController>() ?? managers.AddComponent<BattleTimeController>();
        }

        protected override void Awake()
        {
            base.Awake();

            if (Instance != this)
            {
                return;
            }

            gameplayCamera = gameplayCamera != null ? gameplayCamera : Camera.main;
            defaultFixedDeltaTime = Time.timeScale > 0f ? Time.fixedDeltaTime / Time.timeScale : 0.02f;
            ResetForReady();
        }

        public void ResetForReady()
        {
            isBattleControlActive = true;
            battleStarted = false;
            isPointerHeld = false;
            readyStartedAt = Time.unscaledTime;
            ExitBulletTime(true);
            HideStartPrompt();
            EnsureBulletTimeMask();
        }

        public void NotifyPointerDown(PlayerController sourcePlayer)
        {
            if (!IsBattleControlActive())
            {
                return;
            }

            player = sourcePlayer != null ? sourcePlayer : player;
            isPointerHeld = true;

            var gameManager = GameManager.Instance;
            if (!battleStarted && gameManager != null && gameManager.CurrentState == GameState.Ready)
            {
                battleStarted = true;
                HideStartPrompt();
                gameManager.StartGame();
            }
            else if (!battleStarted && gameManager != null && gameManager.CurrentState == GameState.Playing)
            {
                battleStarted = true;
            }

            ExitBulletTime();
        }

        public void NotifyPointerUp(PlayerController sourcePlayer)
        {
            if (!IsBattleControlActive())
            {
                return;
            }

            player = sourcePlayer != null ? sourcePlayer : player;
            isPointerHeld = false;

            if (!battleStarted || GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Playing)
            {
                return;
            }

            EnterBulletTime();
        }

        private void Update()
        {
            if (!IsBattleControlActive())
            {
                ExitBulletTime();
                HideStartPrompt();
                return;
            }

            var state = GameManager.Instance != null ? GameManager.Instance.CurrentState : GameState.Boot;
            if (!battleStarted && state == GameState.Ready)
            {
                UpdateStartPrompt();
            }
            else
            {
                HideStartPrompt();
            }

            if (state != GameState.Playing && isBulletTimeActive)
            {
                ExitBulletTime();
            }

            if (isBulletTimeActive)
            {
                FitBulletTimeMaskToCamera();
            }
        }

        private void OnDisable()
        {
            ExitBulletTime();
        }

        private void OnDestroy()
        {
            ExitBulletTime();
        }

        private void EnterBulletTime()
        {
            if (isBulletTimeActive || isPointerHeld)
            {
                return;
            }

            isBulletTimeActive = true;
            Time.timeScale = BulletTimeScale;
            Time.fixedDeltaTime = defaultFixedDeltaTime * BulletTimeScale;
            SetBulletTimeMaskVisible(true);
        }

        private void ExitBulletTime(bool force = false)
        {
            if (isBulletTimeActive || force)
            {
                Time.timeScale = 1f;
                Time.fixedDeltaTime = defaultFixedDeltaTime > 0f ? defaultFixedDeltaTime : 0.02f;
            }

            isBulletTimeActive = false;
            SetBulletTimeMaskVisible(false);
        }

        private void UpdateStartPrompt()
        {
            player = player != null ? player : FindObjectOfType<PlayerController>();
            if (player == null)
            {
                HideStartPrompt();
                return;
            }

            var elapsed = Time.unscaledTime - readyStartedAt;
            if (elapsed < StartPromptDelay)
            {
                HideStartPrompt();
                return;
            }

            EnsureStartPrompt(player.transform);

            var promptAge = Mathf.Repeat(elapsed - StartPromptDelay, StartPromptCycleDuration);
            var t = Mathf.Clamp01(promptAge / StartPromptCycleDuration);
            var eased = Mathf.SmoothStep(0f, 1f, t);
            var scale = Mathf.Lerp(StartPromptMinScale, StartPromptMaxScale, eased);
            var alpha = Mathf.Lerp(0f, 0.9f, eased);

            startPromptRoot.localPosition = Vector3.zero;
            startPromptRoot.localRotation = Quaternion.identity;
            startPromptRoot.localScale = Vector3.one * scale;
            startPromptRenderer.enabled = true;
            startPromptRenderer.color = new Color(0.34f, 0.9f, 1f, alpha);
        }

        private void HideStartPrompt()
        {
            if (startPromptRenderer != null)
            {
                startPromptRenderer.enabled = false;
            }
        }

        private void EnsureStartPrompt(Transform playerTransform)
        {
            if (startPromptRoot == null)
            {
                var prompt = new GameObject("StartInputPrompt");
                startPromptRoot = prompt.transform;
                startPromptRenderer = prompt.AddComponent<SpriteRenderer>();
                startPromptRenderer.sprite = GetStartPromptSprite();
                startPromptRenderer.sortingOrder = StartPromptSortingOrder;
                startPromptRenderer.sharedMaterial = SpriteMaterialUtility.DefaultSpriteMaterial;
            }

            if (startPromptRoot.parent != playerTransform)
            {
                startPromptRoot.SetParent(playerTransform, false);
            }

            startPromptRenderer = startPromptRenderer != null
                ? startPromptRenderer
                : startPromptRoot.GetComponent<SpriteRenderer>();
        }

        private void EnsureBulletTimeMask()
        {
            if (bulletTimeMaskRoot != null)
            {
                return;
            }

            var mask = new GameObject("BulletTimeMask");
            mask.transform.SetParent(transform, false);
            bulletTimeMaskRoot = mask.transform;
            bulletTimeMaskRenderer = mask.AddComponent<SpriteRenderer>();
            bulletTimeMaskRenderer.sprite = GetMaskSprite();
            bulletTimeMaskRenderer.sortingOrder = BulletTimeMaskSortingOrder;
            bulletTimeMaskRenderer.sharedMaterial = SpriteMaterialUtility.DefaultSpriteMaterial;
            bulletTimeMaskRenderer.color = new Color(0f, 0f, 0f, BulletTimeMaskAlpha);
            bulletTimeMaskRenderer.enabled = false;
            FitBulletTimeMaskToCamera();
        }

        private void SetBulletTimeMaskVisible(bool visible)
        {
            if (!visible && bulletTimeMaskRenderer == null)
            {
                return;
            }

            if (visible)
            {
                EnsureBulletTimeMask();
            }

            if (bulletTimeMaskRenderer == null)
            {
                return;
            }

            bulletTimeMaskRenderer.enabled = visible;
            if (visible)
            {
                FitBulletTimeMaskToCamera();
            }
        }

        private void FitBulletTimeMaskToCamera()
        {
            gameplayCamera = gameplayCamera != null ? gameplayCamera : Camera.main;
            if (gameplayCamera == null || bulletTimeMaskRoot == null)
            {
                return;
            }

            var cameraTransform = gameplayCamera.transform;
            var distance = Mathf.Abs(cameraTransform.position.z - bulletTimeMaskRoot.position.z);
            var bottomLeft = gameplayCamera.ViewportToWorldPoint(new Vector3(0f, 0f, distance));
            var topRight = gameplayCamera.ViewportToWorldPoint(new Vector3(1f, 1f, distance));
            var size = topRight - bottomLeft;

            bulletTimeMaskRoot.position = new Vector3(cameraTransform.position.x, cameraTransform.position.y, 0f);
            bulletTimeMaskRoot.localRotation = Quaternion.identity;
            bulletTimeMaskRoot.localScale = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), 1f);
        }

        private static bool IsBattleScene()
        {
            return GameSceneManager.IsBattleSceneName(SceneManager.GetActiveScene().name);
        }

        private bool IsBattleControlActive()
        {
            return isBattleControlActive || IsBattleScene();
        }

        private static Sprite GetStartPromptSprite()
        {
            if (startPromptSprite == null)
            {
                startPromptSprite = CreateStartPromptSprite();
            }

            return startPromptSprite;
        }

        private static Sprite GetMaskSprite()
        {
            if (maskSprite == null)
            {
                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                maskSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            }

            return maskSprite;
        }

        private static Sprite CreateStartPromptSprite()
        {
            const int size = 96;
            const float pixelsPerUnit = 96f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            var clear = new Color(0f, 0f, 0f, 0f);
            var color = Color.white;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x, y), center) / (size * 0.5f);
                    if (distance < 0.42f || distance > 1f)
                    {
                        texture.SetPixel(x, y, clear);
                        continue;
                    }

                    var ring = 1f - Mathf.Clamp01(Mathf.Abs(distance - 0.58f) / 0.075f);
                    ring = Mathf.SmoothStep(0f, 1f, ring);
                    var glow = Mathf.Pow(Mathf.Clamp01(1f - distance), 1.35f) * 0.55f;
                    color.a = Mathf.Clamp01(Mathf.Max(ring, glow));
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }
    }
}
