using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LeiTing.UI
{
    public class BottomBar : MonoBehaviour
    {
        private const string HallButtonName = "btnHall";
        private const string ShopButtonName = "btnShop";
        private const string GiftButtonName = "btnGift";
        private const string SpriteArtLayerName = "SpriteArtLayer";

        [SerializeField] private Button hangarButton;
        [SerializeField] private Button lobbyButton;
        [SerializeField] private Button settingButton;

        private readonly Dictionary<UIPageType, Image> buttonImages = new Dictionary<UIPageType, Image>();
        private readonly Dictionary<UIPageType, Text> buttonLabels = new Dictionary<UIPageType, Text>();
        private bool built;

        public void BuildDefaultView()
        {
            if (built)
            {
                BindButtons();
                return;
            }

            built = true;

            BindPrefabReferences();
            if (HasCanvasPrefabButtons())
            {
                CacheNavButton(UIPageType.Hangar, hangarButton);
                CacheNavButton(UIPageType.Lobby, lobbyButton);
                CacheNavButton(UIPageType.Setting, settingButton);
                BindButtons();
                return;
            }

            if (TryBuildSpritePrefabView())
            {
                BindButtons();
                return;
            }

            var background = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            background.color = new Color(0.015f, 0.025f, 0.05f, 0.94f);

            var layout = gameObject.GetComponent<HorizontalLayoutGroup>() ?? gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 16, 20);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            lobbyButton = CreateNavButton(UIPageType.Lobby, "大厅");
            hangarButton = CreateNavButton(UIPageType.Hangar, "机库");
            settingButton = CreateNavButton(UIPageType.Setting, "设置");
            BindButtons();
        }

        public void SetSelected(UIPageType pageType)
        {
            BuildDefaultView();

            foreach (var pair in buttonImages)
            {
                var selected = pair.Key == pageType;
                pair.Value.color = selected
                    ? new Color(0.1f, 0.68f, 1f, 0.96f)
                    : new Color(0.055f, 0.08f, 0.13f, 0.82f);

                if (buttonLabels.TryGetValue(pair.Key, out var label))
                {
                    label.color = selected ? Color.white : UIFactory.MutedTextColor;
                }
            }
        }

        private Button CreateNavButton(UIPageType pageType, string text)
        {
            var button = UIFactory.CreateButton(pageType.ToString(), transform, text, new Color(0.055f, 0.08f, 0.13f, 0.82f), out var label, out var image);
            buttonImages[pageType] = image;
            buttonLabels[pageType] = label;

            var layoutElement = button.gameObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = 94f;
            layoutElement.preferredHeight = 94f;
            return button;
        }

        private void BindPrefabReferences()
        {
            lobbyButton = FindButton(lobbyButton, HallButtonName, "Lobby");
            hangarButton = FindButton(hangarButton, ShopButtonName, "Hangar");
            settingButton = FindButton(settingButton, GiftButtonName, "Setting");
        }

        private bool HasCanvasPrefabButtons()
        {
            return IsCanvasButton(hangarButton)
                && IsCanvasButton(lobbyButton)
                && IsCanvasButton(settingButton);
        }

        private static bool IsCanvasButton(Button button)
        {
            return button != null && button.targetGraphic != null;
        }

        private bool TryBuildSpritePrefabView()
        {
            var spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            if (spriteRenderers.Length == 0)
            {
                return false;
            }

            var backgroundRenderer = FindSpriteRenderer(spriteRenderers, "imgBottomBg") ?? FindLargestSpriteRenderer(spriteRenderers);
            if (backgroundRenderer == null || backgroundRenderer.sprite == null)
            {
                return false;
            }

            var backgroundSize = GetSpriteRendererPixelSize(backgroundRenderer);
            var pixelsPerUnit = backgroundRenderer.sprite.pixelsPerUnit;
            var artLayer = UIFactory.CreateRect(SpriteArtLayerName, transform);
            artLayer.anchorMin = new Vector2(0.5f, 0f);
            artLayer.anchorMax = new Vector2(0.5f, 0f);
            artLayer.pivot = new Vector2(0.5f, 0f);
            artLayer.anchoredPosition = Vector2.zero;
            artLayer.sizeDelta = backgroundSize;

            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                if (button.targetGraphic == null)
                {
                    button.enabled = false;
                }
            }

            foreach (var spriteRenderer in spriteRenderers)
            {
                CreateSpriteImage(spriteRenderer, backgroundRenderer.transform, artLayer, backgroundSize, pixelsPerUnit);
                spriteRenderer.enabled = false;
            }

            return true;
        }

        private void CreateSpriteImage(
            SpriteRenderer spriteRenderer,
            Transform backgroundTransform,
            RectTransform parent,
            Vector2 backgroundSize,
            float pixelsPerUnit)
        {
            if (spriteRenderer == null || spriteRenderer.sprite == null)
            {
                return;
            }

            var rect = UIFactory.CreateRect(spriteRenderer.name + "Image", parent);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = GetSpriteAnchoredPosition(spriteRenderer.transform, backgroundTransform, backgroundSize, pixelsPerUnit);
            rect.sizeDelta = GetSpriteRendererPixelSize(spriteRenderer);

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = spriteRenderer.sprite;
            image.color = spriteRenderer.color;
            image.preserveAspect = true;
            image.raycastTarget = spriteRenderer.GetComponent<Button>() != null;

            var sourceButton = spriteRenderer.GetComponent<Button>();
            if (sourceButton == null)
            {
                return;
            }

            var uiButton = rect.gameObject.AddComponent<Button>();
            uiButton.targetGraphic = image;
            uiButton.transition = Selectable.Transition.ColorTint;
            uiButton.colors = UIFactory.CreateButtonColors(Color.white);

            BindSpriteButton(spriteRenderer.name, uiButton);
        }

        private static Vector2 GetSpriteAnchoredPosition(
            Transform spriteTransform,
            Transform backgroundTransform,
            Vector2 backgroundSize,
            float pixelsPerUnit)
        {
            var localPosition = backgroundTransform.InverseTransformPoint(spriteTransform.position);
            return new Vector2(
                localPosition.x * pixelsPerUnit,
                backgroundSize.y * 0.5f + localPosition.y * pixelsPerUnit);
        }

        private static Vector2 GetSpriteRendererPixelSize(SpriteRenderer spriteRenderer)
        {
            var sprite = spriteRenderer.sprite;
            if (sprite == null)
            {
                return Vector2.zero;
            }

            if (spriteRenderer.drawMode == SpriteDrawMode.Simple)
            {
                return sprite.rect.size;
            }

            return spriteRenderer.size * sprite.pixelsPerUnit;
        }

        private void BindSpriteButton(string buttonName, Button button)
        {
            switch (buttonName)
            {
                case HallButtonName:
                    lobbyButton = button;
                    break;
                case ShopButtonName:
                    hangarButton = button;
                    break;
                case GiftButtonName:
                    settingButton = button;
                    break;
            }
        }

        private static SpriteRenderer FindSpriteRenderer(SpriteRenderer[] renderers, string objectName)
        {
            foreach (var renderer in renderers)
            {
                if (renderer != null && renderer.name == objectName)
                {
                    return renderer;
                }
            }

            return null;
        }

        private static SpriteRenderer FindLargestSpriteRenderer(SpriteRenderer[] renderers)
        {
            SpriteRenderer largest = null;
            var largestArea = 0f;

            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer.sprite == null)
                {
                    continue;
                }

                var size = GetSpriteRendererPixelSize(renderer);
                var area = size.x * size.y;
                if (area > largestArea)
                {
                    largestArea = area;
                    largest = renderer;
                }
            }

            return largest;
        }

        private void CacheNavButton(UIPageType pageType, Button button)
        {
            if (button == null)
            {
                return;
            }

            UIFactory.ApplyButtonTextFont(button);

            var image = button.GetComponent<Image>();
            if (image != null)
            {
                buttonImages[pageType] = image;
            }

            var label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                buttonLabels[pageType] = label;
            }
        }

        private Button FindButton(Button current, params string[] childNames)
        {
            if (current != null)
            {
                return current;
            }

            foreach (var childName in childNames)
            {
                var button = UIFactory.FindComponentInChildren<Button>(transform, childName);
                if (button != null)
                {
                    return button;
                }
            }

            return null;
        }

        private void BindButtons()
        {
            hangarButton?.onClick.RemoveListener(OnClickHangar);
            lobbyButton?.onClick.RemoveListener(OnClickLobby);
            settingButton?.onClick.RemoveListener(OnClickSetting);

            hangarButton?.onClick.AddListener(OnClickHangar);
            lobbyButton?.onClick.AddListener(OnClickLobby);
            settingButton?.onClick.AddListener(OnClickSetting);
        }

        private static void OnClickHangar()
        {
            UIManager.Instance?.SwitchPage(UIPageType.Hangar);
        }

        private static void OnClickLobby()
        {
            UIManager.Instance?.SwitchPage(UIPageType.Lobby);
        }

        private static void OnClickSetting()
        {
            UIManager.Instance?.SwitchPage(UIPageType.Setting);
        }
    }
}
