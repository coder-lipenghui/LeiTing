using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LeiTing.UI
{
    public class BottomBar : MonoBehaviour
    {
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

            hangarButton = CreateNavButton(UIPageType.Hangar, "机库");
            lobbyButton = CreateNavButton(UIPageType.Lobby, "大厅");
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
