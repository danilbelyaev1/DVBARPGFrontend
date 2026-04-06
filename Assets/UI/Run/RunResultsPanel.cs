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
        [Tooltip("Если задано — грузится эта сцена; иначе — хаб текущего акта через FlowRouter (actN_hub).")]
        [SerializeField] private string menuSceneName = "";

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
            var netSession = GameRoot.Instance?.Services?.Get<ISessionService>();
            if (netSession != null)
            {
                netSession.Disconnect();
            }
            RunResultState.Reset();
            var services = GameRoot.Instance?.Services;
            if (!string.IsNullOrWhiteSpace(menuSceneName))
            {
                SceneManager.LoadScene(menuSceneName);
                return;
            }

            var state = services?.Get<SessionState>();
            if (state != null)
            {
                if (!ActHubResolver.TryParseActFromMapCode(state.MapId, out var act))
                    act = state.ActiveActNumber > 0 ? state.ActiveActNumber : 1;
                state.MapId = ActHubResolver.GetHubMapCode(act);
                state.ActiveActNumber = act;
                state.LastApiError = null;
            }

            var router = services?.Get<FlowRouter>();
            if (router != null)
            {
                if (state != null)
                {
                    state.RunLoadingIntent = RunLoadingIntent.LoadHubOnly;
                }

                router.GoTo(FlowRoute.RunLoading);
                return;
            }

            var fallbackAct = state != null && state.ActiveActNumber > 0 ? state.ActiveActNumber : 1;
            SceneManager.LoadScene(ActHubResolver.GetHubSceneName(fallbackAct));
        }
    }
}
