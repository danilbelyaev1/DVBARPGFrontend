using DVBARPG.Core;
using DVBARPG.Core.Services;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DVBARPG.UI.Hub
{
    public sealed class HubPortalController : MonoBehaviour
    {
        [SerializeField] private Button openPortalButton;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private string defaultStatus = "Portal closed";

        private void Awake()
        {
            if (openPortalButton != null)
            {
                openPortalButton.onClick.AddListener(OpenPortal);
            }
        }

        private void OnEnable()
        {
            RefreshStatus();
        }

        private void OnDestroy()
        {
            if (openPortalButton != null)
            {
                openPortalButton.onClick.RemoveListener(OpenPortal);
            }
        }

        private void OpenPortal()
        {
            var services = GameRoot.Instance?.Services;
            var state = services?.Get<SessionState>();
            if (state == null) return;

            if (state.HubPortalOpen && !string.IsNullOrWhiteSpace(state.PendingTravelMapCode))
            {
                ActHubResolver.ApplyDestinationMap(state, state.PendingTravelMapCode);
                state.HubPortalOpen = false;
                state.HubTeleportMenuOpen = false;
                state.PendingTravelMapCode = null;
                state.LastApiError = null;
                Debug.Log($"[HubPortalController] Portal entered. Teleporting to run map={state.MapId}");
                var router = services.Get<FlowRouter>();
                router?.GoTo(FlowRoute.RunLoading);
                return;
            }

            state.HubTeleportMenuOpen = true;
            state.HubPortalOpen = false;
            state.PendingTravelMapCode = null;
            Debug.Log("[HubPortalController] Teleport button clicked. Showing available destinations.");
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            var state = GameRoot.Instance?.Services?.Get<SessionState>();
            var selecting = state != null && state.HubTeleportMenuOpen;
            var isOpen = state != null && state.HubPortalOpen;
            if (statusText != null)
            {
                if (isOpen)
                {
                    statusText.text = "Portal open";
                }
                else if (selecting)
                {
                    statusText.text = "Select destination";
                }
                else
                {
                    statusText.text = defaultStatus;
                }
            }
        }
    }
}
