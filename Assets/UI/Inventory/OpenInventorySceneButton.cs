using UnityEngine;
using UnityEngine.UI;

namespace DVBARPG.UI.Inventory
{
    /// <summary>
    /// Кнопка открытия сцены инвентаря. Вешать на кнопку в CharacterSelect или в любом другом месте.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class OpenInventorySceneButton : MonoBehaviour
    {
        private void Awake()
        {
            var btn = GetComponent<Button>();
            if (btn != null) btn.onClick.AddListener(InventorySceneHelper.Open);
        }

        private void OnDestroy()
        {
            var btn = GetComponent<Button>();
            if (btn != null) btn.onClick.RemoveAllListeners();
        }
    }
}
