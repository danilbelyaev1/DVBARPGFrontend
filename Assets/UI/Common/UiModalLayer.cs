using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DVBARPG.UI.Common
{
    /// <summary>
    /// Reusable modal controller:
    /// - keeps component object active at all times;
    /// - shows/hides only modal visuals;
    /// - creates fullscreen backdrop in same canvas;
    /// - closes by outside click and optional close button.
    /// </summary>
    public sealed class UiModalLayer : MonoBehaviour
    {
        [Tooltip("Main popup content panel. Required for proper layering and outside-click behavior.")]
        [SerializeField] private RectTransform modalContent;
        [Tooltip("Optional close button (X) inside modal content.")]
        [SerializeField] private Button closeButton;
        [Tooltip("Optional existing backdrop image. If null, it is created automatically.")]
        [SerializeField] private Image backdropImage;
        [SerializeField] private float backdropAlpha = 0.55f;
        [SerializeField] private bool hideOnAwake = true;

        public event Action DismissRequested;
        public bool IsVisible => _isVisible;

        private Canvas _canvas;
        private bool _isVisible;

        private void Awake()
        {
            EnsureRuntime();
            BindCloseButton();
            if (hideOnAwake)
            {
                ApplyVisibility(false);
            }
        }

        private void OnDestroy()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(RequestDismiss);
            }
        }

        public void Show()
        {
            EnsureRuntime();
            if (modalContent == null)
            {
                return;
            }

            if (_isVisible)
            {
                EnsureLayerOrder();
                return;
            }

            ApplyVisibility(true);
        }

        public void Hide()
        {
            EnsureRuntime();
            if (modalContent == null)
            {
                return;
            }
            ApplyVisibility(false);
        }

        public void Configure(RectTransform content, Button optionalCloseButton = null)
        {
            modalContent = content;
            if (optionalCloseButton != null)
            {
                closeButton = optionalCloseButton;
            }

            EnsureRuntime();
            BindCloseButton();
        }

        internal void OnBackdropPointerClick(PointerEventData _)
        {
            RequestDismiss();
        }

        private void RequestDismiss()
        {
            DismissRequested?.Invoke();
            if (DismissRequested == null)
            {
                Hide();
            }
        }

        private void EnsureRuntime()
        {
            if (modalContent == null)
            {
                Debug.LogWarning("[UiModalLayer] modalContent is not assigned. Call Configure(...) or assign in inspector.");
                return;
            }

            _canvas = modalContent.GetComponentInParent<Canvas>();
            if (_canvas == null)
            {
                return;
            }

            EnsureBackdrop();
            EnsureLayerOrder();
        }

        private void EnsureBackdrop()
        {
            if (_canvas == null || modalContent == null)
            {
                return;
            }

            if (backdropImage == null)
            {
                var go = new GameObject("ModalBackdrop", typeof(RectTransform), typeof(Image), typeof(UiModalBackdropClickForwarder));
                go.transform.SetParent(_canvas.transform, false);
                var rt = go.GetComponent<RectTransform>();
                StretchFull(rt);
                backdropImage = go.GetComponent<Image>();
                go.GetComponent<UiModalBackdropClickForwarder>().Init(this);
            }
            else
            {
                if (backdropImage.GetComponent<UiModalBackdropClickForwarder>() == null)
                {
                    backdropImage.gameObject.AddComponent<UiModalBackdropClickForwarder>().Init(this);
                }

                if (backdropImage.transform.parent != _canvas.transform)
                {
                    backdropImage.transform.SetParent(_canvas.transform, false);
                }
                StretchFull(backdropImage.rectTransform);
            }

            // Первый среди детей Canvas — рисуется под остальным UI этого канваса.
            backdropImage.transform.SetAsFirstSibling();

            var color = backdropImage.color;
            color.r = 0f;
            color.g = 0f;
            color.b = 0f;
            color.a = Mathf.Clamp01(backdropAlpha);
            backdropImage.color = color;
            backdropImage.raycastTarget = true;
        }

        private void EnsureLayerOrder()
        {
            if (backdropImage == null || modalContent == null || _canvas == null)
            {
                return;
            }

            // Не переносим modalContent в корень Canvas — ломаются anchors/layout и клики по кнопкам.
            if (backdropImage.transform.parent != _canvas.transform)
            {
                backdropImage.transform.SetParent(_canvas.transform, false);
                StretchFull(backdropImage.rectTransform);
            }

            backdropImage.transform.SetAsFirstSibling();
        }

        private void ApplyVisibility(bool visible)
        {
            _isVisible = visible;

            if (backdropImage != null)
            {
                backdropImage.gameObject.SetActive(visible);
            }

            if (modalContent != null)
            {
                modalContent.gameObject.SetActive(visible);
                var group = modalContent.GetComponent<CanvasGroup>();
                if (group == null)
                {
                    group = modalContent.gameObject.AddComponent<CanvasGroup>();
                }
                group.alpha = visible ? 1f : 0f;
                group.interactable = visible;
                group.blocksRaycasts = visible;
            }

            if (visible)
            {
                EnsureLayerOrder();
                BindCloseButton();
            }
        }

        private void BindCloseButton()
        {
            if (closeButton == null)
            {
                return;
            }

            closeButton.onClick.RemoveListener(RequestDismiss);
            closeButton.onClick.AddListener(RequestDismiss);
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }

    internal sealed class UiModalBackdropClickForwarder : MonoBehaviour, IPointerClickHandler
    {
        private UiModalLayer _layer;

        public void Init(UiModalLayer layer) => _layer = layer;

        public void OnPointerClick(PointerEventData eventData)
        {
            _layer?.OnBackdropPointerClick(eventData);
        }
    }
}
