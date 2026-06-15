using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using LeiTing.Config;
using LeiTing.Core;
using LeiTing.Progress;
#if UNITY_WEBGL && !UNITY_EDITOR
using LeiTing.Platform;
#endif
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if UNITY_WEBGL && !UNITY_EDITOR
using TTSDK;
using TTSDK.UNBridgeLib.LitJson;
#endif

namespace LeiTing.UI
{
    public class LobbyPage : BasePage
    {
        private const int DesignedLevelCount = 12;
        private const float StageInfoFadeDuration = 0.16f;
        private const float StageMoveDuration = 0.28f;
        private const float GlowDuration = 0.46f;
        private const float LevelGlowInterval = 3f;
        private const float LevelGlowPulseGap = 0.08f;
        private const int LevelGlowPulseCount = 3;
        private const float LevelHitAreaPadding = 16f;
        private const float StageChoseHeight = 305f;
        private const float StaminaRefreshUiInterval = 1f;
        private const string RankZoneId = "default";
        private const int NumericRankDataType = 0;

        private const string ToggleUnlockedSpritePath = "Assets/Art/Sprites/UI/toggleLvBg01.png";
        private const string ToggleSelectedSpritePath = "Assets/Art/Sprites/UI/toggleLvBg02.png";
        private const string ToggleLockedSpritePath = "Assets/Art/Sprites/UI/toggleLvBg03.png";
        private const string LineUnlockedSpritePath = "Assets/Art/Sprites/UI/lineBlue.png";
        private const string LineLockedSpritePath = "Assets/Art/Sprites/UI/lineZong.png";
        private const string GlowSpritePath = "Assets/Art/Sprites/UI/imgCircle.png";

        private static readonly Color StageInfoAchievedColor = FromHex(0xD7, 0xC1, 0x80);
        private static readonly Color StageInfoUnachievedColor = FromHex(0xBF, 0x7A, 0x02);
        private static readonly Color StartButtonLockedTiliColor = FromHex(0x57, 0x2E, 0x27);
        private static readonly Color LevelLabelDefaultColor = FromHex(0x00, 0xAE, 0xE7);
        private static readonly Color LevelLabelSelectedColor = FromHex(0x09, 0x09, 0x09);
        private static readonly Color LevelGlowColor = FromHex(0x00, 0xAE, 0xE7);
        private static readonly string[] AchievementDescriptions =
        {
            "击毁70%敌机",
            "击毁全部敌机",
            "收集80%星星",
            "无伤通关"
        };

        [SerializeField] private Button startButton;
        [SerializeField] private Button cebianButton;
        [SerializeField] private Button rankButton;
        [SerializeField] private Button settingButton;
        [SerializeField] private RectTransform stageChose;
        [SerializeField] private RectTransform stageInfo;
        [SerializeField] private GameObject cebianlan;
        [SerializeField] private Button sidebarGoButton;
        [SerializeField] private Button sidebarClaimButton;
        [SerializeField] private Button sidebarCloseButton;

        private readonly List<LevelItemView> levelItems = new List<LevelItemView>();
        private readonly List<InfoToggleView> infoItems = new List<InfoToggleView>();
        private readonly Dictionary<int, Image> lineImages = new Dictionary<int, Image>();

        private CanvasGroup stageInfoCanvasGroup;
        private Text stageLevelNameText;
        private TMP_Text stageLevelNameTmpText;
        private Text stageScoreText;
        private TMP_Text stageScoreTmpText;
        private GameObject stageScoreObject;
        private RectTransform stageInfoProgress;
        private GameObject stageUnlockText;
        private Image startButtonImage;
        private Sprite startButtonUnlockedSprite;
        private Color startButtonUnlockedColor = Color.white;
        private SpriteState startButtonUnlockedSpriteState;
        private Text startButtonTiliText;
        private TMP_Text startButtonTiliTmpText;
        private Color startButtonTiliTextColor = Color.white;
        private Color startButtonTiliTmpTextColor = Color.white;
        private RectTransform stageViewport;
        private ScrollRect stageScrollRect;
        private Sprite toggleUnlockedSprite;
        private Sprite toggleSelectedSprite;
        private Sprite toggleLockedSprite;
        private Sprite lineUnlockedSprite;
        private Sprite lineLockedSprite;
        private Sprite glowSprite;
        private Vector2 stageChoseInitialPosition;
        private int selectedLevelNumber = 1;
        private bool battleStartRequested;
        private bool hasStageChoseInitialPosition;
        private bool draggingStageChose;
        private Vector2 stageDragStartLocalPosition;
        private Vector2 stageDragStartAnchoredPosition;
        private float nextStaminaRefreshTime;
        private Coroutine stageInfoRoutine;
        private Coroutine stageMoveRoutine;
        private Coroutine levelGlowRoutine;
        private Coroutine startButtonGlowRoutine;
        private Image activeLevelGlow;
        private Image activeStartButtonGlow;

        public override void OnCreate()
        {
            UIFactory.Stretch(RectTransform);
            if (transform.childCount == 0)
            {
                BuildFallbackView();
            }

            LoadSprites();
            BindPrefabView();
            BindEvents();
            RefreshSidebarVisitState();
            RefreshLobby(true, false);
        }

        public override void OnShow()
        {
            battleStartRequested = false;
            RefreshSidebarVisitState();
            RefreshLobby(true, false);
        }

        public override void OnHide()
        {
            battleStartRequested = false;
            StopRunningCoroutines();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextStaminaRefreshTime)
            {
                return;
            }

            nextStaminaRefreshTime = Time.unscaledTime + StaminaRefreshUiInterval;
            RefreshStartButtonStaminaText();
            RefreshStartButtonAvailability();
        }

        private void BuildFallbackView()
        {
            var title = UIFactory.CreateText("FallbackTitle", transform, "选择关卡", 48f, TextAnchor.MiddleCenter, Color.white);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -120f);
            title.rectTransform.sizeDelta = new Vector2(0f, 80f);

            stageChose = UIFactory.CreateRect("StageChose", transform);
            stageChose.anchorMin = new Vector2(0.5f, 0.5f);
            stageChose.anchorMax = new Vector2(0.5f, 0.5f);
            stageChose.pivot = new Vector2(0.5f, 0.5f);
            stageChose.anchoredPosition = new Vector2(0f, 360f);
            stageChose.sizeDelta = new Vector2(960f, StageChoseHeight);

            stageInfo = UIFactory.CreateRect("StageInfo", transform);
            stageInfo.anchorMin = new Vector2(0.5f, 0.5f);
            stageInfo.anchorMax = new Vector2(0.5f, 0.5f);
            stageInfo.pivot = new Vector2(0.5f, 0.5f);
            stageInfo.anchoredPosition = new Vector2(0f, -120f);
            stageInfo.sizeDelta = new Vector2(760f, 360f);

