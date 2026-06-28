using UnityEngine;
using UnityEngine.UI;

namespace LeiTing.UI
{
    public class PlaneItem : MonoBehaviour
    {
        private PlaneData data;
        private Text nameText;
        private Text statsText;
        private Text actionText;
        private Button actionButton;
        private Image actionImage;
        private bool built;

        public void SetData(PlaneData planeData)
        {
            data = planeData;
            BuildDefaultView();
            RefreshView();
        }

        private void BuildDefaultView()
        {
            if (built)
            {
                return;
            }

            built = true;

            var rect = GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 174f);

            var layoutElement = gameObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = 174f;
            layoutElement.preferredHeight = 174f;

            var background = gameObject.AddComponent<Image>();
            background.color = new Color(0.035f, 0.055f, 0.09f, 0.86f);

            var layout = gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(22, 22, 18, 18);
            layout.spacing = 22f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var iconRect = UIFactory.CreateRect("Icon", transform);
            iconRect.sizeDelta = new Vector2(116f, 116f);
            var iconImage = iconRect.gameObject.AddComponent<Image>();
            iconImage.color = new Color(0.1f, 0.68f, 1f, 0.82f);
            var iconLabel = UIFactory.CreateText("IconLabel", iconRect, "▲", 54f, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Stretch(iconLabel.rectTransform);

            var infoRect = UIFactory.CreateRect("Info", transform);
            infoRect.sizeDelta = new Vector2(0f, 116f);
            var infoLayout = infoRect.gameObject.AddComponent<VerticalLayoutGroup>();
            infoLayout.spacing = 8f;
            infoLayout.childControlHeight = false;
            infoLayout.childControlWidth = true;
            infoLayout.childForceExpandHeight = false;
            infoLayout.childForceExpandWidth = true;

            var infoElement = infoRect.gameObject.AddComponent<LayoutElement>();
            infoElement.flexibleWidth = 1f;

            nameText = UIFactory.CreateText("Name", infoRect, string.Empty, 34f, TextAnchor.MiddleLeft, Color.white);
            nameText.rectTransform.sizeDelta = new Vector2(0f, 46f);

            statsText = UIFactory.CreateText("Stats", infoRect, string.Empty, 24f, TextAnchor.UpperLeft, UIFactory.MutedTextColor);
            statsText.rectTransform.sizeDelta = new Vector2(0f, 58f);

            actionButton = UIFactory.CreateButton("ActionButton", transform, string.Empty, new Color(0.07f, 0.62f, 1f, 0.96f), out actionText, out actionImage);
            var actionRect = actionButton.GetComponent<RectTransform>();
            actionRect.sizeDelta = new Vector2(188f, 86f);

            var actionElement = actionButton.gameObject.AddComponent<LayoutElement>();
            actionElement.minWidth = 188f;
            actionElement.preferredWidth = 188f;
            actionElement.minHeight = 86f;
            actionElement.preferredHeight = 86f;

            actionButton.onClick.AddListener(OnClickAction);
        }

        private void RefreshView()
        {
            if (data == null)
            {
                return;
            }

            nameText.text = data.name;
            statsText.text = $"生命 {data.hp}   攻击 {data.attack}\n射速 {data.fireRate:0.0}   速度 {data.moveSpeed:0.0}";

            if (data.owned && data.selected)
            {
                actionText.text = "使用中";
                actionButton.interactable = false;
                actionImage.color = new Color(0.25f, 0.65f, 0.32f, 0.94f);
            }
            else if (data.owned)
            {
                actionText.text = "使用";
                actionButton.interactable = true;
                actionImage.color = new Color(0.08f, 0.55f, 1f, 0.96f);
            }
            else
            {
                actionText.text = "获取";
                actionButton.interactable = true;
                actionImage.color = new Color(1f, 0.56f, 0.12f, 0.96f);
            }
        }

        private void OnClickAction()
        {
            if (data == null)
            {
                return;
            }

            if (data.owned)
            {
                PlaneManager.GetOrCreate().SelectPlane(data.id);
                HangarPage.Instance?.RefreshList();
                return;
            }

            UIManager.Instance?.OpenPopup(UIConfig.PlaneUnlockPopupName, data);
        }
    }
}
