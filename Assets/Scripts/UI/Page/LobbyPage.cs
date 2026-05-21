using LeiTing.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LeiTing.UI
{
    public class LobbyPage : BasePage
    {
        [SerializeField] private Button startGameButton;
        [SerializeField] private Text planeNameText;
        [SerializeField] private Text planeStatsText;

        private TMP_Text startGameTmpText;
        private TMP_Text planeNameTmpText;
        private TMP_Text planeStatsTmpText;

        private void OnEnable()
        {
            if (startGameButton != null || transform.childCount > 0)
            {
                EnsureStartButtonBound();
            }
        }

        public override void OnCreate()
        {
            if (transform.childCount == 0)
            {
                BuildDefaultView();
            }

            EnsureStartButtonBound();

            if (Application.isPlaying)
            {
                RefreshSelectedPlane();
            }
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

            var title = UIFactory.CreateText("Title", transform, "雷霆战机", 72f, TextAnchor.MiddleCenter, Color.white);
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

            var planeIcon = UIFactory.CreateText("PlaneIcon", previewRect, "▲", 150f, TextAnchor.MiddleCenter, UIFactory.PanelAccentColor);
            var iconRect = planeIcon.rectTransform;
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(0f, 62f);
            iconRect.sizeDelta = new Vector2(360f, 220f);

            planeNameText = UIFactory.CreateText("PlaneName", previewRect, string.Empty, 40f, TextAnchor.MiddleCenter, Color.white);
            var nameRect = planeNameText.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 0f);
            nameRect.anchorMax = new Vector2(1f, 0f);
            nameRect.pivot = new Vector2(0.5f, 0f);
            nameRect.anchoredPosition = new Vector2(0f, 126f);
            nameRect.sizeDelta = new Vector2(0f, 56f);

            planeStatsText = UIFactory.CreateText("PlaneStats", previewRect, string.Empty, 28f, TextAnchor.MiddleCenter, UIFactory.MutedTextColor);
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
        }

        private void EnsureStartButtonBound()
        {
            BindPrefabView();
            BindEvents();
        }

        private void BindPrefabView()
        {
            startGameButton = startGameButton != null
                ? startGameButton
                : UIFactory.FindComponentInChildren<Button>(transform, "BtnStart")
                    ?? UIFactory.FindComponentInChildren<Button>(transform, "StartGameButton");
            planeNameText = planeNameText != null
                ? planeNameText
                : UIFactory.FindComponentInChildren<Text>(transform, "PlaneName");
            planeStatsText = planeStatsText != null
                ? planeStatsText
                : UIFactory.FindComponentInChildren<Text>(transform, "PlaneStats");
            planeNameTmpText = planeNameTmpText != null
                ? planeNameTmpText
                : UIFactory.FindComponentInChildren<TMP_Text>(transform, "PlaneName");
            planeStatsTmpText = planeStatsTmpText != null
                ? planeStatsTmpText
                : UIFactory.FindComponentInChildren<TMP_Text>(transform, "PlaneStats");

            if (startGameButton != null)
            {
                startGameTmpText = startGameButton.GetComponentInChildren<TMP_Text>(true);
                UIFactory.ApplyButtonTextFont(startGameButton);

                if (startGameTmpText != null && string.IsNullOrEmpty(startGameTmpText.text))
                {
                    startGameTmpText.text = "开始游戏";
                }
            }
        }

        private void BindEvents()
        {
            if (startGameButton == null)
            {
                Debug.LogWarning("[UIHall] BtnStart not found, cannot bind start game button.");
                return;
            }

            startGameButton.onClick.RemoveListener(OnClickStartGame);
            startGameButton.onClick.AddListener(OnClickStartGame);
            Debug.Log($"[UIHall] BtnStart bound. activeInHierarchy={startGameButton.gameObject.activeInHierarchy}, interactable={startGameButton.interactable}, targetGraphic={startGameButton.targetGraphic != null}");
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

            if (planeNameTmpText != null)
            {
                planeNameTmpText.text = plane.name;
            }

            if (planeStatsText != null)
            {
                planeStatsText.text = $"HP {plane.hp}   ATK {plane.attack}   RATE {plane.fireRate:0.0}   SPD {plane.moveSpeed:0.0}";
            }

            if (planeStatsTmpText != null)
            {
                planeStatsTmpText.text = $"HP {plane.hp}   ATK {plane.attack}   RATE {plane.fireRate:0.0}   SPD {plane.moveSpeed:0.0}";
            }
        }

        private void OnClickStartGame()
        {
            Debug.Log("[UIHall] BtnStart clicked, request open UIStage.");
            OpenStagePage();
        }

        public void OpenStagePage()
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.OpenStageFromHall();
                return;
            }

            Debug.LogWarning("[UIHall] UIManager not found when opening stage page, entering battle directly.");
            GameSceneManager.GetOrCreate().EnterBattle();
        }
    }
}
