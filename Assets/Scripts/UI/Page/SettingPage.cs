using LeiTing.Core;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LeiTing.UI
{
    public class SettingPage : BasePage
    {
        private const string CloseButtonSpritePath = "Assets/Art/Sprites/UI/btnBack.png";

        [SerializeField] private Toggle musicToggle;
        [SerializeField] private Toggle soundToggle;
        [SerializeField] private Toggle vibrationToggle;
        [SerializeField] private Button closeButton;

        public override void OnCreate()
        {
            if (transform.childCount == 0)
            {
                BuildDefaultView();
            }

            BindPrefabView();
            BindEvents();
            RefreshToggles();
        }

        public override void OnShow()
        {
            RefreshToggles();
        }

        private void BuildDefaultView()
        {
            UIFactory.Stretch(RectTransform);

            var backdrop = UIFactory.CreatePanel("SettingBackdrop", transform, new Color(0.014f, 0.022f, 0.04f, 0.96f));
            UIFactory.Stretch(backdrop.rectTransform);

            var title = UIFactory.CreateText("Title", transform, "设置", 54f, TextAnchor.MiddleLeft, Color.white);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -170f);
            titleRect.offsetMin = new Vector2(56f, titleRect.offsetMin.y);
            titleRect.offsetMax = new Vector2(-56f, titleRect.offsetMax.y);
            titleRect.sizeDelta = new Vector2(titleRect.sizeDelta.x, 72f);

            var panel = UIFactory.CreatePanel("SettingPanel", transform, new Color(0.035f, 0.055f, 0.09f, 0.82f));
            var panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(0f, 40f);
            panelRect.sizeDelta = new Vector2(820f, 420f);

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 34, 34);
            layout.spacing = 26f;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            closeButton = CreateCloseButton(transform);

            musicToggle = UIFactory.CreateToggle("MusicToggle", panelRect, "音乐", out _);
            soundToggle = UIFactory.CreateToggle("SoundToggle", panelRect, "音效", out _);
            vibrationToggle = UIFactory.CreateToggle("VibrationToggle", panelRect, "震动", out _);
        }

        private void BindPrefabView()
        {
            closeButton = closeButton != null
                ? closeButton
                : UIFactory.FindComponentInChildren<Button>(transform, "BtnClose") ?? CreateCloseButton(transform);
            ConfigureCloseButton(closeButton);
            musicToggle = musicToggle != null
                ? musicToggle
                : UIFactory.FindComponentInChildren<Toggle>(transform, "MusicToggle");
            soundToggle = soundToggle != null
                ? soundToggle
                : UIFactory.FindComponentInChildren<Toggle>(transform, "SoundToggle");
            vibrationToggle = vibrationToggle != null
                ? vibrationToggle
                : UIFactory.FindComponentInChildren<Toggle>(transform, "VibrationToggle");
        }

        private void BindEvents()
        {
            if (musicToggle != null)
            {
                musicToggle.onValueChanged.RemoveListener(OnMusicToggleChanged);
                musicToggle.onValueChanged.AddListener(OnMusicToggleChanged);
            }

            if (soundToggle != null)
            {
                soundToggle.onValueChanged.RemoveListener(OnSoundToggleChanged);
                soundToggle.onValueChanged.AddListener(OnSoundToggleChanged);
            }

            if (vibrationToggle != null)
            {
                vibrationToggle.onValueChanged.RemoveListener(OnVibrationToggleChanged);
                vibrationToggle.onValueChanged.AddListener(OnVibrationToggleChanged);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(OnClickClose);
                closeButton.onClick.AddListener(OnClickClose);
            }
        }

        private void RefreshToggles()
        {
            musicToggle?.SetIsOnWithoutNotify(GameSettingManager.MusicEnabled);
            soundToggle?.SetIsOnWithoutNotify(GameSettingManager.SoundEnabled);
            vibrationToggle?.SetIsOnWithoutNotify(GameSettingManager.VibrationEnabled);
        }

        private static void OnMusicToggleChanged(bool value)
        {
            GameSettingManager.MusicEnabled = value;
        }

        private static void OnSoundToggleChanged(bool value)
        {
            GameSettingManager.SoundEnabled = value;
        }

        private static void OnVibrationToggleChanged(bool value)
        {
            GameSettingManager.VibrationEnabled = value;
        }

        private static void OnClickClose()
        {
            UIManager.Instance?.ClosePage(UIPageType.Setting);
        }

        private static Button CreateCloseButton(Transform parent)
        {
            var button = UIFactory.CreateButton("BtnClose", parent, "X", Color.white, out var label, out var image);
            ConfigureCloseButton(button, label, image);
            return button;
        }

        private static void ConfigureCloseButton(Button button)
        {
            if (button == null)
            {
                return;
            }

            var label = button.GetComponentInChildren<Text>(true);
            var image = button.targetGraphic as Image ?? button.GetComponentInChildren<Image>(true);
            ConfigureCloseButton(button, label, image);
        }

        private static void ConfigureCloseButton(Button button, Text label, Image image)
        {
            if (button == null)
            {
                return;
            }

            if (label != null)
            {
                label.fontSize = 34;
                label.text = string.Empty;
            }

            var sprite = LoadSprite(CloseButtonSpritePath);
            if (image != null && sprite != null)
            {
                image.sprite = sprite;
                image.color = Color.white;
            }
            else if (image != null)
            {
                image.color = new Color(0f, 0f, 0f, 0.42f);
            }

            if (image != null)
            {
                button.targetGraphic = image;
            }

            var rect = button.GetComponent<RectTransform>();
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 86f);
            rect.sizeDelta = new Vector2(118f, 118f);
        }

        private static Sprite LoadSprite(string assetPath)
        {
#if UNITY_EDITOR
            if (RuntimeRemoteResourceManager.CanUseEditorLocalAssets)
            {
                var editorSprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (editorSprite != null)
                {
                    return editorSprite;
                }
            }
#endif

            return RuntimeAssetCatalog.LoadSprite(assetPath);
        }
    }
}
