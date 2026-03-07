using System;
using System.Collections.Generic;
using System.Linq;
using DVBARPG.Core.Services;
using DVBARPG.Net.Commands;
using DVBARPG.Net.Network;
using DVBARPG.Game.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace DVBARPG.Game.Network
{
    /// <summary>
    /// Репликация дропов лута из снапшотов: отображает золото/предметы на земле. Подбор по клику. Рамка с названием — LootDropTooltipUI.
    /// </summary>
    public sealed class NetworkLootDropsReplicator : MonoBehaviour
    {
        [Header("Лут")]
        [Tooltip("Префаб маркера дропа (иконка/меш). Должен содержать Collider для клика и LootDropMarker (добавится при отсутствии).")]
        [SerializeField] private Transform lootDropPrefab;
        [Tooltip("Высота маркера над землёй (м).")]
        [SerializeField] private float dropHeightOffset = 0.2f;
        [Tooltip("Камера для рейкаста при клике/ховере. Если не задана — Camera.main.")]
        [SerializeField] private UnityEngine.Camera raycastCamera;
        [Header("Тултип (рамка с названием)")]
        [Tooltip("Панель тултипа в стиле PoE. Если не задана — ищется LootDropTooltipUI в сцене.")]
        [SerializeField] private LootDropTooltipUI tooltipUI;

        private ISessionService _session;
        private readonly Dictionary<int, Transform> _drops = new Dictionary<int, Transform>();
        private SnapshotEnvelope _lastSnapshot;

        private void OnEnable()
        {
            var services = DVBARPG.Core.GameRoot.Instance?.Services;
            if (services == null) return;
            _session = services.Get<ISessionService>();
            var net = _session as NetworkSessionRunner;
            if (net != null)
                net.Snapshot += OnSnapshot;
            if (tooltipUI == null)
                tooltipUI = FindFirstObjectByType<LootDropTooltipUI>();
        }

        private void OnDisable()
        {
            var net = _session as NetworkSessionRunner;
            if (net != null)
                net.Snapshot -= OnSnapshot;
            _session = null;
            foreach (var tr in _drops.Values)
            {
                if (tr != null && tr.gameObject != null)
                    Destroy(tr.gameObject);
            }
            _drops.Clear();
            if (tooltipUI != null)
                tooltipUI.Hide();
        }

        private void Update()
        {
            UpdateHoverTooltip();
            HandlePickupClick();
        }

        private void UpdateHoverTooltip()
        {
            var marker = RaycastLootDrop(out _);
            if (tooltipUI != null)
            {
                if (marker != null)
                    tooltipUI.Show(marker);
                else
                    tooltipUI.Hide();
            }
        }

        private void HandlePickupClick()
        {
            if (!IsPickupClick()) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;
            var marker = RaycastLootDrop(out _);
            if (marker == null || _session == null || !_session.IsConnected) return;
            _session.Send(new CmdPickup { DropIndex = marker.Index });
        }

        private static bool IsPickupClick()
        {
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        }

        /// <summary>Рейкаст от мыши в мир; возвращает LootDropMarker под курсором или null.</summary>
        private LootDropMarker RaycastLootDrop(out RaycastHit hit)
        {
            hit = default;
            var cam = raycastCamera != null ? raycastCamera : UnityEngine.Camera.main;
            if (cam == null) return null;
            var screenPos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            var ray = cam.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out hit, 1000f)) return null;
            return hit.collider.GetComponentInParent<LootDropMarker>();
        }

        private void OnSnapshot(SnapshotEnvelope snap)
        {
            if (snap?.LootDrops == null) return;
            _lastSnapshot = snap;

            var pickedSet = snap.PickedIndices != null ? new HashSet<int>(snap.PickedIndices) : new HashSet<int>();
            var unpicked = snap.LootDrops.Where(d => !pickedSet.Contains(d.Index)).ToList();

            var toRemove = _drops.Keys.Where(i => !unpicked.Any(d => d.Index == i)).ToList();
            foreach (var idx in toRemove)
            {
                if (_drops.TryGetValue(idx, out var tr) && tr != null)
                {
                    Destroy(tr.gameObject);
                    _drops.Remove(idx);
                }
            }

            foreach (var d in unpicked)
            {
                if (!_drops.TryGetValue(d.Index, out var tr) || tr == null)
                {
                    if (lootDropPrefab == null) continue;
                    tr = Instantiate(lootDropPrefab, transform);
                    tr.name = $"Loot-{d.Index}({d.Type})";
                    if (tr.GetComponentInChildren<Collider>(true) == null)
                    {
                        var col = tr.gameObject.AddComponent<SphereCollider>();
                        col.radius = 0.5f;
                        col.isTrigger = true;
                    }
                    _drops[d.Index] = tr;
                }

                float y = SampleHeight(d.X, d.Y);
                tr.position = new Vector3(d.X, y + dropHeightOffset, d.Y);

                var marker = tr.GetComponent<LootDropMarker>();
                if (marker == null) marker = tr.gameObject.AddComponent<LootDropMarker>();
                marker.SetData(d.Index, d.Type ?? "", d.GoldAmount, d.ItemDefinitionId, d.ItemLevel, d.Rarity ?? "common", BuildDisplayText(d));
            }
        }

        private static string BuildDisplayText(LootDropSnapshot d)
        {
            if (string.Equals(d.Type, "gold", StringComparison.OrdinalIgnoreCase))
                return $"Золото ({d.GoldAmount})";
            return $"Предмет [{d.Rarity}] Lv.{d.ItemLevel}";
        }

        private float SampleHeight(float x, float z)
        {
            return UnifiedHeightSampler.SampleHeight(new Vector3(x, 0f, z));
        }
    }
}