            startButton = UIFactory.CreateButton("BtnStart", transform, "开始", Color.white, out var label, out _);
            label.color = Color.black;
            var startRect = startButton.GetComponent<RectTransform>();
            startRect.anchorMin = new Vector2(0.5f, 0f);
            startRect.anchorMax = new Vector2(0.5f, 0f);
            startRect.pivot = new Vector2(0.5f, 0f);
            startRect.anchoredPosition = new Vector2(0f, 120f);
            startRect.sizeDelta = new Vector2(360f, 100f);
        }

        private void LoadSprites()
        {
            toggleUnlockedSprite = LoadSprite(ToggleUnlockedSpritePath);
            toggleSelectedSprite = LoadSprite(ToggleSelectedSpritePath);
            toggleLockedSprite = LoadSprite(ToggleLockedSpritePath);
            lineUnlockedSprite = LoadSprite(LineUnlockedSpritePath);
            lineLockedSprite = LoadSprite(LineLockedSpritePath);
            glowSprite = CreateGlowSprite();
        }

        private void BindPrefabView()
        {
            stageChose = stageChose != null ? stageChose : FindRect("StageChose");
            stageInfo = stageInfo != null ? stageInfo : FindRect("StageInfo");
            EnsureStageScrollView();
            startButton = startButton != null ? startButton : FindOrCreateButton("BtnStart");
            BindStartButtonVisuals();
            cebianButton = cebianButton != null ? cebianButton : FindFirstButton("BtnCebianlan", "BtnCebian");
            rankButton = rankButton != null ? rankButton : FindOrCreateButton("BtnRank");
            settingButton = settingButton != null ? settingButton : FindOrCreateButton("BtnSetting");

            var cebianTransform = FindChildRecursive(transform, "Cebianlan");
            cebianlan = cebianlan != null ? cebianlan : cebianTransform != null ? cebianTransform.gameObject : null;
            if (cebianlan != null)
            {
                cebianlan.SetActive(false);
            }

            sidebarGoButton = sidebarGoButton != null ? sidebarGoButton : FindFirstButton("BtnGod", "BtnGo");
            sidebarClaimButton = sidebarClaimButton != null ? sidebarClaimButton : FindOrCreateButton("BtnClame");
            sidebarCloseButton = sidebarCloseButton != null ? sidebarCloseButton : FindOrCreateButton("BtnClose");

            if (stageInfo != null)
            {
                stageInfoCanvasGroup = EnsureCanvasGroup(stageInfo.gameObject);
                stageInfoCanvasGroup.alpha = 1f;
                stageInfoCanvasGroup.interactable = false;
                stageInfoCanvasGroup.blocksRaycasts = false;
            }

            if (stageChose != null && !hasStageChoseInitialPosition)
            {
                stageChoseInitialPosition = stageChose.anchoredPosition;
                hasStageChoseInitialPosition = true;
            }

            UIFactory.ApplyFontsInChildren(transform);
            BindLevelItems();
            BindInfoItems();
            BindStageInfoTexts();
            BindLineImages();
        }

        private void BindEvents()
        {
            BindButton(startButton, OnClickStart);
            BindButton(cebianButton, OnClickCebian);
            BindButton(rankButton, OnClickRank);
            BindButton(settingButton, OnClickSetting);
            BindButton(sidebarGoButton, OnClickSidebarGo);
            BindButton(sidebarCloseButton, OnClickSidebarClose);

            foreach (var item in levelItems)
            {
                if (item?.button == null)
                {
                    continue;
                }

                var levelNumber = item.levelNumber;
                item.button.onClick.RemoveAllListeners();
                item.button.onClick.AddListener(() => SelectLevel(levelNumber, true));
            }
        }

        private void RefreshLobby(bool selectLatestUnlocked, bool animateStageInfo)
        {
            EnsureDefaultConfigLoaded();
            var latestUnlocked = GetLatestUnlockedLevel();

            if (selectLatestUnlocked || selectedLevelNumber < 1 || selectedLevelNumber > latestUnlocked)
            {
                selectedLevelNumber = latestUnlocked;
            }

            selectedLevelNumber = Mathf.Clamp(selectedLevelNumber, 1, Mathf.Max(1, levelItems.Count));
            GameManager.RequestLevel(selectedLevelNumber);
            RefreshLevelVisuals();
            RefreshStageInfo(selectedLevelNumber, animateStageInfo);
            MoveStageChoseToLevel(selectedLevelNumber, false);
            PlayLevelGlow(selectedLevelNumber);
        }

        private void SelectLevel(int levelNumber, bool animate)
        {
            if (levelNumber < 1 || levelNumber > levelItems.Count)
            {
                return;
            }

            selectedLevelNumber = levelNumber;
            if (IsLevelUnlocked(selectedLevelNumber))
            {
                GameManager.RequestLevel(selectedLevelNumber);
            }

            RefreshLevelVisuals();
            RefreshStageInfo(selectedLevelNumber, animate);
            MoveStageChoseToLevel(selectedLevelNumber, animate);
            PlayLevelGlow(selectedLevelNumber);
        }

        private void RefreshLevelVisuals()
        {
            var latestUnlocked = GetLatestUnlockedLevel();
            for (var index = 0; index < levelItems.Count; index++)
            {
                var item = levelItems[index];
                if (item == null)
                {
                    continue;
                }

                var unlocked = IsLevelUnlocked(item.levelNumber, latestUnlocked);
                var selected = item.levelNumber == selectedLevelNumber;
                var record = unlocked ? LevelProgressService.GetRecord(item.levelNumber) : null;
                var earnedMask = record != null ? record.achievementMask : 0;

                if (item.button != null)
                {
                    item.button.interactable = true;
                }

                if (item.levelToggle != null)
                {
                    item.levelToggle.transition = Selectable.Transition.None;
                    item.levelToggle.interactable = false;
                    item.levelToggle.SetIsOnWithoutNotify(false);
                    item.levelToggle.enabled = true;
                    SetToggleRaycasts(item.levelToggle, false);
                }

                ApplyImageSprite(
                    item.levelImage,
                    selected ? toggleSelectedSprite : unlocked ? toggleUnlockedSprite : toggleLockedSprite,
                    selected || unlocked ? Color.white : new Color(0.55f, 0.55f, 0.55f, 1f));

                SetLevelLabel(item, unlocked ? item.levelNumber.ToString("00") : string.Empty, unlocked, selected);
                SetLockImages(item, !unlocked);
                SetProgressToggles(item, unlocked, earnedMask);
            }

            RefreshLineVisuals(latestUnlocked);
            SetStartButtonState(selectedLevelNumber >= 1 && selectedLevelNumber <= latestUnlocked && !battleStartRequested);
        }

        private void RefreshSidebarVisitState()
        {
            SetSidebarRewardState(IsSidebarRevisit());
        }

        private void SetSidebarRewardState(bool revisitedFromSidebar)
        {
            if (sidebarGoButton != null)
            {
                sidebarGoButton.gameObject.SetActive(!revisitedFromSidebar);
            }

            if (sidebarClaimButton != null)
            {
                sidebarClaimButton.gameObject.SetActive(revisitedFromSidebar);
            }
        }

        private void RefreshLineVisuals(int latestUnlocked)
        {
            foreach (var pair in lineImages)
            {
                var lineIndex = pair.Key;
                var lineImage = pair.Value;
                if (lineImage == null)
                {
                    continue;
                }

                var unlocked = IsLevelUnlocked(lineIndex + 1, latestUnlocked);
                ApplyImageSprite(
                    lineImage,
                    unlocked ? lineUnlockedSprite : lineLockedSprite,
                    unlocked ? Color.white : new Color(0.5f, 0.34f, 0.16f, 1f));
                lineImage.raycastTarget = false;
            }
        }

        private void RefreshStageInfo(int levelNumber, bool animate)
        {
            if (stageInfoCanvasGroup == null)
            {
                UpdateStageInfoContent(levelNumber);
                return;
            }

            if (!animate || !isActiveAndEnabled)
            {
                if (stageInfoRoutine != null)
                {
                    StopCoroutine(stageInfoRoutine);
                    stageInfoRoutine = null;
                }

                stageInfoCanvasGroup.alpha = 1f;
                UpdateStageInfoContent(levelNumber);
                return;
            }

            if (stageInfoRoutine != null)
            {
                StopCoroutine(stageInfoRoutine);
            }

            stageInfoRoutine = StartCoroutine(RefreshStageInfoCoroutine(levelNumber));
        }

        private IEnumerator RefreshStageInfoCoroutine(int levelNumber)
        {
            yield return FadeStageInfo(stageInfoCanvasGroup.alpha, 0f);
            UpdateStageInfoContent(levelNumber);
            yield return FadeStageInfo(0f, 1f);
            stageInfoRoutine = null;
        }

        private IEnumerator FadeStageInfo(float from, float to)
        {
            var timer = 0f;
            while (timer < StageInfoFadeDuration)
            {
                timer += Time.deltaTime;
                var t = Mathf.Clamp01(timer / StageInfoFadeDuration);
                stageInfoCanvasGroup.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }

            stageInfoCanvasGroup.alpha = to;
        }

        private void UpdateStageInfoContent(int levelNumber)
        {
            var unlocked = IsLevelUnlocked(levelNumber);
            var record = unlocked ? LevelProgressService.GetRecord(levelNumber) : null;
            SetStageInfoHeader(levelNumber, record != null ? record.score : 0);
            SetStageInfoLockedState(unlocked);

            for (var index = 0; index < infoItems.Count && index < AchievementDescriptions.Length; index++)
            {
                var item = infoItems[index];
                if (item == null)
                {
                    continue;
                }

                var achieved = record != null && (record.achievementMask & (1 << index)) != 0;
                if (item.toggle != null)
                {
                    item.toggle.interactable = false;
                    item.toggle.SetIsOnWithoutNotify(achieved);
                }

                SetInfoText(item, AchievementDescriptions[index], achieved);
            }
        }

        private void MoveStageChoseToLevel(int levelNumber, bool animate)
        {
            if (stageChose == null)
            {
                return;
            }

            var item = FindLevelItem(levelNumber);
            if (item?.root == null)
            {
                return;
            }

            var targetX = CalculateStageChoseTargetX(item.root);
            var targetPosition = new Vector2(targetX, stageChoseInitialPosition.y);

            if (stageMoveRoutine != null)
            {
                StopCoroutine(stageMoveRoutine);
                stageMoveRoutine = null;
            }

            draggingStageChose = false;

            if (stageScrollRect != null)
            {
                stageScrollRect.StopMovement();
            }

            if (!animate || !isActiveAndEnabled || Vector2.Distance(stageChose.anchoredPosition, targetPosition) < 0.1f)
            {
                stageChose.anchoredPosition = targetPosition;
                return;
            }

            stageMoveRoutine = StartCoroutine(MoveStageChoseCoroutine(targetPosition));
        }

        private IEnumerator MoveStageChoseCoroutine(Vector2 targetPosition)
        {
            var startPosition = stageChose.anchoredPosition;
            var timer = 0f;
            while (timer < StageMoveDuration)
            {
                timer += Time.deltaTime;
                var t = 1f - (1f - Mathf.Clamp01(timer / StageMoveDuration)) * (1f - Mathf.Clamp01(timer / StageMoveDuration));
                stageChose.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, t);
                yield return null;
            }

            stageChose.anchoredPosition = targetPosition;
            stageMoveRoutine = null;
        }

        private float CalculateStageChoseTargetX(RectTransform levelRoot)
        {
            Canvas.ForceUpdateCanvases();

            var targetX = stageChoseInitialPosition.x - levelRoot.anchoredPosition.x;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(stageChose);

            return GetStageChoseXRange(bounds, GetStageViewportWidth(), out var minX, out var maxX)
                ? Mathf.Clamp(targetX, minX, maxX)
                : stageChoseInitialPosition.x;
        }

        internal void BeginStageChoseDrag(PointerEventData eventData)
        {
            if (stageChose == null || eventData == null)
            {
                return;
            }

            if (stageMoveRoutine != null)
            {
                StopCoroutine(stageMoveRoutine);
                stageMoveRoutine = null;
            }

            if (stageScrollRect != null)
            {
                stageScrollRect.StopMovement();
            }

            draggingStageChose = TryGetStageViewportLocalPoint(eventData, out stageDragStartLocalPosition);
            stageDragStartAnchoredPosition = stageChose.anchoredPosition;
        }

        internal void DragStageChose(PointerEventData eventData)
        {
            if (stageChose == null || eventData == null)
            {
                return;
            }

            if (!draggingStageChose)
            {
                BeginStageChoseDrag(eventData);
            }

            if (!TryGetStageViewportLocalPoint(eventData, out var currentLocalPosition))
            {
                return;
            }

            var dragDeltaX = currentLocalPosition.x - stageDragStartLocalPosition.x;
            var targetX = ClampStageChoseX(stageDragStartAnchoredPosition.x + dragDeltaX);
            stageChose.anchoredPosition = new Vector2(targetX, stageChoseInitialPosition.y);
        }

        internal void EndStageChoseDrag()
        {
            draggingStageChose = false;
            if (stageChose != null)
            {
                stageChose.anchoredPosition = new Vector2(ClampStageChoseX(stageChose.anchoredPosition.x), stageChoseInitialPosition.y);
            }
        }

        private bool TryGetStageViewportLocalPoint(PointerEventData eventData, out Vector2 localPoint)
        {
            var viewport = stageViewport != null ? stageViewport : RectTransform;
            if (viewport == null)
            {
                localPoint = Vector2.zero;
                return false;
            }

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                viewport,
                eventData.position,
                eventData.pressEventCamera,
                out localPoint);
        }

        private float ClampStageChoseX(float targetX)
        {
            if (stageChose == null)
            {
                return targetX;
            }

            Canvas.ForceUpdateCanvases();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(stageChose);

            return GetStageChoseXRange(bounds, GetStageViewportWidth(), out var minX, out var maxX)
                ? Mathf.Clamp(targetX, minX, maxX)
                : stageChoseInitialPosition.x;
        }

        private float GetStageViewportWidth()
        {
            var viewport = stageViewport != null ? stageViewport : RectTransform;
            if (viewport != null && viewport.rect.width > 1f)
            {
                return viewport.rect.width;
            }

            if (RectTransform != null && RectTransform.rect.width > 1f)
            {
                return RectTransform.rect.width;
            }

            return 1080f;
        }

        private bool GetStageChoseXRange(Bounds bounds, float viewportWidth, out float minX, out float maxX)
        {
            var contentWidth = bounds.size.x;
            if (contentWidth <= viewportWidth)
            {
                minX = stageChoseInitialPosition.x;
                maxX = stageChoseInitialPosition.x;
                return false;
            }

            var halfViewport = viewportWidth * 0.5f;
            minX = halfViewport - bounds.max.x;
            maxX = -halfViewport - bounds.min.x;
            if (minX > maxX)
            {
                minX = stageChoseInitialPosition.x;
                maxX = stageChoseInitialPosition.x;
                return false;
            }

            return true;
        }

        private void PlayLevelGlow(int levelNumber)
        {
            var item = FindLevelItem(levelNumber);
            if (item?.root == null || glowSprite == null)
            {
                return;
            }

            if (levelGlowRoutine != null)
            {
                StopCoroutine(levelGlowRoutine);
                levelGlowRoutine = null;
            }

            if (activeLevelGlow != null)
            {
                Destroy(activeLevelGlow.gameObject);
                activeLevelGlow = null;
            }

            StopStartButtonGlowPulse();

            var glowParent = item.glowRoot != null ? item.glowRoot : item.root;
            var parentSize = glowParent.rect.size;
            var glowRect = UIFactory.CreateRect("LevelSelectGlow", glowParent);
            glowRect.SetAsFirstSibling();
            glowRect.anchorMin = new Vector2(0.5f, 0.5f);
            glowRect.anchorMax = new Vector2(0.5f, 0.5f);
            glowRect.pivot = new Vector2(0.5f, 0.5f);
            glowRect.anchoredPosition = Vector2.zero;
            glowRect.sizeDelta = Vector2.one * Mathf.Max(120f, Mathf.Max(parentSize.x, parentSize.y) * 1.72f);

            activeLevelGlow = glowRect.gameObject.AddComponent<Image>();
            activeLevelGlow.sprite = glowSprite;
            activeLevelGlow.raycastTarget = false;
            activeLevelGlow.color = new Color(LevelGlowColor.r, LevelGlowColor.g, LevelGlowColor.b, 0f);
            levelGlowRoutine = StartCoroutine(PlayPulseGlowCoroutine(glowRect, activeLevelGlow, LevelGlowPulseCount, LevelGlowInterval));
        }

        private void PulseStartButtonGlowOnce()
        {
            if (!CanStartSelectedLevel())
            {
                StopStartButtonGlowPulse();
                return;
            }

            var glowRect = EnsureStartButtonGlow();
            if (glowRect == null || activeStartButtonGlow == null)
            {
                return;
            }

            if (startButtonGlowRoutine != null)
            {
                StopCoroutine(startButtonGlowRoutine);
                startButtonGlowRoutine = null;
            }

            activeStartButtonGlow.color = new Color(LevelGlowColor.r, LevelGlowColor.g, LevelGlowColor.b, 0f);
            glowRect.localScale = Vector3.one;
            startButtonGlowRoutine = StartCoroutine(PlayStartButtonPulseOnceCoroutine(glowRect, activeStartButtonGlow));
        }

        private RectTransform EnsureStartButtonGlow()
        {
            var startRect = startButton != null ? startButton.GetComponent<RectTransform>() : null;
            if (startRect == null || glowSprite == null)
            {
                return null;
            }

            if (activeStartButtonGlow != null)
            {
                var existingRect = activeStartButtonGlow.rectTransform;
                if (existingRect != null && existingRect.parent == startRect)
                {
                    return existingRect;
                }

                Destroy(activeStartButtonGlow.gameObject);
                activeStartButtonGlow = null;
            }

            var glowRect = UIFactory.CreateRect("BtnStartGlow", startRect);
            glowRect.SetAsFirstSibling();
            glowRect.anchorMin = new Vector2(0.5f, 0.5f);
            glowRect.anchorMax = new Vector2(0.5f, 0.5f);
            glowRect.pivot = new Vector2(0.5f, 0.5f);
            glowRect.anchoredPosition = Vector2.zero;
            glowRect.sizeDelta = Vector2.one * Mathf.Max(160f, Mathf.Max(startRect.rect.width, startRect.rect.height) * 1.35f);

            activeStartButtonGlow = glowRect.gameObject.AddComponent<Image>();
            activeStartButtonGlow.sprite = glowSprite;
            activeStartButtonGlow.raycastTarget = false;
            activeStartButtonGlow.color = new Color(LevelGlowColor.r, LevelGlowColor.g, LevelGlowColor.b, 0f);
            return glowRect;
        }

        private void StopStartButtonGlowPulse()
        {
            if (startButtonGlowRoutine != null)
            {
                StopCoroutine(startButtonGlowRoutine);
                startButtonGlowRoutine = null;
            }

            if (activeStartButtonGlow != null)
            {
                activeStartButtonGlow.color = new Color(LevelGlowColor.r, LevelGlowColor.g, LevelGlowColor.b, 0f);
                activeStartButtonGlow.rectTransform.localScale = Vector3.one;
            }
        }

        private IEnumerator PlayStartButtonPulseOnceCoroutine(RectTransform glowRect, Image glowImage)
        {
            var timer = 0f;
            while (timer < GlowDuration && glowRect != null && glowImage != null)
            {
                timer += Time.deltaTime;
                var t = Mathf.Clamp01(timer / GlowDuration);
                var scale = Mathf.Lerp(0.62f, 1.48f, t);
                glowRect.localScale = Vector3.one * scale;

                var color = glowImage.color;
                color.a = Mathf.Lerp(0.9f, 0f, t);
                glowImage.color = color;
                yield return null;
            }

            if (glowImage != null)
            {
                glowImage.color = new Color(LevelGlowColor.r, LevelGlowColor.g, LevelGlowColor.b, 0f);
            }

            if (startButtonGlowRoutine != null)
            {
                startButtonGlowRoutine = null;
            }
        }

        private IEnumerator PlayPulseGlowCoroutine(RectTransform glowRect, Image glowImage, int pulseCount, float interval)
        {
            while (glowRect != null && glowImage != null)
            {
                var cycleElapsed = 0f;
                for (var pulse = 0; pulse < pulseCount; pulse++)
                {
                    var timer = 0f;
                    while (timer < GlowDuration && glowRect != null && glowImage != null)
                    {
                        timer += Time.deltaTime;
                        cycleElapsed += Time.deltaTime;
                        var t = Mathf.Clamp01(timer / GlowDuration);
                        var scale = Mathf.Lerp(0.62f, 1.48f, t);
                        glowRect.localScale = Vector3.one * scale;

                        var color = glowImage.color;
                        color.a = Mathf.Lerp(0.9f, 0f, t);
                        glowImage.color = color;
                        yield return null;
                    }

                    if (glowImage != null)
                    {
                        glowImage.color = new Color(LevelGlowColor.r, LevelGlowColor.g, LevelGlowColor.b, 0f);
                    }

                    if (pulse < pulseCount - 1)
                    {
                        cycleElapsed += LevelGlowPulseGap;
                        yield return new WaitForSeconds(LevelGlowPulseGap);
                    }
                }

                PulseStartButtonGlowOnce();

                var restDuration = Mathf.Max(0f, interval - cycleElapsed);
                if (restDuration > 0f)
                {
                    yield return new WaitForSeconds(restDuration);
                }
            }
        }

        private void BindLevelItems()
        {
            levelItems.Clear();
            for (var levelNumber = 1; levelNumber <= DesignedLevelCount; levelNumber++)
            {
                var levelRootTransform = FindChildRecursive(transform, "Level" + levelNumber);
                var levelRoot = levelRootTransform as RectTransform;
                if (levelRoot == null)
                {
                    continue;
                }

                var toggleRoot = FindChildRecursive(levelRoot, "ToggleLv");
                var backgroundRoot = toggleRoot != null ? FindDirectChild(toggleRoot, "Background") as RectTransform : null;
                var progressRoot = FindChildRecursive(levelRoot, "Progress");
                var item = new LevelItemView
                {
                    root = levelRoot,
                    levelNumber = levelNumber,
                    glowRoot = backgroundRoot != null ? backgroundRoot : toggleRoot as RectTransform,
                    levelToggle = toggleRoot != null ? toggleRoot.GetComponent<Toggle>() : null,
                    levelImage = ResolveLevelImage(toggleRoot),
                    levelText = toggleRoot != null ? toggleRoot.GetComponentInChildren<Text>(true) : null,
                    levelTmpText = toggleRoot != null ? toggleRoot.GetComponentInChildren<TMP_Text>(true) : null,
                    progressRoot = progressRoot as RectTransform,
                    progressToggles = CollectProgressToggles(progressRoot),
                    lockImages = CollectNamedImages(levelRoot, "ImageLock"),
                };

                item.button = EnsureLevelButton(item);
                levelItems.Add(item);
            }

            levelItems.Sort((left, right) => left.levelNumber.CompareTo(right.levelNumber));
        }

        private void EnsureStageScrollView()
        {
            if (stageChose == null)
            {
                return;
            }

            stageViewport = stageChose.parent as RectTransform;
            if (stageViewport == null)
            {
                return;
            }

            stageViewport.anchorMin = Vector2.zero;
            stageViewport.anchorMax = Vector2.one;
            stageViewport.offsetMin = Vector2.zero;
            stageViewport.offsetMax = Vector2.zero;
            stageViewport.pivot = new Vector2(0.5f, 0.5f);

            var viewportImage = stageViewport.GetComponent<Image>();
            if (viewportImage == null)
            {
                viewportImage = stageViewport.gameObject.AddComponent<Image>();
            }

            viewportImage.color = new Color(1f, 1f, 1f, 0f);
            viewportImage.raycastTarget = false;

            stageScrollRect = stageViewport.GetComponent<ScrollRect>();
            if (stageScrollRect == null)
            {
                stageScrollRect = stageViewport.gameObject.AddComponent<ScrollRect>();
            }

            stageScrollRect.content = stageChose;
            stageScrollRect.viewport = stageViewport;
            stageScrollRect.horizontal = true;
            stageScrollRect.vertical = false;
            stageScrollRect.movementType = ScrollRect.MovementType.Clamped;
            stageScrollRect.inertia = true;
            stageScrollRect.decelerationRate = 0.12f;
            stageScrollRect.scrollSensitivity = 0.5f;
            stageScrollRect.horizontalScrollbar = null;
            stageScrollRect.verticalScrollbar = null;
            stageScrollRect.enabled = false;

            stageChose.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, StageChoseHeight);
            EnsureStageImageDragRelays();
        }

        private void BindInfoItems()
        {
            infoItems.Clear();
            if (stageInfo == null)
            {
                return;
            }

            for (var index = 1; index <= LevelProgressService.AchievementCount; index++)
            {
                var infoRoot = FindChildRecursive(stageInfo, "InfoToggle" + index);
                if (infoRoot == null)
                {
                    continue;
                }

                AddInfoItem(infoRoot);
            }

            if (infoItems.Count >= LevelProgressService.AchievementCount)
            {
                return;
            }

            var toggles = stageInfo.GetComponentsInChildren<Toggle>(true);
            Array.Sort(toggles, CompareTogglesTopToBottom);
            foreach (var toggle in toggles)
            {
                if (toggle == null || ContainsInfoToggle(toggle) || infoItems.Count >= LevelProgressService.AchievementCount)
                {
                    continue;
                }

                AddInfoItem(toggle.transform);
            }
        }

        private void BindLineImages()
        {
            lineImages.Clear();
            for (var index = 1; index < DesignedLevelCount; index++)
            {
                var line = FindChildRecursive(stageChose != null ? stageChose : transform, "line" + index);
                var image = line != null ? line.GetComponent<Image>() : null;
                if (image != null)
                {
                    lineImages[index] = image;
                }
            }
        }

        private void BindStageInfoTexts()
        {
            stageLevelNameText = null;
            stageLevelNameTmpText = null;
            stageScoreText = null;
            stageScoreTmpText = null;
            stageScoreObject = null;
            stageInfoProgress = null;
            stageUnlockText = null;

            if (stageInfo == null)
            {
                return;
            }

            var levelName = FindChildRecursive(stageInfo, "TxtLvName");
            if (levelName != null)
            {
                stageLevelNameText = levelName.GetComponent<Text>();
                stageLevelNameTmpText = levelName.GetComponent<TMP_Text>();
            }

            var score = FindChildRecursive(stageInfo, "TxtScore");
            if (score != null)
            {
                stageScoreObject = score.gameObject;
                stageScoreText = score.GetComponent<Text>();
                stageScoreTmpText = score.GetComponent<TMP_Text>();
            }

            var progress = FindChildRecursive(stageInfo, "StageInfoProgress");
            if (progress != null)
            {
                stageInfoProgress = progress as RectTransform;
            }

            var unlockText = FindChildRecursive(stageInfo, "TxtUnlock");
            if (unlockText != null)
            {
                stageUnlockText = unlockText.gameObject;
            }
        }

        private void BindStartButtonVisuals()
        {
            startButtonImage = startButton != null ? startButton.targetGraphic as Image : null;
            if (startButtonImage == null && startButton != null)
            {
                startButtonImage = startButton.GetComponentInChildren<Image>(true);
                startButton.targetGraphic = startButtonImage;
            }

            if (startButtonImage != null)
            {
                startButtonUnlockedSprite = startButtonImage.sprite;
                startButtonUnlockedColor = startButtonImage.color;
            }

            if (startButton != null)
            {
                startButtonUnlockedSpriteState = startButton.spriteState;
            }

            var tili = startButton != null ? FindChildRecursive(startButton.transform, "txtTili") : null;
            if (tili != null)
            {
                startButtonTiliText = tili.GetComponent<Text>();
                startButtonTiliTmpText = tili.GetComponent<TMP_Text>();

                if (startButtonTiliText != null)
                {
                    startButtonTiliTextColor = startButtonTiliText.color;
                }

                if (startButtonTiliTmpText != null)
                {
                    startButtonTiliTmpTextColor = startButtonTiliTmpText.color;
                }
            }
        }

        private void AddInfoItem(Transform root)
        {
            if (root == null)
            {
                return;
            }

            var toggle = root.GetComponent<Toggle>();
            if (toggle == null)
            {
                return;
            }

            infoItems.Add(new InfoToggleView
            {
                toggle = toggle,
                texts = root.GetComponentsInChildren<Text>(true),
                tmpTexts = root.GetComponentsInChildren<TMP_Text>(true)
            });
        }

        private static int CompareTogglesTopToBottom(Toggle left, Toggle right)
        {
            var leftRect = left != null ? left.GetComponent<RectTransform>() : null;
            var rightRect = right != null ? right.GetComponent<RectTransform>() : null;
            var leftY = leftRect != null ? leftRect.anchoredPosition.y : 0f;
            var rightY = rightRect != null ? rightRect.anchoredPosition.y : 0f;
            return rightY.CompareTo(leftY);
        }

        private bool ContainsInfoToggle(Toggle toggle)
        {
            foreach (var item in infoItems)
            {
                if (item != null && item.toggle == toggle)
                {
                    return true;
                }
            }

            return false;
        }

        private Button EnsureLevelButton(LevelItemView item)
        {
            if (item == null || item.root == null)
            {
                return null;
            }

            var hitArea = FindDirectChild(item.root, "HitArea") as RectTransform;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(item.root);
            if (hitArea == null)
            {
                hitArea = UIFactory.CreateRect("HitArea", item.root);
            }

            hitArea.SetAsLastSibling();
            hitArea.anchorMin = new Vector2(0.5f, 0.5f);
            hitArea.anchorMax = new Vector2(0.5f, 0.5f);
            hitArea.pivot = new Vector2(0.5f, 0.5f);
            hitArea.anchoredPosition = bounds.size.sqrMagnitude > 0.1f ? (Vector2)bounds.center : Vector2.zero;
            hitArea.sizeDelta = new Vector2(
                Mathf.Max(item.root.rect.width, bounds.size.x) + LevelHitAreaPadding,
                Mathf.Max(item.root.rect.height, bounds.size.y) + LevelHitAreaPadding);

            var image = hitArea.GetComponent<Image>();
            if (image == null)
            {
                image = hitArea.gameObject.AddComponent<Image>();
            }

            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;

            var button = hitArea.GetComponent<Button>();
            if (button == null)
            {
                button = hitArea.gameObject.AddComponent<Button>();
            }

            button.transition = Selectable.Transition.None;
            button.targetGraphic = image;
            EnsureStageDragRelay(hitArea);
            return button;
        }

        private void EnsureStageDragRelay(RectTransform target)
        {
            if (target == null)
            {
                return;
            }

            var relay = target.GetComponent<StageChoseDragRelay>();
            if (relay == null)
            {
                relay = target.gameObject.AddComponent<StageChoseDragRelay>();
            }

            relay.Configure(this);
        }

        private void EnsureStageImageDragRelays()
        {
            if (stageChose == null)
            {
                return;
            }

            EnsureStageImageDragRelay("Image1");
            EnsureStageImageDragRelay("Image2");
        }

        private void EnsureStageImageDragRelay(string imageName)
        {
            var dragTarget = FindDirectChild(stageChose, imageName) as RectTransform;
            if (dragTarget == null)
            {
                return;
            }

            var image = dragTarget.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = true;
            }

            EnsureStageDragRelay(dragTarget);
        }

        private static Image ResolveLevelImage(Transform toggleRoot)
        {
            if (toggleRoot == null)
            {
                return null;
            }

            var toggle = toggleRoot.GetComponent<Toggle>();
            var background = FindDirectChild(toggleRoot, "Background");
            if (background != null && background.TryGetComponent<Image>(out var backgroundImage))
            {
                return backgroundImage;
            }

            if (toggle != null && toggle.targetGraphic is Image targetImage)
            {
                return targetImage;
            }

            return toggleRoot.GetComponentInChildren<Image>(true);
        }

        private static Toggle[] CollectProgressToggles(Transform progressRoot)
        {
            if (progressRoot == null)
            {
                return new Toggle[0];
            }

            var toggles = progressRoot.GetComponentsInChildren<Toggle>(true);
            Array.Sort(toggles, CompareTogglesByNameNumber);
            return toggles;
        }

        private static Image[] CollectNamedImages(Transform root, string imageName)
        {
            if (root == null)
            {
                return new Image[0];
            }

            var images = new List<Image>();
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                if (image != null && string.Equals(image.name, imageName, StringComparison.OrdinalIgnoreCase))
                {
                    images.Add(image);
                }
            }

            return images.ToArray();
        }

        private static int CompareTogglesByNameNumber(Toggle left, Toggle right)
        {
            return ExtractTrailingNumber(left != null ? left.name : string.Empty)
                .CompareTo(ExtractTrailingNumber(right != null ? right.name : string.Empty));
        }

        private static int ExtractTrailingNumber(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            var result = 0;
            var multiplier = 1;
            for (var index = value.Length - 1; index >= 0; index--)
            {
                if (!char.IsDigit(value[index]))
                {
                    break;
                }

                result += (value[index] - '0') * multiplier;
                multiplier *= 10;
            }

            return result;
        }

        private void SetLevelLabel(LevelItemView item, string text, bool unlocked, bool selected)
        {
            var color = unlocked
                ? selected ? LevelLabelSelectedColor : LevelLabelDefaultColor
                : new Color(0.45f, 0.45f, 0.45f, 1f);
            if (item.levelText != null)
            {
                item.levelText.text = text;
                item.levelText.color = color;
            }

            if (item.levelTmpText != null)
            {
                item.levelTmpText.text = text;
                item.levelTmpText.color = color;
            }
        }

        private static void SetLockImages(LevelItemView item, bool locked)
        {
            if (item?.lockImages == null)
            {
                return;
            }

            foreach (var image in item.lockImages)
            {
                if (image != null)
                {
                    image.gameObject.SetActive(locked);
                }
            }
        }

        private static void SetProgressToggles(LevelItemView item, bool unlocked, int earnedMask)
        {
            if (item?.progressToggles == null)
            {
                return;
            }

            if (item.progressRoot != null)
            {
                item.progressRoot.gameObject.SetActive(unlocked);
            }

            for (var index = 0; index < item.progressToggles.Length; index++)
            {
                var toggle = item.progressToggles[index];
                if (toggle == null)
                {
                    continue;
                }

                var achieved = unlocked && (earnedMask & (1 << index)) != 0;
                toggle.interactable = false;
                toggle.SetIsOnWithoutNotify(achieved);
            }
        }

        private static void SetToggleRaycasts(Toggle toggle, bool raycastTarget)
        {
            if (toggle == null)
            {
                return;
            }

            foreach (var graphic in toggle.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic != null)
                {
                    graphic.raycastTarget = raycastTarget;
                }
            }
        }

        private static void SetInfoText(InfoToggleView item, string text, bool achieved)
        {
            var color = achieved ? StageInfoAchievedColor : StageInfoUnachievedColor;
            if (item.texts != null)
            {
                foreach (var label in item.texts)
                {
                    if (label == null)
                    {
                        continue;
                    }

                    label.text = text;
                    label.color = color;
                }
            }

            if (item.tmpTexts != null)
            {
                foreach (var label in item.tmpTexts)
                {
                    if (label == null)
                    {
                        continue;
                    }

                    label.text = text;
                    label.color = color;
                }
            }
        }

        private void SetStageInfoHeader(int levelNumber, int score)
        {
            var levelText = $"关卡{Mathf.Max(1, levelNumber):00}";
            var scoreText = FormatScore(score);

            if (stageLevelNameText != null)
            {
                stageLevelNameText.text = levelText;
            }

            if (stageLevelNameTmpText != null)
            {
                stageLevelNameTmpText.text = levelText;
            }

            if (stageScoreText != null)
            {
                stageScoreText.text = scoreText;
            }

            if (stageScoreTmpText != null)
            {
                stageScoreTmpText.text = scoreText;
            }
        }

        private void SetStageInfoLockedState(bool unlocked)
        {
            if (stageScoreObject != null)
            {
                stageScoreObject.SetActive(unlocked);
            }
            else
            {
                if (stageScoreText != null)
                {
                    stageScoreText.gameObject.SetActive(unlocked);
                }

                if (stageScoreTmpText != null)
                {
                    stageScoreTmpText.gameObject.SetActive(unlocked);
                }
            }

            if (stageInfoProgress != null)
            {
                stageInfoProgress.gameObject.SetActive(unlocked);
            }

            if (stageUnlockText != null)
            {
                stageUnlockText.SetActive(!unlocked);
            }
        }

        private static string FormatScore(int score)
        {
            var paddedScore = Mathf.Clamp(score, 0, 999999999).ToString("000000000", CultureInfo.InvariantCulture);
            return $"{paddedScore.Substring(0, 3)} {paddedScore.Substring(3, 3)} {paddedScore.Substring(6, 3)}";
        }

        private void SetStartButtonState(bool interactable)
        {
            if (startButton != null)
            {
                RefreshStartButtonStaminaText();

                var lockedSelection = !IsSelectedLevelUnlocked();
                var lockedStamina = !HasEnoughStamina();
                SetStartButtonLockedVisual(lockedSelection || lockedStamina);
                startButton.interactable = interactable && !lockedSelection;

                if (lockedSelection || lockedStamina)
                {
                    StopStartButtonGlowPulse();
                }
            }
        }

        private void RefreshStartButtonAvailability()
        {
            if (battleStartRequested)
            {
                return;
            }

            var latestUnlocked = GetLatestUnlockedLevel();
            SetStartButtonState(selectedLevelNumber >= 1 && selectedLevelNumber <= latestUnlocked);
        }

        private bool HasEnoughStamina()
        {
            return StaminaService.HasEnough(StaminaService.BattleCost);
        }

        private void RefreshStartButtonStaminaText()
        {
            var staminaText = string.Format(
                CultureInfo.InvariantCulture,
                "{0}/{1}",
                StaminaService.CurrentStamina,
                StaminaService.MaxStamina);
            if (startButtonTiliText != null)
            {
                startButtonTiliText.text = staminaText;
            }

            if (startButtonTiliTmpText != null)
            {
                startButtonTiliTmpText.text = staminaText;
            }
        }

        private void SetStartButtonLockedVisual(bool locked)
        {
            if (startButtonImage == null && startButton != null)
            {
                startButtonImage = startButton.targetGraphic as Image ?? startButton.GetComponentInChildren<Image>(true);
            }

            if (locked)
            {
                var disabledSprite = GetStartButtonPressedSprite();
                if (startButton != null)
                {
                    var lockedState = startButtonUnlockedSpriteState;
                    lockedState.disabledSprite = disabledSprite;
                    startButton.spriteState = lockedState;
                }

                if (startButtonImage != null)
                {
                    startButtonImage.sprite = startButtonUnlockedSprite;
                    startButtonImage.overrideSprite = disabledSprite;
                    startButtonImage.color = GetStartButtonPressedColor();
                }

                SetStartButtonTiliColor(StartButtonLockedTiliColor);
                return;
            }

            if (startButton != null)
            {
                startButton.spriteState = startButtonUnlockedSpriteState;
            }

            if (startButtonImage != null)
            {
                startButtonImage.overrideSprite = null;
                startButtonImage.sprite = startButtonUnlockedSprite;
                startButtonImage.color = startButtonUnlockedColor;
            }

            SetStartButtonTiliColor(startButtonTiliTextColor, startButtonTiliTmpTextColor);
        }

        private Sprite GetStartButtonPressedSprite()
        {
            if (startButtonUnlockedSpriteState.pressedSprite != null)
            {
                return startButtonUnlockedSpriteState.pressedSprite;
            }

            return startButtonUnlockedSprite;
        }

        private Color GetStartButtonPressedColor()
        {
            if (startButton != null)
            {
                return startButton.colors.pressedColor * startButton.colors.colorMultiplier;
            }

            return startButtonUnlockedColor;
        }

        private void SetStartButtonTiliColor(Color color)
        {
            SetStartButtonTiliColor(color, color);
        }

        private void SetStartButtonTiliColor(Color textColor, Color tmpTextColor)
        {
            if (startButtonTiliText != null)
            {
                startButtonTiliText.color = textColor;
            }

            if (startButtonTiliTmpText != null)
            {
                startButtonTiliTmpText.color = tmpTextColor;
            }
        }

        private async void OnClickStart()
        {
            if (battleStartRequested || !IsSelectedLevelUnlocked())
            {
                return;
            }

            battleStartRequested = true;
            SetStartButtonState(false);

            if (!StaminaService.TryConsume(StaminaService.BattleCost))
            {
                var watchedAd = await AdManager.GetOrCreate().ShowRewardAd();
                if (!watchedAd)
                {
                    battleStartRequested = false;
                    RefreshStartButtonAvailability();
                    return;
                }
            }

            if (!isActiveAndEnabled)
            {
                battleStartRequested = false;
                RefreshStartButtonAvailability();
                return;
            }

            GameManager.RequestLevel(selectedLevelNumber);
            GameSceneManager.GetOrCreate().EnterBattle(selectedLevelNumber);
        }

        private bool IsSelectedLevelUnlocked()
        {
            return IsLevelUnlocked(selectedLevelNumber);
        }

        private bool CanStartSelectedLevel()
        {
            return IsSelectedLevelUnlocked() && HasEnoughStamina() && !battleStartRequested;
        }

        private void OnClickCebian()
        {
            if (cebianlan != null)
            {
                RefreshSidebarVisitState();
                cebianlan.SetActive(true);
            }
        }

        private void OnClickSidebarGo()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            OpenDouyinSidebar();
