using DVBARPG.Core;
using DVBARPG.Core.Services;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DVBARPG.Game.Network
{
    public sealed class RunReturnToHubController : MonoBehaviour
    {
        [SerializeField] private bool autoReturn = true;
        [SerializeField] private float autoReturnDelaySeconds = 1.5f;

        private float _returnAt = -1f;

        private void OnEnable()
        {
            RunResultState.OnRunEnded += HandleRunEnded;
        }

        private void OnDisable()
        {
            RunResultState.OnRunEnded -= HandleRunEnded;
        }

        private void Update()
        {
            if (!autoReturn || _returnAt < 0f || Time.unscaledTime < _returnAt) return;
            _returnAt = -1f;
            ReturnToHub();
        }

        private void HandleRunEnded()
        {
            if (!autoReturn) return;
            _returnAt = Time.unscaledTime + autoReturnDelaySeconds;
        }

        public void ReturnToHub()
        {
            ReturnToHubInternal(false);
        }

        public void ReturnToHubViaPortal()
        {
            ReturnToHubInternal(true);
        }

        private void ReturnToHubInternal(bool viaPortal)
        {
            var netSession = GameRoot.Instance?.Services?.Get<ISessionService>();
            if (netSession != null)
            {
                // Всегда полный Disconnect: иначе после портала остаётся «полусессия» и повторный Connect ломается.
                netSession.Disconnect();
            }

            var state = GameRoot.Instance?.Services?.Get<SessionState>();
            var act = 1;
            if (state != null)
            {
                var leavingMap = state.MapId;
                if (!ActHubResolver.TryParseActFromMapCode(leavingMap, out act))
                    act = state.ActiveActNumber > 0 ? state.ActiveActNumber : 1;

                state.MapId = ActHubResolver.GetHubMapCode(act);
                state.ActiveActNumber = act;
                state.LastApiError = null;
                if (viaPortal)
                {
                    // Teleport on the world map is only available when player came through a portal.
                    state.HubPortalOpen = true;
                }
                state.ReturnPortalPlaced = false;
            }

            RunResultState.Reset();
            var router = GameRoot.Instance?.Services?.Get<FlowRouter>();
            if (router != null)
            {
                if (state != null)
                {
                    state.RunLoadingIntent = RunLoadingIntent.LoadHubOnly;
                }

                router.GoTo(FlowRoute.RunLoading);
                return;
            }
            SceneManager.LoadScene(ActHubResolver.GetHubSceneName(act));
        }
    }
}
