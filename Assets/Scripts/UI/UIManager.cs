using System;
using System.Collections;
using System.Collections.Generic;
using LeiTing.Core;
using LeiTing.Player;
using LeiTing.Stage;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LeiTing.UI
{
    public class UIManager : MonoSingleton<UIManager>
    {
        private const float PageSwitchDuration = 0.25f;
        private const float CommonBackgroundMoveDuration = 0.25f;
        private const float MainTopBarHeight = 112f;
        private const float MainBottomBarHeight = 268f;
        private const string BottomBarPrefabAssetPath = "Assets/Prefabs/UI/UIBottom.prefab";
        private const string BottomBarPrefabResourcesPath = "UI/Common/UIBottom";
        private const string MainBackgroundSpritePath = "Assets/Art/Sprites/UI/backgroundH.png";

        private readonly Dictionary<UIPageType, BasePage> pageInstances = new Dictionary<UIPageType, BasePage>();
        private readonly Stack<BasePopup> popupStack = new Stack<BasePopup>();
        private readonly Dictionary<string, BasePopup> cachedPopups = new Dictionary<string, BasePopup>();
        private readonly List<RaycastResult> pointerRaycastResults = new List<RaycastResult>();

        private GameObject mainCanvasObject;
        private RectTransform contentLayer;
        private RectTransform fullScreenPageLayer;
        private RectTransform commonLayer;
        private RectTransform popupLayer;
        private RectTransform popupContainer;
        private Image popupMask;
        private RawImage commonBackgroundImage;
        private RectTransform commonBackgroundRect;
        private Sprite commonBackgroundSprite;
        private Coroutine commonBackgroundMoveRoutine;
        private bool commonBackgroundPositionInitialized;
        private TopBar topBar;
        private BottomBar bottomBar;
        private BasePage currentPageInstance;
        private UIPageType currentPageType;
        private BasePage currentMainPageInstance;
        private UIPageType currentMainPageType;
        private BasePage currentOverlayPageInstance;
        private UIPageType currentOverlayPageType;
        private UIPageType pendingOpenPageType;
        private bool hasCurrentPage;
        private bool hasCurrentMainPage;
        private bool hasCurrentOverlayPage;
        private bool isSwitching;
        private bool hasPendingOpenPage;
        private bool mainUiInitialized;

        private RectTransform canvasRoot;
        private Text hudText;
        private Text stageTimerText;
        private GameObject settlementRoot;
        private Text settlementText;
        private GameObject restartChallengeRoot;
        private Button restartChallengeButton;
        private GameObject nextLevelRoot;
        private Button nextLevelButton;
        private GameObject bossHudRoot;
        private Image bossHealthFill;
        private Text bossNameText;
        private Text bossPhaseText;
        private Text bossNoticeText;
        private Coroutine bossNoticeRoutine;
        private bool battleHudInitialized;

        protected override void Awake()
        {
            base.Awake();

            if (Instance != this)
            {
                return;
            }

            EnsureEventSystem();

            if (GameSceneManager.IsBattleSceneName(SceneManager.GetActiveScene().name))
            {
                EnsureBattleHud();
            }
            else
            {
                Init();
            }
        }

        private void Update()
        {
            LogMainUiPointerRaycast();

            if (!battleHudInitialized)
            {
                return;
            }

            UpdateHud();
            UpdateStageTimer();
            UpdateSettlement();
        }

        public void Init()
        {
            if (mainUiInitialized && IsMainUiReady())
            {
                ShowMainUI(true);
                EnsureLobbyPageVisible();
                return;
            }

            if (mainUiInitialized)
            {
                ResetMainUiState();
            }

            EnsureEventSystem();
            EnsureMainUi();
            ShowTopBar(true);
            ShowBottomBar(true);

            topBar.UpdatePlayerInfo(new PlayerInfo
            {
                coin = 1200,
                diamond = 80,
                score = GameManager.Instance != null ? GameManager.Instance.Score : 0
            });

            OpenPage(UIPageType.Lobby);
        }

        private bool IsMainUiReady()
        {
            return mainCanvasObject != null
                && contentLayer != null
                && fullScreenPageLayer != null
                && commonLayer != null
                && popupLayer != null
                && topBar != null
                && bottomBar != null;
        }

        private void ResetMainUiState()
        {
            Debug.Log("[UIManager] Main UI state was incomplete, rebuilding main UI.");
            if (mainCanvasObject != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(mainCanvasObject);
                }
                else
                {
                    DestroyImmediate(mainCanvasObject);
                }
            }

            mainUiInitialized = false;
            pageInstances.Clear();
            popupStack.Clear();
            cachedPopups.Clear();
            currentPageInstance = null;
            currentPageType = default(UIPageType);
            currentMainPageInstance = null;
            currentMainPageType = default(UIPageType);
            currentOverlayPageInstance = null;
            currentOverlayPageType = default(UIPageType);
            hasCurrentPage = false;
            hasCurrentMainPage = false;
            hasCurrentOverlayPage = false;
            isSwitching = false;
            hasPendingOpenPage = false;
            mainCanvasObject = null;
            contentLayer = null;
            fullScreenPageLayer = null;
            commonLayer = null;
            popupLayer = null;
            popupContainer = null;
            popupMask = null;
            commonBackgroundImage = null;
            commonBackgroundRect = null;
            commonBackgroundSprite = null;
            commonBackgroundMoveRoutine = null;
            commonBackgroundPositionInitialized = false;
            topBar = null;
            bottomBar = null;
        }

        private void EnsureLobbyPageVisible()
        {
            if (hasCurrentOverlayPage)
            {
                CloseOverlayPage(currentOverlayPageType, false);
            }

            if (!hasCurrentMainPage || currentMainPageInstance == null || !currentMainPageInstance.gameObject.activeSelf || currentMainPageType != UIPageType.Lobby)
            {
                Debug.Log($"[UIManager] Init restoring UIHall. hasMain={hasCurrentMainPage}, currentMain={currentMainPageType}, active={currentMainPageInstance != null && currentMainPageInstance.gameObject.activeSelf}");
                OpenPage(UIPageType.Lobby);
                return;
            }

            ApplyPageChrome(UIPageType.Lobby);
            ApplyCommonBackground(UIPageType.Lobby);
            PreparePageForCommonBackground(currentMainPageInstance, UIPageType.Lobby);
            SetPageInput(currentMainPageInstance, true);
            currentMainPageInstance.RectTransform.anchoredPosition = Vector2.zero;
            currentPageInstance = currentMainPageInstance;
            currentPageType = currentMainPageType;
            hasCurrentPage = true;
            bottomBar?.SetSelected(UIPageType.Lobby);
            Debug.Log("[UIManager] Init verified UIHall is visible.");
        }

        public void OpenPage(UIPageType pageType)
        {
            if (!mainUiInitialized)
            {
                EnsureMainUi();
            }

            if (isSwitching)
            {
                QueueOpenPage(pageType);
                return;
            }

            if (IsOverlayPage(pageType))
            {
                OpenOverlayPage(pageType);
                return;
            }

            OpenMainPage(pageType);
        }

        private void OpenMainPage(UIPageType pageType)
        {
            var targetPage = GetOrCreatePage(pageType);
            if (targetPage == null)
            {
                Debug.LogWarning($"[UIManager] OpenPage failed, target page is null: {pageType}");
                return;
            }

            Debug.Log($"[UIManager] OpenPage request. target={pageType}, current={(hasCurrentPage ? currentPageType.ToString() : "None")}, pageObject={targetPage.name}");
            CloseActiveOverlayPage(false);
            ApplyPageChrome(pageType);
            ApplyCommonBackground(pageType);
            PreparePageForCommonBackground(targetPage, pageType);
            HideOtherPages(targetPage, currentMainPageInstance, false);

            if (currentMainPageInstance != null && currentMainPageInstance != targetPage)
            {
                currentMainPageInstance.OnHide();
                SetPageInput(currentMainPageInstance, false);
                currentMainPageInstance.gameObject.SetActive(false);
            }

            targetPage.gameObject.SetActive(true);
            targetPage.transform.SetAsLastSibling();
            targetPage.RectTransform.anchoredPosition = Vector2.zero;
            SetPageInput(targetPage, true);
            targetPage.OnOpen();
            targetPage.OnShow();

            currentPageType = pageType;
            currentPageInstance = targetPage;
            currentMainPageType = pageType;
            currentMainPageInstance = targetPage;
            hasCurrentPage = true;
            hasCurrentMainPage = true;
            bottomBar?.SetSelected(pageType);
            Debug.Log($"[UIManager] OpenPage complete. current={currentPageType}, bottomVisible={bottomBar != null && bottomBar.gameObject.activeSelf}");
        }

        private void OpenOverlayPage(UIPageType pageType)
        {
            var targetPage = GetOrCreatePage(pageType);
            if (targetPage == null)
            {
                Debug.LogWarning($"[UIManager] Open overlay page failed, target page is null: {pageType}");
                return;
            }

            Debug.Log($"[UIManager] OpenOverlayPage request. target={pageType}, currentOverlay={(hasCurrentOverlayPage ? currentOverlayPageType.ToString() : "None")}, pageObject={targetPage.name}");
            ApplyPageChrome(pageType);
            ApplyCommonBackground(pageType);
            HideOtherPages(targetPage, currentOverlayPageInstance, true);
            SetMainContentVisible(false);

            if (currentOverlayPageInstance != null && currentOverlayPageInstance != targetPage)
            {
                currentOverlayPageInstance.OnHide();
                SetPageInput(currentOverlayPageInstance, false);
                currentOverlayPageInstance.gameObject.SetActive(false);
            }

            targetPage.gameObject.SetActive(true);
            targetPage.transform.SetAsLastSibling();
            targetPage.RectTransform.anchoredPosition = Vector2.zero;
            SetPageInput(targetPage, true);
            targetPage.OnOpen();
            targetPage.OnShow();

            currentPageType = pageType;
            currentPageInstance = targetPage;
            currentOverlayPageType = pageType;
            currentOverlayPageInstance = targetPage;
            hasCurrentPage = true;
            hasCurrentOverlayPage = true;
            RestoreMainUiLayerOrder();
            Debug.Log($"[UIManager] OpenOverlayPage complete. current={currentPageType}");
        }

        public void OpenStageFromHall()
        {
            Debug.Log("[UIManager] OpenStageFromHall requested. Close popups, hide bottom/top, and open UIStage.");
            CloseAllPopups();
            OpenPage(UIPageType.Stage);
        }

        public void ReturnStageToHall()
        {
            Debug.Log("[UIManager] ReturnStageToHall requested. Close UIStage, show UIBottom, and switch to UIHall.");

            CloseOverlayPage(UIPageType.Stage, false);

            if (!hasCurrentMainPage || currentMainPageInstance == null)
            {
                OpenMainPage(UIPageType.Lobby);
                return;
            }

            if (currentMainPageType != UIPageType.Lobby)
            {
                OpenMainPage(UIPageType.Lobby);
                return;
            }

            ApplyPageChrome(UIPageType.Lobby);
            ApplyCommonBackground(UIPageType.Lobby);
            PreparePageForCommonBackground(currentMainPageInstance, UIPageType.Lobby);
            currentMainPageInstance.gameObject.SetActive(true);
            currentMainPageInstance.RectTransform.anchoredPosition = Vector2.zero;
            SetPageInput(currentMainPageInstance, true);
            currentPageType = currentMainPageType;
            currentPageInstance = currentMainPageInstance;
            hasCurrentPage = true;
            bottomBar?.SetSelected(UIPageType.Lobby);
        }

        public void SwitchPage(UIPageType targetPageType)
        {
            if (!mainUiInitialized)
            {
                Init();
            }

            if (IsOverlayPage(targetPageType))
            {
                OpenPage(targetPageType);
                return;
            }

            if (isSwitching || hasCurrentMainPage && currentMainPageType == targetPageType)
            {
                return;
            }

            if (!hasCurrentMainPage || currentMainPageInstance == null)
            {
                OpenPage(targetPageType);
                return;
            }

            StartCoroutine(SwitchPageCoroutine(targetPageType));
        }

        public void ClosePage(UIPageType pageType)
        {
            if (!pageInstances.TryGetValue(pageType, out var page) || page == null)
            {
                return;
            }

            page.OnClose();
            SetPageInput(page, false);
            page.gameObject.SetActive(false);

            if (UIConfig.PageConfigs.TryGetValue(pageType, out var config) && !config.cache)
            {
                page.OnDestroyPage();
                pageInstances.Remove(pageType);
                Destroy(page.gameObject);
            }

            if (hasCurrentOverlayPage && currentOverlayPageType == pageType)
            {
                hasCurrentOverlayPage = false;
                currentOverlayPageInstance = null;
                currentOverlayPageType = default(UIPageType);
            }

            if (hasCurrentMainPage && currentMainPageType == pageType)
            {
                hasCurrentMainPage = false;
                currentMainPageInstance = null;
                currentMainPageType = default(UIPageType);
            }

            if (hasCurrentPage && currentPageType == pageType)
            {
                if (hasCurrentOverlayPage)
                {
                    currentPageInstance = currentOverlayPageInstance;
                    currentPageType = currentOverlayPageType;
                }
                else if (hasCurrentMainPage)
                {
                    currentPageInstance = currentMainPageInstance;
                    currentPageType = currentMainPageType;
                    SetPageInput(currentMainPageInstance, true);
                }
                else
                {
                    hasCurrentPage = false;
                    currentPageInstance = null;
                }
            }

            if (IsOverlayPage(pageType) && !hasCurrentOverlayPage && hasCurrentMainPage)
            {
                ApplyPageChrome(currentMainPageType);
                ApplyCommonBackground(currentMainPageType);
                SetPageInput(currentMainPageInstance, true);
            }
        }

        public void OpenPopup(string popupName, object data = null)
        {
            if (string.IsNullOrEmpty(popupName))
            {
                return;
            }

            if (!mainUiInitialized)
            {
                Init();
            }

            StartCoroutine(OpenPopupCoroutine(popupName, data));
        }

        public void ClosePopup(string popupName)
        {
            if (popupStack.Count == 0)
            {
                return;
            }

            var target = FindPopup(popupName);
            if (target != null)
            {
                StartCoroutine(ClosePopupCoroutine(target));
            }
        }

        public void CloseTopPopup()
        {
            if (popupStack.Count == 0)
            {
                return;
            }

            StartCoroutine(ClosePopupCoroutine(popupStack.Peek()));
        }

        public void CloseAllPopups()
        {
            while (popupStack.Count > 0)
            {
                var popup = popupStack.Pop();
                if (popup == null)
                {
                    continue;
                }

                popup.OnClose();

                if (ShouldCachePopup(popup.PopupName))
                {
                    popup.gameObject.SetActive(false);
                }
                else
                {
                    Destroy(popup.gameObject);
                }
            }

            RefreshPopupMask();
        }

        public void ShowTopBar(bool visible)
        {
            if (topBar != null)
            {
                topBar.gameObject.SetActive(visible);
                Debug.Log($"[UIManager] TopBar visible={visible}");
            }
        }

        public void ShowBottomBar(bool visible)
        {
            if (bottomBar != null)
            {
                bottomBar.gameObject.SetActive(visible);
                var canvasGroup = EnsureCanvasGroup(bottomBar.gameObject);
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
                bottomBar.SetInputEnabled(visible);
                if (visible)
                {
                    RestoreMainUiLayerOrder();
                }

                Debug.Log($"[UIManager] UIBottom visible={visible}");
            }
        }

        public void ShowMainUI(bool visible)
        {
            if (mainCanvasObject != null)
            {
                mainCanvasObject.SetActive(visible);
            }
        }

        public void EnsureBattleHud()
        {
            if (battleHudInitialized)
            {
                return;
            }

            battleHudInitialized = true;
            ShowMainUI(false);
            EnsureBattleHudCanvas();
        }

        public void ShowScorePopup(Vector3 worldPosition, int amount)
        {
            if (canvasRoot == null || amount <= 0)
            {
                return;
            }

            var popupObject = new GameObject("ScorePopup", typeof(RectTransform));
            popupObject.transform.SetParent(canvasRoot, false);

            var rect = popupObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(180f, 52f);

            var screenPosition = Camera.main != null ? Camera.main.WorldToScreenPoint(worldPosition) : Vector3.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRoot, screenPosition, null, out var localPosition);
            rect.anchoredPosition = localPosition;

            var text = popupObject.AddComponent<Text>();
            text.font = UIFactory.GetDefaultFont();
            text.fontSize = 34;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.88f, 0.22f, 1f);
            text.raycastTarget = false;
            text.text = $"+{amount}";

            StartCoroutine(AnimateScorePopup(rect, text));
        }

        public void UpdateBossHud(string bossName, int currentHp, int maxHp, string phaseName)
        {
            if (bossHudRoot == null || bossHealthFill == null)
            {
                return;
            }

            bossHudRoot.SetActive(maxHp > 0 && currentHp > 0);
            bossHealthFill.fillAmount = maxHp > 0 ? Mathf.Clamp01(currentHp / (float)maxHp) : 0f;

            if (bossNameText != null)
            {
                bossNameText.text = string.IsNullOrEmpty(bossName) ? "BOSS" : bossName;
            }

            if (bossPhaseText != null)
            {
                bossPhaseText.text = string.IsNullOrEmpty(phaseName) ? string.Empty : phaseName;
            }
        }

        public void HideBossHud()
        {
            if (bossHudRoot != null)
            {
                bossHudRoot.SetActive(false);
            }
        }

        public void ShowBossPhaseNotice(string message)
        {
            if (bossNoticeText == null || string.IsNullOrEmpty(message))
            {
                return;
            }

            if (bossNoticeRoutine != null)
            {
                StopCoroutine(bossNoticeRoutine);
            }

            bossNoticeRoutine = StartCoroutine(AnimateBossNotice(message));
        }

        private IEnumerator SwitchPageCoroutine(UIPageType targetPageType)
        {
            isSwitching = true;

            CloseActiveOverlayPage(false);

            var currentPage = currentMainPageInstance;
            var targetPage = GetOrCreatePage(targetPageType);

            if (currentPage == null || targetPage == null)
            {
                isSwitching = false;
                FlushPendingOpenPage();
                yield break;
            }

            ApplyPageChrome(targetPageType);
            ApplyCommonBackground(targetPageType);
            PreparePageForCommonBackground(targetPage, targetPageType);
            HideOtherPages(targetPage, currentPage, false);

            var width = Mathf.Max(1f, contentLayer != null ? contentLayer.rect.width : Screen.width);
            var targetToRight = targetPage.PageIndex > currentPage.PageIndex;
            var currentTargetX = targetToRight ? -width : width;
            var targetStartX = targetToRight ? width : -width;

            targetPage.gameObject.SetActive(true);
            targetPage.transform.SetAsLastSibling();
            targetPage.RectTransform.anchoredPosition = new Vector2(targetStartX, 0f);
            SetPageInput(currentPage, false);
            SetPageInput(targetPage, false);
            targetPage.OnOpen();

            var timer = 0f;
            var currentStartPos = currentPage.RectTransform.anchoredPosition;
            var currentEndPos = new Vector2(currentTargetX, 0f);
            var targetStartPos = new Vector2(targetStartX, 0f);
            var targetEndPos = Vector2.zero;

            while (timer < PageSwitchDuration)
            {
                timer += Time.deltaTime;
                var t = EaseOutQuad(Mathf.Clamp01(timer / PageSwitchDuration));

                currentPage.RectTransform.anchoredPosition = Vector2.Lerp(currentStartPos, currentEndPos, t);
                targetPage.RectTransform.anchoredPosition = Vector2.Lerp(targetStartPos, targetEndPos, t);
                yield return null;
            }

            currentPage.RectTransform.anchoredPosition = Vector2.zero;
            currentPage.gameObject.SetActive(false);
            currentPage.OnHide();

            targetPage.RectTransform.anchoredPosition = Vector2.zero;
            SetPageInput(targetPage, true);
            targetPage.OnShow();

            currentPageType = targetPageType;
            currentPageInstance = targetPage;
            currentMainPageType = targetPageType;
            currentMainPageInstance = targetPage;
            hasCurrentPage = true;
            hasCurrentMainPage = true;
            bottomBar?.SetSelected(targetPageType);
            isSwitching = false;
            FlushPendingOpenPage();
        }

        private void LogMainUiPointerRaycast()
        {
            if (!mainUiInitialized || EventSystem.current == null || !TryGetPointerDownPosition(out var pointerPosition))
            {
                return;
            }

            var pointerEventData = new PointerEventData(EventSystem.current)
            {
                position = pointerPosition
            };

            pointerRaycastResults.Clear();
            EventSystem.current.RaycastAll(pointerEventData, pointerRaycastResults);

            if (pointerRaycastResults.Count == 0)
            {
                Debug.Log($"[UIManager] PointerDown at {pointerPosition}, raycast hit nothing.");
                return;
            }

            var hitSummary = string.Empty;
            var count = Mathf.Min(pointerRaycastResults.Count, 8);
            for (var index = 0; index < count; index++)
            {
                var result = pointerRaycastResults[index];
                hitSummary += index == 0
                    ? result.gameObject.name
                    : " > " + result.gameObject.name;
            }

            Debug.Log($"[UIManager] PointerDown at {pointerPosition}, topHits={hitSummary}");
        }

        private static bool TryGetPointerDownPosition(out Vector2 pointerPosition)
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

        private void QueueOpenPage(UIPageType pageType)
        {
            pendingOpenPageType = pageType;
            hasPendingOpenPage = true;
        }

        private void HideOtherPages(BasePage targetPage, BasePage pageToKeep, bool overlayLayer)
        {
            foreach (var pair in pageInstances)
            {
                var page = pair.Value;
                if (page == null || page == targetPage || page == pageToKeep || IsOverlayPage(pair.Key) != overlayLayer || !page.gameObject.activeSelf)
                {
                    continue;
                }

                Debug.Log($"[UIManager] Hide inactive page before open. page={pair.Key}, object={page.name}");
                page.OnHide();
                SetPageInput(page, false);
                page.gameObject.SetActive(false);
            }
        }

        private void CloseActiveOverlayPage(bool callOnClose)
        {
            if (!hasCurrentOverlayPage)
            {
                return;
            }

            CloseOverlayPage(currentOverlayPageType, callOnClose);
        }

        private void CloseOverlayPage(UIPageType pageType, bool callOnClose)
        {
            if (!IsOverlayPage(pageType) || !pageInstances.TryGetValue(pageType, out var page) || page == null)
            {
                return;
            }

            if (page.gameObject.activeSelf)
            {
                if (callOnClose)
                {
                    page.OnClose();
                }
                else
                {
                    page.OnHide();
                }
            }

            SetPageInput(page, false);
            page.gameObject.SetActive(false);

            if (hasCurrentOverlayPage && currentOverlayPageType == pageType)
            {
                hasCurrentOverlayPage = false;
                currentOverlayPageInstance = null;
                currentOverlayPageType = default(UIPageType);
            }

            if (hasCurrentMainPage && currentMainPageInstance != null)
            {
                SetMainContentVisible(true);
                currentPageInstance = currentMainPageInstance;
                currentPageType = currentMainPageType;
                hasCurrentPage = true;
                SetPageInput(currentMainPageInstance, true);
            }
            else if (hasCurrentPage && currentPageType == pageType)
            {
                hasCurrentPage = false;
                currentPageInstance = null;
            }
        }

        private static void SetPageInput(BasePage page, bool enabled)
        {
            if (page == null)
            {
                return;
            }

            var canvasGroup = EnsureCanvasGroup(page.gameObject);
            canvasGroup.interactable = enabled;
            canvasGroup.blocksRaycasts = enabled;
        }

        private void SetMainContentVisible(bool visible)
        {
            if (contentLayer != null && contentLayer.gameObject.activeSelf != visible)
            {
                contentLayer.gameObject.SetActive(visible);
            }

            SetPageInput(currentMainPageInstance, visible);
        }

        private static bool IsOverlayPage(UIPageType pageType)
        {
            return pageType == UIPageType.Stage;
        }

        private void FlushPendingOpenPage()
        {
            if (!hasPendingOpenPage || isSwitching)
            {
                return;
            }

            var pageType = pendingOpenPageType;
            hasPendingOpenPage = false;
            OpenPage(pageType);
        }

        private IEnumerator OpenPopupCoroutine(string popupName, object data)
        {
            var popup = GetOrCreatePopup(popupName);
            if (popup == null)
            {
                yield break;
            }

            popup.gameObject.SetActive(true);
            popup.transform.SetAsLastSibling();
            popup.OnOpen(data);
            popupStack.Push(popup);
            RefreshPopupMask();
            yield return popup.PlayOpenAnim();
        }

        private IEnumerator ClosePopupCoroutine(BasePopup popup)
        {
            if (popup == null || popup.IsClosing)
            {
                yield break;
            }

            popup.OnClose();
            yield return popup.PlayCloseAnim();
            RemovePopupFromStack(popup);

            if (ShouldCachePopup(popup.PopupName))
            {
                popup.gameObject.SetActive(false);
            }
            else
            {
                cachedPopups.Remove(popup.PopupName);
                Destroy(popup.gameObject);
            }

            RefreshPopupMask();
        }

        private void EnsureMainUi()
        {
            mainUiInitialized = true;

            mainCanvasObject = new GameObject("MainUICanvas", typeof(RectTransform));
            mainCanvasObject.transform.SetParent(transform, false);

            var canvas = mainCanvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            var scaler = mainCanvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            mainCanvasObject.AddComponent<GraphicRaycaster>();

            var root = UIFactory.CreateRect("UIManagerRoot", mainCanvasObject.transform);
            UIFactory.Stretch(root);

            var backgroundLayer = CreateLayer("BackgroundLayer", root);
            contentLayer = CreateLayer("ContentLayer", root);
            UIFactory.SetInset(contentLayer, 0f, MainTopBarHeight, 0f, MainBottomBarHeight);
            fullScreenPageLayer = CreateLayer("FullScreenPageLayer", root);
            commonLayer = CreateLayer("CommonLayer", root);
            popupLayer = CreateLayer("PopupLayer", root);

            CreateBackgroundLayer(backgroundLayer);
            topBar = CreateTopBar(commonLayer);
            bottomBar = CreateBottomBar(commonLayer);
            CreatePopupLayer(popupLayer);

            RestoreMainUiLayerOrder();
        }

        private void RestoreMainUiLayerOrder()
        {
            if (fullScreenPageLayer != null)
            {
                fullScreenPageLayer.SetAsLastSibling();
            }

            if (commonLayer != null)
            {
                commonLayer.SetAsLastSibling();
            }

            if (bottomBar != null)
            {
                bottomBar.transform.SetAsLastSibling();
            }

            if (popupLayer != null)
            {
                popupLayer.SetAsLastSibling();
            }
        }

        private RectTransform CreateLayer(string layerName, Transform parent)
        {
            var layer = UIFactory.CreateRect(layerName, parent);
            UIFactory.Stretch(layer);
            return layer;
        }

        private void CreateBackgroundLayer(RectTransform parent)
        {
            var background = UIFactory.CreatePanel("PixelSpaceBackground", parent, new Color(0.006f, 0.01f, 0.024f, 1f));
            UIFactory.Stretch(background.rectTransform);
            background.raycastTarget = false;

            commonBackgroundSprite = LoadMainBackgroundSprite();
            if (commonBackgroundSprite != null && commonBackgroundSprite.texture != null)
            {
                var imageRect = UIFactory.CreateRect("CommonBackground", parent);
                UIFactory.Stretch(imageRect);
                commonBackgroundRect = imageRect;
                commonBackgroundImage = imageRect.gameObject.AddComponent<RawImage>();
                commonBackgroundImage.texture = commonBackgroundSprite.texture;
                commonBackgroundImage.uvRect = GetSpriteUv(commonBackgroundSprite);
                commonBackgroundImage.color = Color.white;
                commonBackgroundImage.raycastTarget = false;
                return;
            }

            for (var index = 0; index < 32; index++)
            {
                var star = UIFactory.CreatePanel("Star_" + index, parent, index % 3 == 0 ? UIFactory.PanelAccentColor : UIFactory.MutedTextColor);
                star.raycastTarget = false;
                var rect = star.rectTransform;
                rect.anchorMin = new Vector2((index * 37 % 100) / 100f, (index * 61 % 100) / 100f);
                rect.anchorMax = rect.anchorMin;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = Vector2.one * (index % 3 == 0 ? 5f : 3f);
            }
        }

        private void ApplyCommonBackground(UIPageType pageType)
        {
            if (commonBackgroundImage == null
                || commonBackgroundRect == null
                || commonBackgroundSprite == null
                || commonBackgroundSprite.texture == null
                || bottomBar == null
                || !bottomBar.TryGetNavigationSegment(pageType, out var segmentIndex, out var segmentCount))
            {
                return;
            }

            commonBackgroundImage.uvRect = GetSpriteUv(commonBackgroundSprite);
            MoveCommonBackground(segmentIndex, segmentCount);
        }

        private void MoveCommonBackground(int segmentIndex, int segmentCount)
        {
            var targetAnchorMin = new Vector2(-segmentIndex, 0f);
            var targetAnchorMax = new Vector2(segmentCount - segmentIndex, 1f);

            if (!Application.isPlaying || !gameObject.activeInHierarchy || !commonBackgroundPositionInitialized)
            {
                SetCommonBackgroundAnchors(targetAnchorMin, targetAnchorMax);
                commonBackgroundPositionInitialized = true;
                return;
            }

            if (commonBackgroundMoveRoutine != null)
            {
                StopCoroutine(commonBackgroundMoveRoutine);
            }

            commonBackgroundMoveRoutine = StartCoroutine(MoveCommonBackgroundCoroutine(targetAnchorMin, targetAnchorMax));
        }

        private IEnumerator MoveCommonBackgroundCoroutine(Vector2 targetAnchorMin, Vector2 targetAnchorMax)
        {
            var startAnchorMin = commonBackgroundRect.anchorMin;
            var startAnchorMax = commonBackgroundRect.anchorMax;
            var timer = 0f;

            while (timer < CommonBackgroundMoveDuration)
            {
                timer += Time.deltaTime;
                var t = EaseOutQuad(Mathf.Clamp01(timer / CommonBackgroundMoveDuration));
                SetCommonBackgroundAnchors(
                    Vector2.Lerp(startAnchorMin, targetAnchorMin, t),
                    Vector2.Lerp(startAnchorMax, targetAnchorMax, t));
                yield return null;
            }

            SetCommonBackgroundAnchors(targetAnchorMin, targetAnchorMax);
            commonBackgroundMoveRoutine = null;
        }

        private void SetCommonBackgroundAnchors(Vector2 anchorMin, Vector2 anchorMax)
        {
            if (commonBackgroundRect == null)
            {
                return;
            }

            commonBackgroundRect.anchorMin = anchorMin;
            commonBackgroundRect.anchorMax = anchorMax;
            commonBackgroundRect.offsetMin = Vector2.zero;
            commonBackgroundRect.offsetMax = Vector2.zero;
        }

        private static Rect GetSpriteUv(Sprite sprite)
        {
            var texture = sprite.texture;
            var rect = sprite.textureRect;
            return new Rect(
                rect.x / texture.width,
                rect.y / texture.height,
                rect.width / texture.width,
                rect.height / texture.height);
        }

        private static Sprite LoadMainBackgroundSprite()
        {
#if UNITY_EDITOR
            var editorSprite = AssetDatabase.LoadAssetAtPath<Sprite>(MainBackgroundSpritePath);
            if (editorSprite != null)
            {
                return editorSprite;
            }
#endif

            return RuntimeAssetCatalog.LoadSprite(MainBackgroundSpritePath);
        }

        private void PreparePageForCommonBackground(BasePage page, UIPageType pageType)
        {
            if (page == null || commonBackgroundImage == null || !IsMainNavigationPage(pageType))
            {
                return;
            }

            foreach (var graphic in page.GetComponentsInChildren<Graphic>(true))
            {
                if (!ShouldHidePageBackground(page.transform, graphic))
                {
                    continue;
                }

                var color = graphic.color;
                color.a = 0f;
                graphic.color = color;
                graphic.raycastTarget = false;
            }
        }

        private static bool IsMainNavigationPage(UIPageType pageType)
        {
            return pageType == UIPageType.Hangar
                || pageType == UIPageType.Lobby
                || pageType == UIPageType.Setting;
        }

        private static bool ShouldHidePageBackground(Transform pageRoot, Graphic graphic)
        {
            if (graphic == null || graphic.rectTransform == null || graphic.transform.parent != pageRoot)
            {
                return false;
            }

            var isPageBackdrop = graphic.name.IndexOf("Backdrop", StringComparison.OrdinalIgnoreCase) >= 0;
            if (isPageBackdrop && IsStretchedToParent(graphic.rectTransform))
            {
                return true;
            }

            return false;
        }

        private static bool IsStretchedToParent(RectTransform rect)
        {
            const float epsilon = 0.01f;
            return NearlyEqual(rect.anchorMin, Vector2.zero, epsilon)
                && NearlyEqual(rect.anchorMax, Vector2.one, epsilon)
                && NearlyEqual(rect.offsetMin, Vector2.zero, epsilon)
                && NearlyEqual(rect.offsetMax, Vector2.zero, epsilon);
        }

        private static bool NearlyEqual(Vector2 left, Vector2 right, float epsilon)
        {
            return Mathf.Abs(left.x - right.x) <= epsilon
                && Mathf.Abs(left.y - right.y) <= epsilon;
        }

        private TopBar CreateTopBar(RectTransform parent)
        {
            var rect = UIFactory.CreateRect("TopBar", parent);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, MainTopBarHeight);

            var bar = rect.gameObject.AddComponent<TopBar>();
            bar.BuildDefaultView();
            return bar;
        }

        private BottomBar CreateBottomBar(RectTransform parent)
        {
            GameObject barObject = null;
            var prefab = LoadBottomBarPrefab();
            if (prefab != null)
            {
                barObject = Instantiate(prefab, parent);
                barObject.name = "BottomBar";
            }

            var rect = barObject != null ? barObject.GetComponent<RectTransform>() : null;
            if (rect == null)
            {
                if (barObject != null)
                {
                    Destroy(barObject);
                }

                rect = UIFactory.CreateRect("BottomBar", parent);
                barObject = rect.gameObject;
            }

            var useFullScreenPrefabRect = HasEmbeddedCanvas(barObject);
            if (useFullScreenPrefabRect)
            {
                UIFactory.Stretch(rect);
            }
            else
            {
                ConfigureBottomBarRect(rect);
            }

            UIFactory.NormalizeEmbeddedCanvases(barObject.transform);
            rect.SetAsLastSibling();
            EnsureCanvasGroup(barObject);

            var bar = barObject.GetComponent<BottomBar>();
            if (bar == null)
            {
                bar = barObject.AddComponent<BottomBar>();
            }

            bar.BuildDefaultView();
            return bar;
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

        private static GameObject LoadBottomBarPrefab()
        {
#if UNITY_EDITOR
            var editorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BottomBarPrefabAssetPath);
            if (editorPrefab != null)
            {
                return editorPrefab;
            }
#endif

            var catalogPrefab = RuntimeAssetCatalog.LoadPrefab(BottomBarPrefabAssetPath);
            return catalogPrefab != null
                ? catalogPrefab
                : Resources.Load<GameObject>(BottomBarPrefabResourcesPath);
        }

        private static GameObject LoadPagePrefab(string prefabPath)
        {
            if (string.IsNullOrEmpty(prefabPath))
            {
                return null;
            }

#if UNITY_EDITOR
            var editorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (editorPrefab != null)
            {
                return editorPrefab;
            }
#endif

            var catalogPrefab = RuntimeAssetCatalog.LoadPrefab(prefabPath);
            if (catalogPrefab != null)
            {
                return catalogPrefab;
            }

            return Resources.Load<GameObject>(ToResourcesPath(prefabPath));
        }

        private static string ToResourcesPath(string prefabPath)
        {
            var normalized = prefabPath.Replace("\\", "/").Trim();
            const string resourcesSegment = "/Resources/";
            var resourcesIndex = normalized.IndexOf(resourcesSegment, StringComparison.OrdinalIgnoreCase);
            if (resourcesIndex >= 0)
            {
                normalized = normalized.Substring(resourcesIndex + resourcesSegment.Length);
            }

            const string resourcesPrefix = "Assets/Resources/";
            if (normalized.StartsWith(resourcesPrefix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(resourcesPrefix.Length);
            }

            const string prefabExtension = ".prefab";
            if (normalized.EndsWith(prefabExtension, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(0, normalized.Length - prefabExtension.Length);
            }

            return normalized;
        }

        private static void ConfigureBottomBarRect(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, MainBottomBarHeight);
        }

        private static bool HasEmbeddedCanvas(GameObject root)
        {
            if (root == null)
            {
                return false;
            }

            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                if (canvas != null && canvas.transform != root.transform)
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyPageChrome(UIPageType pageType)
        {
            var overlayPage = IsOverlayPage(pageType);
            var fullContentPage = pageType == UIPageType.Lobby;
            Debug.Log($"[UIManager] ApplyPageChrome. page={pageType}, topVisible={!overlayPage}, bottomVisible={!overlayPage}, fullContent={fullContentPage}");
            ShowTopBar(!overlayPage);
            ShowBottomBar(!overlayPage);
            RestoreMainUiLayerOrder();

            if (contentLayer == null)
            {
                return;
            }

            if (overlayPage)
            {
                SetMainContentVisible(false);
                return;
            }

            SetMainContentVisible(true);

            if (fullContentPage)
            {
                UIFactory.Stretch(contentLayer);
                return;
            }

            UIFactory.SetInset(contentLayer, 0f, MainTopBarHeight, 0f, MainBottomBarHeight);
        }

        private void CreatePopupLayer(RectTransform parent)
        {
            var maskObject = UIFactory.CreateRect("Mask", parent);
            UIFactory.Stretch(maskObject);
            popupMask = maskObject.gameObject.AddComponent<Image>();
            popupMask.color = new Color(0f, 0f, 0f, 0.55f);

            var maskButton = maskObject.gameObject.AddComponent<Button>();
            maskButton.targetGraphic = popupMask;
            maskButton.onClick.AddListener(OnClickPopupMask);
            maskObject.gameObject.SetActive(false);

            popupContainer = UIFactory.CreateRect("PopupContainer", parent);
            UIFactory.Stretch(popupContainer);
        }

        private BasePage GetOrCreatePage(UIPageType pageType)
        {
            if (pageInstances.TryGetValue(pageType, out var page) && page != null)
            {
                Debug.Log($"[UIManager] Reuse cached page: {pageType}, active={page.gameObject.activeSelf}");
                return page;
            }

            if (!UIConfig.PageConfigs.TryGetValue(pageType, out var config))
            {
                Debug.LogWarning($"[UIManager] UI page config not found: {pageType}");
                return null;
            }

            var pageParent = GetPageLayer(pageType);
            GameObject pageObject = null;
            var prefab = LoadPagePrefab(config.prefabPath);
            if (prefab != null)
            {
                pageObject = Instantiate(prefab, pageParent);
                Debug.Log($"[UIManager] Loaded page prefab. page={pageType}, path={config.prefabPath}");
            }

            if (pageObject == null)
            {
                Debug.LogWarning($"[UIManager] Page prefab not found, create fallback page. page={pageType}, path={config.prefabPath}");
                pageObject = CreateFallbackPage(config, pageParent);
            }

            var rectTransform = pageObject.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                UIFactory.Stretch(rectTransform);
            }

            UIFactory.NormalizeEmbeddedCanvases(pageObject.transform);

            page = pageObject.GetComponent<BasePage>();
            if (page == null)
            {
                page = AddPageComponent(pageObject, pageType);
            }

            page.Configure(config.pageType, config.index);
            SetPageInput(page, false);
            page.gameObject.SetActive(false);
            pageInstances[pageType] = page;
            Debug.Log($"[UIManager] Page created. page={pageType}, object={pageObject.name}, component={page.GetType().Name}");
            return page;
        }

        private Transform GetPageLayer(UIPageType pageType)
        {
            if (IsOverlayPage(pageType) && fullScreenPageLayer != null)
            {
                return fullScreenPageLayer;
            }

            return contentLayer;
        }

        private GameObject CreateFallbackPage(UIPageConfig config, Transform parent)
        {
            var rect = UIFactory.CreateRect(config.pageType + "Page", parent);
            UIFactory.Stretch(rect);
            AddPageComponent(rect.gameObject, config.pageType);
            return rect.gameObject;
        }

        private BasePage AddPageComponent(GameObject pageObject, UIPageType pageType)
        {
            switch (pageType)
            {
                case UIPageType.Hangar:
                    return pageObject.AddComponent<HangarPage>();
                case UIPageType.Setting:
                    return pageObject.AddComponent<SettingPage>();
                case UIPageType.Stage:
                    return pageObject.AddComponent<StagePage>();
                default:
                    return pageObject.AddComponent<LobbyPage>();
            }
        }

        private BasePopup GetOrCreatePopup(string popupName)
        {
            if (cachedPopups.TryGetValue(popupName, out var cached) && cached != null)
            {
                return cached;
            }

            if (!UIConfig.PopupConfigs.TryGetValue(popupName, out var config))
            {
                Debug.LogWarning($"Popup config not found: {popupName}");
                return null;
            }

            GameObject popupObject = null;
            var prefab = Resources.Load<GameObject>(config.prefabPath);
            if (prefab != null)
            {
                popupObject = Instantiate(prefab, popupContainer);
            }

            if (popupObject == null)
            {
                popupObject = CreateFallbackPopup(config);
            }

            var popup = popupObject.GetComponent<BasePopup>();
            if (popup == null)
            {
                popup = AddPopupComponent(popupObject, popupName);
            }

            popup.Configure(popupName);
            cachedPopups[popupName] = popup;
            return popup;
        }

        private GameObject CreateFallbackPopup(PopupConfig config)
        {
            var rect = UIFactory.CreateRect(config.popupName, popupContainer);
            AddPopupComponent(rect.gameObject, config.popupName);
            return rect.gameObject;
        }

        private BasePopup AddPopupComponent(GameObject popupObject, string popupName)
        {
            if (popupName == UIConfig.PlaneUnlockSuccessPopupName)
            {
                return popupObject.AddComponent<PlaneUnlockSuccessPopup>();
            }

            return popupObject.AddComponent<PlaneUnlockPopup>();
        }

        private BasePopup FindPopup(string popupName)
        {
            foreach (var popup in popupStack)
            {
                if (popup != null && popup.PopupName == popupName)
                {
                    return popup;
                }
            }

            return null;
        }

        private void RemovePopupFromStack(BasePopup target)
        {
            var popups = new List<BasePopup>(popupStack);
            popupStack.Clear();

            for (var index = popups.Count - 1; index >= 0; index--)
            {
                if (popups[index] != null && popups[index] != target)
                {
                    popupStack.Push(popups[index]);
                }
            }
        }

        private bool ShouldCachePopup(string popupName)
        {
            return UIConfig.PopupConfigs.TryGetValue(popupName, out var config) && config.cache;
        }

        private void RefreshPopupMask()
        {
            if (popupMask != null)
            {
                popupMask.gameObject.SetActive(popupStack.Count > 0);
                popupMask.transform.SetAsFirstSibling();
            }
        }

        private void OnClickPopupMask()
        {
            if (popupStack.Count == 0)
            {
                return;
            }

            var top = popupStack.Peek();
            if (top != null
                && UIConfig.PopupConfigs.TryGetValue(top.PopupName, out var config)
                && config.closeOnMaskClick)
            {
                CloseTopPopup();
            }
        }

        private static float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }

        private void EnsureBattleHudCanvas()
        {
            EnsureEventSystem();

            var canvasObject = new GameObject("BattleHudCanvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();
            canvasRoot = canvasObject.GetComponent<RectTransform>();

            CreateHud(canvasObject.transform);
            CreateStageTimer(canvasObject.transform);
            CreateBossHud(canvasObject.transform);
            CreateBossNotice(canvasObject.transform);
            CreateSettlementText(canvasObject.transform);
            CreateRestartChallengeButton(canvasObject.transform);
            CreateNextLevelButton(canvasObject.transform);
        }

        private void CreateHud(Transform parent)
        {
            var hudObject = new GameObject("HudText", typeof(RectTransform));
            hudObject.transform.SetParent(parent, false);

            var rect = hudObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(36f, -148f);
            rect.sizeDelta = new Vector2(560f, 132f);

            hudText = hudObject.AddComponent<Text>();
            hudText.font = UIFactory.GetDefaultFont();
            hudText.fontSize = 32;
            hudText.fontStyle = FontStyle.Bold;
            hudText.alignment = TextAnchor.UpperLeft;
            hudText.color = Color.white;
            hudText.raycastTarget = false;
        }

        private void CreateStageTimer(Transform parent)
        {
            var timerObject = new GameObject("StageTimer", typeof(RectTransform));
            timerObject.transform.SetParent(parent, false);

            var rect = timerObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -36f);
            rect.sizeDelta = new Vector2(360f, 64f);

            stageTimerText = timerObject.AddComponent<Text>();
            stageTimerText.font = UIFactory.GetDefaultFont();
            stageTimerText.fontSize = 34;
            stageTimerText.fontStyle = FontStyle.Bold;
            stageTimerText.alignment = TextAnchor.MiddleCenter;
            stageTimerText.color = new Color(0.78f, 0.96f, 1f, 1f);
            stageTimerText.raycastTarget = false;
            stageTimerText.text = "TIME 00:00";
        }

        private void CreateSettlementText(Transform parent)
        {
            settlementRoot = new GameObject("SettlementPanel", typeof(RectTransform));
            settlementRoot.transform.SetParent(parent, false);

            var panelRect = settlementRoot.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(860f, 460f);

            var panelImage = settlementRoot.AddComponent<Image>();
            panelImage.color = new Color(0.03f, 0.05f, 0.09f, 0.9f);

            var settlementObject = new GameObject("SettlementText", typeof(RectTransform));
            settlementObject.transform.SetParent(settlementRoot.transform, false);

            var rect = settlementObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(48f, 120f);
            rect.offsetMax = new Vector2(-48f, -48f);

            settlementText = settlementObject.AddComponent<Text>();
            settlementText.font = UIFactory.GetDefaultFont();
            settlementText.fontSize = 50;
            settlementText.fontStyle = FontStyle.Bold;
            settlementText.alignment = TextAnchor.MiddleCenter;
            settlementText.color = new Color(1f, 0.95f, 0.75f, 1f);
            settlementText.raycastTarget = false;
            settlementText.enabled = false;
            settlementRoot.SetActive(false);
        }

        private void CreateRestartChallengeButton(Transform parent)
        {
            restartChallengeRoot = new GameObject("RestartChallengeButton", typeof(RectTransform));
            restartChallengeRoot.transform.SetParent(parent, false);

            var rect = restartChallengeRoot.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -152f);
            rect.sizeDelta = new Vector2(340f, 82f);

            var image = restartChallengeRoot.AddComponent<Image>();
            image.color = new Color(0.1f, 0.62f, 1f, 0.94f);

            restartChallengeButton = restartChallengeRoot.AddComponent<Button>();
            restartChallengeButton.targetGraphic = image;
            restartChallengeButton.transition = Selectable.Transition.ColorTint;
            restartChallengeButton.colors = CreateButtonColors();
            restartChallengeButton.onClick.AddListener(RestartChallenge);

            CreateBattleButtonLabel(restartChallengeRoot.transform, "重新挑战");
            restartChallengeRoot.SetActive(false);
        }

        private void CreateNextLevelButton(Transform parent)
        {
            nextLevelRoot = new GameObject("NextLevelButton", typeof(RectTransform));
            nextLevelRoot.transform.SetParent(parent, false);

            var rect = nextLevelRoot.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(190f, -152f);
            rect.sizeDelta = new Vector2(340f, 82f);

            var image = nextLevelRoot.AddComponent<Image>();
            image.color = new Color(1f, 0.58f, 0.12f, 0.96f);

            nextLevelButton = nextLevelRoot.AddComponent<Button>();
            nextLevelButton.targetGraphic = image;
            nextLevelButton.transition = Selectable.Transition.ColorTint;
            nextLevelButton.colors = CreateButtonColors();
            nextLevelButton.onClick.AddListener(LoadNextLevel);

            CreateBattleButtonLabel(nextLevelRoot.transform, "下一关");
            nextLevelRoot.SetActive(false);
        }

        private static void CreateBattleButtonLabel(Transform parent, string label)
        {
            var labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);

            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var text = labelObject.AddComponent<Text>();
            text.text = label;
            text.font = UIFactory.GetDefaultFont();
            text.fontSize = 34;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
        }

        private void CreateBossHud(Transform parent)
        {
            bossHudRoot = new GameObject("BossHud", typeof(RectTransform));
            bossHudRoot.transform.SetParent(parent, false);

            var rootRect = bossHudRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, -150f);
            rootRect.sizeDelta = new Vector2(840f, 112f);

            var nameObject = new GameObject("BossName", typeof(RectTransform));
            nameObject.transform.SetParent(bossHudRoot.transform, false);
            var nameRect = nameObject.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.anchoredPosition = Vector2.zero;
            nameRect.sizeDelta = new Vector2(0f, 42f);

            bossNameText = nameObject.AddComponent<Text>();
            bossNameText.font = UIFactory.GetDefaultFont();
            bossNameText.fontSize = 32;
            bossNameText.fontStyle = FontStyle.Bold;
            bossNameText.alignment = TextAnchor.MiddleLeft;
            bossNameText.color = new Color(1f, 0.92f, 0.74f, 1f);
            bossNameText.raycastTarget = false;

            var phaseObject = new GameObject("BossPhase", typeof(RectTransform));
            phaseObject.transform.SetParent(bossHudRoot.transform, false);
            var phaseRect = phaseObject.GetComponent<RectTransform>();
            phaseRect.anchorMin = new Vector2(0f, 1f);
            phaseRect.anchorMax = new Vector2(1f, 1f);
            phaseRect.pivot = new Vector2(0.5f, 1f);
            phaseRect.anchoredPosition = new Vector2(0f, -38f);
            phaseRect.sizeDelta = new Vector2(0f, 28f);

            bossPhaseText = phaseObject.AddComponent<Text>();
            bossPhaseText.font = UIFactory.GetDefaultFont();
            bossPhaseText.fontSize = 22;
            bossPhaseText.fontStyle = FontStyle.Bold;
            bossPhaseText.alignment = TextAnchor.MiddleRight;
            bossPhaseText.color = new Color(0.8f, 0.96f, 1f, 1f);
            bossPhaseText.raycastTarget = false;

            var barBackObject = new GameObject("BossHealthBack", typeof(RectTransform));
            barBackObject.transform.SetParent(bossHudRoot.transform, false);
            var barBackRect = barBackObject.GetComponent<RectTransform>();
            barBackRect.anchorMin = new Vector2(0f, 0f);
            barBackRect.anchorMax = new Vector2(1f, 0f);
            barBackRect.pivot = new Vector2(0.5f, 0f);
            barBackRect.anchoredPosition = Vector2.zero;
            barBackRect.sizeDelta = new Vector2(0f, 34f);

            var backImage = barBackObject.AddComponent<Image>();
            backImage.color = new Color(0.05f, 0.06f, 0.09f, 0.88f);
            backImage.raycastTarget = false;

            var fillObject = new GameObject("BossHealthFill", typeof(RectTransform));
            fillObject.transform.SetParent(barBackObject.transform, false);
            var fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = new Vector2(5f, 5f);
            fillRect.offsetMax = new Vector2(-5f, -5f);

            bossHealthFill = fillObject.AddComponent<Image>();
            bossHealthFill.type = Image.Type.Filled;
            bossHealthFill.fillMethod = Image.FillMethod.Horizontal;
            bossHealthFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            bossHealthFill.color = new Color(1f, 0.25f, 0.18f, 0.96f);
            bossHealthFill.raycastTarget = false;

            bossHudRoot.SetActive(false);
        }

        private void CreateBossNotice(Transform parent)
        {
            var noticeObject = new GameObject("BossNotice", typeof(RectTransform));
            noticeObject.transform.SetParent(parent, false);

            var rect = noticeObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.72f);
            rect.anchorMax = new Vector2(0.5f, 0.72f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(900f, 180f);

            bossNoticeText = noticeObject.AddComponent<Text>();
            bossNoticeText.font = UIFactory.GetDefaultFont();
            bossNoticeText.fontSize = 58;
            bossNoticeText.fontStyle = FontStyle.Bold;
            bossNoticeText.alignment = TextAnchor.MiddleCenter;
            bossNoticeText.color = new Color(1f, 0.85f, 0.22f, 0f);
            bossNoticeText.raycastTarget = false;
            bossNoticeText.enabled = false;
        }

        private IEnumerator AnimateScorePopup(RectTransform rect, Text text)
        {
            const float lifetime = 0.55f;
            var age = 0f;
            var start = rect.anchoredPosition;
            var end = start + Vector2.up * 78f;
            var startColor = text.color;
            var endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);

            while (age < lifetime)
            {
                age += Time.deltaTime;
                var t = Mathf.Clamp01(age / lifetime);
                rect.anchoredPosition = Vector2.Lerp(start, end, t);
                text.color = Color.Lerp(startColor, endColor, t);
                yield return null;
            }

            if (rect != null)
            {
                Destroy(rect.gameObject);
            }
        }

        private IEnumerator AnimateBossNotice(string message)
        {
            const float fadeIn = 0.12f;
            const float hold = 1.0f;
            const float fadeOut = 0.32f;

            bossNoticeText.text = message;
            bossNoticeText.enabled = true;

            yield return FadeBossNotice(0f, 1f, fadeIn);
            yield return new WaitForSeconds(hold);
            yield return FadeBossNotice(1f, 0f, fadeOut);

            bossNoticeText.enabled = false;
            bossNoticeRoutine = null;
        }

        private IEnumerator FadeBossNotice(float from, float to, float duration)
        {
            var age = 0f;
            while (age < duration)
            {
                age += Time.deltaTime;
                var t = Mathf.Clamp01(age / Mathf.Max(0.01f, duration));
                SetBossNoticeAlpha(Mathf.Lerp(from, to, t));
                yield return null;
            }

            SetBossNoticeAlpha(to);
        }

        private void SetBossNoticeAlpha(float alpha)
        {
            if (bossNoticeText == null)
            {
                return;
            }

            var color = bossNoticeText.color;
            color.a = alpha;
            bossNoticeText.color = color;
        }

        private void UpdateHud()
        {
            if (hudText == null)
            {
                return;
            }

            var player = FindObjectOfType<PlayerController>();
            var hp = player != null ? player.CurrentHp : 0;
            var shield = player != null ? player.CurrentShield : 0;
            var stars = player != null ? player.CurrentStars : 0;
            var coins = player != null ? player.CurrentCoins : 0;
            var score = GameManager.Instance != null ? GameManager.Instance.Score : 0;
            var levelText = GameManager.Instance != null
                ? $"LEVEL {GameManager.Instance.CurrentLevelNumber}/{GameManager.Instance.MaxLevelCount}"
                : "LEVEL -";
            hudText.text = $"{levelText}\nHP {hp}  SH {shield}  STAR {stars}  COIN {coins}\nSCORE {score}";
        }

        private void UpdateStageTimer()
        {
            if (stageTimerText == null)
            {
                return;
            }

            var stageTime = StageManager.Instance != null ? StageManager.Instance.StageTime : 0f;
            stageTimerText.text = "TIME " + FormatStageTime(stageTime);
        }

        private static string FormatStageTime(float time)
        {
            var totalSeconds = Mathf.Max(0, Mathf.FloorToInt(time));
            var hours = totalSeconds / 3600;
            var minutes = totalSeconds / 60 % 60;
            var seconds = totalSeconds % 60;

            return hours > 0
                ? $"{hours:00}:{minutes:00}:{seconds:00}"
                : $"{minutes:00}:{seconds:00}";
        }

        private void UpdateSettlement()
        {
            if (settlementText == null || GameManager.Instance == null)
            {
                return;
            }

            var state = GameManager.Instance.CurrentState;
            var finished = state == GameState.Defeat || state == GameState.Victory;
            var isVictory = state == GameState.Victory;
            var showNextLevel = isVictory && GameManager.Instance.HasNextLevel;
            if (settlementRoot != null && settlementRoot.activeSelf != finished)
            {
                settlementRoot.SetActive(finished);
            }

            settlementText.enabled = finished;
            SetSettlementButtonsVisible(finished, showNextLevel);

            if (!finished)
            {
                return;
            }

            var title = isVictory
                ? GameManager.Instance.HasNextLevel ? "CLEAR" : "ALL CLEAR"
                : "GAME OVER";
            var level = $"{GameManager.Instance.CurrentLevelDisplayName}  {GameManager.Instance.CurrentLevelNumber}/{GameManager.Instance.MaxLevelCount}";
            var boss = isVictory ? $"\n击破 {GameManager.Instance.CurrentLevelBossDisplayName}" : string.Empty;
            settlementText.text = $"{level}\n{title}{boss}\nSCORE {GameManager.Instance.Score}";
        }

        private void SetSettlementButtonsVisible(bool showRestart, bool showNextLevel)
        {
            if (restartChallengeRoot != null && restartChallengeRoot.activeSelf != showRestart)
            {
                restartChallengeRoot.SetActive(showRestart);
            }

            if (restartChallengeButton != null)
            {
                restartChallengeButton.interactable = showRestart;
            }

            if (nextLevelRoot != null && nextLevelRoot.activeSelf != showNextLevel)
            {
                nextLevelRoot.SetActive(showNextLevel);
            }

            if (nextLevelButton != null)
            {
                nextLevelButton.interactable = showNextLevel;
            }

            UpdateSettlementButtonLayout(showNextLevel);
        }

        private void UpdateSettlementButtonLayout(bool showNextLevel)
        {
            if (restartChallengeRoot != null)
            {
                var rect = restartChallengeRoot.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchoredPosition = showNextLevel ? new Vector2(-190f, -152f) : new Vector2(0f, -152f);
                }
            }

            if (nextLevelRoot != null)
            {
                var rect = nextLevelRoot.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchoredPosition = new Vector2(190f, -152f);
                }
            }
        }

        private static void RestartChallenge()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RestartCurrentScene();
            }
        }

        private static void LoadNextLevel()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.LoadNextLevel();
            }
        }

        private static ColorBlock CreateButtonColors()
        {
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.82f, 0.95f, 1f, 1f);
            colors.pressedColor = new Color(0.42f, 0.82f, 1f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.65f);
            colors.colorMultiplier = 1f;
            return colors;
        }

        private static void EnsureEventSystem()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                var createdEventSystemObject = new GameObject("EventSystem");
                eventSystem = createdEventSystemObject.AddComponent<EventSystem>();
            }

            var eventSystemObject = eventSystem.gameObject;
            if (eventSystemObject.GetComponent<StandaloneInputModule>() == null)
            {
                eventSystemObject.AddComponent<StandaloneInputModule>();
            }

#if UNITY_WEBGL
            if (eventSystemObject.GetComponent<TTSDK.TTInputOverrideBypass>() == null)
            {
                eventSystemObject.AddComponent<TTSDK.TTInputOverrideBypass>();
            }
#endif
        }
    }
}
