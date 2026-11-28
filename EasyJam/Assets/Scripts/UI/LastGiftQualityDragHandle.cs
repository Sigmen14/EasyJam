using UnityEngine;
using UnityEngine.EventSystems;

namespace TheLastGift.UI
{
    /// <summary>Small UI drag/tap relay used by the gift quality check.</summary>
    [DisallowMultipleComponent]
    public sealed class LastGiftQualityDragHandle : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        private LastGiftUIController owner;
        private RectTransform rectTransform;
        private Vector2 startingPosition;
        private bool tapeInteraction;

        public void Initialize(LastGiftUIController controller, object task, bool isTape)
        {
            owner = controller;
            tapeInteraction = isTape;
            rectTransform = transform as RectTransform;
            if (rectTransform != null)
                startingPosition = rectTransform.anchoredPosition;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (rectTransform == null)
                rectTransform = transform as RectTransform;

            if (rectTransform != null)
                startingPosition = rectTransform.anchoredPosition;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (tapeInteraction || rectTransform == null)
                return;

            Canvas canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null ? Mathf.Max(0.01f, canvas.scaleFactor) : 1f;
            rectTransform.anchoredPosition += eventData.delta / scale;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (owner == null)
                return;

            if (tapeInteraction)
                owner.HandleQualitySealInteraction(this, eventData);
            else
                owner.HandleQualityDragEnd(this, eventData);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (tapeInteraction && owner != null)
                owner.HandleQualitySealInteraction(this, eventData);
        }

        public void ReturnToStartPosition()
        {
            if (rectTransform == null)
                rectTransform = transform as RectTransform;

            if (rectTransform != null)
                rectTransform.anchoredPosition = startingPosition;
        }
    }
}
