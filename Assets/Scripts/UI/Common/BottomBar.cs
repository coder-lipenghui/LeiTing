using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LeiTing.UI
{
    public class BottomBar : MonoBehaviour
    {
        private const string HallToggleName = "tabHall";
        private const string HangarToggleName = "tabHangar";
        private const string SettingToggleName = "tabSetting";
        private const string HallSpriteButtonName = "btnHall";
        private const string ShopSpriteButtonName = "btnShop";
        private const string GiftSpriteButtonName = "btnGift";
        private const string SpriteArtLayerName = "SpriteArtLayer";

        [SerializeField] private Toggle hangarToggle;
        [SerializeField] private Toggle lobbyToggle;
        [SerializeField] private Toggle settingToggle;

        private readonly Dictionary<UIPageType, Toggle> navToggles = new Dictionary<UIPageType, Toggle>();
        private readonly Dictionary<UIPageType, Graphic> navLabels = new Dictionary<UIPageType, Graphic>();
        private bool built;
        private bool suppressToggleCallbacks;

        public void BuildDefaultView()
        {
            if (built)
            {
                BindToggles();
                return;
            }

            built = true;

            BindPrefabReferences();
            if (HasCanvasPrefabToggles())
            {
                CacheNavToggle(UIPageType.Hangar, hangarToggle);
                CacheNavToggle(UIPageType.Lobby, lobbyToggle);
                CacheNavToggle(UIPageType.Setting, settingToggle);
                BindToggles();
                return;
            }

            if (TryBuildSpritePrefabView())
            {
                BindToggles();
                return;
            }

            RemoveDeprecatedButtons();

            var group = EnsureToggleGroup();
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

            lobbyToggle = CreateNavToggle(UIPageType.Lobby, "大厅", group);
            hangarToggle = CreateNavToggle(UIPageType.Hangar, "机库", group);
            settingToggle = CreateNavToggle(UIPageType.Setting, "设置", group);
            BindToggles();
        }

        public void SetSelected(UIPageType pageType)
        {
            BuildDefaultView();

            suppressToggleCallbacks = true;
            try
            {
                foreach (var pair in navToggles)
                {
                    var selected = pair.Key == pageType;
                    pair.Value.SetIsOnWithoutNotify(selected);

                    if (navLabels.TryGetValue(pair.Key, out var label))
                    {
                        label.color = selected ? Color.white : UIFactory.MutedTextColor;
                    }
                }
            }
            finally
            {
                suppressToggleCallbacks = false;
            }
        }

        private Toggle CreateNavToggle(UIPageType pageType, string text, ToggleGroup group)
        {
            var rect = UIFactory.CreateRect(pageType.ToString(), transform);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.055f, 0.08f, 0.13f, 0.82f);

            var checkmark = UIFactory.CreatePanel("Checkmark", rect, new Color(0.1f, 0.68f, 1f, 0.96f));
            UIFactory.Stretch(checkmark.rectTransform);
            checkmark.raycastTarget = false;

            var toggle = rect.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = image;
            toggle.graphic = checkmark;
            toggle.group = group;

            var label = UIFactory.CreateText("Label", rect, text, 30f, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Stretch(label.rectTransform);

            navToggles[pageType] = toggle;
            navLabels[pageType] = label;

            var layoutElement = toggle.gameObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = 94f;
            layoutElement.preferredHeight = 94f;
            return toggle;
        }

        private void BindPrefabReferences()
        {
            lobbyToggle = FindToggle(lobbyToggle, HallToggleName, "BtnHall", "Hall", "Lobby");
            hangarToggle = FindToggle(hangarToggle, HangarToggleName, "btnHangar", "BtnHangar", "Hangar");
            settingToggle = FindToggle(settingToggle, SettingToggleName, "BtnSetting", "Setting");
            ConfigureToggleGroups();
        }

        private bool HasCanvasPrefabToggles()
        {
            return IsCanvasToggle(hangarToggle)
                && IsCanvasToggle(lobbyToggle)
                && IsCanvasToggle(settingToggle);
        }

        private static bool IsCanvasToggle(Toggle toggle)
        {
            return toggle != null && toggle.targetGraphic != null;
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

            var uiToggle = rect.gameObject.AddComponent<Toggle>();
            uiToggle.targetGraphic = image;
            uiToggle.group = EnsureToggleGroup();
            uiToggle.transition = Selectable.Transition.ColorTint;
            uiToggle.colors = UIFactory.CreateButtonColors(Color.white);

            BindSpriteToggle(spriteRenderer.name, uiToggle);
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

        private void BindSpriteToggle(string buttonName, Toggle toggle)
        {
            switch (buttonName)
            {
                case HallSpriteButtonName:
                    lobbyToggle = toggle;
                    break;
                case ShopSpriteButtonName:
                    hangarToggle = toggle;
                    break;
                case GiftSpriteButtonName:
                    settingToggle = toggle;
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

        private void CacheNavToggle(UIPageType pageType, Toggle toggle)
        {
            if (toggle == null)
            {
                return;
            }

            UIFactory.ApplyFontsInChildren(toggle.transform);

            navToggles[pageType] = toggle;

            Graphic label = toggle.GetComponentInChildren<Text>(true);
            if (label == null)
            {
                label = toggle.GetComponentInChildren<TMP_Text>(true);
            }

            if (label != null)
            {
                navLabels[pageType] = label;
            }
        }

        private Toggle FindToggle(Toggle current, params string[] childNames)
        {
            if (current != null)
            {
                return current;
            }

            foreach (var childName in childNames)
            {
                var toggle = UIFactory.FindComponentInChildren<Toggle>(transform, childName);
                if (toggle != null)
                {
                    return toggle;
                }
            }

            return null;
        }

        private void RemoveDeprecatedButtons()
        {
            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                if (button == null || button.transform == transform)
                {
                    continue;
                }

                DestroyObject(button.gameObject);
            }
        }

        private static void DestroyObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                target.SetActive(false);
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private ToggleGroup EnsureToggleGroup()
        {
            var group = gameObject.GetComponent<ToggleGroup>() ?? gameObject.AddComponent<ToggleGroup>();
            group.allowSwitchOff = false;
            return group;
        }

        private void ConfigureToggleGroups()
        {
            foreach (var group in GetComponentsInChildren<ToggleGroup>(true))
            {
                group.allowSwitchOff = false;
            }
        }

        private void BindToggles()
        {
            hangarToggle?.onValueChanged.RemoveListener(OnHangarToggleChanged);
            lobbyToggle?.onValueChanged.RemoveListener(OnLobbyToggleChanged);
            settingToggle?.onValueChanged.RemoveListener(OnSettingToggleChanged);

            hangarToggle?.onValueChanged.AddListener(OnHangarToggleChanged);
            lobbyToggle?.onValueChanged.AddListener(OnLobbyToggleChanged);
            settingToggle?.onValueChanged.AddListener(OnSettingToggleChanged);
        }

        private void OnHangarToggleChanged(bool isOn)
        {
            if (isOn && !suppressToggleCallbacks)
            {
                UIManager.Instance?.SwitchPage(UIPageType.Hangar);
            }
        }

        private void OnLobbyToggleChanged(bool isOn)
        {
            if (isOn && !suppressToggleCallbacks)
            {
                UIManager.Instance?.SwitchPage(UIPageType.Lobby);
            }
        }

        private void OnSettingToggleChanged(bool isOn)
        {
            if (isOn && !suppressToggleCallbacks)
            {
                UIManager.Instance?.SwitchPage(UIPageType.Setting);
            }
        }
    }
}
