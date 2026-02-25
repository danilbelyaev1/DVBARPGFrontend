using UnityEngine;
using UnityEngine.EventSystems;

namespace DVBARPG.Game.Player
{
    public sealed class JoystickInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform handle;
        [SerializeField] private float radius = 80f;

        public Vector2 Direction { get; private set; }

        private RectTransform _root;
        private Canvas _canvas;

        private void Awake()
        {
            _root = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_root == null || handle == null) return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _root, eventData.position, eventData.pressEventCamera, out var local))
            {
                return;
            }

            var clamped = Vector2.ClampMagnitude(local, radius);
            Direction = clamped / radius;
            handle.anchoredPosition = clamped;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Direction = Vector2.zero;
            if (handle != null) handle.anchoredPosition = Vector2.zero;
        }
    }
}
