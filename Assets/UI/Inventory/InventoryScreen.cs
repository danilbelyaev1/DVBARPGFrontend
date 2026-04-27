using System;
using System.Collections.Generic;
using DVBARPG.Core;
using DVBARPG.Core.Services;
using DVBARPG.UI.Common;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

namespace DVBARPG.UI.Inventory
{
    /// <summary>
    /// Экран инвентаря: сумка (ячейки по bagCapacity), слоты экипировки (привязка к объектам в сцене), клик по предмету — панель описания с Экипировать/Снять.
    /// </summary>
    public sealed class InventoryScreen : MonoBehaviour
    {
        private const string RightWindowId = "hub_inventory";
        private static readonly string[] DefaultEquipmentSlots =
        {
            "weapon", "offhand", "helmet", "chest", "gloves", "boots", "amulet", "ring1", "ring2", "belt"
        };

        [Tooltip("Если true, кнопка закрытия только скрывает панель (режим оверлея в Run). Иначе — переход в CharacterSelect.")]
        [SerializeField] private bool closeAsOverlay;
        [Tooltip("При closeAsOverlay — какой объект скрыть (обычно корень панели инвентаря).")]
        [SerializeField] private GameObject panelToHide;

        [Header("Статы персонажа")]
        [Tooltip("Опционально: блок статов (скорость и т.д.). Обновляется после экипировки/снятия.")]
        [SerializeField] private CharacterStatsDisplay characterStatsDisplay;
        [Header("UI Toolkit")]
        [SerializeField] private UIDocument uiDocument;

        private IInventoryService _inventory;
        private IProfileService _profile;
        private InventoryResult _lastResult;
        private ScrollView _uiBagList;
        private VisualElement _uiEquipmentList;
        private Label _uiStatus;
        private VisualElement _uiRoot;
        private VisualElement _uiPanel;
        private VisualElement _uiItemDetailPanel;
        private Label _uiItemTitle;
        private Label _uiItemDescription;
        private UnityEngine.UIElements.Button _uiItemActionButton;
        private InventoryItemDto _uiSelectedItem;
        private bool _uiSelectedIsEquipped;
        private string _uiSelectedSlot;

        private void Awake()
        {
            if (!TryInitUiToolkit())
            {
                Debug.LogError("[InventoryScreen] UIDocument/UXML is required. Canvas fallback removed.", this);
                enabled = false;
                return;
            }

            HudWindowCoordinator.WindowOpened += OnOtherWindowOpened;
            SetUiVisible(false);
        }

        private bool TryInitUiToolkit()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            if (uiDocument == null)
            {
                return false;
            }

            var root = uiDocument.rootVisualElement;
            if (root == null)
            {
                return false;
            }

            _uiPanel = root.Q<VisualElement>("InventoryPanel");
            _uiRoot = root.Q<VisualElement>("InventoryRoot");
            _uiBagList = root.Q<ScrollView>("BagList");
            _uiEquipmentList = root.Q<VisualElement>("EquipmentList");
            _uiStatus = root.Q<Label>("InventoryStatusLabel");
            _uiItemDetailPanel = root.Q<VisualElement>("ItemDetailPanel");
            _uiItemTitle = root.Q<Label>("ItemTitleLabel");
            _uiItemDescription = root.Q<Label>("ItemDescriptionLabel");
            _uiItemActionButton = root.Q<UnityEngine.UIElements.Button>("ItemActionButton");
            var refresh = root.Q<UnityEngine.UIElements.Button>("InventoryRefreshButton");
            var close = root.Q<UnityEngine.UIElements.Button>("InventoryCloseButton");
            var detailClose = root.Q<UnityEngine.UIElements.Button>("ItemCloseButton");
            refresh?.RegisterCallback<ClickEvent>(_ => Refresh());
            close?.RegisterCallback<ClickEvent>(_ => OnClose());
            detailClose?.RegisterCallback<ClickEvent>(_ => HideUiItemDetail());
            _uiItemActionButton?.RegisterCallback<ClickEvent>(_ => OnUiItemAction());
            if (_uiRoot != null)
            {
                _uiRoot.pickingMode = PickingMode.Ignore;
            }
            if (_uiPanel != null)
            {
                _uiPanel.pickingMode = PickingMode.Position;
            }
            HideUiItemDetail();
            return _uiRoot != null && _uiPanel != null && _uiBagList != null && _uiEquipmentList != null && _uiStatus != null;
        }

