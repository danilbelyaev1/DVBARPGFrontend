using DVBARPG.Core;
using DVBARPG.Core.Services;
using DVBARPG.Game.Player;
using DVBARPG.Game.World;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace DVBARPG.Game.Portal
{
    public sealed class RunPortalPlacementController : MonoBehaviour
    {
        [SerializeField] private GameObject returnPortalPrefab;
        [SerializeField] private Button placePortalButton;
        [SerializeField] private Text statusText;
        [SerializeField] private KeyCode hotkey = KeyCode.T;

        private GameObject _portalInstance;

        private void Awake()
        {
            if (placePortalButton != null)
            {
                placePortalButton.onClick.AddListener(PlacePortal);
            }
        }

        private void OnDestroy()
        {
            if (placePortalButton != null)
            {
                placePortalButton.onClick.RemoveListener(PlacePortal);
            }
        }

        private void Update()
        {
            if (IsPortalHotkeyPressed())
            {
                PlacePortal();
            }
        }

        private bool IsPortalHotkeyPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            // MVP: currently using T by default for portal placement.
            if (hotkey == KeyCode.T) return keyboard.tKey.wasPressedThisFrame;
            if (hotkey == KeyCode.Y) return keyboard.yKey.wasPressedThisFrame;
            if (hotkey == KeyCode.U) return keyboard.uKey.wasPressedThisFrame;
            return false;
        }

        public void PlacePortal()
        {
            if (_portalInstance != null)
            {
                Destroy(_portalInstance);
                _portalInstance = null;
            }

            var player = NetworkPlayerReplicator.PlayerTransform;
            if (player == null)
            {
                SetStatus("Player not found.");
                return;
            }

            var pos = ResolveSpawnPosition(player);
            if (returnPortalPrefab != null)
            {
                _portalInstance = Instantiate(returnPortalPrefab, pos, Quaternion.identity);
                _portalInstance.transform.position = pos;
                if (_portalInstance.GetComponent<RunPortalMarker>() == null)
                {
                    _portalInstance.AddComponent<RunPortalMarker>();
                }
            }
            else
            {
                _portalInstance = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                _portalInstance.transform.position = pos;
                _portalInstance.transform.localScale = new Vector3(1.2f, 0.15f, 1.2f);
                _portalInstance.name = "ReturnPortal";
                if (_portalInstance.GetComponent<RunPortalMarker>() == null)
                {
                    _portalInstance.AddComponent<RunPortalMarker>();
                }
            }

            var state = GameRoot.Instance?.Services?.Get<SessionState>();
            if (state != null)
            {
                state.ReturnPortalPlaced = true;
            }

            SetStatus("Return portal placed.");
        }

        private Vector3 ResolveSpawnPosition(Transform player)
        {
            // Force behavior from request: portal at player XZ and +3m above sampled ground.
            var basePos = player.position;
            var sampledY = UnifiedHeightSampler.SampleHeight(basePos);
            var result = new Vector3(basePos.x, sampledY + 1f, basePos.z);
            return result;
        }

        private void SetStatus(string text)
        {
            if (statusText != null) statusText.text = text;
        }
    }
}
