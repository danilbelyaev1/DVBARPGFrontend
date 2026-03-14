using UnityEngine;
using TMPro;

namespace DVBARPG.Game.Network
{
    /// <summary>
    /// Рамка с названием предмета в стиле PoE: показывается при наведении на дроп, цвет по рарности.
    /// </summary>
    public sealed class LootDropTooltipUI : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("Корневая панель (рамка). Скрывается, когда курсор не над дропом.")]
        [SerializeField] private RectTransform panel;
        [Tooltip("Текст названия (цвет задаётся по рарности).")]
        [SerializeField] private TMP_Text label;
        [Tooltip("Камера для перевода мировых координат в экранные. Если не задана — Camera.main.")]
        [SerializeField] private UnityEngine.Camera worldCamera;
        [Header("Оформление")]
        [Tooltip("Смещение тултипа над точкой дропа (в пикселях от центра экрана).")]
        [SerializeField] private Vector2 screenOffset = new Vector2(0f, 24f);
        [Tooltip("Максимальная дистанция от камеры: дальше — тултип не показываем.")]
        [SerializeField] private float maxDistance = 50f;

        private Canvas _canvas;
        private RectTransform _canvasRect;

        private void Awake()
        {
            if (panel != null)
                panel.gameObject.SetActive(false);
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                _canvasRect = _canvas.GetComponent<RectTransform>();
        }

        public void Show(LootDropMarker marker)
        {
            if (marker == null || panel == null) return;
            var cam = worldCamera != null ? worldCamera : UnityEngine.Camera.main;
            if (cam == null) return;
            var worldPos = marker.transform.position;
            if (Vector3.Distance(cam.transform.position, worldPos) > maxDistance)
            {
                Hide();
                return;
            }
            var screenPos = cam.WorldToScreenPoint(worldPos);
            if (label != null)
            {
                label.text = marker.DisplayText;
                label.color = LootDropMarker.GetRarityColor(marker.Rarity);
            }
            if (_canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                if (_canvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPos + (Vector3)screenOffset, cam, out var local))
                    panel.anchoredPosition = local;
            }
            else
            {
                panel.position = screenPos + (Vector3)screenOffset;
            }
            panel.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (panel != null)
                panel.gameObject.SetActive(false);
        }
    }
}
