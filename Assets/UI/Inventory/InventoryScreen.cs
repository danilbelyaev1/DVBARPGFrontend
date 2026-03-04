using System;
using System.Collections.Generic;
using DVBARPG.Core;
using DVBARPG.Core.Services;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DVBARPG.UI.Inventory
{
    [Serializable]
    public sealed class EquipmentSlotBinding
    {
        [Tooltip("Идентификатор слота (weapon, offhand, helmet, chest, gloves, boots, amulet, ring1, ring2, belt).")]
        public string slotId = "";
        [Tooltip("Корень UI ячейки в сцене (расставь сам).")]
        public RectTransform slotRoot;
    }

    /// <summary>
    /// Экран инвентаря: сумка (ячейки по bagCapacity), слоты экипировки (привязка к объектам в сцене), клик по предмету — панель описания с Экипировать/Снять.
    /// </summary>
    public sealed class InventoryScreen : MonoBehaviour
    {
        [Header("Контейнеры")]
        [Tooltip("Родитель для ячеек сумки (сетка/список).")]
        [SerializeField] private Transform bagContentRoot;
        [Tooltip("Префаб одной ячейки сумки (пустая или с предметом — один префаб, скрипт заполняет текст/кнопку).")]
        [SerializeField] private GameObject bagCellPrefab;

        [Header("Слоты экипировки")]
        [Tooltip("Привязка слотов к уже расставленным в сцене объектам (шлем, кольцо и т.д.).")]
        [SerializeField] private List<EquipmentSlotBinding> equipmentSlotBindings = new List<EquipmentSlotBinding>();

        [Header("Панель описания предмета")]
        [SerializeField] private ItemDetailPanel itemDetailPanel;

        [Header("Кнопки")]
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button closeButton;
        [Tooltip("Если true, кнопка закрытия только скрывает панель (режим оверлея в Run). Иначе — переход в CharacterSelect.")]
        [SerializeField] private bool closeAsOverlay;
        [Tooltip("При closeAsOverlay — какой объект скрыть (обычно корень панели инвентаря).")]
        [SerializeField] private GameObject panelToHide;

        [Header("Текст")]
        [SerializeField] private Text statusText;

        [Header("Статы персонажа")]
        [Tooltip("Опционально: блок статов (скорость и т.д.). Обновляется после экипировки/снятия.")]
        [SerializeField] private CharacterStatsDisplay characterStatsDisplay;

        private IInventoryService _inventory;
        private IProfileService _profile;
        private readonly List<GameObject> _spawnedBagCells = new List<GameObject>();
        private InventoryResult _lastResult;

        private void Awake()
        {
            if (refreshButton != null) refreshButton.onClick.AddListener(Refresh);
            if (closeButton != null) closeButton.onClick.AddListener(OnClose);
            if (itemDetailPanel != null) itemDetailPanel.SetInventoryScreen(this);
            EnsureSingleEventSystem();
        }

        private static Vector2 GetPointerScreenPosition()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null) return mouse.position.ReadValue();
            return Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }

        private void EnsureSingleEventSystem()
        {
            var eventSystems = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            if (eventSystems.Length <= 1) return;
            var ourScene = gameObject.scene;
            for (int i = 0; i < eventSystems.Length; i++)
            {
                if (eventSystems[i].gameObject.scene == ourScene)
                {
                    eventSystems[i].gameObject.SetActive(false);
                }
            }
        }

        private void OnEnable()
        {
            _inventory = GameRoot.Instance?.Services?.Get<IInventoryService>();
            _profile = GameRoot.Instance?.Services?.Get<IProfileService>();
            Refresh();
        }

        private void Refresh()
        {
            if (_inventory == null || _profile == null)
            {
                SetStatus("Сервисы недоступны.");
                return;
            }

            var characterId = _profile.SelectedCharacterId;
            var seasonId = _profile.CurrentSeasonId;
            if (string.IsNullOrWhiteSpace(characterId) || string.IsNullOrWhiteSpace(seasonId))
            {
                SetStatus("Выберите персонажа и сезон.");
                return;
            }

            SetStatus("Загрузка...");
            _inventory.GetInventory(characterId, seasonId, OnInventoryLoaded);
        }

        private void OnInventoryLoaded(InventoryResult result)
        {
            _lastResult = result;
            ClearSpawnedBag();
            itemDetailPanel?.Hide();

            var fullResultJson = result != null
                ? JsonConvert.SerializeObject(new
                {
                    result.Ok,
                    result.Error,
                    result.BagCapacity,
                    result.StashCapacity,
                    result.BagUsage,
                    result.StashUsage,
                    EquipmentSlots = result.EquipmentSlots ?? Array.Empty<string>(),
                    ItemsCount = result.Items?.Length ?? 0,
                    Items = result.Items ?? Array.Empty<InventoryItemDto>(),
                }, Formatting.Indented)
                : "null";
            SetStatus(fullResultJson);

            var hasData = result != null && string.IsNullOrEmpty(result.Error)
                && (result.EquipmentSlots != null || result.Items != null || result.BagCapacity > 0);
            var success = result != null && (result.Ok || hasData);
            if (!success)
            {
                return;
            }

            var capacity = result.BagCapacity > 0 ? result.BagCapacity : 40;

            var items = result.Items ?? Array.Empty<InventoryItemDto>();
            var equipmentSlots = result.EquipmentSlots ?? Array.Empty<string>();

            if (bagContentRoot != null && bagCellPrefab != null)
            {
                var bagItemsByIndex = BuildBagSlotIndex(items);
                for (int i = 0; i < capacity; i++)
                {
                    var cell = Instantiate(bagCellPrefab, bagContentRoot);
                    _spawnedBagCells.Add(cell);
                    var item = bagItemsByIndex.TryGetValue(i, out var it) ? it : null;
                    BindBagCell(cell, i, item);
                }
            }

            foreach (var binding in equipmentSlotBindings ?? new List<EquipmentSlotBinding>())
            {
                if (binding?.slotRoot == null || string.IsNullOrEmpty(binding.slotId)) continue;
                var slotItem = FindItemInSlot(items, binding.slotId);
                BindEquipmentSlotRoot(binding.slotRoot, binding.slotId, slotItem);
            }
        }

        private static Dictionary<int, InventoryItemDto> BuildBagSlotIndex(InventoryItemDto[] items)
        {
            var dict = new Dictionary<int, InventoryItemDto>();
            if (items == null) return dict;
            var bagItems = new List<InventoryItemDto>();
            foreach (var item in items)
            {
                if (item == null) continue;
                if (!string.Equals(item.InventoryContainer, "bag", StringComparison.OrdinalIgnoreCase))
                    continue;
                bagItems.Add(item);
            }
            foreach (var item in bagItems)
            {
                int idx = -1;
                if (!string.IsNullOrEmpty(item.InventorySlot) && int.TryParse(item.InventorySlot, out idx))
                {
                    if (idx >= 0 && !dict.ContainsKey(idx)) dict[idx] = item;
                    continue;
                }
                for (int i = 0; i < 1000; i++)
                {
                    if (!dict.ContainsKey(i)) { dict[i] = item; break; }
                }
            }
            return dict;
        }

        private void BindBagCell(GameObject cell, int slotIndex, InventoryItemDto item)
        {
            var label = cell.GetComponentInChildren<Text>();
            if (label != null)
                label.text = item != null ? (item.Definition != null ? $"{item.Definition.Name ?? item.Definition.Code} x{item.StackCount}" : item.InstanceId) : "";

            var btn = GetOrAddButton(cell);
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                if (item != null)
                {
                    btn.onClick.AddListener(() =>
                    {
                        Vector2? pos = null;
                        var screenPos = GetPointerScreenPosition();
                        var rt = btn.GetComponent<RectTransform>();
                        if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, null))
                            pos = new Vector2(screenPos.x, screenPos.y);
                        itemDetailPanel?.Show(item, false, null, pos);
                    });
                }
                btn.interactable = item != null;
            }
        }

        private static InventoryItemDto FindItemInSlot(InventoryItemDto[] items, string slot)
        {
            if (items == null) return null;
            foreach (var item in items)
            {
                if (item != null && string.Equals(item.InventorySlot, slot, StringComparison.OrdinalIgnoreCase))
                    return item;
            }
            return null;
        }

        private void BindEquipmentSlotRoot(RectTransform root, string slot, InventoryItemDto item)
        {
            var label = root.GetComponentInChildren<Text>();
            if (label != null)
                label.text = item != null ? (item.Definition?.Name ?? item.Definition?.Code ?? item.InstanceId) : $"[{slot}] —";

            var unequipBtn = GetOrAddButton(root != null ? root.gameObject : null);
            if (unequipBtn != null)
            {
                unequipBtn.onClick.RemoveAllListeners();
                if (item != null)
                {
                    unequipBtn.onClick.AddListener(() =>
                    {
                        Vector2? pos = null;
                        var screenPos = GetPointerScreenPosition();
                        if (root != null && RectTransformUtility.RectangleContainsScreenPoint(root, screenPos, null))
                            pos = new Vector2(screenPos.x, screenPos.y);
                        itemDetailPanel?.Show(item, true, slot, pos);
                    });
                }
                else
                {
                    unequipBtn.onClick.AddListener(() => { });
                }
                unequipBtn.interactable = item != null;
            }
        }

        /// <summary>
        /// Находит Button в объекте или добавляет его на первый Graphic с raycastTarget, чтобы клик по ячейке работал.
        /// </summary>
        private static Button GetOrAddButton(GameObject go)
        {
            if (go == null) return null;
            var btn = go.GetComponentInChildren<Button>(true);
            if (btn != null) return btn;
            var graphic = go.GetComponentInChildren<Graphic>(true);
            if (graphic != null && graphic.raycastTarget)
            {
                btn = graphic.gameObject.GetComponent<Button>();
                if (btn == null) btn = graphic.gameObject.AddComponent<Button>();
                return btn;
            }
            return null;
        }

        public void Equip(string instanceId, string slot)
        {
            if (_inventory == null || _profile == null) return;
            var characterId = _profile.SelectedCharacterId;
            var seasonId = _profile.CurrentSeasonId;
            var requestId = $"equip-{Guid.NewGuid():N}";
            _inventory.Equip(characterId, seasonId, instanceId, slot, requestId, r =>
            {
                if (r != null && r.Ok) { Refresh(); RefreshProfileStats(); } else SetStatus(r?.Error ?? "Ошибка экипировки.");
            });
        }

        public void UnequipSlot(string slot)
        {
            if (_inventory == null || _profile == null) return;
            var characterId = _profile.SelectedCharacterId;
            var seasonId = _profile.CurrentSeasonId;
            var requestId = $"unequip-{Guid.NewGuid():N}";
            _inventory.Unequip(characterId, seasonId, slot, requestId, r =>
            {
                if (r != null && r.Ok) { Refresh(); RefreshProfileStats(); } else SetStatus(r?.Error ?? "Ошибка снятия.");
            });
        }

        private void RefreshProfileStats()
        {
            if (_profile == null) return;
            var meta = GameRoot.Instance?.Services?.Get<IRuntimeMetaService>();
            if (meta == null) { characterStatsDisplay?.Refresh(); return; }
            var auth = _profile.CurrentAuth;
            if (auth == null) { characterStatsDisplay?.Refresh(); return; }
            meta.ValidateAuth(auth, _profile.SelectedCharacterId, _profile.CurrentSeasonId, result =>
            {
                if (result != null && result.Ok && result.MoveSpeed > 0f)
                    _profile.SetBaseMoveSpeed(result.MoveSpeed);
                characterStatsDisplay?.Refresh();
            });
        }

        private void ClearSpawnedBag()
        {
            foreach (var go in _spawnedBagCells)
            {
                if (go != null) Destroy(go);
            }
            _spawnedBagCells.Clear();
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }

        private void OnClose()
        {
            if (gameObject.scene.name == InventorySceneHelper.SceneName)
            {
                InventorySceneHelper.Close();
                return;
            }
            if (closeAsOverlay && panelToHide != null)
            {
                panelToHide.SetActive(false);
                return;
            }
            UnityEngine.SceneManagement.SceneManager.LoadScene("CharacterSelect");
        }
    }
}
