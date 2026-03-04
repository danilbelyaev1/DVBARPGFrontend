using DVBARPG.Core;
using DVBARPG.Core.Services;
using DVBARPG.Game.Network;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DVBARPG.UI.Run
{
    /// <summary>
    /// Панель результатов забега: поражение/победа, убийства, кнопка «В меню». Показывается при RunResultState.IsRunEnded.
    /// </summary>
    public sealed class RunResultsPanel : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("Корень панели (включается при завершении забега).")]
        [SerializeField] private GameObject panelRoot;
        [Tooltip("Текст результата: «Поражение» / «Забег завершён».")]
        [SerializeField] private Text resultText;
        [Tooltip("Текст убийств (опционально).")]
        [SerializeField] private Text killsText;
        [Tooltip("Кнопка «В меню».")]
        [SerializeField] private Button backToMenuButton;

        [Header("Настройки")]
        [Tooltip("Имя сцены для перехода в меню.")]
        [SerializeField] private string menuSceneName = "CharacterSelect";

        private void Awake()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            if (backToMenuButton != null) backToMenuButton.onClick.AddListener(OnBackToMenu);
        }

        private void OnEnable()
        {
            RunResultState.OnRunEnded += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            RunResultState.OnRunEnded -= Refresh;
        }

        private void Update()
        {
            if (RunResultState.IsRunEnded && panelRoot != null && !panelRoot.activeSelf)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            if (!RunResultState.IsRunEnded)
            {
                if (panelRoot != null) panelRoot.SetActive(false);
                return;
            }

            if (panelRoot != null) panelRoot.SetActive(true);
            if (resultText != null)
            {
                resultText.text = RunResultState.PlayerDied ? "Поражение" : "Забег завершён";
            }

            if (killsText != null)
            {
                killsText.text = RunResultState.Kills > 0 ? $"Убийств: {RunResultState.Kills}" : "";
            }
        }

        private void OnBackToMenu()
        {
            var session = GameRoot.Instance?.Services?.Get<ISessionService>();
            if (session != null && session.IsConnected)
            {
                session.Disconnect();
            }
            RunResultState.Reset();
            SceneManager.LoadScene(string.IsNullOrEmpty(menuSceneName) ? "CharacterSelect" : menuSceneName);
        }
    }
}
