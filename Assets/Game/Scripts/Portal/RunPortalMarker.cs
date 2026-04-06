using DVBARPG.Game.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DVBARPG.Game.Portal
{
    [RequireComponent(typeof(Collider))]
    public sealed class RunPortalMarker : MonoBehaviour
    {
        [SerializeField] private float activationRadius = 3f;
        [SerializeField] private float clickTolerance = 1.35f;
        [SerializeField] private LayerMask clickMask = ~0;
        [SerializeField] private float activationDelaySeconds = 1.5f;
        private bool _used;
        private Collider _portalCollider;
        private float _canUseAt;

        private void Awake()
        {
            _portalCollider = GetComponent<Collider>();
            if (_portalCollider != null) _portalCollider.isTrigger = true;
            _canUseAt = Time.time + Mathf.Max(0f, activationDelaySeconds);
        }

        private void Update()
        {
            if (_used) return;
            if (Time.time < _canUseAt) return;
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            var cam = UnityEngine.Camera.main;
            if (cam == null) return;

            var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(ray, out var hit, 500f, clickMask, QueryTriggerInteraction.Collide)) return;

            var hitPortal = hit.collider != null && (hit.collider == _portalCollider || hit.collider.GetComponentInParent<RunPortalMarker>() == this);
            if (!hitPortal)
            {
                // Allow clicking near portal footprint even if ray hit ground mesh first.
                var hitNearPortal = Vector3.Distance(hit.point, transform.position) <= clickTolerance;
                if (!hitNearPortal) return;
            }

            var player = NetworkPlayerReplicator.PlayerTransform;
            if (player == null) return;
            var distance = Vector3.Distance(player.position, transform.position);
            if (distance > activationRadius) return;

            TryReturnToHub();
        }

        private void TryReturnToHub()
        {
            if (_used) return;
            var returnController = FindFirstObjectByType<DVBARPG.Game.Network.RunReturnToHubController>();
            if (returnController != null)
            {
                _used = true;
                returnController.ReturnToHubViaPortal();
            }
        }
    }
}
