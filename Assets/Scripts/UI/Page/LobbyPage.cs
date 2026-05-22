using System.Collections.Generic;
using LeiTing.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LeiTing.UI
{
    public class LobbyPage : BasePage, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const float SwipeThreshold = 80f;

        [SerializeField] private Button startGameButton;
        [SerializeField] private Text planeNameText;
        [SerializeField] private Text planeStatsText;
        [SerializeField] private Image hallBackgroundImage;
        [SerializeField] private Image planeImage;
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private Sprite startButtonSprite;
        [SerializeField] private Sprite defaultPlaneSprite;

        private readonly List<PlaneData> selectablePlanes = new List<PlaneData>();
        private TMP_Text startGameTmpText;
        private TMP_Text planeNameTmpText;
        private TMP_Text planeStatsTmpText;
        private int selectedPlaneIndex;
        private bool stageOpenRequested;
        private bool draggingPlane;
        private Vector2 dragStartPosition;
        private Vector2 lastDragDelta;

        private void OnEnable()
        {
            if (startGameButton != null || transform.childCount > 0)
            {
                EnsureViewBound();
            }
        }

        public override void OnCreate()
        {
            if (transform.childCount == 0)
            {
                BuildDefaultView();
            }

            EnsureViewBound();

            if (Application.isPlaying)
            {
                RefreshPlaneList(true);
            }
        }

        public override void OnShow()
        {
            stageOpenRequested = false;
            RefreshPlaneList(true);

            if (startGameButton != null)
            {
                startGameButton.interactable = true;
            }
        }

        public void ConfigureSprites(Sprite background, Sprite startButton, Sprite plane)
        {
            backgroundSprite = background;
            startButtonSprite = startButton;
            defaultPlaneSprite = plane;
        }

        public void SetSelectedPlane(int planeId)
        {
            PlaneManager.GetOrCreate().SelectPlane(planeId);
            RefreshPlaneList(true);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            draggingPlane = true;
            dragStartPosition = eventData.position;
            lastDragDelta = Vector2.zero;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!draggingPlane)
            {
                return;
            }

            lastDragDelta = eventData.position - dragStartPosition;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!draggingPlane)
            {
                return;
            }

            draggingPlane = false;
            lastDragDelta = eventData.position - dragStartPosition;

            if (Mathf.Abs(lastDragDelta.x) < SwipeThreshold || Mathf.Abs(lastDragDelta.x) <= Mathf.Abs(lastDragDelta.y))
            {
                return;
            }

            SwitchPlane(lastDragDelta.x < 0f ? 1 : -1);
        }

        private void Update()
        {
            if (stageOpenRequested || startGameButton == null || !startGameButton.gameObject.activeInHierarchy)
            {
                return;
            }

            if (TryGetStartPointerDownPosition(out var pointerPosition)
                && IsPointerInsideStartButton(pointerPosition))
            {
                Debug.Log($"[UIHall] BtnStart pointer fallback hit at {pointerPosition}, opening UIStage.");
                OnClickStartGame();
            }
        }

        private void BuildDefaultView()
        {
            UIFactory.Stretch(RectTransform);

            var backdrop = UIFactory.CreatePanel("LobbyBackdrop", transform, new Color(0.015f, 0.02f, 0.045f, 0.96f));
            UIFactory.Stretch(backdrop.rectTransform);

            var hallBackground = UIFactory.CreatePanel("HallBackground", transform, Color.white);
            hallBackground.sprite = backgroundSprite;
            hallBackground.preserveAspect = true;
            hallBackground.raycastTarget = true;
            hallBackground.color = backgroundSprite != null ? Color.white : new Color(0.04f, 0.08f, 0.13f, 0.72f);
            var hallRect = hallBackground.rectTransform;
            hallRect.anchorMin = new Vector2(0.5f, 0.5f);
            hallRect.anchorMax = new Vector2(0.5f, 0.5f);
            hallRect.pivot = new Vector2(0.5f, 0.5f);
            hallRect.anchoredPosition = new Vector2(0f, 122f);
            hallRect.sizeDelta = new Vector2(780f, 834f);

            var swipeArea = UIFactory.CreatePanel("PlaneSwipeArea", transform, new Color(1f, 1f, 1f, 0f));
            swipeArea.raycastTarget = true;
            var swipeRect = swipeArea.rectTransform;
            swipeRect.anchorMin = new Vector2(0.5f, 0.5f);
            swipeRect.anchorMax = new Vector2(0.5f, 0.5f);
            swipeRect.pivot = new Vector2(0.5f, 0.5f);
            swipeRect.anchoredPosition = new Vector2(0f, 130f);
            swipeRect.sizeDelta = new Vector2(780f, 720f);

            planeImage = UIFactory.CreatePanel("PlaneImage", swipeRect, Color.white);
            planeImage.sprite = defaultPlaneSprite;
            planeImage.preserveAspect = true;
            planeImage.raycastTarget = false;
            var planeRect = planeImage.rectTransform;
            planeRect.anchorMin = new Vector2(0.5f, 0.5f);
            planeRect.anchorMax = new Vector2(0.5f, 0.5f);
            planeRect.pivot = new Vector2(0.5f, 0.5f);
            planeRect.anchoredPosition = new Vector2(0f, 86f);
            planeRect.sizeDelta = new Vector2(420f, 420f);

            planeNameText = UIFactory.CreateText("PlaneName", transform, string.Empty, 44f, TextAnchor.MiddleCenter, Color.white);
            var nameRect = planeNameText.rectTransform;
            nameRect.anchorMin = new Vector2(0.5f, 0.5f);
            nameRect.anchorMax = new Vector2(0.5f, 0.5f);
            nameRect.pivot = new Vector2(0.5f, 0.5f);
            nameRect.anchoredPosition = new Vector2(0f, -210f);
            nameRect.sizeDelta = new Vector2(700f, 72f);

            planeStatsText = UIFactory.CreateText("PlaneStats", transform, string.Empty, 28f, TextAnchor.MiddleCenter, UIFactory.MutedTextColor);
            var statRect = planeStatsText.rectTransform;
            statRect.anchorMin = new Vector2(0.5f, 0.5f);
            statRect.anchorMax = new Vector2(0.5f, 0.5f);
            statRect.pivot = new Vector2(0.5f, 0.5f);
            statRect.anchoredPosition = new Vector2(0f, -270f);
            statRect.sizeDelta = new Vector2(780f, 56f);

            startGameButton = UIFactory.CreateButton("BtnStart", transform, "前往挑战", Color.white, out var buttonLabel, out var buttonImage);
            buttonImage.sprite = startButtonSprite;
            buttonImage.type = Image.Type.Simple;
            buttonImage.preserveAspect = false;
            buttonImage.color = startButtonSprite != null ? Color.white : new Color(0.05f, 0.62f, 1f, 0.96f);
            buttonLabel.fontSize = 42;
            var buttonRect = startGameButton.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0f);
            buttonRect.anchorMax = new Vector2(0.5f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0f);
            buttonRect.anchoredPosition = new Vector2(0f, 348f);
            buttonRect.sizeDelta = new Vector2(493f, 146f);
        }

        private void EnsureViewBound()
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
            hallBackgroundImage = hallBackgroundImage != null
                ? hallBackgroundImage
                : UIFactory.FindComponentInChildren<Image>(transform, "HallBackground");
            planeImage = planeImage != null
                ? planeImage
                : UIFactory.FindComponentInChildren<Image>(transform, "PlaneImage");
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
                ApplyButtonSprite();
                //SetStartButtonText("前往挑战");
            }

            ApplyImageSprites();
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

        private void RefreshPlaneList(bool alignToSelected)
        {
            selectablePlanes.Clear();

            var manager = PlaneManager.GetOrCreate();
            var selectedPlane = manager.GetSelectedPlane();
            var planes = manager.GetPlanes();
            foreach (var plane in planes)
            {
                if (plane != null && plane.owned)
                {
                    selectablePlanes.Add(plane);
                }
            }

            if (selectablePlanes.Count == 0 && selectedPlane != null)
            {
                selectablePlanes.Add(selectedPlane);
            }

            if (selectablePlanes.Count == 0)
            {
                RefreshSelectedPlane(null);
                return;
            }

            if (alignToSelected && selectedPlane != null)
            {
                selectedPlaneIndex = FindPlaneIndex(selectedPlane.id);
            }

            selectedPlaneIndex = Mathf.Clamp(selectedPlaneIndex, 0, selectablePlanes.Count - 1);
            RefreshSelectedPlane(selectablePlanes[selectedPlaneIndex]);
        }

        private int FindPlaneIndex(int planeId)
        {
            for (var index = 0; index < selectablePlanes.Count; index++)
            {
                if (selectablePlanes[index].id == planeId)
                {
                    return index;
                }
            }

            return 0;
        }

        private void SwitchPlane(int direction)
        {
            if (selectablePlanes.Count <= 1)
            {
                return;
            }

            selectedPlaneIndex = (selectedPlaneIndex + direction + selectablePlanes.Count) % selectablePlanes.Count;
            var plane = selectablePlanes[selectedPlaneIndex];
            PlaneManager.GetOrCreate().SelectPlane(plane.id);
            RefreshSelectedPlane(plane);
        }

        private void RefreshSelectedPlane(PlaneData plane)
        {
            if (plane == null)
            {
                SetPlaneText("暂无飞机", string.Empty);
                return;
            }

            SetPlaneText(
                plane.name,
                $"HP {plane.hp}   ATK {plane.attack}   RATE {plane.fireRate:0.0}   SPD {plane.moveSpeed:0.0}");

            if (planeImage != null)
            {
                var iconSprite = ResolvePlaneSprite(plane);
                if (iconSprite != null)
                {
                    planeImage.sprite = iconSprite;
                    planeImage.color = Color.white;
                }
            }
        }

        private void SetPlaneText(string planeName, string planeStats)
        {
            if (planeNameText != null)
            {
                planeNameText.text = planeName;
            }

            if (planeNameTmpText != null)
            {
                planeNameTmpText.text = planeName;
            }

            if (planeStatsText != null)
            {
                planeStatsText.text = planeStats;
            }

            if (planeStatsTmpText != null)
            {
                planeStatsTmpText.text = planeStats;
            }
        }

        private Sprite ResolvePlaneSprite(PlaneData plane)
        {
            if (plane != null && !string.IsNullOrEmpty(plane.iconPath))
            {
                var iconSprite = RuntimeAssetCatalog.LoadSprite(plane.iconPath);
                if (iconSprite != null)
                {
                    return iconSprite;
                }
            }

            return defaultPlaneSprite;
        }

        private void ApplyImageSprites()
        {
            if (hallBackgroundImage != null && backgroundSprite != null)
            {
                hallBackgroundImage.sprite = backgroundSprite;
                hallBackgroundImage.color = Color.white;
                hallBackgroundImage.preserveAspect = true;
            }

            if (planeImage != null && defaultPlaneSprite != null && planeImage.sprite == null)
            {
                planeImage.sprite = defaultPlaneSprite;
                planeImage.color = Color.white;
                planeImage.preserveAspect = true;
            }
        }

        private void ApplyButtonSprite()
        {
            if (startGameButton == null || startButtonSprite == null)
            {
                return;
            }

            if (startGameButton.targetGraphic is Image buttonImage)
            {
                buttonImage.sprite = startButtonSprite;
                buttonImage.color = Color.white;
            }
        }

        private void SetStartButtonText(string text)
        {
            if (startGameButton == null)
            {
                return;
            }

            var label = startGameButton.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = text;
            }

            if (startGameTmpText != null)
            {
                startGameTmpText.text = text;
            }
        }

        private void OnClickStartGame()
        {
            if (stageOpenRequested)
            {
                Debug.Log("[UIHall] BtnStart click ignored because UIStage open was already requested.");
                return;
            }

            stageOpenRequested = true;
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

        private static bool TryGetStartPointerDownPosition(out Vector2 pointerPosition)
        {
            if (Input.GetMouseButtonDown(0))
            {
                pointerPosition = Input.mousePosition;
                return true;
            }

            for (var index = 0; index < Input.touchCount; index++)
            {
                var touch = Input.GetTouch(index);
                if (touch.phase == TouchPhase.Began)
                {
                    pointerPosition = touch.position;
                    return true;
                }
            }

            pointerPosition = Vector2.zero;
            return false;
        }

        private bool IsPointerInsideStartButton(Vector2 pointerPosition)
        {
            var buttonRect = startGameButton != null ? startGameButton.GetComponent<RectTransform>() : null;
            if (buttonRect == null)
            {
                return false;
            }

            return RectTransformUtility.RectangleContainsScreenPoint(buttonRect, pointerPosition, null);
        }
    }
}
