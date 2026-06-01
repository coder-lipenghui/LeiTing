using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LeiTing.UI
{
    [DisallowMultipleComponent]
    public sealed class DefeatGameOverView : MonoBehaviour
    {
        private const float LetterHeight = 138f;
        private const float LetterSpacing = 18f;
        private const float GroupVerticalOffset = 86f;
        private const float WobbleDuration = 5f;
        private const float WobblePeriod = 2.85f;
        private const float WobbleAngle = 7.5f;
        private const float WobbleTilt = 13f;
        private const float WobbleScale = 0.11f;
        private const float WobbleYOffset = 11f;
        private const float LetterDisappearDelay = 0.12f;
        private const float LetterDisappearDuration = 0.42f;

        private readonly List<LetterVisual> letters = new List<LetterVisual>();

        private RectTransform gameGroup;
        private RectTransform overGroup;
        private Vector2 gameBasePosition;
        private Vector2 overBasePosition;
        private float animationAge;
        private bool isPlaying;
        private bool hasBuilt;

        public void Build(Sprite[] gameSprites, Sprite[] overSprites)
        {
            if (hasBuilt)
            {
                return;
            }

            hasBuilt = true;
            letters.Clear();

            var root = GetComponent<RectTransform>();
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(760f, 360f);

            gameGroup = CreateGroup("GAME", transform, new Vector2(0f, GroupVerticalOffset));
            overGroup = CreateGroup("OVER", transform, new Vector2(0f, -GroupVerticalOffset));
            gameBasePosition = gameGroup.anchoredPosition;
            overBasePosition = overGroup.anchoredPosition;

            BuildLetters(gameGroup, "GAME", gameSprites);
            BuildLetters(overGroup, "OVER", overSprites);
            gameObject.SetActive(false);
        }

        public void Play()
        {
            if (!hasBuilt)
            {
                return;
            }

            gameObject.SetActive(true);
            animationAge = 0f;
            isPlaying = true;
            ResetLetters();
            ApplyWobble(0f);
        }

        public void Hide()
        {
            isPlaying = false;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!isPlaying)
            {
                return;
            }

            animationAge += Time.unscaledDeltaTime;

            if (animationAge <= WobbleDuration)
            {
                ApplyWobble(animationAge);
                return;
            }

            ApplyWobble(animationAge);
            UpdateDisappear(animationAge - WobbleDuration);
        }

        private static RectTransform CreateGroup(string groupName, Transform parent, Vector2 anchoredPosition)
        {
            var groupObject = new GameObject(groupName, typeof(RectTransform));
            groupObject.transform.SetParent(parent, false);

            var rect = groupObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(540f, 150f);
            return rect;
        }

        private void BuildLetters(RectTransform group, string word, Sprite[] sprites)
        {
            var widths = new float[word.Length];
            var totalWidth = 0f;
            for (var index = 0; index < word.Length; index++)
            {
                var sprite = sprites != null && index < sprites.Length ? sprites[index] : null;
                var aspect = sprite != null && sprite.rect.height > 0f
                    ? sprite.rect.width / sprite.rect.height
                    : 0.75f;
                widths[index] = LetterHeight * aspect;
                totalWidth += widths[index];
            }

            totalWidth += Mathf.Max(0, word.Length - 1) * LetterSpacing;

            var cursor = -totalWidth * 0.5f;
            for (var index = 0; index < word.Length; index++)
            {
                var letterObject = new GameObject(word[index].ToString(), typeof(RectTransform));
                letterObject.transform.SetParent(group, false);

                var rect = letterObject.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(widths[index], LetterHeight);
                rect.anchoredPosition = new Vector2(cursor + widths[index] * 0.5f, 0f);

                var image = letterObject.AddComponent<Image>();
                image.sprite = sprites != null && index < sprites.Length ? sprites[index] : null;
                image.preserveAspect = true;
                image.raycastTarget = false;

                letters.Add(new LetterVisual(rect, image));
                cursor += widths[index] + LetterSpacing;
            }
        }

        private void ResetLetters()
        {
            foreach (var letter in letters)
            {
                letter.Reset();
            }
        }

        private void ApplyWobble(float age)
        {
            var phase = age * Mathf.PI * 2f / WobblePeriod;
            ApplyGroupWobble(gameGroup, gameBasePosition, phase);
            ApplyGroupWobble(overGroup, overBasePosition, phase);
        }

        private static void ApplyGroupWobble(RectTransform group, Vector2 basePosition, float phase)
        {
            if (group == null)
            {
                return;
            }

            var wave = Mathf.Sin(phase);
            var orbit = Mathf.Cos(phase);
            group.anchoredPosition = basePosition + new Vector2(orbit * 10f, wave * WobbleYOffset);
            group.localRotation = Quaternion.Euler(
                -orbit * WobbleTilt,
                wave * WobbleTilt * 0.62f,
                WobbleAngle + wave * WobbleAngle);
            group.localScale = new Vector3(
                1f + wave * WobbleScale,
                1f - wave * WobbleScale * 0.36f,
                1f);
        }

        private void UpdateDisappear(float disappearAge)
        {
            var allHidden = true;
            for (var index = 0; index < letters.Count; index++)
            {
                var start = index * LetterDisappearDelay;
                var progress = Mathf.Clamp01((disappearAge - start) / LetterDisappearDuration);
                var eased = EaseOutCubic(progress);
                letters[index].ApplyDisappear(eased);

                if (progress < 1f)
                {
                    allHidden = false;
                }
            }

            if (allHidden)
            {
                isPlaying = false;
            }
        }

        private static float EaseOutCubic(float t)
        {
            var inverse = 1f - Mathf.Clamp01(t);
            return 1f - inverse * inverse * inverse;
        }

        private sealed class LetterVisual
        {
            private readonly RectTransform rect;
            private readonly Image image;
            private readonly Vector2 basePosition;
            private readonly Vector3 baseScale;
            private readonly Quaternion baseRotation;
            private readonly Color baseColor;

            public LetterVisual(RectTransform rect, Image image)
            {
                this.rect = rect;
                this.image = image;
                basePosition = rect != null ? rect.anchoredPosition : Vector2.zero;
                baseScale = rect != null ? rect.localScale : Vector3.one;
                baseRotation = rect != null ? rect.localRotation : Quaternion.identity;
                baseColor = image != null ? image.color : Color.white;
            }

            public void Reset()
            {
                if (rect != null)
                {
                    rect.anchoredPosition = basePosition;
                    rect.localScale = baseScale;
                    rect.localRotation = baseRotation;
                }

                if (image != null)
                {
                    image.color = baseColor;
                    image.enabled = true;
                }
            }

            public void ApplyDisappear(float eased)
            {
                if (rect != null)
                {
                    rect.anchoredPosition = basePosition + new Vector2(0f, 54f * eased);
                    rect.localScale = baseScale * (1f + 0.28f * eased);
                    rect.localRotation = baseRotation * Quaternion.Euler(0f, 0f, -18f * eased);
                }

                if (image != null)
                {
                    var color = baseColor;
                    color.a = Mathf.Lerp(baseColor.a, 0f, eased);
                    image.color = color;
                    image.enabled = eased < 1f;
                }
            }
        }
    }
}
