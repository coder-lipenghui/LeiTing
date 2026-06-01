using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace LeiTing.UI
{
    [DisallowMultipleComponent]
    public sealed class CircularProgress : MonoBehaviour
    {
        private const string DefaultBackgroundName = "bg";
        private const string DefaultBarName = "bar";

        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image barImage;
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private Sprite barSprite;
        [SerializeField, Range(0f, 1f)] private float progress = 1f;

        public float Progress => progress;

        public void SetProgress(float value)
        {
            progress = Mathf.Clamp01(value);
            Apply();
        }

        public void SetSprites(Sprite background, Sprite bar)
        {
            backgroundSprite = background;
            barSprite = bar;
            Apply();
        }

        public static float ParseProgressText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0f;
            }

            var trimmed = value.Trim();
            var slashIndex = trimmed.IndexOf("/", StringComparison.Ordinal);
            if (slashIndex > 0 && slashIndex < trimmed.Length - 1)
            {
                if (float.TryParse(trimmed.Substring(0, slashIndex), NumberStyles.Float, CultureInfo.InvariantCulture, out var current)
                    && float.TryParse(trimmed.Substring(slashIndex + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var total)
                    && total > 0f)
                {
                    return Mathf.Clamp01(current / total);
                }
            }

            var isPercent = trimmed.EndsWith("%", StringComparison.Ordinal);
            if (isPercent)
            {
                trimmed = trimmed.Substring(0, trimmed.Length - 1);
            }

            if (!float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                return 0f;
            }

            return Mathf.Clamp01(isPercent ? number / 100f : number);
        }

        private void Awake()
        {
            Apply();
        }

        private void OnEnable()
        {
            Apply();
        }

#if UNITY_EDITOR
        private void Reset()
        {
            AutoResolveImages();
            Apply();
        }

        private void OnValidate()
        {
            progress = Mathf.Clamp01(progress);
            AutoResolveImages();
            Apply();
        }
#endif

        private void Apply()
        {
            AutoResolveImages();
            ApplyBackground();
            ApplyBar();
        }

        private void ApplyBackground()
        {
            if (backgroundImage == null)
            {
                return;
            }

            if (backgroundSprite != null)
            {
                backgroundImage.sprite = backgroundSprite;
            }

            backgroundImage.type = Image.Type.Simple;
            backgroundImage.fillAmount = 1f;
            backgroundImage.preserveAspect = true;
            backgroundImage.raycastTarget = false;
        }

        private void ApplyBar()
        {
            if (barImage == null)
            {
                return;
            }

            if (barSprite != null)
            {
                barImage.sprite = barSprite;
            }

            barImage.type = Image.Type.Filled;
            barImage.fillMethod = Image.FillMethod.Radial360;
            barImage.fillOrigin = (int)Image.Origin360.Top;
            barImage.fillClockwise = true;
            barImage.fillAmount = progress;
            barImage.preserveAspect = true;
            barImage.raycastTarget = false;
        }

        private void AutoResolveImages()
        {
            if (backgroundImage == null)
            {
                backgroundImage = FindImage(DefaultBackgroundName);
            }

            if (barImage == null)
            {
                barImage = FindImage(DefaultBarName);
            }
        }

        private Image FindImage(string imageName)
        {
            foreach (var image in GetComponentsInChildren<Image>(true))
            {
                if (image != null && string.Equals(image.name, imageName, StringComparison.OrdinalIgnoreCase))
                {
                    return image;
                }
            }

            return null;
        }
    }
}
