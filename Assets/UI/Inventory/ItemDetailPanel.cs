using System;
using DVBARPG.Core;
using DVBARPG.Core.Services;
using UnityEngine;
using UnityEngine.UI;

namespace DVBARPG.UI.Inventory
{
    /// <summary>
    /// Панель описания предмета: название, описание, кнопки Экипировать/Снять и Закрыть.
    /// На мобилке — по центру экрана, на ПК — над предметом.
    /// </summary>
    public sealed class ItemDetailPanel : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text titleText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Button equipButton;
        [SerializeField] private Button unequipButton;
        [SerializeField] private Button closeButton;
        [Tooltip("Контейнер для кнопок слотов при экипировке (если несколько allowedSlots).")]
        [SerializeField] private Transform equipSlotButtonsRoot;
        [Tooltip("Префаб кнопки «Экипировать в слот X».")]
        [SerializeField] private GameObject equipSlotButtonPrefab;

        private InventoryItemDto _currentItem;
        private string _currentEquippedSlot;
        private InventoryScreen _inventoryScreen;
        private RectTransform _rectTransform;

        private void Awake()
        {
            _rectTransform = panelRoot != null ? panelRoot.GetComponent<RectTransform>() : null;
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (equipButton != null) equipButton.onClick.AddListener(OnEquipFirst);
            if (unequipButton != null) unequipButton.onClick.AddListener(OnUnequip);
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        public void SetInventoryScreen(InventoryScreen screen)
        {
            _inventoryScreen = screen;
        }

        /// <param name="screenPosition">Позиция клика (для ПК — панель над предметом). На мобилке можно передать null — будет по центру.</param>
        public void Show(InventoryItemDto item, bool isEquipped, string equippedSlot, Vector2? screenPosition)
        {
            _currentItem = item;
            _currentEquippedSlot = equippedSlot;
            if (item == null || panelRoot == null) return;

            panelRoot.SetActive(true);

            if (titleText != null)
                titleText.text = item.Definition != null ? (item.Definition.Name ?? item.Definition.Code) : item.InstanceId;

            if (descriptionText != null)
            {
                var parts = new System.Collections.Generic.List<string>();
                if (item.Definition != null)
                {
                    if (item.ItemLevel > 0) parts.Add($"Ур. {item.ItemLevel}");
                    if (!string.IsNullOrEmpty(item.Rarity)) parts.Add(item.Rarity);
                    if (item.StackCount > 1) parts.Add($"x{item.StackCount}");
                }
                descriptionText.text = parts.Count > 0 ? string.Join(", ", parts) : "—";
            }

            if (equipButton != null)
            {
                bool showEquip = !isEquipped && item.Definition?.AllowedSlots != null && item.Definition.AllowedSlots.Length > 0;
                bool oneSlot = showEquip && item.Definition.AllowedSlots.Length == 1;
                equipButton.gameObject.SetActive(oneSlot);
                equipButton.onClick.RemoveAllListeners();
                if (oneSlot)
                {
                    var slot = item.Definition.AllowedSlots[0];
                    equipButton.onClick.AddListener(() => EquipTo(slot));
                }
            }

            if (equipSlotButtonsRoot != null)
            {
                bool showSlotButtons = !isEquipped && item.Definition?.AllowedSlots != null && item.Definition.AllowedSlots.Length > 1;
                equipSlotButtonsRoot.gameObject.SetActive(showSlotButtons);
                if (showSlotButtons && equipSlotButtonPrefab != null)
                {
                    foreach (Transform c in equipSlotButtonsRoot) Destroy(c.gameObject);
                    foreach (var slot in item.Definition.AllowedSlots)
                {
                    var btnGo = Instantiate(equipSlotButtonPrefab, equipSlotButtonsRoot);
                    var btn = btnGo.GetComponent<Button>();
                    var label = btnGo.GetComponentInChildren<Text>();
                    if (label != null) label.text = SlotLabel(slot);
                    if (btn != null)
                    {
                        var s = slot;
                        btn.onClick.AddListener(() => EquipTo(s));
                    }
                }
                }
            }

            if (unequipButton != null)
                unequipButton.gameObject.SetActive(isEquipped && !string.IsNullOrEmpty(equippedSlot));

            PositionPanel(screenPosition);
        }

        private static string SlotLabel(string slot)
        {
            if (string.IsNullOrEmpty(slot)) return "—";
            return slot switch
            {
                "weapon" => "Оружие",
                "offhand" => "Вторая рука",
                "helmet" => "Шлем",
                "chest" => "Нагрудник",
                "gloves" => "Перчатки",
                "boots" => "Ботфорты",
                "amulet" => "Амулет",
                "ring1" => "Кольцо 1",
                "ring2" => "Кольцо 2",
                "belt" => "Пояс",
                _ => slot
            };
        }

        private void PositionPanel(Vector2? screenPosition)
        {
            if (_rectTransform == null) return;
            bool isMobile = Application.isMobilePlatform;
            if (isMobile || !screenPosition.HasValue)
            {
                _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                _rectTransform.anchoredPosition = Vector2.zero;
            }
            else
            {
                var canvas = _rectTransform.GetComponentInParent<Canvas>();
                var cam = canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera ? canvas.worldCamera : null;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rectTransform.parent as RectTransform, screenPosition.Value, cam, out var local);
                _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                _rectTransform.anchoredPosition = local + new Vector2(0f, _rectTransform.rect.height * 0.5f + 10f);
            }
        }

        private void OnEquipFirst()
        {
            if (_currentItem?.Definition?.AllowedSlots != null && _currentItem.Definition.AllowedSlots.Length > 0)
                EquipTo(_currentItem.Definition.AllowedSlots[0]);
        }

        private void OnUnequip()
        {
            if (string.IsNullOrEmpty(_currentEquippedSlot)) return;
            _inventoryScreen?.UnequipSlot(_currentEquippedSlot);
            Hide();
        }

        private void EquipTo(string slot)
        {
            if (_currentItem == null) return;
            _inventoryScreen?.Equip(_currentItem.InstanceId, slot);
            Hide();
        }

        public void Hide()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }
    }
}
