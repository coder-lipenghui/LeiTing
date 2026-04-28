using UnityEngine;

namespace LeiTing.UI
{
    public abstract class BasePage : MonoBehaviour
    {
        public UIPageType PageType { get; protected set; }
        public int PageIndex { get; protected set; }

        protected RectTransform rectTransform;
        private bool created;

        public RectTransform RectTransform
        {
            get
            {
                if (rectTransform == null)
                {
                    rectTransform = GetComponent<RectTransform>();
                }

                return rectTransform;
            }
        }

        public virtual void Configure(UIPageType pageType, int pageIndex)
        {
            PageType = pageType;
            PageIndex = pageIndex;
            rectTransform = GetComponent<RectTransform>();

            if (!created)
            {
                created = true;
                OnCreate();
            }
        }

        public virtual void OnCreate()
        {
        }

        public virtual void OnOpen(object data = null)
        {
        }

        public virtual void OnShow()
        {
        }

        public virtual void OnHide()
        {
        }

        public virtual void OnClose()
        {
        }

        public virtual void OnDestroyPage()
        {
        }
    }
}
