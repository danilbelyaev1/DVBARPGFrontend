using System.Collections;
using DVBARPG.Core;
using DVBARPG.Core.Services;
using DVBARPG.Game.Player;
using DVBARPG.UI.Common;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DVBARPG.Game.World
{
    /// <summary>
    /// Портал вперёд по кампании: наведение — подсветка и тултип у курсора; клик ЛКМ по коллайдеру (игрок рядом) → ValidateTravel и RunLoading.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class WorldTransitionTrigger : MonoBehaviour
    {
        [Header("Travel")]
        [SerializeField] private string targetMapCode = "";
        [SerializeField] private string travelType = "portal";
        [SerializeField] private float interactCooldownSeconds = 1.5f;
        [SerializeField] private ErrorToast errorToast;

        [Header("Взаимодействие")]
        [Tooltip("Макс. дистанция от игрока до центра портала для клика (как у RunPortalMarker).")]
        [SerializeField] private float activationRadius = 6f;
        [SerializeField] private LayerMask clickMask = ~0;
        [Tooltip("Если луч попал в пол/стену, но точка близко к центру портала — считаем наведение (как RunPortalMarker).")]
        [SerializeField] private float clickTolerance = 1.5f;
        [Tooltip("Камера для рейкаста. Пусто = Camera.main.")]
        [SerializeField] private UnityEngine.Camera raycastCamera;

        [Header("Подсказка при наведении")]
        [Tooltip("Заголовок в табличке у курсора.")]
        [SerializeField] private string locationTitle = "";
        [Tooltip("Доп. строка (описание).")]
        [TextArea(1, 4)]
        [SerializeField] private string locationDescription = "";
        [SerializeField] private WorldPortalTooltipUI tooltipUi;

        [Header("Подсветка")]
        [Tooltip("Объекты, которые включаются при наведении луча на портал (контур, меш и т.п.).")]
        [SerializeField] private GameObject[] highlightRoots;

        private Collider _collider;
        private bool _busy;
        private float _nextInteractAt;

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            if (_collider != null) _collider.isTrigger = true;
            _nextInteractAt = Time.unscaledTime + Mathf.Max(0f, interactCooldownSeconds);
            if (tooltipUi == null)
                tooltipUi = FindFirstObjectByType<WorldPortalTooltipUI>();
        }

        private void OnDisable()
        {
            SetHighlight(false);
        }

        private void Update()
        {
            var sessionState = GameRoot.Instance?.Services?.Get<SessionState>();
            if (sessionState != null && sessionState.HubBlocksWorldInput)
            {
                SetHighlight(false);
                return;
            }

            var hover = RaycastHitsThisPortal(out _);
            SetHighlight(hover);

            if (hover)
            {
                var title = string.IsNullOrWhiteSpace(locationTitle) ? targetMapCode : locationTitle.Trim();
                tooltipUi?.RequestShow(title, locationDescription?.Trim() ?? "");
            }

            if (!hover || _busy || Time.unscaledTime < _nextInteractAt) return;

#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
#else
            if (!Input.GetMouseButtonDown(0)) return;
#endif
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            var player = NetworkPlayerReplicator.PlayerTransform;
            if (player == null) return;
            if (Vector3.Distance(player.position, transform.position) > activationRadius)
                return;

            var root = GameRoot.Instance;
            if (root == null) return;

            if (string.IsNullOrWhiteSpace(targetMapCode))
            {
                Debug.LogWarning("[WorldTransitionTrigger] targetMapCode is empty.", this);
                return;
            }

            root.StartCoroutine(CoTravel());
        }

        private bool RaycastHitsThisPortal(out RaycastHit hit)
        {
            hit = default;
            var cam = raycastCamera != null ? raycastCamera : UnityEngine.Camera.main;
            if (cam == null) return false;
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            if (m == null) return false;
            var ray = cam.ScreenPointToRay(m.position.ReadValue());
#else
            var ray = cam.ScreenPointToRay(Input.mousePosition);
#endif
            if (!Physics.Raycast(ray, out hit, 500f, clickMask, QueryTriggerInteraction.Collide))
                return false;
            if (hit.collider != null
                && (hit.collider == _collider || hit.collider.GetComponentInParent<WorldTransitionTrigger>() == this))
                return true;
            return Vector3.Distance(hit.point, transform.position) <= clickTolerance;
        }

        private void SetHighlight(bool on)
        {
            if (highlightRoots == null) return;
            for (var i = 0; i < highlightRoots.Length; i++)
            {
                var go = highlightRoots[i];
                if (go != null) go.SetActive(on);
            }
        }

        private IEnumerator CoTravel()
        {
            _busy = true;
            var sessionState = GameRoot.Instance?.Services?.Get<SessionState>();
            var from = sessionState?.MapId?.Trim();
            if (string.IsNullOrWhiteSpace(from))
            {
                const string err = "map_not_selected";
                if (sessionState != null) sessionState.LastApiError = err;
                errorToast?.ShowErrorCode(err);
                Debug.LogWarning("[WorldTransitionTrigger] SessionState.MapId is empty.");
                _busy = false;
                _nextInteractAt = Time.unscaledTime + interactCooldownSeconds;
                yield break;
            }

            yield return WorldTravelFlow.CoTravelToMap(
                from,
                targetMapCode.Trim(),
                string.IsNullOrWhiteSpace(travelType) ? "portal" : travelType.Trim(),
                onError: code =>
                {
                    var st = GameRoot.Instance?.Services?.Get<SessionState>();
                    if (st != null) st.LastApiError = code;
                    errorToast?.ShowErrorCode(code);
                    Debug.LogWarning($"[WorldTransitionTrigger] Travel failed: {code}");
                },
                onSuccessBeforeRouter: null,
                clearHubTravelUiState: false);

            _busy = false;
            _nextInteractAt = Time.unscaledTime + interactCooldownSeconds;
        }
    }
}