        private void OnDestroy()
        {
            HudWindowCoordinator.WindowOpened -= OnOtherWindowOpened;
        }

        private void OnEnable()
        {
            _inventory = GameRoot.Instance?.Services?.Get<IInventoryService>();
            _profile = GameRoot.Instance?.Services?.Get<IProfileService>();
        }

        private void Start()
        {
            SetUiVisible(false);
        }

        private void Update()
        {
            if (Keyboard.current == null) return;
            if (!Keyboard.current.escapeKey.wasPressedThisFrame) return;
            if (!IsVisible()) return;
            HideInventory();
        }

        public void ToggleVisibility()
        {
            if (IsVisible())
            {
                HideInventory();
                return;
            }

            ShowInventory();
        }

        public void ShowInventory()
        {
            HudWindowCoordinator.NotifyWindowOpened(HudWindowGroup.Right, RightWindowId);
            SetUiVisible(true);
            Refresh();
        }

        public void HideInventory()
        {
            SetUiVisible(false);
            HideUiItemDetail();
        }

        private bool IsVisible()
        {
            return _uiRoot != null && _uiRoot.style.display != DisplayStyle.None;
        }

        private void OnOtherWindowOpened(HudWindowGroup group, string sourceId)
        {
            if (group != HudWindowGroup.Right)
            {
                return;
            }

            if (string.Equals(sourceId, RightWindowId, StringComparison.Ordinal))
            {
                return;
            }

            if (IsVisible())
            {
                HideInventory();
            }
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
            HideUiItemDetail();

            if (result == null)
            {
                SetStatus("Нет данных инвентаря.");
                return;
            }

            var hasData = result != null && string.IsNullOrEmpty(result.Error)
                && (result.EquipmentSlots != null || result.Items != null || result.BagCapacity > 0);
            var success = result != null && (result.Ok || hasData);
            if (!success)
            {
                SetStatus(result.Error ?? "Ошибка загрузки инвентаря.");
                return;
            }

            var capacity = result.BagCapacity > 0 ? result.BagCapacity : 40;

            var items = result.Items ?? Array.Empty<InventoryItemDto>();
            var equipmentSlots = result.EquipmentSlots ?? Array.Empty<string>();
            SetStatus($"Bag: {result.BagUsage}/{capacity} | Equipped: {CountEquipped(items)}");

            BuildUiToolkitInventory(capacity, items, equipmentSlots);
        }

        private static int CountEquipped(InventoryItemDto[] items)
        {
            if (items == null) return 0;
            var count = 0;
            foreach (var item in items)
            {
                if (item == null) continue;
                if (!string.IsNullOrWhiteSpace(item.InventorySlot))
                {
                    count++;
                }
            }
            return count;
        }

