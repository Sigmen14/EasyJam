using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheLastGift.UI
{
    /// <summary>
    /// Adds a subtle, smooth scale animation to UI buttons while they are hovered
    /// with a pointer or focused through keyboard/gamepad navigation.
    /// Uses unscaled time so it remains responsive while the game is paused.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class LastGiftButtonHover : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField, Range(1.01f, 1.15f)]
        private float hoverScale = 1.055f;

        [SerializeField, Range(4f, 30f)]
        private float animationSharpness = 18f;

        private Vector3 baseScale;
        private bool pointerInside;
        private bool selected;

        private void Awake()
        {
            baseScale = transform.localScale;
        }

        private void OnEnable()
        {
            pointerInside = false;
            selected = false;
            if (baseScale == Vector3.zero)
                baseScale = transform.localScale;
        }

        private void OnDisable()
        {
            pointerInside = false;
            selected = false;
            transform.localScale = baseScale;
        }

        private void Update()
        {
            float targetMultiplier = pointerInside || selected ? hoverScale : 1f;
            Vector3 targetScale = baseScale * targetMultiplier;
            float blend = 1f - Mathf.Exp(-animationSharpness * Time.unscaledDeltaTime);
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, blend);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
        }

        public void OnSelect(BaseEventData eventData)
        {
            selected = true;
        }

        public void OnDeselect(BaseEventData eventData)
        {
            selected = false;
        }

        public static void Attach(Button button)
        {
            if (button == null)
                return;

            if (button.GetComponent<LastGiftButtonHover>() == null)
                button.gameObject.AddComponent<LastGiftButtonHover>();
        }
    }
}