#else
            Debug.Log("[UIHall] Douyin sidebar requested.");
#endif
        }

        private void OnClickSidebarClose()
        {
            if (cebianlan != null)
            {
                cebianlan.SetActive(false);
            }
        }

        private static void OnClickRank()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            DouyinAccountService.EnsureLogin((success, message) =>
            {
                if (!success)
                {
                    Debug.LogWarning($"[UIHall] Douyin rank open skipped: login failed: {message}");
                    return;
                }

                OpenDouyinRank();
            });
#else
            Debug.Log($"[UIHall] Douyin rank requested. Total score={LevelProgressService.GetTotalScore()}");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private static void OpenDouyinRank()
        {
            try
            {
                var rankData = new JsonData();
                rankData["rankType"] = "all";
                rankData["dataType"] = NumericRankDataType;
                rankData["relationType"] = "all";
                rankData["suffix"] = "\u5206";
                rankData["rankTitle"] = "\u6392\u884c\u699c";
                rankData["zoneId"] = RankZoneId;
                TT.GetImRankList(rankData, (success, message) =>
                {
                    if (!success)
                    {
                        Debug.LogWarning($"[UIHall] Douyin rank open failed: {message}");
                    }
                });
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[UIHall] Douyin rank open failed: {exception.Message}");
            }
        }
