using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        [Header("Режим отображения")]
        [Tooltip("Показывать все панели лута одновременно. Если выключено — только по наведению.")]
        [SerializeField] private bool showAllPanels = true;
        [Tooltip("Максимум одновременно видимых панелей лута.")]
        [SerializeField] private int maxVisiblePanels = 10;
        [Tooltip("Вертикальный отступ при разводке пересекающихся панелей.")]
        [SerializeField] private float overlapPadding = 4f;

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private readonly List<RectTransform> _panelPool = new();
        private readonly List<TMP_Text> _labelPool = new();
        private readonly List<Button> _buttonPool = new();
        private readonly List<LootDropMarker> _activeMarkers = new();
        private readonly List<Rect> _placedRects = new();
        private Action<int> _pickupHandler;

        public bool ShowAllPanels => showAllPanels;

        private void Awake()
        {
            if (panel != null)
                panel.gameObject.SetActive(false);
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                _canvasRect = _canvas.GetComponent<RectTransform>();
        }

        public void SetPickupHandler(Action<int> pickupHandler)
        {
            _pickupHandler = pickupHandler;
        }

        public void ToggleDisplayMode()
        {
            showAllPanels = !showAllPanels;
            Hide();
            HideAllPooled();
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

        public void ShowAll(IReadOnlyList<LootDropMarker> markers)
        {
            if (!showAllPanels)
            {
                HideAllPooled();
                return;
            }

            if (markers == null || markers.Count == 0 || panel == null)
            {
                HideAllPooled();
                return;
            }

            var cam = worldCamera != null ? worldCamera : UnityEngine.Camera.main;
            if (cam == null)
            {
                HideAllPooled();
                return;
            }

            Hide();

            var candidates = new List<(LootDropMarker marker, Vector3 screenPos, float distance)>(markers.Count);
            foreach (var marker in markers)
            {
                if (marker == null) continue;
                var worldPos = marker.transform.position;
                var distance = Vector3.Distance(cam.transform.position, worldPos);
                if (distance > maxDistance) continue;
                var screenPos = cam.WorldToScreenPoint(worldPos);
                if (screenPos.z <= 0f) continue;
                candidates.Add((marker, screenPos, distance));
            }

            if (candidates.Count == 0)
            {
                HideAllPooled();
                return;
            }

            candidates.Sort((a, b) => a.distance.CompareTo(b.distance));
            var visibleCount = Mathf.Min(Mathf.Max(0, maxVisiblePanels), candidates.Count);

            EnsurePoolSize(visibleCount);
            _placedRects.Clear();
            _activeMarkers.Clear();

            for (int i = 0; i < visibleCount; i++)
            {
                var entry = candidates[i];
                var rt = _panelPool[i];
                var txt = _labelPool[i];
                var marker = entry.marker;

                _activeMarkers.Add(marker);
                if (txt != null)
                {
                    txt.text = marker.DisplayText;
                    txt.color = LootDropMarker.GetRarityColor(marker.Rarity);
                }

                var desired = entry.screenPos + (Vector3)screenOffset;
                var resolved = ResolveNonOverlappingPosition(desired, rt.rect.size);
                SetPanelPosition(rt, resolved, cam);
                rt.gameObject.SetActive(true);
            }

            for (int i = visibleCount; i < _panelPool.Count; i++)
            {
                _panelPool[i].gameObject.SetActive(false);
            }
        }

        public void Hide()
        {
            if (panel != null)
                panel.gameObject.SetActive(false);
        }

        private void EnsurePoolSize(int size)
        {
            if (panel == null) return;
            while (_panelPool.Count < size)
            {
                var clone = Instantiate(panel.gameObject, panel.parent);
                clone.name = $"{panel.name}_Auto_{_panelPool.Count}";
                var rt = clone.GetComponent<RectTransform>();
                var txt = clone.GetComponentInChildren<TMP_Text>(true);
                var btn = clone.GetComponent<Button>();
                if (btn == null) btn = clone.AddComponent<Button>();
                var slot = _panelPool.Count;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnPanelClicked(slot));
                clone.SetActive(false);
                _panelPool.Add(rt);
                _labelPool.Add(txt);
                _buttonPool.Add(btn);
            }
        }

        private void HideAllPooled()
        {
            for (int i = 0; i < _panelPool.Count; i++)
                _panelPool[i].gameObject.SetActive(false);
            _activeMarkers.Clear();
            _placedRects.Clear();
        }

        private void OnPanelClicked(int slot)
        {
            if (slot < 0 || slot >= _activeMarkers.Count) return;
            var marker = _activeMarkers[slot];
            if (marker == null) return;
            _pickupHandler?.Invoke(marker.Index);
        }

        private Vector3 ResolveNonOverlappingPosition(Vector3 desiredScreenPos, Vector2 size)
        {
            var width = Mathf.Max(1f, size.x);
            var height = Mathf.Max(1f, size.y);
            var pos = desiredScreenPos;
            var test = new Rect(pos.x - width * 0.5f, pos.y - height * 0.5f, width, height);
            var step = height + overlapPadding;

            var guard = 0;
            while (OverlapsAny(test) && guard < 20)
            {
                pos.y -= step;
                test = new Rect(pos.x - width * 0.5f, pos.y - height * 0.5f, width, height);
                guard++;
            }

            _placedRects.Add(test);
            return pos;
        }

        private bool OverlapsAny(Rect rect)
        {
            for (int i = 0; i < _placedRects.Count; i++)
            {
                if (_placedRects[i].Overlaps(rect))
                    return true;
            }
            return false;
        }

        private void SetPanelPosition(RectTransform rt, Vector3 screenPos, UnityEngine.Camera cam)
        {
            if (_canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                if (_canvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPos, cam, out var local))
                    rt.anchoredPosition = local;
            }
            else
            {
                rt.position = screenPos;
            }
        }
    }
}
