using System;
using UnityEngine;
using UnityEngine.UI;

namespace LeiTing.UI
{
    public sealed class RuntimeResourceDownloadView
    {
        private const string LoadingPrefabPath = "UI/Page/UILoading";
        private const string ProgressFillName = "ProgressFill";
        private const string PercentTextName = "PercentText";
        private const string StatusTextName = "StatusText";
        private const string RetryButtonName = "RetryButton";
        private const string ErrorDialogName = "ErrorDialog";
        private const string ErrorMessageTextName = "ErrorMessageText";
        private const string ErrorRetryButtonName = "ErrorRetryButton";

        private readonly GameObject root;
        private readonly Image fillImage;
        private readonly Text percentText;
        private readonly Text statusText;
        private readonly Button retryButton;
        private GameObject errorDialog;
        private Text errorMessageText;
        private Button errorRetryButton;

        private RuntimeResourceDownloadView(
            GameObject root,
            Image fillImage,
            Text percentText,
            Text statusText,
            Button retryButton,
            GameObject errorDialog,
            Text errorMessageText,
            Button errorRetryButton)
        {
            this.root = root;
            this.fillImage = fillImage;
            this.percentText = percentText;
            this.statusText = statusText;
            this.retryButton = retryButton;
            this.errorDialog = errorDialog;
            this.errorMessageText = errorMessageText;
            this.errorRetryButton = errorRetryButton;
        }

        public static RuntimeResourceDownloadView Create()
        {
            var root = CreateCanvasRoot();
            if (TryCreatePrefabContent(
                    root.transform,
                    out var fill,
                    out var percent,
                    out var status,
                    out var retryButton,
                    out var errorDialog,
                    out var errorMessageText,
                    out var errorRetryButton))
            {
                return new RuntimeResourceDownloadView(
                    root,
                    fill,
                    percent,
                    status,
                    retryButton,
                    errorDialog,
                    errorMessageText,
                    errorRetryButton);
            }

            CreateFallbackContent(
                root.transform,
                out fill,
                out percent,
                out status,
                out retryButton,
                out errorDialog,
                out errorMessageText,
                out errorRetryButton);
            return new RuntimeResourceDownloadView(
                root,
                fill,
                percent,
                status,
                retryButton,
                errorDialog,
                errorMessageText,
                errorRetryButton);
        }

        private static GameObject CreateCanvasRoot()
        {
            var root = new GameObject("RuntimeResourceDownloadView", typeof(RectTransform));
            UnityEngine.Object.DontDestroyOnLoad(root);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            root.AddComponent<GraphicRaycaster>();
            return root;
        }

        private static bool TryCreatePrefabContent(
            Transform parent,
            out Image fill,
            out Text percent,
            out Text status,
            out Button retryButton,
            out GameObject errorDialog,
            out Text errorMessageText,
            out Button errorRetryButton)
        {
            fill = null;
            percent = null;
            status = null;
            retryButton = null;
            errorDialog = null;
            errorMessageText = null;
            errorRetryButton = null;

            var prefab = Resources.Load<GameObject>(LoadingPrefabPath);
            if (prefab == null)
            {
                return false;
            }

            var content = UnityEngine.Object.Instantiate(prefab, parent, false);
            content.name = "UILoading";
            if (content.transform is RectTransform rect)
            {
                Stretch(rect);
            }

            fill = FindChildComponent<Image>(content.transform, ProgressFillName);
            percent = FindChildComponent<Text>(content.transform, PercentTextName) ?? FindChildComponent<Text>(content.transform, "Percent");
            status = FindChildComponent<Text>(content.transform, StatusTextName) ?? FindChildComponent<Text>(content.transform, "Status");
            retryButton = FindChildComponent<Button>(content.transform, RetryButtonName);
            errorDialog = FindChildTransform(content.transform, ErrorDialogName)?.gameObject;
            errorMessageText = FindChildComponent<Text>(content.transform, ErrorMessageTextName);
            errorRetryButton = FindChildComponent<Button>(content.transform, ErrorRetryButtonName);

            if (fill != null)
            {
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = 0;
                fill.fillAmount = 0f;
            }

            if (retryButton != null)
            {
                retryButton.gameObject.SetActive(false);
            }

            if (errorDialog != null)
            {
                errorDialog.SetActive(false);
            }

            if (fill == null || percent == null || status == null)
            {
                Debug.LogWarning($"UILoading prefab loaded, but one or more binding nodes are missing. Expected child names: {ProgressFillName}, {PercentTextName}, {StatusTextName}, {RetryButtonName}. Optional error dialog names: {ErrorDialogName}, {ErrorMessageTextName}, {ErrorRetryButtonName}.");
            }

            return true;
        }

