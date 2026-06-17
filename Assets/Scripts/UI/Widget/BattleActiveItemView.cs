using System;
using System.Collections.Generic;
using System.Globalization;
using LeiTing.Bullets;
using LeiTing.Core;
using LeiTing.Pickups;
using LeiTing.Player;
using LeiTing.Stage;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LeiTing.UI
{
    [DisallowMultipleComponent]
    public sealed class BattleActiveItemView : MonoBehaviour
    {
        private const float ItemRadius = 168f;
        private const float ItemSize = 112f;
        private const float IconSize = 68f;
        private const float CountBadgeSize = 38f;
        private const float LinkWidth = 5f;
        private const float ShieldDuration = 5f;
        private const string RewardAdSourcePrefix = "BattleActiveItem";

        private const string LightningIconPath = "Assets/Art/Sprites/Item/item_lightning.png";
        private const string ShieldIconPath = "Assets/Art/Sprites/Item/item_shield.png";
        private const string BombIconPath = "Assets/Art/Sprites/Item/item_boom.png";

        private static readonly ActiveItemKind[] ItemOrder =
        {
            ActiveItemKind.Lightning,
            ActiveItemKind.Shield,
            ActiveItemKind.Bomb
        };

        private static readonly float[] ItemAngles =
        {
            145f,
            90f,
            35f
        };

        private static readonly Color LinkColor = new Color(0.36f, 0.78f, 1f, 0.54f);
        private static readonly Color ReadyColor = new Color(0.07f, 0.34f, 0.84f, 0.88f);
        private static readonly Color EmptyColor = new Color(0.06f, 0.1f, 0.17f, 0.82f);
        private static readonly Color UsedColor = new Color(0.18f, 0.2f, 0.24f, 0.74f);
        private static readonly Color CountBadgeColor = new Color(0.02f, 0.04f, 0.08f, 0.9f);

        private readonly List<ItemBinding> bindings = new List<ItemBinding>();

        private RectTransform rectTransform;
        private RectTransform itemRoot;
        private RectTransform promptRoot;
        private Text promptTitle;
        private Text promptMessage;
        private Text confirmText;
        private Button confirmButton;
        private Button cancelButton;
        private Canvas parentCanvas;
        private Camera gameplayCamera;
        private PlayerController player;
        private ActiveItemKind pendingAdKind;
        private bool promptIsAdRequest;
        private bool adInProgress;
        private bool adPauseActive;
        private bool adPausePausedGame;
        private float adPausePreviousTimeScale = 1f;
        private float adPausePreviousFixedDeltaTime = 0.02f;

        private static Sprite circleSprite;
        private static readonly Dictionary<ActiveItemKind, Sprite> fallbackIcons =
            new Dictionary<ActiveItemKind, Sprite>();

        public void BindPlayer(PlayerController target)
        {
            player = target;
        }

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            parentCanvas = GetComponentInParent<Canvas>();
            gameplayCamera = Camera.main;

            UIFactory.Stretch(rectTransform);
            BuildItemControls();
            BuildAdPrompt();
            ActiveItemInventory.InventoryChanged += RefreshItems;
            RefreshItems();
        }

        private void OnDestroy()
        {
            ActiveItemInventory.InventoryChanged -= RefreshItems;
            EndAdPause(pendingAdKind);
        }

        private void Update()
        {
            ResolvePlayer();

            var visible = ShouldShowItems();
            if (itemRoot != null && itemRoot.gameObject.activeSelf != visible)
            {
                itemRoot.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                return;
            }

            UpdateItemLayout();
            RefreshItems();
        }

        private void BuildItemControls()
        {
            itemRoot = UIFactory.CreateRect("ActiveItemRoot", transform);
            UIFactory.Stretch(itemRoot);
            itemRoot.gameObject.SetActive(false);

            for (var index = 0; index < ItemOrder.Length; index++)
            {
                bindings.Add(CreateItemBinding(ItemOrder[index], index));
            }
        }

        private ItemBinding CreateItemBinding(ActiveItemKind kind, int index)
        {
            var lineRect = UIFactory.CreateRect(kind + "Link", itemRoot);
            lineRect.pivot = new Vector2(0f, 0.5f);
            lineRect.sizeDelta = new Vector2(1f, LinkWidth);
            var lineImage = lineRect.gameObject.AddComponent<Image>();
            lineImage.color = LinkColor;
            lineImage.raycastTarget = false;

            var buttonRect = UIFactory.CreateRect(kind + "Button", itemRoot);
            buttonRect.sizeDelta = new Vector2(ItemSize, ItemSize);

            var background = buttonRect.gameObject.AddComponent<Image>();
            background.sprite = GetCircleSprite();
            background.color = ReadyColor;

            var button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = UIFactory.CreateButtonColors(Color.white);
            button.onClick.AddListener(() => OnClickItem(kind));

            var iconRect = UIFactory.CreateRect("Icon", buttonRect);
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(IconSize, IconSize);
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.sprite = LoadIcon(kind);
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var badgeRect = UIFactory.CreateRect("CountBadge", buttonRect);
            badgeRect.anchorMin = new Vector2(1f, 0f);
            badgeRect.anchorMax = new Vector2(1f, 0f);
            badgeRect.pivot = new Vector2(1f, 0f);
            badgeRect.anchoredPosition = new Vector2(-6f, 7f);
            badgeRect.sizeDelta = new Vector2(CountBadgeSize, CountBadgeSize);
            var badge = badgeRect.gameObject.AddComponent<Image>();
            badge.sprite = GetCircleSprite();
            badge.color = CountBadgeColor;
            badge.raycastTarget = false;

            var countText = UIFactory.CreateText(
                "CountText",
                badgeRect,
                "0",
                24f,
                TextAnchor.MiddleCenter,
                Color.white);
            UIFactory.Stretch(countText.rectTransform);

            return new ItemBinding
            {
                kind = kind,
                index = index,
                buttonRect = buttonRect,
                lineRect = lineRect,
                background = background,
                button = button,
                icon = icon,
                countText = countText
            };
        }

        private void BuildAdPrompt()
        {
            promptRoot = UIFactory.CreateRect("ActiveItemAdPrompt", transform);
            UIFactory.Stretch(promptRoot);

            var dim = promptRoot.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.58f);

            var maskButton = promptRoot.gameObject.AddComponent<Button>();
            maskButton.targetGraphic = dim;
            maskButton.onClick.AddListener(HidePrompt);

            var panel = UIFactory.CreatePanel("Panel", promptRoot, new Color(0.025f, 0.04f, 0.075f, 0.96f));
            var panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(680f, 360f);

            promptTitle = UIFactory.CreateText(
                "Title",
                panelRect,
                "\u9053\u5177\u4E0D\u8DB3",
                42f,
                TextAnchor.MiddleCenter,
                Color.white);
            promptTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            promptTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            promptTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            promptTitle.rectTransform.anchoredPosition = new Vector2(0f, -38f);
            promptTitle.rectTransform.sizeDelta = new Vector2(0f, 64f);

            promptMessage = UIFactory.CreateText(
                "Message",
                panelRect,
                string.Empty,
                34f,
                TextAnchor.MiddleCenter,
                UIFactory.TextColor);
            promptMessage.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            promptMessage.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            promptMessage.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            promptMessage.rectTransform.anchoredPosition = new Vector2(0f, 30f);
            promptMessage.rectTransform.sizeDelta = new Vector2(-96f, 92f);

            confirmButton = UIFactory.CreateButton(
                "ConfirmButton",
                panelRect,
                "\u786E\u5B9A",
                new Color(0.08f, 0.52f, 1f, 0.95f),
                out confirmText,
                out _);
            var confirmRect = confirmButton.GetComponent<RectTransform>();
            confirmRect.anchorMin = new Vector2(0.5f, 0f);
            confirmRect.anchorMax = new Vector2(0.5f, 0f);
            confirmRect.pivot = new Vector2(0.5f, 0f);
            confirmRect.anchoredPosition = new Vector2(-150f, 42f);
            confirmRect.sizeDelta = new Vector2(240f, 76f);
            confirmButton.onClick.AddListener(OnClickPromptConfirm);

            cancelButton = UIFactory.CreateButton(
                "CancelButton",
                panelRect,
                "\u53D6\u6D88",
                new Color(0.09f, 0.13f, 0.18f, 0.94f),
                out _,
                out _);
            var cancelRect = cancelButton.GetComponent<RectTransform>();
            cancelRect.anchorMin = new Vector2(0.5f, 0f);
            cancelRect.anchorMax = new Vector2(0.5f, 0f);
            cancelRect.pivot = new Vector2(0.5f, 0f);
            cancelRect.anchoredPosition = new Vector2(150f, 42f);
            cancelRect.sizeDelta = new Vector2(240f, 76f);
            cancelButton.onClick.AddListener(HidePrompt);

            promptRoot.gameObject.SetActive(false);
        }

        private bool ShouldShowItems()
        {
            return player != null
                && GameManager.Instance != null
                && GameManager.Instance.CurrentState == GameState.Playing
                && BattleTimeController.Instance != null
                && BattleTimeController.Instance.IsBulletTimeActive
                && !adInProgress;
        }

        private void UpdateItemLayout()
        {
            if (player == null || gameplayCamera == null || rectTransform == null)
            {
                return;
            }

            var screenPosition = gameplayCamera.WorldToScreenPoint(player.transform.position);
            if (screenPosition.z < 0f)
            {
                itemRoot.gameObject.SetActive(false);
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                screenPosition,
                GetUiCamera(),
                out var center);

            for (var index = 0; index < bindings.Count; index++)
            {
                var binding = bindings[index];
                var angle = ItemAngles[Mathf.Clamp(binding.index, 0, ItemAngles.Length - 1)] * Mathf.Deg2Rad;
                var position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ItemRadius;

                binding.buttonRect.anchoredPosition = position;
                UpdateLink(binding.lineRect, center, position);
            }
        }

        private void UpdateLink(RectTransform line, Vector2 start, Vector2 end)
        {
            if (line == null)
            {
                return;
            }

            var delta = end - start;
            line.anchoredPosition = start;
            line.sizeDelta = new Vector2(delta.magnitude, LinkWidth);
            line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        private void RefreshItems()
        {
            for (var index = 0; index < bindings.Count; index++)
            {
                RefreshItem(bindings[index]);
            }
        }

        private void RefreshItem(ItemBinding binding)
        {
            if (binding == null)
            {
                return;
            }

            var count = ActiveItemInventory.GetCount(binding.kind);
            var used = ActiveItemInventory.WasUsedThisBattle(binding.kind);
            var canWatchAd = count < 1 && ActiveItemInventory.CanClaimAdRewardThisBattle(binding.kind);
            var interactable = !used && !adInProgress && (count > 0 || canWatchAd);

            if (binding.countText != null)
            {
                binding.countText.text = count.ToString(CultureInfo.InvariantCulture);
            }

            if (binding.button != null)
            {
                binding.button.interactable = interactable;
            }

            if (binding.background != null)
            {
                binding.background.color = used
                    ? UsedColor
                    : count > 0
                        ? ReadyColor
                        : EmptyColor;
            }

            if (binding.icon != null)
            {
                binding.icon.color = used ? new Color(1f, 1f, 1f, 0.42f) : Color.white;
            }
        }

        private void OnClickItem(ActiveItemKind kind)
        {
            if (adInProgress || !ShouldShowItems())
            {
                return;
            }

            if (ActiveItemInventory.WasUsedThisBattle(kind))
            {
                return;
            }

            if (ActiveItemInventory.GetCount(kind) < 1)
            {
                if (ActiveItemInventory.CanClaimAdRewardThisBattle(kind))
                {
                    ShowAdPrompt(kind);
                }
                else
                {
                    ShowInfoPrompt("\u672C\u5C40\u5E7F\u544A\u5956\u52B1\u5DF2\u9886\u53D6");
                }

                return;
            }

            if (!ActiveItemInventory.TryConsumeForBattle(kind))
            {
                RefreshItems();
                return;
            }

            ApplyItemEffect(kind);
            RefreshItems();
        }

        private void ApplyItemEffect(ActiveItemKind kind)
        {
            ResolvePlayer();
            switch (kind)
            {
                case ActiveItemKind.Lightning:
                    BulletManager.Instance?.ConvertVisibleEnemyBulletsToStars();
                    break;
                case ActiveItemKind.Shield:
                    player?.ActivateInvincibleShield(ShieldDuration);
                    break;
                case ActiveItemKind.Bomb:
                    PickupManager.GetOrCreate().KillAllMinions();
                    break;
            }
        }

        private void ShowAdPrompt(ActiveItemKind kind)
        {
            pendingAdKind = kind;
            promptIsAdRequest = true;
            promptRoot.SetAsLastSibling();
            promptRoot.gameObject.SetActive(true);
            promptTitle.text = "\u9053\u5177\u4E0D\u8DB3";
            promptMessage.text = "\u89C2\u770B\u5E7F\u544A\u53EF\u83B7\u5F97";
            confirmText.text = "\u786E\u5B9A";
            SetPromptButtonsInteractable(true);
            cancelButton.gameObject.SetActive(true);
            confirmButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-150f, 42f);
        }

        private void ShowInfoPrompt(string message)
        {
            promptIsAdRequest = false;
            promptRoot.SetAsLastSibling();
            promptRoot.gameObject.SetActive(true);
            promptTitle.text = "\u63D0\u793A";
            promptMessage.text = message;
            confirmText.text = "\u786E\u5B9A";
            SetPromptButtonsInteractable(true);
            cancelButton.gameObject.SetActive(false);
            confirmButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 42f);
        }

        private void HidePrompt()
        {
            if (adInProgress || promptRoot == null)
            {
                return;
            }

            promptRoot.gameObject.SetActive(false);
        }

        private void OnClickPromptConfirm()
        {
            if (!promptIsAdRequest)
            {
                HidePrompt();
                return;
            }

            WatchAdForPendingItem();
        }

        private async void WatchAdForPendingItem()
        {
            if (adInProgress || !ActiveItemInventory.CanClaimAdRewardThisBattle(pendingAdKind))
            {
                Debug.LogWarning(
                    $"[BattleActiveItem] Reward ad request ignored. kind={pendingAdKind}, adInProgress={adInProgress}, canClaim={ActiveItemInventory.CanClaimAdRewardThisBattle(pendingAdKind)}");
                return;
            }

            Debug.LogWarning($"[BattleActiveItem] Reward ad requested for active item. kind={pendingAdKind}");
            adInProgress = true;
            confirmText.text = "\u5E7F\u544A\u4E2D...";
            SetPromptButtonsInteractable(false);
            RefreshItems();

            BeginAdPause(pendingAdKind);

            var watchedAd = false;
            try
            {
                watchedAd = await AdManager.GetOrCreate().ShowRewardAd($"{RewardAdSourcePrefix}:{pendingAdKind}");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[BattleActiveItem] Reward ad exception. kind={pendingAdKind}, message={exception.Message}");
            }
            finally
            {
                EndAdPause(pendingAdKind);
            }

            if (this == null)
            {
                return;
            }

            adInProgress = false;
            Debug.LogWarning($"[BattleActiveItem] Reward ad result received. kind={pendingAdKind}, watched={watchedAd}");
            if (watchedAd)
            {
                ActiveItemInventory.TryClaimAdReward(pendingAdKind);
                promptRoot.gameObject.SetActive(false);
                RefreshItems();
                return;
            }

            ShowInfoPrompt("\u5E7F\u544A\u672A\u5B8C\u6210");
            RefreshItems();
        }

        private void BeginAdPause(ActiveItemKind kind)
        {
            if (adPauseActive)
            {
                return;
            }

            adPauseActive = true;
            adPausePreviousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            adPausePreviousFixedDeltaTime = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f;

            var gameManager = GameManager.Instance;
            adPausePausedGame = gameManager != null && gameManager.CurrentState == GameState.Playing;
            if (adPausePausedGame)
            {
                gameManager.PauseGame();
            }

            Time.timeScale = 0f;
            Time.fixedDeltaTime = 0f;
            Debug.LogWarning(
                $"[BattleActiveItem] Battle paused for reward ad. kind={kind}, pausedGame={adPausePausedGame}, previousTimeScale={adPausePreviousTimeScale}, previousFixedDeltaTime={adPausePreviousFixedDeltaTime}");
        }

        private void EndAdPause(ActiveItemKind kind)
        {
            if (!adPauseActive)
            {
                return;
            }

            var gameManager = GameManager.Instance;
            if (adPausePausedGame && gameManager != null && gameManager.CurrentState == GameState.Paused)
            {
                gameManager.ResumeGame();
            }

            Time.timeScale = adPausePreviousTimeScale > 0f ? adPausePreviousTimeScale : 1f;
            Time.fixedDeltaTime = adPausePreviousFixedDeltaTime > 0f ? adPausePreviousFixedDeltaTime : 0.02f;
            Debug.LogWarning(
                $"[BattleActiveItem] Battle resumed after reward ad. kind={kind}, restoredTimeScale={Time.timeScale}, restoredFixedDeltaTime={Time.fixedDeltaTime}");

            adPauseActive = false;
            adPausePausedGame = false;
        }

        private void SetPromptButtonsInteractable(bool interactable)
        {
            if (confirmButton != null)
            {
                confirmButton.interactable = interactable;
            }

            if (cancelButton != null)
            {
                cancelButton.interactable = interactable;
            }
        }

        private Camera GetUiCamera()
        {
            if (parentCanvas == null || parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            return parentCanvas.worldCamera;
        }

        private void ResolvePlayer()
        {
            if (player == null)
            {
                player = FindObjectOfType<PlayerController>();
            }

            if (gameplayCamera == null)
            {
                gameplayCamera = Camera.main;
            }
        }

        private static Sprite LoadIcon(ActiveItemKind kind)
        {
            var path = GetIconPath(kind);
            Sprite sprite = null;

#if UNITY_EDITOR
            if (RuntimeRemoteResourceManager.CanUseEditorLocalAssets)
            {
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
#endif

            sprite = sprite != null ? sprite : RuntimeAssetCatalog.LoadSprite(path);
            return sprite != null ? sprite : GetFallbackIcon(kind);
        }

        private static string GetIconPath(ActiveItemKind kind)
        {
            switch (kind)
            {
                case ActiveItemKind.Lightning:
                    return LightningIconPath;
                case ActiveItemKind.Shield:
                    return ShieldIconPath;
                case ActiveItemKind.Bomb:
                    return BombIconPath;
                default:
                    return string.Empty;
            }
        }

        private static Sprite GetCircleSprite()
        {
            if (circleSprite == null)
            {
                circleSprite = CreateCircleSprite();
            }

            return circleSprite;
        }

        private static Sprite GetFallbackIcon(ActiveItemKind kind)
        {
            if (!fallbackIcons.TryGetValue(kind, out var sprite) || sprite == null)
            {
                sprite = CreateFallbackIcon(kind);
                fallbackIcons[kind] = sprite;
            }

            return sprite;
        }

        private static Sprite CreateCircleSprite()
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
                    if (distance > 1f)
                    {
                        texture.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    var border = distance > 0.82f ? 1f : 0f;
                    var alpha = Mathf.Lerp(0.72f, 1f, border);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite CreateFallbackIcon(ActiveItemKind kind)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    texture.SetPixel(x, y, Color.clear);
                }
            }

            switch (kind)
            {
                case ActiveItemKind.Bomb:
                    DrawCircle(texture, new Vector2(31f, 36f), 18f, new Color(0.02f, 0.02f, 0.025f, 1f));
                    DrawCircle(texture, new Vector2(38f, 19f), 4f, new Color(1f, 0.62f, 0.1f, 1f));
                    break;
                case ActiveItemKind.Shield:
                    DrawPolygon(
                        texture,
                        new[]
                        {
                            new Vector2(32f, 8f),
                            new Vector2(50f, 17f),
                            new Vector2(45f, 43f),
                            new Vector2(32f, 57f),
                            new Vector2(19f, 43f),
                            new Vector2(14f, 17f)
                        },
                        new Color(0.22f, 0.72f, 1f, 1f));
                    break;
                default:
                    DrawPolygon(
                        texture,
                        new[]
                        {
                            new Vector2(36f, 4f),
                            new Vector2(18f, 33f),
                            new Vector2(31f, 33f),
                            new Vector2(24f, 60f),
                            new Vector2(48f, 26f),
                            new Vector2(34f, 26f)
                        },
                        new Color(0.38f, 0.86f, 1f, 1f));
                    break;
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static void DrawCircle(Texture2D texture, Vector2 center, float radius, Color color)
        {
            var radiusSquared = radius * radius;
            for (var y = 0; y < texture.height; y++)
            {
                for (var x = 0; x < texture.width; x++)
                {
                    var delta = new Vector2(x, y) - center;
                    if (delta.sqrMagnitude <= radiusSquared)
                    {
                        texture.SetPixel(x, y, color);
                    }
                }
            }
        }

        private static void DrawPolygon(Texture2D texture, Vector2[] points, Color color)
        {
            for (var y = 0; y < texture.height; y++)
            {
                for (var x = 0; x < texture.width; x++)
                {
                    if (IsPointInPolygon(new Vector2(x, y), points))
                    {
                        texture.SetPixel(x, y, color);
                    }
                }
            }
        }

        private static bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
        {
            var inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                var crosses = (polygon[i].y > point.y) != (polygon[j].y > point.y);
                if (!crosses)
                {
                    continue;
                }

                var denominator = polygon[j].y - polygon[i].y;
                if (Mathf.Abs(denominator) < 0.0001f)
                {
                    continue;
                }

                var x = (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y)
                    / denominator
                    + polygon[i].x;
                if (point.x < x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private sealed class ItemBinding
        {
            public ActiveItemKind kind;
            public int index;
            public RectTransform buttonRect;
            public RectTransform lineRect;
            public Image background;
            public Button button;
            public Image icon;
            public Text countText;
        }
    }
}
