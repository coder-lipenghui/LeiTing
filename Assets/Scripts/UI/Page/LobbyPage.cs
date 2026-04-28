using LeiTing.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LeiTing.UI
{
    public class LobbyPage : BasePage
    {
        [SerializeField] private Button startGameButton;

        private TextMeshProUGUI planeNameText;
        private TextMeshProUGUI planeStatsText;

        public override void OnCreate()
        {
            BuildDefaultView();
        }

        public override void OnShow()
        {
            RefreshSelectedPlane();

            if (startGameButton != null)
            {
                startGameButton.interactable = true;
            }
        }

        public void SetSelectedPlane(int planeId)
        {
            var plane = PlaneManager.GetOrCreate().GetPlane(planeId);
            RefreshSelectedPlane(plane);
        }

        private void BuildDefaultView()
        {
            UIFactory.Stretch(RectTransform);

            var backdrop = UIFactory.CreatePanel("LobbyBackdrop", transform, new Color(0.015f, 0.02f, 0.045f, 0.96f));
            UIFactory.Stretch(backdrop.rectTransform);

            var title = UIFactory.CreateText("Title", transform, "雷霆战机", 72f, TextAlignmentOptions.Center, Color.white);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -190f);
            titleRect.sizeDelta = new Vector2(760f, 120f);

            var preview = UIFactory.CreatePanel("PlanePreview", transform, new Color(0.04f, 0.08f, 0.13f, 0.78f));
            var previewRect = preview.rectTransform;
            previewRect.anchorMin = new Vector2(0.5f, 0.5f);
            previewRect.anchorMax = new Vector2(0.5f, 0.5f);
            previewRect.pivot = new Vector2(0.5f, 0.5f);
            previewRect.anchoredPosition = new Vector2(0f, 80f);
            previewRect.sizeDelta = new Vector2(720f, 520f);

            var planeIcon = UIFactory.CreateText("PlaneIcon", previewRect, "▲", 150f, TextAlignmentOptions.Center, UIFactory.PanelAccentColor);
            var iconRect = planeIcon.rectTransform;
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(0f, 62f);
            iconRect.sizeDelta = new Vector2(360f, 220f);

            planeNameText = UIFactory.CreateText("PlaneName", previewRect, string.Empty, 40f, TextAlignmentOptions.Center, Color.white);
            var nameRect = planeNameText.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 0f);
            nameRect.anchorMax = new Vector2(1f, 0f);
            nameRect.pivot = new Vector2(0.5f, 0f);
            nameRect.anchoredPosition = new Vector2(0f, 126f);
            nameRect.sizeDelta = new Vector2(0f, 56f);

            planeStatsText = UIFactory.CreateText("PlaneStats", previewRect, string.Empty, 28f, TextAlignmentOptions.Center, UIFactory.MutedTextColor);
            var statRect = planeStatsText.rectTransform;
            statRect.anchorMin = new Vector2(0f, 0f);
            statRect.anchorMax = new Vector2(1f, 0f);
            statRect.pivot = new Vector2(0.5f, 0f);
            statRect.anchoredPosition = new Vector2(0f, 66f);
            statRect.sizeDelta = new Vector2(0f, 52f);

            startGameButton = UIFactory.CreateButton("StartGameButton", transform, "开始游戏", new Color(0.05f, 0.62f, 1f, 0.96f), out _, out _);
            var buttonRect = startGameButton.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0f);
            buttonRect.anchorMax = new Vector2(0.5f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0f);
            buttonRect.anchoredPosition = new Vector2(0f, 250f);
            buttonRect.sizeDelta = new Vector2(500f, 112f);
            startGameButton.onClick.AddListener(OnClickStartGame);

            RefreshSelectedPlane();
        }

        private void RefreshSelectedPlane()
        {
            RefreshSelectedPlane(PlaneManager.GetOrCreate().GetSelectedPlane());
        }

        private void RefreshSelectedPlane(PlaneData plane)
        {
            if (plane == null)
            {
                return;
            }

            if (planeNameText != null)
            {
                planeNameText.text = plane.name;
            }

            if (planeStatsText != null)
            {
                planeStatsText.text = $"HP {plane.hp}   ATK {plane.attack}   RATE {plane.fireRate:0.0}   SPD {plane.moveSpeed:0.0}";
            }
        }

        private void OnClickStartGame()
        {
            if (startGameButton != null)
            {
                startGameButton.interactable = false;
            }

            GameSceneManager.GetOrCreate().EnterBattle();
        }
    }
}