        private static void CreateFallbackContent(
            Transform parent,
            out Image fill,
            out Text percent,
            out Text status,
            out Button retryButton,
            out GameObject errorDialog,
            out Text errorMessageText,
            out Button errorRetryButton)
        {
            var background = CreateImage("Background", parent, new Color(0.02f, 0.04f, 0.07f, 1f));
            Stretch(background.rectTransform);

            var title = CreateText("Title", parent, "DOWNLOADING RESOURCES", 44, FontStyle.Bold, TextAnchor.MiddleCenter);
            title.color = Color.white;
            ConfigureRect(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(720f, 80f), new Vector2(0f, 112f));

            var barBack = CreateImage("ProgressBackground", parent, new Color(0.12f, 0.2f, 0.28f, 1f));
            ConfigureRect(barBack.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(660f, 28f), new Vector2(0f, 12f));

            fill = CreateImage(ProgressFillName, barBack.transform, new Color(0.22f, 0.74f, 1f, 1f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 0f;
            Stretch(fill.rectTransform);

            percent = CreateText(PercentTextName, parent, "0%", 30, FontStyle.Bold, TextAnchor.MiddleCenter);
            percent.color = new Color(0.86f, 0.96f, 1f, 1f);
            ConfigureRect(percent.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(240f, 48f), new Vector2(0f, -48f));

            status = CreateText(StatusTextName, parent, "CONNECTING", 24, FontStyle.Normal, TextAnchor.MiddleCenter);
            status.color = new Color(0.65f, 0.78f, 0.9f, 1f);
            ConfigureRect(status.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(720f, 48f), new Vector2(0f, -96f));

            retryButton = CreateRetryButton(parent);
            retryButton.gameObject.SetActive(false);
            CreateFallbackErrorDialog(parent, out errorDialog, out errorMessageText, out errorRetryButton);
        }

        public void SetProgress(float progress, string status)
        {
            SetErrorDialogVisible(false);

            var clamped = Mathf.Clamp01(progress);
            if (fillImage != null)
            {
                fillImage.fillAmount = clamped;
            }

            if (percentText != null)
            {
                percentText.text = Mathf.RoundToInt(clamped * 100f) + "%";
            }

            if (statusText != null && !string.IsNullOrWhiteSpace(status))
            {
                statusText.text = status;
            }
        }

        public void ShowRetry(string message, Action retry)
        {
            Debug.LogError($"CDN resource loading failed: {message}");

            if (statusText != null)
            {
                statusText.text = string.IsNullOrWhiteSpace(message) ? "DOWNLOAD FAILED" : "DOWNLOAD FAILED";
            }

            if (retryButton != null)
            {
                retryButton.onClick.RemoveAllListeners();
                retryButton.onClick.AddListener(() => retry?.Invoke());
                retryButton.gameObject.SetActive(true);
            }

            EnsureErrorDialog();
            if (errorMessageText != null)
            {
                errorMessageText.text = string.IsNullOrWhiteSpace(message)
                    ? "CDN resource loading failed. Please check CDN configuration and network."
                    : message;
            }

            if (errorRetryButton != null)
            {
                errorRetryButton.onClick.RemoveAllListeners();
                errorRetryButton.onClick.AddListener(() => retry?.Invoke());
            }

            SetErrorDialogVisible(true);
        }

        public void Destroy()
        {
            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        private void EnsureErrorDialog()
        {
            if (errorDialog != null)
            {
                return;
            }

            CreateFallbackErrorDialog(root.transform, out errorDialog, out errorMessageText, out errorRetryButton);
        }

        private void SetErrorDialogVisible(bool visible)
        {
            if (errorDialog != null)
            {
                errorDialog.SetActive(visible);
            }
        }

        private static Button CreateRetryButton(Transform parent)
        {
            var image = CreateImage(RetryButtonName, parent, new Color(0.1f, 0.48f, 0.76f, 1f));
            ConfigureRect(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(260f, 76f), new Vector2(0f, -188f));
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var label = CreateText("Label", image.transform, "RETRY", 28, FontStyle.Bold, TextAnchor.MiddleCenter);
            label.color = Color.white;
            Stretch(label.rectTransform);
            return button;
        }

        private static void CreateFallbackErrorDialog(
            Transform parent,
            out GameObject dialog,
            out Text messageText,
            out Button retryButton)
        {
            var overlay = CreateImage(ErrorDialogName, parent, new Color(0f, 0f, 0f, 0.64f));
            Stretch(overlay.rectTransform);
            overlay.raycastTarget = true;
            dialog = overlay.gameObject;

            var panel = CreateImage("Panel", overlay.transform, new Color(0.06f, 0.1f, 0.14f, 0.96f));
            ConfigureRect(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(760f, 360f), Vector2.zero);

            var title = CreateText("Title", panel.transform, "RESOURCE LOAD FAILED", 34, FontStyle.Bold, TextAnchor.MiddleCenter);
            title.color = Color.white;
            ConfigureRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(680f, 70f), new Vector2(0f, -64f));

            messageText = CreateText(ErrorMessageTextName, panel.transform, string.Empty, 22, FontStyle.Normal, TextAnchor.MiddleCenter);
            messageText.color = new Color(0.84f, 0.92f, 1f, 1f);
            messageText.horizontalOverflow = HorizontalWrapMode.Wrap;
            messageText.verticalOverflow = VerticalWrapMode.Truncate;
            ConfigureRect(messageText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(660f, 130f), new Vector2(0f, 14f));

            var retryImage = CreateImage(ErrorRetryButtonName, panel.transform, new Color(0.1f, 0.48f, 0.76f, 1f));
            ConfigureRect(retryImage.rectTransform, new Vector2(0.5f, 0f), new Vector2(260f, 76f), new Vector2(0f, 70f));
            retryButton = retryImage.gameObject.AddComponent<Button>();
            retryButton.targetGraphic = retryImage;

            var label = CreateText("Label", retryImage.transform, "RETRY", 28, FontStyle.Bold, TextAnchor.MiddleCenter);
            label.color = Color.white;
            Stretch(label.rectTransform);

            dialog.SetActive(false);
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var image = obj.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(string name, Transform parent, string text, int fontSize, FontStyle fontStyle, TextAnchor alignment)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var label = obj.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.alignment = alignment;
            label.raycastTarget = false;
            return label;
        }

        private static Transform FindChildTransform(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != null && child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }

        private static T FindChildComponent<T>(Transform root, string childName) where T : Component
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

        private static void ConfigureRect(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 anchoredPosition)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
