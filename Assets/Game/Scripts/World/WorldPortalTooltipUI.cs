using TMPro;
using UnityEngine;

namespace DVBARPG.Game.World
{
    /// <summary>
    /// Одна панель на сцену: показывает название/описание портала у курсора при наведении на <see cref="WorldTransitionTrigger"/>.
    /// </summary>
    public sealed class WorldPortalTooltipUI : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [Tooltip("Смещение от позиции курсора (пиксели).")]
        [SerializeField] private Vector2 screenOffset = new Vector2(18f, -18f);
        [SerializeField] private UnityEngine.Camera worldCamera;

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private int _lastRequestFrame = -1;

        private void Awake()
        {
            if (panel != null)
                panel.gameObject.SetActive(false);
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                _canvasRect = _canvas.GetComponent<RectTransform>();
        }

        /// <summary>Запросить показ на этот кадр; вызывать из Update портала при наведении.</summary>
        public void RequestShow(string title, string body)
        {
            if (panel == null) return;
            _lastRequestFrame = Time.frameCount;
            if (titleText != null)
                titleText.text = title ?? "";
            if (bodyText != null)
            {
                var hasBody = !string.IsNullOrEmpty(body);
                bodyText.gameObject.SetActive(hasBody);
                if (hasBody) bodyText.text = body;
            }
            panel.gameObject.SetActive(true);
            UpdatePosition();
        }

        private void LateUpdate()
        {
            if (panel == null || !panel.gameObject.activeSelf) return;
            if (_lastRequestFrame != Time.frameCount)
            {
                panel.gameObject.SetActive(false);
                return;
            }
            UpdatePosition();
        }

        private void UpdatePosition()
        {
            var cam = worldCamera != null ? worldCamera : UnityEngine.Camera.main;
            var screenPos = MousePosition();
            var target = screenPos + (Vector3)screenOffset;

            if (_canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceCamera && cam != null && _canvasRect != null)
            {
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, target, cam, out var local))
                    panel.anchoredPosition = local;
            }
            else
            {
                panel.position = target;
            }
        }

        private static Vector3 MousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            var m = UnityEngine.InputSystem.Mouse.current;
            if (m != null)
                return m.position.ReadValue();
#endif
            return Input.mousePosition;
        }
    }
}
