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
            var session = DVBARPG.Core.GameRoot.Instance.Services.Get<DVBARPG.Core.Services.ISessionService>();
            if (session is NetworkSessionRunner net)
            {
                var profile = DVBARPG.Core.GameRoot.Instance.Services.Get<DVBARPG.Core.Services.IProfileService>();
                StartCoroutine(ConnectWhenReady(net, profile));
            }
        }

        private System.Collections.IEnumerator ConnectWhenReady(NetworkSessionRunner net, DVBARPG.Core.Services.IProfileService profile)
        {
            // Ждём, пока выбраны персонаж и сезон.
            while (profile == null ||
                   profile.CurrentAuth == null ||
                   string.IsNullOrWhiteSpace(profile.SelectedCharacterId) ||
                   string.IsNullOrWhiteSpace(profile.CurrentSeasonId))
            {
                yield return null;
            }

            var auth = profile.CurrentAuth;
            var meta = DVBARPG.Core.GameRoot.Instance.Services.Get<DVBARPG.Core.Services.IRuntimeMetaService>();
            if (meta != null)
            {
                bool done = false;
                DVBARPG.Core.Services.RuntimeAuthSnapshot result = null;
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

        private static void LogEquippedSkills(DVBARPG.Core.Services.RuntimeAuthSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Loadout == null) return;
            var skills = snapshot.Skills ?? System.Array.Empty<DVBARPG.Core.Services.RuntimeSkillSnapshot>();
            LogEquipped(skills, snapshot.Loadout.AttackSkillId, "attack");
            LogEquipped(skills, snapshot.Loadout.SupportASkillId, "supportA");
            LogEquipped(skills, snapshot.Loadout.SupportBSkillId, "supportB");
            LogEquipped(skills, snapshot.Loadout.MovementSkillId, "movement");
        }

        private static void LogEquipped(DVBARPG.Core.Services.RuntimeSkillSnapshot[] skills, string skillId, string slot)
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
