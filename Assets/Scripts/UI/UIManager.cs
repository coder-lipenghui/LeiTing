using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using LeiTing.Core;
using LeiTing.Player;
using LeiTing.Progress;
using LeiTing.Stage;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
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
    public class UIManager : MonoSingleton<UIManager>
    {
        private const float PageSwitchDuration = 0.25f;
        private const float BattleHudTopOffset = 65f;
        private const float BattleHudRowSpacing = 58f;
        private const int BossHealthSegmentCount = 10;
        private const string VictorySettlementPrefabPath = "Assets/Prefabs/UI/UIVictorySettlement.prefab";
        private const string VictoryContinueSpritePath = "Assets/Art/Sprites/UI/btnNext.png";

        private static readonly Color BossHealthColor = new Color32(0x86, 0x28, 0x00, 0xFF);

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
        private RectTransform scoreRect;
        private RectTransform stageTimerRect;
        private Text hudText;
        private Text scoreText;
        private Text stageTimerText;
        private GameObject settlementRoot;
        private Image settlementPanelImage;
        private RectTransform settlementTitleRect;
        private Text settlementTitleText;
        private Text settlementDetailText;
        private GameObject defeatBackRoot;
        private Button defeatBackButton;
        private GameObject victoryContinueRoot;
        private Button victoryContinueButton;
        private GameObject settlementContinueRoot;
        private Button settlementContinueButton;
        private GameObject settlementShareRoot;
        private Button settlementShareButton;
        private GameObject victorySettlementRoot;
        private VictorySettlementView victorySettlementView;
        private GameObject bossHudRoot;
        private Image bossHealthFill;
        private RectTransform bossHealthFillRect;
        private Text bossNameText;
        private Text bossPhaseText;
        private Text bossNoticeText;
        private Coroutine bossNoticeRoutine;
        private GameState lastBattleEndState = GameState.Boot;
        private bool victorySettlementVisible;
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
            UpdateBattleHudSafeAreaLayout();
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

            OpenPage(UIPageType.Lobby);
        }

        private bool IsMainUiReady()
        {
            return mainCanvasObject != null
                && contentLayer != null
                && fullScreenPageLayer != null
                && commonLayer != null
                && popupLayer != null;
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
        }

        private void EnsureLobbyPageVisible()
        {
            if (hasCurrentOverlayPage)
            {
                CloseOverlayPage(currentOverlayPageType, false);
            }

            if (!hasCurrentMainPage || currentMainPageInstance == null || !currentMainPageInstance.gameObject.activeSelf || currentMainPageType != UIPageType.Lobby)
            {
                Debug.Log($"[UIManager] Init restoring lobby. hasMain={hasCurrentMainPage}, currentMain={currentMainPageType}, active={currentMainPageInstance != null && currentMainPageInstance.gameObject.activeSelf}");
                OpenPage(UIPageType.Lobby);
                return;
            }

            ApplyPageChrome(UIPageType.Lobby);
            SetPageInput(currentMainPageInstance, true);
            currentMainPageInstance.RectTransform.anchoredPosition = Vector2.zero;
            currentPageInstance = currentMainPageInstance;
            currentPageType = currentMainPageType;
            hasCurrentPage = true;
            Debug.Log("[UIManager] Init verified lobby is visible.");
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
            Debug.Log($"[UIManager] OpenPage complete. current={currentPageType}");
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
            Debug.Log("[UIManager] OpenStageFromHall requested.");
            CloseAllPopups();
            OpenPage(UIPageType.Stage);
        }

        public void ReturnStageToHall()
        {
            Debug.Log("[UIManager] ReturnStageToHall requested.");

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
            currentMainPageInstance.gameObject.SetActive(true);
            currentMainPageInstance.RectTransform.anchoredPosition = Vector2.zero;
            SetPageInput(currentMainPageInstance, true);
            currentPageType = currentMainPageType;
            currentPageInstance = currentMainPageInstance;
            hasCurrentPage = true;
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
        }

        public void ShowBottomBar(bool visible)
        {
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
            if (bossHudRoot == null || bossHealthFill == null || bossHealthFillRect == null)
            {
                return;
            }

            var hpPercent = maxHp > 0 ? Mathf.Clamp01(currentHp / (float)maxHp) : 0f;
            bossHudRoot.SetActive(maxHp > 0);
            bossHealthFill.enabled = hpPercent > 0f;
            bossHealthFill.fillAmount = hpPercent;

            var anchorMax = bossHealthFillRect.anchorMax;
            anchorMax.x = hpPercent;
            bossHealthFillRect.anchorMax = anchorMax;

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
            return pageType == UIPageType.Setting || pageType == UIPageType.Stage;
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
            UIFactory.Stretch(contentLayer);
            fullScreenPageLayer = CreateLayer("FullScreenPageLayer", root);
            commonLayer = CreateLayer("CommonLayer", root);
            popupLayer = CreateLayer("PopupLayer", root);

            CreateBackgroundLayer(backgroundLayer);
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
            var background = UIFactory.CreatePanel("BlackBackground", parent, Color.black);
            UIFactory.Stretch(background.rectTransform);
            background.raycastTarget = false;
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

        private static Sprite LoadSprite(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

#if UNITY_EDITOR
            var editorSprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (editorSprite != null)
            {
                return editorSprite;
            }
#endif

            return RuntimeAssetCatalog.LoadSprite(assetPath);
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

        private void ApplyPageChrome(UIPageType pageType)
        {
            var overlayPage = IsOverlayPage(pageType);
            Debug.Log($"[UIManager] ApplyPageChrome. page={pageType}, overlay={overlayPage}");
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
            UIFactory.Stretch(contentLayer);
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
            CreateScoreText(canvasObject.transform);
            CreateStageTimer(canvasObject.transform);
            CreateBossHud(canvasObject.transform);
            CreateBossNotice(canvasObject.transform);
            CreateSettlementPanel(canvasObject.transform);
            CreateVictorySettlementPanel(canvasObject.transform);
            CreateVictoryContinueButton(canvasObject.transform);
            UpdateBattleHudSafeAreaLayout();
        }

        private void CreateHud(Transform parent)
        {
            var hudObject = new GameObject("BulletTimeHudText", typeof(RectTransform));
            hudObject.transform.SetParent(parent, false);

            var rect = hudObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(560f, 260f);

            hudText = hudObject.AddComponent<Text>();
            hudText.font = UIFactory.GetDefaultFont();
            hudText.fontSize = 36;
            hudText.fontStyle = FontStyle.Bold;
            hudText.alignment = TextAnchor.MiddleCenter;
            hudText.color = Color.white;
            hudText.raycastTarget = false;
            hudText.enabled = false;
        }

        private void CreateScoreText(Transform parent)
        {
            var scoreObject = new GameObject("ScoreText", typeof(RectTransform));
            scoreObject.transform.SetParent(parent, false);

            scoreRect = scoreObject.GetComponent<RectTransform>();
            scoreRect.anchorMin = new Vector2(0.5f, 1f);
            scoreRect.anchorMax = new Vector2(0.5f, 1f);
            scoreRect.pivot = new Vector2(0.5f, 1f);
            scoreRect.anchoredPosition = new Vector2(0f, -32f);
            scoreRect.sizeDelta = new Vector2(420f, 56f);

            scoreText = scoreObject.AddComponent<Text>();
            scoreText.font = UIFactory.GetDefaultFont();
            scoreText.fontSize = 36;
            scoreText.fontStyle = FontStyle.Bold;
            scoreText.alignment = TextAnchor.MiddleCenter;
            scoreText.color = new Color(1f, 0.9f, 0.34f, 1f);
            scoreText.raycastTarget = false;
            scoreText.text = "SCORE 0";
        }

        private void CreateStageTimer(Transform parent)
        {
            var timerObject = new GameObject("StageTimer", typeof(RectTransform));
            timerObject.transform.SetParent(parent, false);

            stageTimerRect = timerObject.GetComponent<RectTransform>();
            stageTimerRect.anchorMin = new Vector2(0.5f, 1f);
            stageTimerRect.anchorMax = new Vector2(0.5f, 1f);
            stageTimerRect.pivot = new Vector2(0.5f, 1f);
            stageTimerRect.anchoredPosition = new Vector2(0f, -88f);
            stageTimerRect.sizeDelta = new Vector2(360f, 56f);

            stageTimerText = timerObject.AddComponent<Text>();
            stageTimerText.font = UIFactory.GetDefaultFont();
            stageTimerText.fontSize = 30;
            stageTimerText.fontStyle = FontStyle.Bold;
            stageTimerText.alignment = TextAnchor.MiddleCenter;
            stageTimerText.color = new Color(0.78f, 0.96f, 1f, 1f);
            stageTimerText.raycastTarget = false;
            stageTimerText.text = "TIME 00:00";
        }

        private void CreateSettlementPanel(Transform parent)
        {
            settlementRoot = new GameObject("SettlementPanel", typeof(RectTransform));
            settlementRoot.transform.SetParent(parent, false);

            var panelRect = settlementRoot.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(860f, 640f);

            settlementPanelImage = settlementRoot.AddComponent<Image>();
            settlementPanelImage.color = new Color(0.03f, 0.05f, 0.09f, 0.9f);

            var titleObject = new GameObject("SettlementTitle", typeof(RectTransform));
            titleObject.transform.SetParent(settlementRoot.transform, false);

            settlementTitleRect = titleObject.GetComponent<RectTransform>();
            ConfigureSettlementTitleForVictory();

            settlementTitleText = titleObject.AddComponent<Text>();
            settlementTitleText.font = UIFactory.GetDefaultFont();
            settlementTitleText.fontSize = 78;
            settlementTitleText.fontStyle = FontStyle.Bold;
            settlementTitleText.alignment = TextAnchor.MiddleCenter;
            settlementTitleText.color = new Color(1f, 0.95f, 0.75f, 1f);
            settlementTitleText.raycastTarget = false;
            settlementTitleText.verticalOverflow = VerticalWrapMode.Overflow;

            var detailObject = new GameObject("SettlementDetail", typeof(RectTransform));
            detailObject.transform.SetParent(settlementRoot.transform, false);

            var detailRect = detailObject.GetComponent<RectTransform>();
            detailRect.anchorMin = Vector2.zero;
            detailRect.anchorMax = Vector2.one;
            detailRect.pivot = new Vector2(0.5f, 0.5f);
            detailRect.offsetMin = new Vector2(90f, 166f);
            detailRect.offsetMax = new Vector2(-90f, -250f);

            settlementDetailText = detailObject.AddComponent<Text>();
            settlementDetailText.font = UIFactory.GetDefaultFont();
            settlementDetailText.fontSize = 34;
            settlementDetailText.fontStyle = FontStyle.Bold;
            settlementDetailText.alignment = TextAnchor.UpperLeft;
            settlementDetailText.color = new Color(0.85f, 0.95f, 1f, 1f);
            settlementDetailText.raycastTarget = false;
            settlementDetailText.verticalOverflow = VerticalWrapMode.Overflow;

            CreateDefeatBackButton(parent);
            settlementRoot.SetActive(false);
        }

        private void CreateVictorySettlementPanel(Transform parent)
        {
            var prefab = LoadPagePrefab(VictorySettlementPrefabPath);
            if (prefab != null)
            {
                victorySettlementRoot = Instantiate(prefab, parent, false);
                victorySettlementRoot.name = "VictorySettlementPanel";

                var rect = victorySettlementRoot.GetComponent<RectTransform>();
                if (rect != null)
                {
                    UIFactory.Stretch(rect);
                }

                UIFactory.NormalizeEmbeddedCanvases(victorySettlementRoot.transform);
                UIFactory.ApplyFontsInChildren(victorySettlementRoot.transform);

                victorySettlementView = victorySettlementRoot.GetComponent<VictorySettlementView>();
                if (victorySettlementView == null)
                {
                    victorySettlementView = victorySettlementRoot.AddComponent<VictorySettlementView>();
                }

                BindVictorySettlementView();
                victorySettlementRoot.SetActive(false);
                return;
            }

            CreateFallbackVictorySettlementPanel(parent);
        }

        private void CreateFallbackVictorySettlementPanel(Transform parent)
        {
            victorySettlementRoot = new GameObject("VictorySettlementPanel", typeof(RectTransform));
            victorySettlementRoot.transform.SetParent(parent, false);

            var rect = victorySettlementRoot.GetComponent<RectTransform>();
            UIFactory.Stretch(rect);

            var background = victorySettlementRoot.AddComponent<Image>();
            background.color = new Color(0.015f, 0.02f, 0.032f, 0.82f);

            var title = UIFactory.CreateText(
                "TitleText",
                victorySettlementRoot.transform,
                "胜利结算",
                76f,
                TextAnchor.MiddleCenter,
                new Color(1f, 0.95f, 0.72f, 1f));
            title.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -420f);
            title.rectTransform.sizeDelta = new Vector2(920f, 132f);
            title.verticalOverflow = VerticalWrapMode.Overflow;

            var detail = UIFactory.CreateText(
                "DetailText",
                victorySettlementRoot.transform,
                string.Empty,
                34f,
                TextAnchor.UpperLeft,
                new Color(0.86f, 0.95f, 1f, 1f));
            detail.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            detail.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            detail.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            detail.rectTransform.anchoredPosition = new Vector2(0f, -52f);
            detail.rectTransform.sizeDelta = new Vector2(780f, 510f);
            detail.verticalOverflow = VerticalWrapMode.Overflow;

            settlementContinueRoot = CreateIconButton(
                victorySettlementRoot.transform,
                "ContinueButton",
                VictoryContinueSpritePath,
                new Vector2(-135f, 212f),
                new Vector2(126f, 126f));
            settlementContinueButton = settlementContinueRoot.GetComponent<Button>();

            settlementShareRoot = CreateSettlementActionButton(
                victorySettlementRoot.transform,
                "ShareButton",
                "分享",
                new Vector2(135f, 170f));
            settlementShareButton = settlementShareRoot.GetComponent<Button>();

            victorySettlementView = victorySettlementRoot.AddComponent<VictorySettlementView>();
            BindVictorySettlementView();
            victorySettlementRoot.SetActive(false);
        }

        private void BindVictorySettlementView()
        {
            if (victorySettlementView == null)
            {
                return;
            }

            victorySettlementView.ApplyContinueSprite(LoadSprite(VictoryContinueSpritePath));
            victorySettlementView.BindButtons(ReturnToLobby, ShareVictorySettlement);

            settlementContinueButton = victorySettlementView.ContinueButton;
            settlementShareButton = victorySettlementView.ShareButton;
            settlementContinueRoot = settlementContinueButton != null ? settlementContinueButton.gameObject : settlementContinueRoot;
            settlementShareRoot = settlementShareButton != null ? settlementShareButton.gameObject : settlementShareRoot;
        }

        private void CreateDefeatBackButton(Transform parent)
        {
            defeatBackRoot = new GameObject("DefeatBackButton", typeof(RectTransform));
            defeatBackRoot.transform.SetParent(parent, false);

            var rect = defeatBackRoot.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 640f);
            rect.sizeDelta = new Vector2(118f, 118f);

            var image = defeatBackRoot.AddComponent<Image>();
            image.sprite = LoadSprite("Assets/Art/Sprites/UI/btnBack.png");
            image.color = image.sprite != null ? Color.white : new Color(0.1f, 0.62f, 1f, 0.94f);

            defeatBackButton = defeatBackRoot.AddComponent<Button>();
            defeatBackButton.targetGraphic = image;
            defeatBackButton.transition = Selectable.Transition.ColorTint;
            defeatBackButton.colors = CreateButtonColors();
            defeatBackButton.onClick.AddListener(ReturnToLobby);

            defeatBackRoot.SetActive(false);
            UpdateDefeatBackButtonLayout();
        }

        private void CreateVictoryContinueButton(Transform parent)
        {
            victoryContinueRoot = CreateIconButton(
                parent,
                "VictoryContinueButton",
                VictoryContinueSpritePath,
                new Vector2(0f, 118f),
                new Vector2(118f, 118f));
            victoryContinueButton = victoryContinueRoot.GetComponent<Button>();
            victoryContinueButton.onClick.AddListener(ShowVictorySettlement);

            victoryContinueRoot.SetActive(false);
        }

        private static GameObject CreateIconButton(
            Transform parent,
            string name,
            string spritePath,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);

            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = root.AddComponent<Image>();
            var sprite = LoadSprite(spritePath);
            image.sprite = sprite;
            image.preserveAspect = sprite != null;
            image.color = sprite != null ? Color.white : new Color(1f, 0.58f, 0.12f, 0.96f);

            var button = root.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = CreateButtonColors();

            if (sprite == null)
            {
                CreateBattleButtonLabel(root.transform, "继续");
            }

            return root;
        }

        private void CreateSettlementActionButtons(Transform parent)
        {
            settlementContinueRoot = CreateSettlementActionButton(parent, "SettlementContinueButton", "继续", new Vector2(-164f, 68f));
            settlementContinueButton = settlementContinueRoot.GetComponent<Button>();
            settlementContinueButton.onClick.AddListener(ReturnToLobby);

            settlementShareRoot = CreateSettlementActionButton(parent, "SettlementShareButton", "分享", new Vector2(164f, 68f));
            settlementShareButton = settlementShareRoot.GetComponent<Button>();
            settlementShareButton.onClick.AddListener(ShareVictorySettlement);

            settlementContinueRoot.SetActive(false);
            settlementShareRoot.SetActive(false);
        }

        private static GameObject CreateSettlementActionButton(Transform parent, string name, string label, Vector2 anchoredPosition)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);

            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(260f, 86f);

            var image = root.AddComponent<Image>();
            image.color = new Color(0.1f, 0.62f, 1f, 0.94f);

            var button = root.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = CreateButtonColors();

            CreateBattleButtonLabel(root.transform, label);
            return root;
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
            rootRect.anchoredPosition = new Vector2(0f, -(BattleHudTopOffset + BattleHudRowSpacing * 2f));
            rootRect.sizeDelta = new Vector2(840f, 36f);

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
            nameObject.SetActive(false);

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
            phaseObject.SetActive(false);

            var barBackObject = new GameObject("BossHealthBack", typeof(RectTransform));
            barBackObject.transform.SetParent(bossHudRoot.transform, false);
            var barBackRect = barBackObject.GetComponent<RectTransform>();
            barBackRect.anchorMin = Vector2.zero;
            barBackRect.anchorMax = Vector2.one;
            barBackRect.pivot = new Vector2(0.5f, 0.5f);
            barBackRect.anchoredPosition = Vector2.zero;
            barBackRect.sizeDelta = Vector2.zero;

            var backImage = barBackObject.AddComponent<Image>();
            backImage.color = new Color(0.05f, 0.06f, 0.09f, 0.88f);
            backImage.raycastTarget = false;

            var fillAreaObject = new GameObject("BossHealthFillArea", typeof(RectTransform));
            fillAreaObject.transform.SetParent(barBackObject.transform, false);
            var fillAreaRect = fillAreaObject.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = new Vector2(5f, 5f);
            fillAreaRect.offsetMax = new Vector2(-5f, -5f);

            var fillObject = new GameObject("BossHealthFill", typeof(RectTransform));
            fillObject.transform.SetParent(fillAreaObject.transform, false);
            bossHealthFillRect = fillObject.GetComponent<RectTransform>();
            bossHealthFillRect.anchorMin = Vector2.zero;
            bossHealthFillRect.anchorMax = Vector2.one;
            bossHealthFillRect.offsetMin = Vector2.zero;
            bossHealthFillRect.offsetMax = Vector2.zero;

            bossHealthFill = fillObject.AddComponent<Image>();
            bossHealthFill.type = Image.Type.Simple;
            bossHealthFill.color = BossHealthColor;
            bossHealthFill.raycastTarget = false;

            CreateBossHealthDividers(fillAreaObject.transform);
            bossHudRoot.SetActive(false);
        }

        private static void CreateBossHealthDividers(Transform parent)
        {
            for (var index = 1; index < BossHealthSegmentCount; index++)
            {
                var dividerObject = new GameObject($"BossHealthDivider{index:00}", typeof(RectTransform));
                dividerObject.transform.SetParent(parent, false);

                var rect = dividerObject.GetComponent<RectTransform>();
                var x = index / (float)BossHealthSegmentCount;
                rect.anchorMin = new Vector2(x, 0f);
                rect.anchorMax = new Vector2(x, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(3f, 0f);

                var image = dividerObject.AddComponent<Image>();
                image.color = new Color(0.02f, 0.025f, 0.035f, 0.82f);
                image.raycastTarget = false;
            }
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
            if (hudText == null && scoreText == null)
            {
                return;
            }

            var player = FindObjectOfType<PlayerController>();
            var hp = player != null ? player.CurrentHp : 0;
            var stars = player != null ? player.CurrentStars : 0;
            var coins = player != null ? player.CurrentCoins : 0;
            var score = GameManager.Instance != null ? GameManager.Instance.Score : 0;
            var levelText = GameManager.Instance != null
                ? $"LEVEL {GameManager.Instance.CurrentLevelNumber}/{GameManager.Instance.MaxLevelCount}"
                : "LEVEL -";

            if (scoreText != null)
            {
                scoreText.text = $"SCORE {score}";
            }

            if (hudText == null)
            {
                return;
            }

            var showBulletTimeHud = BattleTimeController.Instance != null && BattleTimeController.Instance.IsBulletTimeActive;
            hudText.enabled = showBulletTimeHud;
            if (!showBulletTimeHud)
            {
                return;
            }

            hudText.text = $"{levelText}\nHP {hp}\nSTAR {stars}\nCOIN {coins}";
        }

        private void UpdateBattleHudSafeAreaLayout()
        {
            if (canvasRoot == null)
            {
                return;
            }

            var scoreTopOffset = BattleHudTopOffset;
            var timerTopOffset = scoreTopOffset + BattleHudRowSpacing;
            var bossTopOffset = timerTopOffset + BattleHudRowSpacing;

            if (scoreRect != null)
            {
                scoreRect.anchoredPosition = new Vector2(0f, -scoreTopOffset);
            }

            if (stageTimerRect != null)
            {
                stageTimerRect.anchoredPosition = new Vector2(0f, -timerTopOffset);
            }

            if (bossHudRoot != null)
            {
                var bossRect = bossHudRoot.GetComponent<RectTransform>();
                if (bossRect != null)
                {
                    bossRect.anchoredPosition = new Vector2(0f, -bossTopOffset);
                }
            }

            UpdateDefeatBackButtonLayout();
        }

        private float GetCanvasHeight()
        {
            if (canvasRoot != null && canvasRoot.rect.height > 1f)
            {
                return canvasRoot.rect.height;
            }

            return 1920f;
        }

        private void UpdateDefeatBackButtonLayout()
        {
            if (defeatBackRoot == null)
            {
                return;
            }

            var rect = defeatBackRoot.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = new Vector2(0f, GetCanvasHeight() / 3f);
            }
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
            if (settlementTitleText == null || GameManager.Instance == null)
            {
                return;
            }

            var state = GameManager.Instance.CurrentState;
            if (state != lastBattleEndState)
            {
                lastBattleEndState = state;
                victorySettlementVisible = false;
            }

            var finished = state == GameState.Defeat || state == GameState.Victory;
            var isVictory = state == GameState.Victory;

            if (!finished)
            {
                HideBattleEndUi();
                return;
            }

            if (isVictory)
            {
                UpdateVictoryEndUi();
            }
            else
            {
                UpdateDefeatEndUi();
            }
        }

        private void HideBattleEndUi()
        {
            SetVictorySlowMotionActive(false);
            SetObjectActive(settlementRoot, false);
            SetObjectActive(victorySettlementRoot, false);
            SetButtonVisible(victoryContinueRoot, victoryContinueButton, false);
            SetButtonVisible(defeatBackRoot, defeatBackButton, false);
            SetButtonVisible(settlementContinueRoot, settlementContinueButton, false);
            SetButtonVisible(settlementShareRoot, settlementShareButton, false);
        }

        private void UpdateVictoryEndUi()
        {
            var waitingForContinue = !victorySettlementVisible;
            SetVictorySlowMotionActive(waitingForContinue);
            SetButtonVisible(victoryContinueRoot, victoryContinueButton, waitingForContinue);
            SetObjectActive(settlementRoot, false);
            SetObjectActive(victorySettlementRoot, victorySettlementVisible);
            SetButtonVisible(defeatBackRoot, defeatBackButton, false);

            if (!victorySettlementVisible)
            {
                SetButtonVisible(settlementContinueRoot, settlementContinueButton, false);
                SetButtonVisible(settlementShareRoot, settlementShareButton, false);
                return;
            }

            if (victorySettlementView != null)
            {
                victorySettlementView.SetContent("胜利结算", BuildVictorySettlementDetails());
            }

            SetButtonVisible(settlementContinueRoot, settlementContinueButton, true);
            SetButtonVisible(settlementShareRoot, settlementShareButton, true);
        }

        private void UpdateDefeatEndUi()
        {
            SetVictorySlowMotionActive(false);
            SetButtonVisible(victoryContinueRoot, victoryContinueButton, false);
            SetObjectActive(victorySettlementRoot, false);
            SetObjectActive(settlementRoot, true);
            SetSettlementPanelBackgroundVisible(false);
            ConfigureSettlementTitleForDefeat();
            settlementTitleText.text = "GAME\nOVER";
            settlementTitleText.fontSize = 112;
            settlementTitleText.fontStyle = FontStyle.Bold;
            settlementDetailText.enabled = false;
            SetButtonVisible(defeatBackRoot, defeatBackButton, true);
            SetButtonVisible(settlementContinueRoot, settlementContinueButton, false);
            SetButtonVisible(settlementShareRoot, settlementShareButton, false);
        }

        private void ConfigureSettlementTitleForVictory()
        {
            if (settlementTitleRect == null)
            {
                return;
            }

            settlementTitleRect.anchorMin = new Vector2(0f, 1f);
            settlementTitleRect.anchorMax = new Vector2(1f, 1f);
            settlementTitleRect.pivot = new Vector2(0.5f, 1f);
            settlementTitleRect.anchoredPosition = new Vector2(0f, -42f);
            settlementTitleRect.sizeDelta = new Vector2(0f, 190f);
        }

        private void ConfigureSettlementTitleForDefeat()
        {
            if (settlementTitleRect == null)
            {
                return;
            }

            settlementTitleRect.anchorMin = new Vector2(0.5f, 0.5f);
            settlementTitleRect.anchorMax = new Vector2(0.5f, 0.5f);
            settlementTitleRect.pivot = new Vector2(0.5f, 0.5f);
            settlementTitleRect.anchoredPosition = new Vector2(0f, GetCanvasHeight() / 6f);
            settlementTitleRect.sizeDelta = new Vector2(760f, 320f);
        }

        private void SetSettlementPanelBackgroundVisible(bool visible)
        {
            if (settlementPanelImage != null)
            {
                settlementPanelImage.enabled = visible;
            }
        }

        private static void SetObjectActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private static void SetButtonVisible(GameObject root, Button button, bool visible)
        {
            SetObjectActive(root, visible);
            if (button != null)
            {
                button.interactable = visible;
            }
        }

        private void ShowVictorySettlement()
        {
            SetVictorySlowMotionActive(false);
            victorySettlementVisible = true;
            UpdateSettlement();
        }

        private static void ReturnToLobby()
        {
            SetVictorySlowMotionActive(false);
            GameSceneManager.GetOrCreate().EnterLobby();
        }

        private static void SetVictorySlowMotionActive(bool active)
        {
            var battleTimeController = BattleTimeController.Instance;
            if (battleTimeController == null)
            {
                return;
            }

            if (active)
            {
                battleTimeController.StartVictorySlowMotion();
            }
            else
            {
                battleTimeController.StopVictorySlowMotion();
            }
        }

        private static string BuildVictorySettlementDetails()
        {
            var gameManager = GameManager.Instance;
            var record = LevelProgressService.LastCompletedRecord;
            var score = record != null ? record.score : gameManager != null ? gameManager.Score : 0;
            var builder = new StringBuilder();

            if (gameManager != null)
            {
                builder.AppendLine($"关卡：{gameManager.CurrentLevelDisplayName}");
                builder.AppendLine($"击破：{gameManager.CurrentLevelBossDisplayName}");
            }

            builder.AppendLine($"本关积分：{score}");
            builder.AppendLine($"累计积分：{LevelProgressService.GetTotalScore()}");

            if (record != null)
            {
                builder.AppendLine($"击毁敌机：{record.enemyKillCount}/{Mathf.Max(0, record.totalEnemyCount)}");
                builder.AppendLine($"收集星星：{record.starCount}/{Mathf.Max(0, record.totalStarCount)}");
                builder.AppendLine($"达成目标：{record.EarnedAchievementCount}/{LevelProgressService.AchievementCount}");
                builder.AppendLine($"受击情况：{(record.wasHit ? "已受击" : "无伤")}");
            }

            return builder.ToString().TrimEnd();
        }

        private static void ShareVictorySettlement()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                var score = GameManager.Instance != null ? GameManager.Instance.Score : 0;
                var shareData = new JsonData();
                shareData["title"] = "雷霆战机";
                shareData["desc"] = $"本关积分 {score}";
                shareData["query"] = $"level={(GameManager.Instance != null ? GameManager.Instance.CurrentLevelNumber : 1)}&score={score}";
                TT.ShareAppMessage(
                    shareData,
                    data => Debug.Log("[BattleHud] Douyin share success."),
                    message => Debug.LogWarning($"[BattleHud] Douyin share failed: {message}"),
                    () => Debug.Log("[BattleHud] Douyin share cancelled."));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[BattleHud] Douyin share failed: {exception.Message}");
            }
#else
            Debug.Log("[BattleHud] Douyin share requested.");
#endif
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
