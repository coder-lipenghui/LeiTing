using System;
using System.Collections.Generic;

namespace LeiTing.UI
{
    [Serializable]
    public class UIPageConfig
    {
        public UIPageType pageType;
        public int index;
        public string prefabPath;
        public bool cache;
    }

    [Serializable]
    public class PopupConfig
    {
        public string popupName;
        public string prefabPath;
        public bool cache;
        public bool closeOnMaskClick;
    }

    public static class UIConfig
    {
        public const string PlaneUnlockPopupName = "PlaneUnlockPopup";
        public const string PlaneUnlockSuccessPopupName = "PlaneUnlockSuccessPopup";

        public static readonly Dictionary<UIPageType, UIPageConfig> PageConfigs =
            new Dictionary<UIPageType, UIPageConfig>
            {
                {
                    UIPageType.Hangar,
                    new UIPageConfig
                    {
                        pageType = UIPageType.Hangar,
                        index = 1,
                        prefabPath = "UI/Page/HangarPage",
                        cache = true
                    }
                },
                {
                    UIPageType.Lobby,
                    new UIPageConfig
                    {
                        pageType = UIPageType.Lobby,
                        index = 2,
                        prefabPath = "Assets/Prefabs/UI/UIHall.prefab",
                        cache = true
                    }
                },
                {
                    UIPageType.Stage,
                    new UIPageConfig
                    {
                        pageType = UIPageType.Stage,
                        index = 3,
                        prefabPath = "Assets/Prefabs/UI/UIStage.prefab",
                        cache = true
                    }
                },
                {
                    UIPageType.Setting,
                    new UIPageConfig
                    {
                        pageType = UIPageType.Setting,
                        index = 4,
                        prefabPath = "UI/Page/SettingPage",
                        cache = true
                    }
                }
            };

        public static readonly Dictionary<string, PopupConfig> PopupConfigs =
            new Dictionary<string, PopupConfig>
            {
                {
                    PlaneUnlockPopupName,
                    new PopupConfig
                    {
                        popupName = PlaneUnlockPopupName,
                        prefabPath = "UI/Popup/PlaneUnlockPopup",
                        cache = false,
                        closeOnMaskClick = true
                    }
                },
                {
                    PlaneUnlockSuccessPopupName,
                    new PopupConfig
                    {
                        popupName = PlaneUnlockSuccessPopupName,
                        prefabPath = "UI/Popup/PlaneUnlockSuccessPopup",
                        cache = false,
                        closeOnMaskClick = true
                    }
                }
            };
    }
}
