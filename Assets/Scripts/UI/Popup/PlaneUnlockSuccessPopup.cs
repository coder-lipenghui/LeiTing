using UnityEngine;
using UnityEngine.UI;

namespace LeiTing.UI
{
    public class PlaneUnlockSuccessPopup : BasePopup
    {
        private Text messageText;
        private Button closeButton;
        private bool built;

        private void Awake()
        {
            BuildDefaultView();
        }

        public override void OnOpen(object data = null)
        {
            base.OnOpen(data);
            BuildDefaultView();

            var plane = data as PlaneData;
            messageText.text = plane != null ? $"{plane.name}\n已加入机库" : "飞机已解锁";
        }

        private void BuildDefaultView()
        {
            if (built)
            {
                return;
            }

            built = true;

            var rect = RectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(680f, 420f);

            var background = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            background.color = new Color(0.03f, 0.05f, 0.08f, 0.96f);

            messageText = UIFactory.CreateText("Message", transform, string.Empty, 42f, TextAnchor.MiddleCenter, Color.white);
            var messageRect = messageText.rectTransform;
            messageRect.anchorMin = new Vector2(0f, 0f);
            messageRect.anchorMax = new Vector2(1f, 1f);
            messageRect.offsetMin = new Vector2(44f, 128f);
            messageRect.offsetMax = new Vector2(-44f, -44f);

            closeButton = UIFactory.CreateButton("CloseButton", transform, "确定", new Color(0.05f, 0.62f, 1f, 0.96f), out _, out _);
            var closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0f);
            closeRect.anchoredPosition = new Vector2(0f, 44f);
            closeRect.sizeDelta = new Vector2(320f, 86f);
            closeButton.onClick.AddListener(() => UIManager.Instance?.ClosePopup(PopupName));
        }
    }
}
