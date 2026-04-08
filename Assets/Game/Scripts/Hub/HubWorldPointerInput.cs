using DVBARPG.Core;
using DVBARPG.Core.Services;
using DVBARPG.UI.Hub;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DVBARPG.Game.Hub
{
    /// <summary>
    /// Клик по коллайдеру NPC в хабе (игнорирует UI). Повесь на объект в сцене хаба вместе с ссылкой на камеру.
    /// </summary>
    public sealed class HubWorldPointerInput : MonoBehaviour
    {
        [SerializeField] private UnityEngine.Camera rayCamera;
        [SerializeField] private NpcInteractionMenu interactionMenu;
        [SerializeField] private float maxDistance = 80f;

        private void Awake()
        {
            if (rayCamera == null)
            {
                rayCamera = UnityEngine.Camera.main;
            }

            if (interactionMenu == null)
            {
                interactionMenu = FindFirstObjectByType<NpcInteractionMenu>();
            }
        }

        private void Update()
        {
            if (!TryReadClick(out var screenPos))
            {
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            var session = GameRoot.Instance?.Services?.Get<SessionState>();
            if (session != null && session.HubNpcDialogOpen)
            {
                return;
            }

            if (rayCamera == null || interactionMenu == null)
            {
                return;
            }

            var ray = rayCamera.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out var hit, maxDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            var actor = hit.collider.GetComponentInParent<HubNpcActor>();
            if (actor == null || actor.Data == null)
            {
                return;
            }

            interactionMenu.Open(actor.Data);
        }

        private static bool TryReadClick(out Vector2 screenPos)
        {
            screenPos = default;
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            {
                return false;
            }

            screenPos = mouse.position.ReadValue();
            return true;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (!Input.GetMouseButtonDown(0))
            {
                return false;
            }

            screenPos = Input.mousePosition;
            return true;
#else
            return false;
#endif
        }
    }
}
