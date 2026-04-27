using DVBARPG.Core;
using DVBARPG.Core.Services;
using DVBARPG.Game.Player;
using DVBARPG.Game.Portal;
using DVBARPG.Game.World;
using DVBARPG.UI.Hub;
using UnityEngine;
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
        [SerializeField] private PlayerInputController playerInput;
        [SerializeField] private float npcInteractDistance = 3f;
        [SerializeField] private float defaultMoveStopDistance = 0.3f;
        [SerializeField] private float maxDistance = 300f;
        private HubNpcActor _hoveredActor;

        private void Awake()
        {
            if (rayCamera == null)
            {
                rayCamera = UnityEngine.Camera.main;
            }

            if (interactionMenu == null)
            {
                interactionMenu = FindFirstObjectByType<NpcInteractionMenu>(FindObjectsInactive.Include);
            }

            if (playerInput == null)
            {
                playerInput = FindFirstObjectByType<PlayerInputController>(FindObjectsInactive.Include);
            }
        }

        private void Update()
        {
            if (interactionMenu == null)
            {
                interactionMenu = FindFirstObjectByType<NpcInteractionMenu>(FindObjectsInactive.Include);
            }

            if (playerInput == null)
            {
                playerInput = FindFirstObjectByType<PlayerInputController>(FindObjectsInactive.Include);
            }

            UpdateHover();

            if (!TryReadClick(out var screenPos))
            {
                return;
            }

            var session = GameRoot.Instance?.Services?.Get<SessionState>();
            if (session != null && session.HubNpcDialogOpen)
            {
                return;
            }

            if (rayCamera == null)
            {
                return;
            }

            if (!TryGetHitUnderPointer(screenPos, out var hits))
            {
                return;
            }

            if (TryExtractNpc(hits, out var actor))
            {
                MoveAndInteract(actor.transform.position, npcInteractDistance, () =>
                {
                    if (interactionMenu != null && actor != null && actor.Data != null)
                    {
                        interactionMenu.Open(actor.Data);
                    }
                });
                return;
            }

            if (TryExtractWorldTrigger(hits, out var worldTrigger))
            {
                MoveAndInteract(worldTrigger.transform.position, worldTrigger.ActivationRadius, worldTrigger.TryInteractByAutoMove);
                return;
            }

            if (TryExtractRunPortal(hits, out var runPortal))
            {
                MoveAndInteract(runPortal.transform.position, runPortal.ActivationRadius, runPortal.TryInteractByAutoMove);
                return;
            }

            if (TryExtractMovePoint(hits, out var movePoint))
            {
                playerInput?.SetAutoMoveTarget(movePoint, defaultMoveStopDistance);
                return;
            }

            // Фолбэк: если под курсором нет валидного коллайдера поверхности,
            // берём точку на горизонтальной плоскости уровня игрока.
            var player = NetworkPlayerReplicator.PlayerTransform;
            if (player != null && TryProjectPointerToPlayerPlane(screenPos, player.position, out var planePoint))
            {
                playerInput?.SetAutoMoveTarget(planePoint, defaultMoveStopDistance);
            }
        }

        private void UpdateHover()
        {
            if (rayCamera == null)
            {
                SetHoveredActor(null);
                return;
            }

            if (!TryReadPointerPosition(out var pointerPos))
            {
                SetHoveredActor(null);
                return;
            }

            if (!TryGetNpcUnderPointer(pointerPos, out var actor))
            {
                SetHoveredActor(null);
                return;
            }

            SetHoveredActor(actor);
        }

        private void MoveAndInteract(Vector3 target, float interactDistance, System.Action interaction)
        {
            var player = NetworkPlayerReplicator.PlayerTransform;
            if (player == null || playerInput == null)
            {
                interaction?.Invoke();
                return;
            }

            var distance = Vector3.Distance(player.position, target);
            if (distance <= interactDistance)
            {
                interaction?.Invoke();
                return;
            }

            playerInput.SetAutoMoveTarget(target, interactDistance, interaction);
        }

        private bool TryGetNpcUnderPointer(Vector2 screenPos, out HubNpcActor actor)
        {
            actor = null;
            if (!TryGetHitUnderPointer(screenPos, out var hits))
            {
                return false;
            }

            return TryExtractNpc(hits, out actor);
        }

        private bool TryGetHitUnderPointer(Vector2 screenPos, out RaycastHit[] hits)
        {
            hits = null;
            var ray = rayCamera.ScreenPointToRay(screenPos);
            hits = Physics.RaycastAll(ray, maxDistance, ~0, QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            return true;
        }

        private static bool TryExtractNpc(RaycastHit[] hits, out HubNpcActor actor)
        {
            actor = null;
            for (var i = 0; i < hits.Length; i++)
            {
                var candidate = hits[i].collider != null ? hits[i].collider.GetComponentInParent<HubNpcActor>() : null;
                if (candidate == null || candidate.Data == null)
                {
                    continue;
                }

                actor = candidate;
                return true;
            }

            return false;
        }

        private static bool TryExtractWorldTrigger(RaycastHit[] hits, out WorldTransitionTrigger trigger)
        {
            trigger = null;
            for (var i = 0; i < hits.Length; i++)
            {
                trigger = hits[i].collider != null ? hits[i].collider.GetComponentInParent<WorldTransitionTrigger>() : null;
                if (trigger != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryExtractRunPortal(RaycastHit[] hits, out RunPortalMarker marker)
        {
            marker = null;
            for (var i = 0; i < hits.Length; i++)
            {
                marker = hits[i].collider != null ? hits[i].collider.GetComponentInParent<RunPortalMarker>() : null;
                if (marker != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryExtractMovePoint(RaycastHit[] hits, out Vector3 movePoint)
        {
            movePoint = default;
            var player = NetworkPlayerReplicator.PlayerTransform;
            for (var i = 0; i < hits.Length; i++)
            {
                var collider = hits[i].collider;
                if (collider == null)
                {
                    continue;
                }

                // Игнорируем собственные коллайдеры игрока: иначе клик часто "попадает" в себя.
                if (player != null && collider.transform.IsChildOf(player))
                {
                    continue;
                }

                // Не используем триггеры как цель движения по поверхности.
                if (collider.isTrigger)
                {
                    continue;
                }

                movePoint = hits[i].point;
                return true;
            }

            return false;
        }

        private bool TryProjectPointerToPlayerPlane(Vector2 screenPos, Vector3 playerPosition, out Vector3 point)
        {
            point = default;
            if (rayCamera == null)
            {
                return false;
            }

            var ray = rayCamera.ScreenPointToRay(screenPos);
            var plane = new Plane(Vector3.up, playerPosition);
            if (!plane.Raycast(ray, out var enter))
            {
                return false;
            }

            point = ray.GetPoint(enter);
            return true;
        }

        private void SetHoveredActor(HubNpcActor actor)
        {
            if (_hoveredActor == actor)
            {
                return;
            }

            if (_hoveredActor != null)
            {
                _hoveredActor.SetHovered(false);
            }

            _hoveredActor = actor;
            if (_hoveredActor != null)
            {
                _hoveredActor.SetHovered(true);
            }
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

        private static bool TryReadPointerPosition(out Vector2 screenPos)
        {
            screenPos = default;
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return false;
            }

            screenPos = mouse.position.ReadValue();
            return true;
#elif ENABLE_LEGACY_INPUT_MANAGER
            screenPos = Input.mousePosition;
            return true;
#else
            return false;
#endif
        }
    }
}
