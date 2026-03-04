using DVBARPG.Game.Network;
using UnityEngine;
using UnityEngine.UI;

namespace DVBARPG.UI.Run
{
    /// <summary>
    /// Кнопка досрочного выхода из забега. Отправляет команду finish и приводит к показу экрана результатов.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class RunExitButton : MonoBehaviour
    {
        private void Awake()
        {
            var button = GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(OnClick);
            }
        }

        private void OnDestroy()
        {
            var button = GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveListener(OnClick);
            }
        }

        private void OnClick()
        {
            var controller = FindFirstObjectByType<RunEndController>();
            if (controller != null)
            {
                controller.RequestFinishRun();
            }
        }
    }
}
