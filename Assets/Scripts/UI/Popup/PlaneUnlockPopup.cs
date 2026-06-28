using UnityEngine;
using UnityEngine.UI;

namespace LeiTing.UI
{
    public class PlaneUnlockPopup : BasePopup
    {
        private PlaneData data;
        private Text nameText;
        private Text statsText;
        private Text progressText;
        private Text watchAdText;
        private Button watchAdButton;
        private Button closeButton;
        private bool built;

        private void Awake()
        {
            BuildDefaultView();
        }

        public override void OnOpen(object popupData = null)
        {
            base.OnOpen(popupData);
            BuildDefaultView();
            data = popupData as PlaneData;
            RefreshView();
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
            rect.sizeDelta = new Vector2(820f, 850f);

            var background = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            background.color = new Color(0.03f, 0.045f, 0.08f, 0.96f);

            var title = UIFactory.CreateText("Title", transform, "飞机获取", 44f, TextAnchor.MiddleCenter, Color.white);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -30f);
            titleRect.sizeDelta = new Vector2(0f, 64f);

            var iconPanel = UIFactory.CreatePanel("PlanePanel", transform, new Color(0.06f, 0.095f, 0.15f, 0.9f));
            var iconRect = iconPanel.rectTransform;
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.anchoredPosition = new Vector2(0f, -120f);
            iconRect.sizeDelta = new Vector2(560f, 260f);

            var icon = UIFactory.CreateText("PlaneIcon", iconRect, "▲", 116f, TextAnchor.MiddleCenter, UIFactory.PanelAccentColor);
            UIFactory.Stretch(icon.rectTransform);

            nameText = UIFactory.CreateText("Name", transform, string.Empty, 40f, TextAnchor.MiddleCenter, Color.white);
            var nameRect = nameText.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.anchoredPosition = new Vector2(0f, -404f);
            nameRect.sizeDelta = new Vector2(0f, 56f);

            statsText = UIFactory.CreateText("Stats", transform, string.Empty, 29f, TextAnchor.MiddleCenter, UIFactory.TextColor);
            var statRect = statsText.rectTransform;
            statRect.anchorMin = new Vector2(0f, 1f);
            statRect.anchorMax = new Vector2(1f, 1f);
            statRect.pivot = new Vector2(0.5f, 1f);
            statRect.anchoredPosition = new Vector2(0f, -470f);
            statRect.sizeDelta = new Vector2(0f, 105f);

            progressText = UIFactory.CreateText("Progress", transform, string.Empty, 30f, TextAnchor.MiddleCenter, UIFactory.WarningColor);
            var progressRect = progressText.rectTransform;
            progressRect.anchorMin = new Vector2(0f, 1f);
            progressRect.anchorMax = new Vector2(1f, 1f);
            progressRect.pivot = new Vector2(0.5f, 1f);
            progressRect.anchoredPosition = new Vector2(0f, -610f);
            progressRect.sizeDelta = new Vector2(0f, 54f);

            watchAdButton = UIFactory.CreateButton("WatchAdButton", transform, "观看广告", new Color(1f, 0.56f, 0.12f, 0.96f), out watchAdText, out _);
            var watchRect = watchAdButton.GetComponent<RectTransform>();
            watchRect.anchorMin = new Vector2(0.5f, 0f);
            watchRect.anchorMax = new Vector2(0.5f, 0f);
            watchRect.pivot = new Vector2(0.5f, 0f);
            watchRect.anchoredPosition = new Vector2(0f, 118f);
            watchRect.sizeDelta = new Vector2(460f, 96f);
            watchAdButton.onClick.AddListener(OnClickWatchAd);

            closeButton = UIFactory.CreateButton("CloseButton", transform, "关闭", new Color(0.09f, 0.13f, 0.18f, 0.94f), out _, out _);
            var closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0f);
            closeRect.anchoredPosition = new Vector2(0f, 34f);
            closeRect.sizeDelta = new Vector2(300f, 68f);
            closeButton.onClick.AddListener(() => UIManager.Instance?.ClosePopup(PopupName));
        }

        private void RefreshView()
        {
            if (data == null)
            {
                return;
            }

            var latest = PlaneManager.GetOrCreate().GetPlane(data.id) ?? data;
            data = latest;

            nameText.text = data.name;
            statsText.text = $"生命 {data.hp}    攻击 {data.attack}\n射速 {data.fireRate:0.0}    速度 {data.moveSpeed:0.0}";
            progressText.text = $"广告进度 {data.adCountWatched}/{data.adCountRequired}";

            if (data.owned)
            {
                watchAdText.text = "已解锁";
                watchAdButton.interactable = false;
            }
            else
            {
                watchAdText.text = "观看广告";
                watchAdButton.interactable = true;
            }
        }

        private async void OnClickWatchAd()
        {
            if (data == null || watchAdButton == null)
            {
                return;
            }

            watchAdButton.interactable = false;
            Debug.LogWarning($"[PlaneUnlockPopup] Requesting reward ad. planeId={data.id}");
            var success = await AdManager.GetOrCreate().ShowRewardAd($"PlaneUnlock:{data.id}");
            Debug.LogWarning($"[PlaneUnlockPopup] Reward ad result. planeId={data.id}, success={success}");

            if (!success)
            {
                watchAdButton.interactable = true;
                return;
            }

            PlaneManager.GetOrCreate().AddAdProgress(data.id);
            data = PlaneManager.GetOrCreate().GetPlane(data.id);

            if (data != null && data.owned)
            {
                UIManager.Instance?.ClosePopup(PopupName);
                UIManager.Instance?.OpenPopup(UIConfig.PlaneUnlockSuccessPopupName, data);
                HangarPage.Instance?.RefreshList();
                return;
            }

            RefreshView();
        }
    }
}
