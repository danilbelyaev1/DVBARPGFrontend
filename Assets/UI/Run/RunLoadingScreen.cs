using System.Collections;
using DVBARPG.Core;
using DVBARPG.Core.Services;
using DVBARPG.Game.Network;
using DVBARPG.Net.Network;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DVBARPG.UI.Run
{
    public sealed class RunLoadingScreen : MonoBehaviour
    {
        [SerializeField] private Slider loadingSlider;
        [Tooltip("UDP (как на NetworkRunConnector в сцене Run).")]
        [SerializeField] private string serverUrl = "udp://127.0.0.1:8081";
        [Tooltip("Максимум ожидания instance_start от сервера (сек).")]
        [SerializeField] private float instanceReadyTimeoutSec = 45f;

        private void Start()
        {
            var root = GameRoot.Instance;
            if (root == null)
            {
                Debug.LogError("[RunLoadingScreen] GameRoot missing.");
                return;
            }

            root.StartCoroutine(CoTransition(loadingSlider, serverUrl, instanceReadyTimeoutSec));
        }

        /// <summary>Висит на GameRoot, чтобы не оборвалась при выгрузке сцены RunLoading.</summary>
        private static IEnumerator CoTransition(Slider loadingSlider, string serverUrl, float instanceReadyTimeoutSec)
        {
            var services = GameRoot.Instance?.Services;
            var state = services?.Get<SessionState>();
            if (services == null || state == null)
            {
                FailToCharacterSelect(services, "session_state_missing");
                yield break;
            }

            var intent = state.RunLoadingIntent;
            state.RunLoadingIntent = RunLoadingIntent.EnterRun;

            if (intent == RunLoadingIntent.LoadHubOnly)
            {
                yield return CoLoadHubSceneAsync(loadingSlider, services);
                yield break;
            }

            ClearHubTravelUiState(state);
            // Старый код ошибки (например с прошлого UDP) не должен рвать вход в ран после успешного travel из хаба.
            state.LastApiError = null;

            if (string.IsNullOrWhiteSpace(state.MapId))
            {
                FailToCharacterSelect(services, "map_not_selected");
                yield break;
            }

            var profile = services.Get<IProfileService>();
            var net = services.Get<ISessionService>() as NetworkSessionRunner;
            if (profile == null || net == null)
            {
                FailToCharacterSelect(services, "run_services_missing");
                yield break;
            }

            if (loadingSlider != null) loadingSlider.value = 0.05f;

            yield return NetworkRunConnector.CoWaitForProfileContext(profile);
            if (loadingSlider != null) loadingSlider.value = 0.12f;

            yield return NetworkRunConnector.CoFetchProfileAndValidateAuth(profile);
            if (loadingSlider != null) loadingSlider.value = 0.28f;

            var auth = NetworkRunConnector.BuildAuthForRun(profile);
            var mapId = state.MapId.Trim();
            net.Connect(auth, mapId, serverUrl);

            var timeout = Mathf.Max(5f, instanceReadyTimeoutSec);
            var deadline = Time.unscaledTime + timeout;
            while (Time.unscaledTime < deadline)
            {
                if (!string.IsNullOrWhiteSpace(state.LastApiError))
                {
                    net.Disconnect();
                    FailToCharacterSelect(services, state.LastApiError);
                    yield break;
                }

                if (net.HasInstance)
                {
                    break;
                }

                if (loadingSlider != null)
                {
                    var t = 1f - (deadline - Time.unscaledTime) / timeout;
                    loadingSlider.value = Mathf.Clamp01(0.28f + Mathf.Clamp01(t) * 0.28f);
                }

                yield return null;
            }

            if (!net.HasInstance)
            {
                net.Disconnect();
                FailToCharacterSelect(services, "instance_start_timeout");
                yield break;
            }

            if (loadingSlider != null) loadingSlider.value = 0.58f;

            var router = services.Get<FlowRouter>();
            var runSceneName = router.GetSceneName(FlowRoute.Run);
            var op = SceneManager.LoadSceneAsync(runSceneName);
            if (op == null)
            {
                net.Disconnect();
                FailToCharacterSelect(services, "run_scene_load_failed");
                yield break;
            }

            op.allowSceneActivation = false;
            while (op.progress < 0.9f)
            {
                if (loadingSlider != null)
                {
                    loadingSlider.value = 0.58f + (op.progress / 0.9f) * 0.38f;
                }

                yield return null;
            }

            if (loadingSlider != null) loadingSlider.value = 0.98f;
            op.allowSceneActivation = true;
            yield return op;
        }

        private static IEnumerator CoLoadHubSceneAsync(Slider loadingSlider, ServiceRegistry services)
        {
            if (loadingSlider != null) loadingSlider.value = 0.05f;

            var router = services.Get<FlowRouter>();
            var hubSceneName = router.GetSceneName(FlowRoute.Hub);
            var op = SceneManager.LoadSceneAsync(hubSceneName);
            if (op == null)
            {
                Debug.LogError("[RunLoadingScreen] LoadSceneAsync hub failed.");
                yield break;
            }

            op.allowSceneActivation = false;
            while (op.progress < 0.9f)
            {
                if (loadingSlider != null)
                {
                    loadingSlider.value = 0.05f + (op.progress / 0.9f) * 0.93f;
                }

                yield return null;
            }

            if (loadingSlider != null) loadingSlider.value = 0.98f;
            op.allowSceneActivation = true;
            yield return op;
        }

        private static void ClearHubTravelUiState(SessionState state)
        {
            state.HubTeleportMenuOpen = false;
            state.HubPortalOpen = false;
            state.PendingTravelMapCode = null;
        }

        private static void FailToCharacterSelect(ServiceRegistry services, string errorCode)
        {
            Debug.LogWarning($"[RunLoadingScreen] Ошибка входа в ран: {errorCode}");
            var state = services?.Get<SessionState>();
            if (state != null)
            {
                state.LastApiError = errorCode;
            }

            var router = services?.Get<FlowRouter>();
            router?.GoTo(FlowRoute.CharacterSelect);
        }
    }
}