#endif

        private static void OnClickSetting()
        {
            UIManager.Instance?.OpenPage(UIPageType.Setting);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                RefreshSidebarVisitState();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus)
            {
                RefreshSidebarVisitState();
            }
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private static void OpenDouyinSidebar()
        {
            try
            {
                var data = new JsonData();
                data["scene"] = "sidebar";
                TT.NavigateToScene(
                    data,
                    () => Debug.Log("[UIHall] Douyin sidebar opened."),
                    () => { },
                    (code, message) =>
                    {
                        Debug.LogWarning($"[UIHall] Douyin sidebar open failed: code={code}, message={message}");
                    });
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[UIHall] Douyin sidebar open failed: {exception.Message}");
            }
        }

        private static bool IsSidebarRevisit()
        {
            try
            {
                var launchOptions = TT.GetLaunchOptionsSync();
                var containerEnv = TT.s_ContainerEnv;

                return IsSidebarLaunchValue(containerEnv != null ? containerEnv.GetLaunchFrom() : null)
                    || IsSidebarLaunchValue(containerEnv != null ? containerEnv.GetLocation() : null)
                    || (launchOptions != null
                        && (IsSidebarLaunchValue(launchOptions.Scene)
                            || IsSidebarLaunchValue(launchOptions.SubScene)));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[UIHall] Douyin sidebar launch check failed: {exception.Message}");
                return false;
            }
        }

        private static bool IsSidebarLaunchValue(object value)
        {
            if (value == null)
            {
                return false;
            }

            var text = value.ToString();
            return string.Equals(text, "sidebar", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "homepage", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "home_page", StringComparison.OrdinalIgnoreCase);
        }
