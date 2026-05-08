using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LeiTing.Config;
using LeiTing.Core;
using LeiTing.Enemy;
using LeiTing.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LeiTing.UI
{
    public class UIManager : MonoSingleton<UIManager>
    {
        private const float PageSwitchDuration = 0.25f;
        private const float MainTopBarHeight = 112f;
        private const float MainBottomBarHeight = 144f;
        private const string SingleBulletId = "player_bullet_01";
        private const string DoubleBulletId = "player_bullet_double_01";
        private const string SpreadBulletId = "player_bullet_spread_01";
        private const string PierceBulletId = "player_bullet_pierce_01";
        private const string LaserBulletId = "player_laser_01";
        private const float SimulatorDebugPanelWidth = 980f;
        private const float SimulatorDebugRowHeight = 64f;

        private readonly Dictionary<UIPageType, BasePage> pageInstances = new Dictionary<UIPageType, BasePage>();
        private readonly Stack<BasePopup> popupStack = new Stack<BasePopup>();
        private readonly Dictionary<string, BasePopup> cachedPopups = new Dictionary<string, BasePopup>();

        private GameObject mainCanvasObject;
        private RectTransform contentLayer;
        private RectTransform popupLayer;
        private RectTransform popupContainer;
        private Image popupMask;
        private TopBar topBar;
        private BottomBar bottomBar;
        private BasePage currentPageInstance;
        private UIPageType currentPageType;
        private bool hasCurrentPage;
        private bool isSwitching;
        private bool mainUiInitialized;

        private readonly WeaponButton[] weaponButtons =
        {
            new WeaponButton("单发", SingleBulletId),
            new WeaponButton("双发", DoubleBulletId),
            new WeaponButton("5散射", SpreadBulletId),
            new WeaponButton("穿透2", PierceBulletId),
            new WeaponButton("激光", LaserBulletId)
        };

        private readonly List<string> spawnOptionIds = new List<string>();
        private RectTransform canvasRoot;
        private Text hudText;
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
        private string selectedBulletId = LaserBulletId;
        private bool battleHudInitialized;
        private bool debugConfigLoadRequested;
        private string debugSpawnOptionsSignature;
        private Dropdown weaponDropdown;
        private Dropdown spawnModeDropdown;
        private Dropdown spawnOptionDropdown;
        private InputField spawnIdInput;
        private Text debugFeedbackText;

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

        private void Start()
        {
            if (battleHudInitialized)
            {
                RefreshDebugSpawnOptions();
                ApplyWeaponSelection(selectedBulletId);
            }
        }

        private void Update()
        {
            if (!battleHudInitialized)
            {
                return;
            }

            UpdateHud();
            UpdateSettlement();

            if (ShouldShowSimulatorDebugUi() && spawnOptionDropdown != null)
            {
                RefreshDebugSpawnOptions(false);
            }
        }

        public void Init()
        {
            if (mainUiInitialized)
            {
                ShowMainUI(true);
                return;
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

        public void OpenPage(UIPageType pageType)
        {
            if (!mainUiInitialized)
            {
                EnsureMainUi();
            }

            if (isSwitching)
            {
                return;
            }

            var targetPage = GetOrCreatePage(pageType);
            if (targetPage == null)
            {
                return;
            }

            if (currentPageInstance != null && currentPageInstance != targetPage)
            {
                currentPageInstance.OnHide();
                currentPageInstance.gameObject.SetActive(false);
            }

            targetPage.gameObject.SetActive(true);
            targetPage.RectTransform.anchoredPosition = Vector2.zero;
            targetPage.OnOpen();
            targetPage.OnShow();

            currentPageType = pageType;
            currentPageInstance = targetPage;
            hasCurrentPage = true;
            bottomBar?.SetSelected(pageType);
        }

        public void SwitchPage(UIPageType targetPageType)
        {
            if (!mainUiInitialized)
            {
                Init();
            }

            if (isSwitching || hasCurrentPage && currentPageType == targetPageType)
            {
                return;
            }

            if (!hasCurrentPage || currentPageInstance == null)
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
            page.gameObject.SetActive(false);

            if (UIConfig.PageConfigs.TryGetValue(pageType, out var config) && !config.cache)
            {
                page.OnDestroyPage();
                pageInstances.Remove(pageType);
                Destroy(page.gameObject);
            }

            if (hasCurrentPage && currentPageType == pageType)
            {
                hasCurrentPage = false;
                currentPageInstance = null;
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
            }
        }

        public void ShowBottomBar(bool visible)
        {
            if (bottomBar != null)
            {
                bottomBar.gameObject.SetActive(visible);
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
            EnsureWeaponTestUi();
            ApplyWeaponSelection(selectedBulletId);
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
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
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

            var currentPage = currentPageInstance;
            var targetPage = GetOrCreatePage(targetPageType);

            if (currentPage == null || targetPage == null)
            {
                isSwitching = false;
                yield break;
            }

            var width = Mathf.Max(1f, contentLayer != null ? contentLayer.rect.width : Screen.width);
            var targetToRight = targetPage.PageIndex > currentPage.PageIndex;
            var currentTargetX = targetToRight ? -width : width;
            var targetStartX = targetToRight ? width : -width;

            targetPage.gameObject.SetActive(true);
            targetPage.RectTransform.anchoredPosition = new Vector2(targetStartX, 0f);
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
            targetPage.OnShow();

            currentPageType = targetPageType;
            currentPageInstance = targetPage;
            hasCurrentPage = true;
            bottomBar?.SetSelected(targetPageType);
            isSwitching = false;
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
            var commonLayer = CreateLayer("CommonLayer", root);
            popupLayer = CreateLayer("PopupLayer", root);

            CreateBackgroundLayer(backgroundLayer);
            topBar = CreateTopBar(commonLayer);
            bottomBar = CreateBottomBar(commonLayer);
            CreatePopupLayer(popupLayer);
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

            for (var index = 0; index < 32; index++)
            {
                var star = UIFactory.CreatePanel("Star_" + index, parent, index % 3 == 0 ? UIFactory.PanelAccentColor : UIFactory.MutedTextColor);
                var rect = star.rectTransform;
                rect.anchorMin = new Vector2((index * 37 % 100) / 100f, (index * 61 % 100) / 100f);
                rect.anchorMax = rect.anchorMin;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = Vector2.one * (index % 3 == 0 ? 5f : 3f);
            }
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
            var rect = UIFactory.CreateRect("BottomBar", parent);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, MainBottomBarHeight);

            var bar = rect.gameObject.AddComponent<BottomBar>();
            bar.BuildDefaultView();
            return bar;
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
                return page;
            }

            if (!UIConfig.PageConfigs.TryGetValue(pageType, out var config))
            {
                Debug.LogWarning($"UI page config not found: {pageType}");
                return null;
            }

            GameObject pageObject = null;
            var prefab = Resources.Load<GameObject>(config.prefabPath);
            if (prefab != null)
            {
                pageObject = Instantiate(prefab, contentLayer);
            }

            if (pageObject == null)
            {
                pageObject = CreateFallbackPage(config);
            }

            page = pageObject.GetComponent<BasePage>();
            if (page == null)
            {
                page = AddPageComponent(pageObject, pageType);
            }

            page.Configure(config.pageType, config.index);
            page.gameObject.SetActive(false);
            pageInstances[pageType] = page;
            return page;
        }

        private GameObject CreateFallbackPage(UIPageConfig config)
        {
            var rect = UIFactory.CreateRect(config.pageType + "Page", contentLayer);
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

        private void EnsureWeaponTestUi()
        {
            EnsureEventSystem();

            var canvasObject = new GameObject("WeaponTestCanvas", typeof(RectTransform));
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

            var showDebugPanel = ShouldShowSimulatorDebugUi();
            if (showDebugPanel)
            {
                CreateSimulatorDebugPanel(canvasObject.transform);
            }

            CreateHud(canvasObject.transform, showDebugPanel);
            CreateBossHud(canvasObject.transform, showDebugPanel);
            CreateBossNotice(canvasObject.transform);
            CreateSettlementText(canvasObject.transform);
            CreateRestartChallengeButton(canvasObject.transform);
            CreateNextLevelButton(canvasObject.transform);
        }

        private void CreateSimulatorDebugPanel(Transform parent)
        {
            var panelObject = new GameObject("SimulatorDebugPanel", typeof(RectTransform));
            panelObject.transform.SetParent(parent, false);

            var panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -24f);
            panelRect.sizeDelta = new Vector2(SimulatorDebugPanelWidth, 252f);

            var panelImage = panelObject.AddComponent<Image>();
            panelImage.color = new Color(0.025f, 0.038f, 0.065f, 0.88f);

            var layout = panelObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.padding = new RectOffset(18, 18, 14, 12);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var title = UIFactory.CreateText(
                "Title",
                panelObject.transform,
                "模拟器调试",
                24f,
                TextAnchor.MiddleLeft,
                new Color(0.78f, 0.93f, 1f, 1f));
            SetPreferredSize(title.gameObject, -1f, 30f);

            var weaponRow = CreateDebugRow("WeaponRow", panelObject.transform);
            CreateDebugLabel(weaponRow.transform, "武器", 88f);
            weaponDropdown = CreateDebugDropdown("WeaponDropdown", weaponRow.transform, 704f);
            ConfigureWeaponDropdown();

            var weaponApplyButton = CreateDebugButton("WeaponConfirm", weaponRow.transform, "确认", 112f);
            weaponApplyButton.onClick.AddListener(ApplySelectedWeaponFromDebugPanel);

            var spawnRow = CreateDebugRow("SpawnRow", panelObject.transform);
            CreateDebugLabel(spawnRow.transform, "刷敌", 88f);
            spawnModeDropdown = CreateDebugDropdown("SpawnModeDropdown", spawnRow.transform, 132f);
            ConfigureSpawnModeDropdown();
            spawnOptionDropdown = CreateDebugDropdown("SpawnOptionDropdown", spawnRow.transform, 300f);
            spawnOptionDropdown.onValueChanged.AddListener(UpdateSpawnInputFromOption);
            spawnIdInput = CreateDebugInput("SpawnIdInput", spawnRow.transform, 260f, "输入 ID");

            var spawnConfirmButton = CreateDebugButton("SpawnConfirm", spawnRow.transform, "确认", 112f);
            spawnConfirmButton.onClick.AddListener(ConfirmDebugSpawn);

            debugFeedbackText = UIFactory.CreateText(
                "DebugFeedback",
                panelObject.transform,
                string.Empty,
                22f,
                TextAnchor.MiddleLeft,
                UIFactory.MutedTextColor);
            SetPreferredSize(debugFeedbackText.gameObject, -1f, 28f);

            RefreshWeaponDropdownState();
            RefreshDebugSpawnOptions();
        }

        private GameObject CreateDebugRow(string name, Transform parent)
        {
            var row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            SetPreferredSize(row, -1f, SimulatorDebugRowHeight);

            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            return row;
        }

        private static Text CreateDebugLabel(Transform parent, string label, float width)
        {
            var text = UIFactory.CreateText(
                label + "Label",
                parent,
                label,
                24f,
                TextAnchor.MiddleLeft,
                new Color(0.78f, 0.9f, 1f, 1f));
            SetPreferredSize(text.gameObject, width, SimulatorDebugRowHeight);
            return text;
        }

        private Dropdown CreateDebugDropdown(string name, Transform parent, float width)
        {
            var root = UIFactory.CreateRect(name, parent);
            SetPreferredSize(root.gameObject, width, SimulatorDebugRowHeight);

            var image = root.gameObject.AddComponent<Image>();
            image.color = new Color(0.055f, 0.08f, 0.13f, 0.95f);

            var dropdown = root.gameObject.AddComponent<Dropdown>();
            dropdown.targetGraphic = image;
            dropdown.transition = Selectable.Transition.ColorTint;
            dropdown.colors = CreateButtonColors();

            var caption = UIFactory.CreateText(
                "Label",
                root,
                string.Empty,
                24f,
                TextAnchor.MiddleLeft,
                Color.white);
            var captionRect = caption.rectTransform;
            captionRect.anchorMin = Vector2.zero;
            captionRect.anchorMax = Vector2.one;
            captionRect.offsetMin = new Vector2(16f, 0f);
            captionRect.offsetMax = new Vector2(-44f, 0f);
            dropdown.captionText = caption;

            var arrow = UIFactory.CreateText(
                "Arrow",
                root,
                "v",
                22f,
                TextAnchor.MiddleCenter,
                new Color(0.72f, 0.86f, 0.96f, 1f));
            var arrowRect = arrow.rectTransform;
            arrowRect.anchorMin = new Vector2(1f, 0f);
            arrowRect.anchorMax = Vector2.one;
            arrowRect.pivot = new Vector2(1f, 0.5f);
            arrowRect.offsetMin = new Vector2(-40f, 0f);
            arrowRect.offsetMax = Vector2.zero;

            var template = CreateDropdownTemplate(root);
            dropdown.template = template;
            dropdown.itemText = template.Find("Viewport/Content/Item/Item Label")?.GetComponent<Text>();
            template.gameObject.SetActive(false);
            return dropdown;
        }

        private RectTransform CreateDropdownTemplate(RectTransform parent)
        {
            var template = UIFactory.CreateRect("Template", parent);
            template.anchorMin = new Vector2(0f, 0f);
            template.anchorMax = new Vector2(1f, 0f);
            template.pivot = new Vector2(0.5f, 1f);
            template.anchoredPosition = new Vector2(0f, -4f);
            template.sizeDelta = new Vector2(0f, 246f);

            var templateImage = template.gameObject.AddComponent<Image>();
            templateImage.color = new Color(0.035f, 0.052f, 0.085f, 0.98f);

            var scrollRect = template.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            var viewport = UIFactory.CreateRect("Viewport", template);
            UIFactory.Stretch(viewport);

            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.03f);

            var mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var content = UIFactory.CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 0f;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var item = UIFactory.CreateRect("Item", content);
            item.sizeDelta = new Vector2(0f, 46f);

            var itemImage = item.gameObject.AddComponent<Image>();
            itemImage.color = new Color(0.06f, 0.09f, 0.14f, 0.95f);

            var toggle = item.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = itemImage;
            toggle.transition = Selectable.Transition.ColorTint;
            toggle.colors = CreateButtonColors();

            var checkmark = UIFactory.CreatePanel("Item Checkmark", item, new Color(0.1f, 0.62f, 1f, 0.92f));
            var checkmarkRect = checkmark.rectTransform;
            checkmarkRect.anchorMin = new Vector2(0f, 0.5f);
            checkmarkRect.anchorMax = new Vector2(0f, 0.5f);
            checkmarkRect.pivot = new Vector2(0f, 0.5f);
            checkmarkRect.anchoredPosition = new Vector2(6f, 0f);
            checkmarkRect.sizeDelta = new Vector2(4f, 30f);
            toggle.graphic = checkmark;

            var itemLabel = UIFactory.CreateText(
                "Item Label",
                item,
                string.Empty,
                22f,
                TextAnchor.MiddleLeft,
                Color.white);
            var itemLabelRect = itemLabel.rectTransform;
            itemLabelRect.anchorMin = Vector2.zero;
            itemLabelRect.anchorMax = Vector2.one;
            itemLabelRect.offsetMin = new Vector2(18f, 0f);
            itemLabelRect.offsetMax = new Vector2(-8f, 0f);

            scrollRect.viewport = viewport;
            scrollRect.content = content;
            return template;
        }

        private InputField CreateDebugInput(string name, Transform parent, float width, string placeholderText)
        {
            var root = UIFactory.CreateRect(name, parent);
            SetPreferredSize(root.gameObject, width, SimulatorDebugRowHeight);

            var image = root.gameObject.AddComponent<Image>();
            image.color = new Color(0.04f, 0.06f, 0.1f, 0.94f);
            root.gameObject.AddComponent<RectMask2D>();

            var input = root.gameObject.AddComponent<InputField>();
            input.targetGraphic = image;
            input.transition = Selectable.Transition.ColorTint;
            input.colors = CreateButtonColors();
            input.lineType = InputField.LineType.SingleLine;

            var text = UIFactory.CreateText(
                "Text",
                root,
                string.Empty,
                24f,
                TextAnchor.MiddleLeft,
                Color.white);
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 0f);
            textRect.offsetMax = new Vector2(-16f, 0f);
            text.supportRichText = false;

            var placeholder = UIFactory.CreateText(
                "Placeholder",
                root,
                placeholderText,
                24f,
                TextAnchor.MiddleLeft,
                UIFactory.MutedTextColor);
            var placeholderRect = placeholder.rectTransform;
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(16f, 0f);
            placeholderRect.offsetMax = new Vector2(-16f, 0f);

            input.textComponent = text;
            input.placeholder = placeholder;
            return input;
        }

        private Button CreateDebugButton(string name, Transform parent, string label, float width)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform));
            buttonObject.transform.SetParent(parent, false);
            SetPreferredSize(buttonObject, width, SimulatorDebugRowHeight);

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.1f, 0.62f, 1f, 0.94f);

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = CreateButtonColors();

            CreateBattleButtonLabel(buttonObject.transform, label);
            return button;
        }

        private static void SetPreferredSize(GameObject target, float preferredWidth, float preferredHeight)
        {
            var layoutElement = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
            if (preferredWidth >= 0f)
            {
                layoutElement.preferredWidth = preferredWidth;
                layoutElement.minWidth = preferredWidth;
            }

            if (preferredHeight >= 0f)
            {
                layoutElement.preferredHeight = preferredHeight;
                layoutElement.minHeight = preferredHeight;
            }
        }

        private void CreateHud(Transform parent, bool offsetForDebugPanel)
        {
            var hudObject = new GameObject("HudText", typeof(RectTransform));
            hudObject.transform.SetParent(parent, false);

            var rect = hudObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(36f, offsetForDebugPanel ? -304f : -148f);
            rect.sizeDelta = new Vector2(560f, 132f);

            hudText = hudObject.AddComponent<Text>();
            hudText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            hudText.fontSize = 32;
            hudText.fontStyle = FontStyle.Bold;
            hudText.alignment = TextAnchor.UpperLeft;
            hudText.color = Color.white;
            hudText.raycastTarget = false;
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
            settlementText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
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
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 34;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
        }

        private void CreateBossHud(Transform parent, bool offsetForDebugPanel)
        {
            bossHudRoot = new GameObject("BossHud", typeof(RectTransform));
            bossHudRoot.transform.SetParent(parent, false);

            var rootRect = bossHudRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, offsetForDebugPanel ? -306f : -150f);
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
            bossNameText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
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
            bossPhaseText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
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
            bossNoticeText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
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

        private void ApplyWeaponSelection(string bulletId)
        {
            selectedBulletId = bulletId;

            var shooter = FindObjectOfType<PlayerShooter>();
            if (shooter != null)
            {
                shooter.SetBulletId(bulletId);
            }

            RefreshWeaponDropdownState();
        }

        private void ConfigureWeaponDropdown()
        {
            if (weaponDropdown == null)
            {
                return;
            }

            var options = new List<Dropdown.OptionData>();
            for (var index = 0; index < weaponButtons.Length; index++)
            {
                options.Add(new Dropdown.OptionData(weaponButtons[index].Label));
            }

            weaponDropdown.ClearOptions();
            weaponDropdown.AddOptions(options);
            RefreshWeaponDropdownState();
        }

        private void ApplySelectedWeaponFromDebugPanel()
        {
            if (weaponDropdown == null || weaponButtons.Length == 0)
            {
                return;
            }

            var index = Mathf.Clamp(weaponDropdown.value, 0, weaponButtons.Length - 1);
            ApplyWeaponSelection(weaponButtons[index].BulletId);
            SetDebugFeedback($"已切换武器: {weaponButtons[index].Label}", false);
        }

        private void RefreshWeaponDropdownState()
        {
            if (weaponDropdown == null)
            {
                return;
            }

            var selectedIndex = 0;
            for (var index = 0; index < weaponButtons.Length; index++)
            {
                if (weaponButtons[index].BulletId == selectedBulletId)
                {
                    selectedIndex = index;
                    break;
                }
            }

            weaponDropdown.SetValueWithoutNotify(selectedIndex);
            weaponDropdown.RefreshShownValue();
        }

        private void ConfigureSpawnModeDropdown()
        {
            if (spawnModeDropdown == null)
            {
                return;
            }

            spawnModeDropdown.ClearOptions();
            spawnModeDropdown.AddOptions(new List<Dropdown.OptionData>
            {
                new Dropdown.OptionData("波次"),
                new Dropdown.OptionData("敌机"),
                new Dropdown.OptionData("Boss")
            });
            spawnModeDropdown.onValueChanged.AddListener(_ => RefreshDebugSpawnOptions(true));
        }

        private void RefreshDebugSpawnOptions()
        {
            RefreshDebugSpawnOptions(true);
        }

        private void RefreshDebugSpawnOptions(bool force)
        {
            if (!ShouldShowSimulatorDebugUi() || spawnOptionDropdown == null)
            {
                return;
            }

            var configManager = ConfigManager.Instance;
            var config = ResolveDebugSpawnConfig(configManager);
            var signature = BuildDebugSpawnOptionsSignature(config);
            if (!force && string.Equals(debugSpawnOptionsSignature, signature, StringComparison.Ordinal))
            {
                return;
            }

            debugSpawnOptionsSignature = signature;

            spawnOptionIds.Clear();
            var options = new List<Dropdown.OptionData>();

            if (config != null)
            {
                switch (GetDebugSpawnMode())
                {
                    case DebugSpawnMode.Enemy:
                        AddEnemySpawnOptions(config, false, options);
                        break;
                    case DebugSpawnMode.Boss:
                        AddEnemySpawnOptions(config, true, options);
                        break;
                    default:
                        AddWaveSpawnOptions(configManager, config, options);
                        break;
                }
            }

            if (options.Count == 0)
            {
                spawnOptionIds.Add(string.Empty);
                options.Add(new Dropdown.OptionData("无可用配置"));
            }

            spawnOptionDropdown.ClearOptions();
            spawnOptionDropdown.AddOptions(options);
            spawnOptionDropdown.SetValueWithoutNotify(0);
            spawnOptionDropdown.RefreshShownValue();
            UpdateSpawnInputFromOption(0);
        }

        private GameConfig ResolveDebugSpawnConfig(ConfigManager configManager)
        {
            if (configManager == null)
            {
                return null;
            }

            var config = configManager.IsLoaded ? configManager.Config : null;
            if (!debugConfigLoadRequested && IsDebugSpawnConfigEmpty(config))
            {
                debugConfigLoadRequested = true;
                configManager.LoadDefaultConfig();
                config = configManager.IsLoaded ? configManager.Config : null;
            }

            return config;
        }

        private static bool IsDebugSpawnConfigEmpty(GameConfig config)
        {
            var enemyCount = config?.enemies != null ? config.enemies.Count : 0;
            var waveCount = config?.waves != null ? config.waves.Count : 0;
            return enemyCount == 0 && waveCount == 0;
        }

        private string BuildDebugSpawnOptionsSignature(GameConfig config)
        {
            if (config == null)
            {
                return $"{GetDebugSpawnMode()}:no-config";
            }

            var levelNumber = GameManager.Instance != null ? GameManager.Instance.CurrentLevelNumber : 0;
            var enemyCount = config.enemies != null ? config.enemies.Count : 0;
            var waveCount = config.waves != null ? config.waves.Count : 0;
            var enemyLastId = enemyCount > 0 ? config.enemies[enemyCount - 1]?.id : string.Empty;
            var waveLastId = waveCount > 0 ? config.waves[waveCount - 1]?.id : string.Empty;
            return $"{GetDebugSpawnMode()}:{levelNumber}:{enemyCount}:{waveCount}:{enemyLastId}:{waveLastId}";
        }

        private void AddWaveSpawnOptions(ConfigManager configManager, GameConfig config, List<Dropdown.OptionData> options)
        {
            IEnumerable<WaveConfig> waves = config.waves ?? new List<WaveConfig>();
            if (configManager != null && GameManager.Instance != null)
            {
                waves = configManager.GetWavesForLevel(GameManager.Instance.CurrentLevelNumber);
            }

            foreach (var wave in waves.Where(item => item != null && !string.IsNullOrEmpty(item.id)).OrderBy(item => item.startTime))
            {
                spawnOptionIds.Add(wave.id);
                options.Add(new Dropdown.OptionData($"{wave.id}  {wave.startTime:0.#}s"));
            }
        }

        private void AddEnemySpawnOptions(GameConfig config, bool bossOnly, List<Dropdown.OptionData> options)
        {
            var enemies = config.enemies ?? new List<EnemyConfig>();
            foreach (var enemy in enemies
                         .Where(item => item != null && !string.IsNullOrEmpty(item.id) && IsBossId(item.id) == bossOnly)
                         .OrderBy(item => item.id))
            {
                spawnOptionIds.Add(enemy.id);
                var label = string.IsNullOrEmpty(enemy.displayName)
                    ? enemy.id
                    : $"{enemy.id}  {enemy.displayName}";
                options.Add(new Dropdown.OptionData(label));
            }
        }

        private void UpdateSpawnInputFromOption(int optionIndex)
        {
            if (spawnIdInput == null)
            {
                return;
            }

            var id = optionIndex >= 0 && optionIndex < spawnOptionIds.Count
                ? spawnOptionIds[optionIndex]
                : string.Empty;
            spawnIdInput.SetTextWithoutNotify(id);
            SetDebugFeedback(string.Empty, false);
        }

        private void ConfirmDebugSpawn()
        {
            if (EnemyManager.Instance == null)
            {
                SetDebugFeedback("EnemyManager 未就绪", true);
                return;
            }

            var id = spawnIdInput != null ? spawnIdInput.text.Trim() : GetSelectedSpawnOptionId();
            if (string.IsNullOrEmpty(id))
            {
                SetDebugFeedback("请输入或选择 ID", true);
                return;
            }

            string message;
            bool succeeded;
            switch (GetDebugSpawnMode())
            {
                case DebugSpawnMode.Enemy:
                    succeeded = EnemyManager.Instance.TrySpawnEnemyNow(id, out message);
                    break;
                case DebugSpawnMode.Boss:
                    succeeded = EnemyManager.Instance.TrySpawnBossNow(id, out message);
                    break;
                default:
                    succeeded = EnemyManager.Instance.TrySpawnWaveNow(id, out message);
                    break;
            }

            SetDebugFeedback(message, !succeeded);
        }

        private string GetSelectedSpawnOptionId()
        {
            var index = spawnOptionDropdown != null ? spawnOptionDropdown.value : 0;
            return index >= 0 && index < spawnOptionIds.Count ? spawnOptionIds[index] : string.Empty;
        }

        private DebugSpawnMode GetDebugSpawnMode()
        {
            var value = spawnModeDropdown != null ? spawnModeDropdown.value : 0;
            if (value == 1)
            {
                return DebugSpawnMode.Enemy;
            }

            return value == 2 ? DebugSpawnMode.Boss : DebugSpawnMode.Wave;
        }

        private void SetDebugFeedback(string message, bool warning)
        {
            if (debugFeedbackText == null)
            {
                return;
            }

            debugFeedbackText.text = message;
            debugFeedbackText.color = warning
                ? UIFactory.WarningColor
                : new Color(0.68f, 0.95f, 0.8f, 1f);
        }

        private static bool IsBossId(string enemyId)
        {
            return !string.IsNullOrEmpty(enemyId)
                && enemyId.StartsWith("boss", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldShowSimulatorDebugUi()
        {
#if UNITY_EDITOR
            return Application.isEditor;
#else
            return false;
#endif
        }

        private enum DebugSpawnMode
        {
            Wave,
            Enemy,
            Boss
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
            if (EventSystem.current != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        private readonly struct WeaponButton
        {
            public readonly string Label;
            public readonly string BulletId;

            public WeaponButton(string label, string bulletId)
            {
                Label = label;
                BulletId = bulletId;
            }
        }
    }
}
