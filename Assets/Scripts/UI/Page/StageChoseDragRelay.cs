using UnityEngine;
using UnityEngine.EventSystems;

namespace LeiTing.UI
{
    public sealed class StageChoseDragRelay : MonoBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private LobbyPage owner;

        public void Configure(LobbyPage lobbyPage)
        {
            owner = lobbyPage;
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (eventData != null)
            {
                eventData.useDragThreshold = true;
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            owner?.BeginStageChoseDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            owner?.DragStageChose(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            owner?.EndStageChoseDrag();
        }
    }
}
