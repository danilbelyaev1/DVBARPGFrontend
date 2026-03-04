using System;
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
        [SerializeField] private string mapId = "default";

        private void Start()
        {
            RunResultState.Reset();
            var session = DVBARPG.Core.GameRoot.Instance.Services.Get<ISessionService>();
            if (session is NetworkSessionRunner net)
            {
                var profile = DVBARPG.Core.GameRoot.Instance.Services.Get<IProfileService>();
                StartCoroutine(ConnectWhenReady(net, profile));
            }
        }

        private System.Collections.IEnumerator ConnectWhenReady(NetworkSessionRunner net, IProfileService profile)
        {
            while (profile == null ||
                   profile.CurrentAuth == null ||
                   string.IsNullOrWhiteSpace(profile.SelectedCharacterId) ||
                   string.IsNullOrWhiteSpace(profile.CurrentSeasonId))
            {
                yield return null;
            }

            // Всегда подставляем выбранного персонажа и сезон в сессию (на случай если CharacterSelect не обновил CurrentAuth).
            var auth = BuildAuthForRun(profile);
            var meta = DVBARPG.Core.GameRoot.Instance.Services.Get<IRuntimeMetaService>();
            if (meta != null)
            {
                bool done = false;
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
                    LogEquippedSkills(result);
                }
                else
                {
                }

                if (result != null && result.MoveSpeed > 0f)
                {
                    profile.SetBaseMoveSpeed(result.MoveSpeed);
                }
            }

            net.Connect(auth, mapId, serverUrl);
        }

        private static AuthSession BuildAuthForRun(IProfileService profile)
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

        private static void LogEquippedSkills(RuntimeAuthSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Loadout == null) return;
            var skills = snapshot.Skills ?? System.Array.Empty<RuntimeSkillSnapshot>();
            LogEquipped(skills, snapshot.Loadout.AttackSkillId, "attack");
            LogEquipped(skills, snapshot.Loadout.SupportASkillId, "supportA");
            LogEquipped(skills, snapshot.Loadout.SupportBSkillId, "supportB");
            LogEquipped(skills, snapshot.Loadout.MovementSkillId, "movement");
        }

        private static void LogEquipped(RuntimeSkillSnapshot[] skills, string skillId, string slot)
        {
            if (string.IsNullOrWhiteSpace(skillId)) return;
            for (int i = 0; i < skills.Length; i++)
            {
                var s = skills[i];
                if (s == null) continue;
                if (!string.Equals(s.SkillId, skillId, System.StringComparison.OrdinalIgnoreCase)) continue;
                Debug.Log($"EquippedSkill: slot={slot} id={s.SkillId} level={s.Level} modifiers={s.ModifiersJson}");
                return;
            }

            Debug.Log($"EquippedSkill: slot={slot} id={skillId} level=<unknown> modifiers=<unknown>");
        }
    }
}
