using System;
using UnityEngine;
using UnityEngine.UI;

namespace LeiTing.UI
{
    internal static class UIFactory
    {
        private static Font defaultFont;

        public static readonly Color PanelColor = new Color(0.035f, 0.05f, 0.09f, 0.88f);
        public static readonly Color PanelAccentColor = new Color(0.1f, 0.68f, 1f, 0.95f);
        public static readonly Color TextColor = new Color(0.9f, 0.96f, 1f, 1f);
        public static readonly Color MutedTextColor = new Color(0.56f, 0.68f, 0.78f, 1f);
        public static readonly Color WarningColor = new Color(1f, 0.72f, 0.18f, 1f);

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<RectTransform>();
        }

        public static Image CreatePanel(string name, Transform parent, Color color)
        {
            var rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static Text CreateText(
            string name,
            Transform parent,
            string text,
            float fontSize,
            TextAnchor alignment,
            Color color)
        {
            var rect = CreateRect(name, parent);
            var label = rect.gameObject.AddComponent<Text>();
            label.text = text;
            label.font = GetDefaultFont();
            label.fontSize = Mathf.RoundToInt(fontSize);
            label.fontStyle = FontStyle.Bold;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        public static Button CreateButton(
            string name,
            Transform parent,
            string text,
            Color normalColor,
            out Text label,
            out Image image)
        {
            var rect = CreateRect(name, parent);
            image = rect.gameObject.AddComponent<Image>();
            image.color = normalColor;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = CreateButtonColors(normalColor);

            label = CreateText("Label", rect, text, 30f, TextAnchor.MiddleCenter, Color.white);
            Stretch(label.rectTransform);
            return button;
        }

        public static Toggle CreateToggle(string name, Transform parent, string text, out Text label)
        {
            var root = CreateRect(name, parent);
            root.sizeDelta = new Vector2(760f, 88f);

            var panel = CreatePanel("Background", root, new Color(0.05f, 0.08f, 0.13f, 0.82f));
            Stretch(panel.rectTransform);

            label = CreateText("Label", root, text, 32f, TextAnchor.MiddleLeft, TextColor);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(28f, 0f);
            labelRect.offsetMax = new Vector2(-140f, 0f);

            var toggleObject = CreateRect("Toggle", root);
            toggleObject.anchorMin = new Vector2(1f, 0.5f);
            toggleObject.anchorMax = new Vector2(1f, 0.5f);
            toggleObject.pivot = new Vector2(1f, 0.5f);
            toggleObject.anchoredPosition = new Vector2(-30f, 0f);
            toggleObject.sizeDelta = new Vector2(84f, 52f);

            var background = toggleObject.gameObject.AddComponent<Image>();
            background.color = new Color(0.02f, 0.03f, 0.045f, 0.95f);

            var checkmarkRect = CreateRect("Checkmark", toggleObject);
            checkmarkRect.anchorMin = new Vector2(0.5f, 0.5f);
            checkmarkRect.anchorMax = new Vector2(0.5f, 0.5f);
            checkmarkRect.pivot = new Vector2(0.5f, 0.5f);
            checkmarkRect.anchoredPosition = Vector2.zero;
            checkmarkRect.sizeDelta = new Vector2(56f, 30f);

            var checkmark = checkmarkRect.gameObject.AddComponent<Image>();
            checkmark.color = PanelAccentColor;

            var toggle = toggleObject.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = checkmark;
            return toggle;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static void SetInset(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        public static ColorBlock CreateButtonColors(Color normalColor)
        {
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.Lerp(normalColor, Color.white, 0.2f);
            colors.pressedColor = Color.Lerp(normalColor, Color.black, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.24f, 0.27f, 0.3f, 0.62f);
            colors.colorMultiplier = 1f;
            return colors;
        }

        private static Font GetDefaultFont()
        {
            if (defaultFont != null)
            {
                return defaultFont;
            }

            defaultFont = TryGetBuiltinFont("Arial.ttf") ?? TryGetBuiltinFont("LegacyRuntime.ttf");
            return defaultFont;
        }

        private static Font TryGetBuiltinFont(string path)
        {
            try
            {
                return Resources.GetBuiltinResource<Font>(path);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        public static T FindComponentInChildren<T>(Transform root, string childName) where T : Component
        {
            if (root == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            foreach (var component in root.GetComponentsInChildren<T>(true))
            {
                if (component != null && component.name == childName)
                {
                    return component;
                }
            }

            return null;
        }
    }
}