        private void BuildUiToolkitInventory(int capacity, InventoryItemDto[] items, string[] equipmentSlots)
        {
            if (_uiBagList == null || _uiEquipmentList == null)
            {
                return;
            }

            _uiBagList.Clear();
            _uiEquipmentList.Clear();
            var bagItemsByIndex = BuildBagSlotIndex(items);
            for (var i = 0; i < capacity; i++)
            {
                var item = bagItemsByIndex.TryGetValue(i, out var found) ? found : null;
                var button = new UnityEngine.UIElements.Button(() => ShowUiItemDetail(item, false, null))
                {
                    text = item != null
                        ? $"{(item.Definition?.Name ?? item.Definition?.Code ?? item.InstanceId)} x{item.StackCount}"
                        : $"[{i}] Empty"
                };
                button.SetEnabled(item != null);
                button.AddToClassList("hud-button");
                _uiBagList.Add(button);
            }

            var slotsToRender = equipmentSlots.Length > 0 ? equipmentSlots : DefaultEquipmentSlots;
            foreach (var slot in slotsToRender)
            {
                var slotItem = FindItemInSlot(items, slot);
                var selectedSlot = slot;
                var button = new UnityEngine.UIElements.Button(() => ShowUiItemDetail(slotItem, true, selectedSlot))
                {
                    text = slotItem != null
                        ? $"{SlotLabel(selectedSlot)}: {slotItem.Definition?.Name ?? slotItem.Definition?.Code ?? slotItem.InstanceId}"
                        : $"{SlotLabel(selectedSlot)}: —"
                };
                button.SetEnabled(slotItem != null);
                button.AddToClassList("hud-button");
                _uiEquipmentList.Add(button);
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

        private void ShowUiItemDetail(InventoryItemDto item, bool isEquipped, string slot)
        {
            if (item == null || _uiItemDetailPanel == null)
            {
                return;
            }

            _uiSelectedItem = item;
            _uiSelectedIsEquipped = isEquipped;
            _uiSelectedSlot = slot;
            if (_uiItemTitle != null)
            {
                _uiItemTitle.text = item.Definition?.Name ?? item.Definition?.Code ?? item.InstanceId;
            }

            if (_uiItemDescription != null)
            {
                _uiItemDescription.text = $"{item.Rarity} | ilvl {item.ItemLevel} | stack {item.StackCount}";
            }

            if (_uiItemActionButton != null)
            {
                _uiItemActionButton.text = isEquipped ? "Unequip" : "Equip";
            }

            _uiItemDetailPanel.style.display = DisplayStyle.Flex;
        }

        private void HideUiItemDetail()
        {
            if (_uiItemDetailPanel == null)
            {
                return;
            }

            _uiSelectedItem = null;
            _uiSelectedSlot = null;
            _uiItemDetailPanel.style.display = DisplayStyle.None;
        }

        private void OnUiItemAction()
        {
            if (_uiSelectedItem == null)
            {
                return;
            }

            if (_uiSelectedIsEquipped && !string.IsNullOrWhiteSpace(_uiSelectedSlot))
            {
                UnequipSlot(_uiSelectedSlot);
                HideUiItemDetail();
                return;
            }

            var allowedSlots = _uiSelectedItem.Definition?.AllowedSlots;
            if (allowedSlots != null && allowedSlots.Length > 0)
            {
                Equip(_uiSelectedItem.InstanceId, allowedSlots[0]);
            }
            HideUiItemDetail();
        }

        private static string SlotLabel(string slot)
        {
            return slot switch
            {
                "weapon" => "Weapon",
                "offhand" => "Offhand",
                "helmet" => "Helmet",
                "chest" => "Chest",
                "gloves" => "Gloves",
                "boots" => "Boots",
                "amulet" => "Amulet",
                "ring1" => "Ring 1",
                "ring2" => "Ring 2",
                "belt" => "Belt",
                _ => string.IsNullOrWhiteSpace(slot) ? "Slot" : slot
            };
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

        private void SetStatus(string message)
        {
            _uiStatus.text = message;
        }

        private void OnClose()
        {
            if (gameObject.scene.name == InventorySceneHelper.SceneName)
            {
                InventorySceneHelper.Close();
                return;
            }
            if (closeAsOverlay)
            {
                HideInventory();
                return;
            }
            UnityEngine.SceneManagement.SceneManager.LoadScene("CharacterSelect");
        }

        private void SetUiVisible(bool isVisible)
        {
            if (_uiRoot == null)
            {
                return;
            }

            _uiRoot.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!isVisible && panelToHide != null)
            {
                panelToHide.SetActive(false);
            }
        }
    }
}
