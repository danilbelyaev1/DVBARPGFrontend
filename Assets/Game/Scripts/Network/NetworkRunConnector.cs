using System;
using System.Collections;
using DVBARPG.Core.Services;
using DVBARPG.Net.Network;
using UnityEngine;

namespace DVBARPG.Game.Network
{
    public sealed class NetworkRunConnector : MonoBehaviour
    {
        [Header("Network")]
        [Tooltip("UDP server endpoint.")]
        [SerializeField] private string serverUrl = "udp://127.0.0.1:8081";
        [Tooltip("Server map id.")]
        [SerializeField] private string mapId = "act1_hub";

        private void Start()
        {
            RunResultState.Reset();
            var sessionState = DVBARPG.Core.GameRoot.Instance?.Services?.Get<SessionState>();
            if (sessionState != null)
            {
                sessionState.HubTeleportMenuOpen = false;
                sessionState.HubPortalOpen = false;
                sessionState.PendingTravelMapCode = null;
            }

            var session = DVBARPG.Core.GameRoot.Instance.Services.Get<ISessionService>();
            if (session is NetworkSessionRunner net)
            {
                var profile = DVBARPG.Core.GameRoot.Instance.Services.Get<IProfileService>();
                StartCoroutine(ConnectWhenReady(net, profile));
            }
        }

        private IEnumerator ConnectWhenReady(NetworkSessionRunner net, IProfileService profile)
        {
            yield return CoWaitForProfileContext(profile);

            if (net.IsConnected && net.HasInstance)
            {
                yield break;
            }

            yield return CoFetchProfileAndValidateAuth(profile);

            var sessionState = DVBARPG.Core.GameRoot.Instance.Services.Get<SessionState>();
            var resolvedMapId = !string.IsNullOrWhiteSpace(sessionState?.MapId) ? sessionState.MapId : mapId;
            var auth = BuildAuthForRun(profile);
            net.Connect(auth, resolvedMapId, serverUrl);
        }

        /// <summary>Ожидание выбранного персонажа и сезона (как в оригинальном ConnectWhenReady).</summary>
        public static IEnumerator CoWaitForProfileContext(IProfileService profile)
        {
            while (profile == null ||
                   profile.CurrentAuth == null ||
                   string.IsNullOrWhiteSpace(profile.SelectedCharacterId) ||
                   string.IsNullOrWhiteSpace(profile.CurrentSeasonId))
            {
                yield return null;
            }
        }

        /// <summary>Профиль + validate auth перед UDP.</summary>
        public static IEnumerator CoFetchProfileAndValidateAuth(IProfileService profile)
        {
            var meta = DVBARPG.Core.GameRoot.Instance.Services.Get<IRuntimeMetaService>();
            if (meta != null)
            {
                var profileDone = false;
                RuntimeProfileSnapshot profileSnapshot = null;
                meta.FetchProfile(profile.CurrentAuth, profile.SelectedCharacterId, profile.CurrentSeasonId, snapshot =>
                {
                    profileSnapshot = snapshot;
                    profileDone = true;
                });

                while (!profileDone)
                {
                    yield return null;
                }

                if (profileSnapshot != null && profileSnapshot.Ok && profileSnapshot.Progression != null)
                {
                    profile.SetProgression(profileSnapshot.Progression);
                }
            }

            var auth = BuildAuthForRun(profile);
            meta = DVBARPG.Core.GameRoot.Instance.Services.Get<IRuntimeMetaService>();
            if (meta != null)
            {
                var done = false;
                RuntimeAuthSnapshot result = null;
                meta.ValidateAuth(auth, profile.SelectedCharacterId, profile.CurrentSeasonId, snapshot =>
                {
                    result = snapshot;
                    done = true;
                });

                while (!done)
                {
                    yield return null;
                }

                if (result != null && result.Ok && result.Loadout != null)
                {
                    profile.SetServerLoadout(result.Loadout);
                }

                if (result != null && result.MoveSpeed > 0f)
                {
                    profile.SetBaseMoveSpeed(result.MoveSpeed);
                }

                if (result != null && result.Skills != null && result.Skills.Length > 0)
                {
                    profile.SetServerSkills(result.Skills);
                }
            }
        }

        public static AuthSession BuildAuthForRun(IProfileService profile)
        {
            var current = profile.CurrentAuth;
            if (current == null) return null;
            var characterId = profile.SelectedCharacterId;
            var seasonId = profile.CurrentSeasonId;
            if (string.IsNullOrWhiteSpace(characterId) || string.IsNullOrWhiteSpace(seasonId))
                return current;
            if (!Guid.TryParse(characterId, out var cid) || !Guid.TryParse(seasonId, out var sid))
                return current;
            return new AuthSession
            {
                PlayerId = current.PlayerId,
                Token = current.Token,
                CharacterId = cid,
                SeasonId = sid
            };
        }
    }
}