#else
        private static bool IsSidebarRevisit()
        {
            return false;
        }
#endif

        private static void EnsureDefaultConfigLoaded()
        {
            var configManager = ConfigManager.Instance;
            if (configManager != null && !configManager.IsLoaded)
            {
                configManager.LoadDefaultConfig();
            }
        }

        private int GetLatestUnlockedLevel()
        {
            return GameManager.GetMaxUnlockedLevel(Mathf.Max(1, levelItems.Count));
        }

        private bool IsLevelUnlocked(int levelNumber)
        {
            return IsLevelUnlocked(levelNumber, GetLatestUnlockedLevel());
        }

        private static bool IsLevelUnlocked(int levelNumber, int latestUnlockedLevel)
        {
            return levelNumber >= 1 && levelNumber <= latestUnlockedLevel;
        }

        private LevelItemView FindLevelItem(int levelNumber)
        {
            foreach (var item in levelItems)
            {
                if (item != null && item.levelNumber == levelNumber)
                {
                    return item;
                }
            }

            return null;
        }

        private Button FindFirstButton(params string[] names)
        {
            if (names == null)
            {
                return null;
            }

            foreach (var name in names)
            {
                var button = FindOrCreateButton(name);
                if (button != null)
                {
                    return button;
                }
            }

            return null;
        }

        private RectTransform FindRect(string name)
        {
            return FindChildRecursive(transform, name) as RectTransform;
        }

        private Button FindOrCreateButton(string name)
        {
            var target = FindChildRecursive(transform, name);
            if (target == null)
            {
                return null;
            }

            var button = target.GetComponent<Button>();
            if (button == null)
            {
                button = target.gameObject.AddComponent<Button>();
            }

            if (button.targetGraphic == null)
            {
                button.targetGraphic = target.GetComponent<Image>() ?? target.GetComponentInChildren<Image>(true);
            }

            return button;
        }

        private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null || action == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            if (root.name == childName)
            {
                return root;
            }

            for (var index = 0; index < root.childCount; index++)
            {
                var result = FindChildRecursive(root.GetChild(index), childName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static Transform FindDirectChild(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            for (var index = 0; index < root.childCount; index++)
            {
                var child = root.GetChild(index);
                if (child != null && child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }

        private static CanvasGroup EnsureCanvasGroup(GameObject target)
        {
            var canvasGroup = target.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = target.AddComponent<CanvasGroup>();
            }

            return canvasGroup;
        }

        private static void ApplyImageSprite(Image image, Sprite sprite, Color fallbackColor)
        {
            if (image == null)
            {
                return;
            }

            if (sprite == null)
            {
                image.color = fallbackColor;
                return;
            }

            image.sprite = sprite;
            image.color = Color.white;
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

        private static Sprite CreateGlowSprite()
        {
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x, y), center) / (size * 0.5f);
                    var outward = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.16f, 0.82f, distance));
                    var edgeFade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.86f, 1f, distance));
                    var alpha = distance > 1f ? 0f : Mathf.Clamp01(outward * edgeFade);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Color FromHex(byte r, byte g, byte b)
        {
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }

        private void StopRunningCoroutines()
        {
            draggingStageChose = false;

            if (stageInfoRoutine != null)
            {
                StopCoroutine(stageInfoRoutine);
                stageInfoRoutine = null;
            }

            if (stageMoveRoutine != null)
            {
                StopCoroutine(stageMoveRoutine);
                stageMoveRoutine = null;
            }

            if (levelGlowRoutine != null)
            {
                StopCoroutine(levelGlowRoutine);
                levelGlowRoutine = null;
            }

            if (startButtonGlowRoutine != null)
            {
                StopCoroutine(startButtonGlowRoutine);
                startButtonGlowRoutine = null;
            }

            if (activeLevelGlow != null)
            {
                Destroy(activeLevelGlow.gameObject);
                activeLevelGlow = null;
            }

            if (activeStartButtonGlow != null)
            {
                Destroy(activeStartButtonGlow.gameObject);
                activeStartButtonGlow = null;
            }
        }

        private sealed class LevelItemView
        {
            public RectTransform root;
            public RectTransform glowRoot;
            public Button button;
            public Toggle levelToggle;
            public Image levelImage;
            public Text levelText;
            public TMP_Text levelTmpText;
            public RectTransform progressRoot;
            public Toggle[] progressToggles;
            public Image[] lockImages;
            public int levelNumber;
        }

        private sealed class InfoToggleView
        {
            public Toggle toggle;
            public Text[] texts;
            public TMP_Text[] tmpTexts;
        }
    }

}
