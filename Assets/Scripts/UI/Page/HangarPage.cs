using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LeiTing.UI
{
    public class HangarPage : BasePage
    {
        public static HangarPage Instance { get; private set; }

        [SerializeField] private RectTransform listContent;
        private readonly List<PlaneItem> planeItems = new List<PlaneItem>();

        public override void OnCreate()
        {
            Instance = this;

            if (transform.childCount == 0)
            {
                BuildDefaultView();
            }

            BindPrefabView();

            if (Application.isPlaying)
            {
                PlaneManager.GetOrCreate().OnPlaneDataChanged += RefreshList;
            }
        }

        public override void OnShow()
        {
            RefreshList();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            var manager = PlaneManager.Instance;
            if (manager != null)
            {
                manager.OnPlaneDataChanged -= RefreshList;
            }
        }

        public void RefreshList()
        {
            if (listContent == null)
            {
                BindPrefabView();
            }

            if (listContent == null)
            {
                return;
            }

            for (var index = planeItems.Count - 1; index >= 0; index--)
            {
                if (planeItems[index] != null)
                {
                    Destroy(planeItems[index].gameObject);
                }
            }

            planeItems.Clear();

            foreach (var plane in PlaneManager.GetOrCreate().GetPlanes())
            {
                var itemRect = UIFactory.CreateRect("PlaneItem_" + plane.id, listContent);
                var item = itemRect.gameObject.AddComponent<PlaneItem>();
                item.SetData(plane);
                planeItems.Add(item);
            }
        }

        private void BuildDefaultView()
        {
            UIFactory.Stretch(RectTransform);

            var backdrop = UIFactory.CreatePanel("HangarBackdrop", transform, new Color(0.016f, 0.023f, 0.04f, 0.96f));
            UIFactory.Stretch(backdrop.rectTransform);

            var title = UIFactory.CreateText("Title", transform, "机库", 54f, TextAnchor.MiddleLeft, Color.white);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -170f);
            titleRect.offsetMin = new Vector2(56f, titleRect.offsetMin.y);
            titleRect.offsetMax = new Vector2(-56f, titleRect.offsetMax.y);
            titleRect.sizeDelta = new Vector2(titleRect.sizeDelta.x, 72f);

            var scrollRoot = UIFactory.CreateRect("PlaneScroll", transform);
            scrollRoot.anchorMin = new Vector2(0f, 0f);
            scrollRoot.anchorMax = new Vector2(1f, 1f);
            scrollRoot.offsetMin = new Vector2(48f, 190f);
            scrollRoot.offsetMax = new Vector2(-48f, -270f);

            var scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            var viewport = UIFactory.CreatePanel("Viewport", scrollRoot, new Color(0f, 0f, 0f, 0f));
            UIFactory.Stretch(viewport.rectTransform);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            listContent = UIFactory.CreateRect("Content", viewport.rectTransform);
            listContent.anchorMin = new Vector2(0f, 1f);
            listContent.anchorMax = new Vector2(1f, 1f);
            listContent.pivot = new Vector2(0.5f, 1f);
            listContent.offsetMin = Vector2.zero;
            listContent.offsetMax = Vector2.zero;

            var layout = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 18f;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = listContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport.rectTransform;
            scrollRect.content = listContent;
        }

        private void BindPrefabView()
        {
            listContent = listContent != null
                ? listContent
                : UIFactory.FindComponentInChildren<RectTransform>(transform, "Content");
        }
    }
}
