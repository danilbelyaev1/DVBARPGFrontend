using System;
using System.Collections;
using DVBARPG.Core;
using UnityEngine;

namespace DVBARPG.Core.Services
{
    /// <summary>
    /// Общий поток: ValidateTravel → ApplyDestinationMap → RunLoading.
    /// </summary>
    public static class WorldTravelFlow
    {
        public static IEnumerator CoTravelToMap(
            string fromMapCode,
            string toMapCode,
            string travelType,
            Action<string> onError,
            Action onSuccessBeforeRouter = null,
            bool clearHubTravelUiState = true)
        {
            var services = GameRoot.Instance?.Services;
            var profile = services?.Get<IProfileService>();
            var session = profile?.CurrentAuth;
            var mvp = services?.Get<IRuntimeMvpService>();
            var sessionState = services?.Get<SessionState>();
            if (profile == null || session == null || mvp == null || sessionState == null)
            {
                Debug.LogWarning("[WorldTravelFlow] Travel aborted: missing services.");
                yield break;
            }

            if (!TryResolveCharacterContext(profile, sessionState, out var characterId, out var seasonId))
            {
                const string error = "character_or_season_missing";
                sessionState.LastApiError = error;
                onError?.Invoke(error);
                yield break;
            }

            var fromNorm = string.IsNullOrWhiteSpace(fromMapCode) ? "" : fromMapCode.Trim();
            var toNorm = string.IsNullOrWhiteSpace(toMapCode) ? "" : toMapCode.Trim();
            var typeNorm = string.IsNullOrWhiteSpace(travelType) ? "portal" : travelType.Trim();

            Debug.Log($"[WorldTravelFlow] ValidateTravel: from={fromNorm}, to={toNorm}, type={typeNorm}");

            var done = false;
            RuntimeTravelValidateResult result = null;
            mvp.ValidateTravel(
                session,
                characterId,
                seasonId,
                fromNorm,
                toNorm,
                typeNorm,
                travelResult =>
                {
                    result = travelResult;
                    done = true;
                });
            while (!done) yield return null;

            if (result == null || !result.Ok)
            {
                var error = result?.Error ?? "travel_error";
                Debug.LogWarning($"[WorldTravelFlow] ValidateTravel failed: error={error}, to={toNorm}");
                sessionState.LastApiError = error;
                onError?.Invoke(error);
                yield break;
            }

            sessionState.LastApiError = null;
            ActHubResolver.ApplyDestinationMap(sessionState, toNorm);
            if (clearHubTravelUiState)
            {
                sessionState.HubTeleportMenuOpen = false;
                sessionState.HubPortalOpen = false;
                sessionState.PendingTravelMapCode = null;
            }

            sessionState.RunLoadingIntent = RunLoadingIntent.EnterRun;

            onSuccessBeforeRouter?.Invoke();

            var router = services.Get<FlowRouter>();
            router?.GoTo(FlowRoute.RunLoading);
        }

        private static bool TryResolveCharacterContext(IProfileService profile, SessionState sessionState, out string characterId, out string seasonId)
        {
            characterId = profile?.SelectedCharacterId;
            seasonId = profile?.CurrentSeasonId;
            if (!string.IsNullOrWhiteSpace(characterId) && !string.IsNullOrWhiteSpace(seasonId))
            {
                return true;
            }

            if (sessionState != null)
            {
                if (string.IsNullOrWhiteSpace(characterId))
                {
                    characterId = sessionState.CharacterId;
                }

                if (string.IsNullOrWhiteSpace(seasonId))
                {
                    seasonId = sessionState.SeasonId;
                }
            }

            var auth = profile?.CurrentAuth;
            if (auth != null)
            {
                if (string.IsNullOrWhiteSpace(characterId) && auth.CharacterId != Guid.Empty)
                {
                    characterId = auth.CharacterId.ToString();
                }

                if (string.IsNullOrWhiteSpace(seasonId) && auth.SeasonId != Guid.Empty)
                {
                    seasonId = auth.SeasonId.ToString();
                }
            }

            return !string.IsNullOrWhiteSpace(characterId) && !string.IsNullOrWhiteSpace(seasonId);
        }
    }
}
