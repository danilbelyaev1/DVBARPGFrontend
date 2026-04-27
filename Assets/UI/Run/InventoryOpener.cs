using DVBARPG.UI.Inventory;
using DVBARPG.UI.Common;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DVBARPG.UI.Run
{
    /// <summary>
    /// Открывает/закрывает сцену инвентаря по кнопке и по клавише I (ПК). Инвентарь — отдельная сцена (Inventory).
    /// </summary>
    public sealed class InventoryOpener : MonoBehaviour
    {
        [Header("UI Toolkit Inventory")]
        [SerializeField] private InventoryScreen inventoryScreen;

        [Header("Кнопка")]
        [Tooltip("Кнопка открытия/закрытия инвентаря (опционально).")]
        [SerializeField] private Button openButton;
        [Tooltip("Если задано — переключаем этот HUD-панельный инвентарь вместо additive-сцены Inventory.")]
        [SerializeField] private GameObject hudInventoryPanel;
        [SerializeField] private UiModalLayer inventoryModalLayer;

        [Header("Клавиша (Input System)")]
        [Tooltip("Клавиша переключения инвентаря на ПК.")]
        [SerializeField] private Key toggleKey = Key.I;

        private void Awake()
        {
            if (openButton != null) openButton.onClick.AddListener(ToggleInventory);
            if (inventoryScreen == null)
            {
                inventoryScreen = FindFirstObjectByType<InventoryScreen>(FindObjectsInactive.Include);
            }
            if (hudInventoryPanel != null)
            {
                if (inventoryModalLayer == null)
                {
                    inventoryModalLayer = hudInventoryPanel.GetComponent<UiModalLayer>();
                }

                hudInventoryPanel.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (openButton != null) openButton.onClick.RemoveAllListeners();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
                ToggleInventory();
        }

        private void ToggleInventory()
        {
            if (inventoryScreen != null)
            {
                inventoryScreen.ToggleVisibility();
                return;
            }

            if (hudInventoryPanel != null)
            {
                if (inventoryModalLayer != null)
                {
                    if (inventoryModalLayer.IsVisible)
                    {
                        inventoryModalLayer.Hide();
                    }
                    else
                    {
                        inventoryModalLayer.Show();
                    }
                }
                else
                {
                    hudInventoryPanel.SetActive(!hudInventoryPanel.activeSelf);
                }
                return;
            }

            InventorySceneHelper.Toggle();
        }
    }
}
