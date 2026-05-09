using UnityEngine;
using UnityEngine.UI;

namespace LeiTing.UI
{
    public class SettingPage : BasePage
    {
        [SerializeField] private Toggle musicToggle;
        [SerializeField] private Toggle soundToggle;
        [SerializeField] private Toggle vibrationToggle;

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

            musicToggle = UIFactory.CreateToggle("MusicToggle", panelRect, "音乐", out _);
            soundToggle = UIFactory.CreateToggle("SoundToggle", panelRect, "音效", out _);
            vibrationToggle = UIFactory.CreateToggle("VibrationToggle", panelRect, "震动", out _);
        }

        private void BindPrefabView()
        {
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
    }
}
