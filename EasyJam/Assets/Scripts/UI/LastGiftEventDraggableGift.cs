using UnityEngine;
using UnityEngine.EventSystems;

namespace TheLastGift.UI
{
    /// <summary>Drag handler for the temporary Christmas-event gift cards.</summary>
    public sealed class LastGiftEventDraggableGift : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private LastGiftUIController owner;
        private Sprite sprite;
        private string giftName;
        private GameObject dragPreview;

        public void Initialize(LastGiftUIController controller, Sprite giftSprite, string label)
        {
            owner = controller;
            sprite = giftSprite;
            giftName = label;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (owner == null || sprite == null)
                return;

            dragPreview = owner.CreateDragPreview(sprite, eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (owner != null && dragPreview != null)
                owner.MoveDragPreview(dragPreview, eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (owner != null && dragPreview != null)
                owner.TrySortMixedGift(giftName, eventData.position);

            if (dragPreview != null)
                Destroy(dragPreview);

            dragPreview = null;
        }
    }
}
