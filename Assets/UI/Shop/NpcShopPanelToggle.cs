using UnityEngine;
using UnityEngine.UI;

namespace DVBARPG.UI.Shop
{
    public sealed class NpcShopPanelToggle : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private GameObject targetPanel;
        [SerializeField] private bool openOnClick = true;

        private void Awake()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (button != null)
            {
                button.onClick.AddListener(OnClicked);
            }
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnClicked);
            }
        }

        private void OnClicked()
        {
            if (targetPanel == null)
            {
                return;
            }

            targetPanel.SetActive(openOnClick);
        }
    }
}
