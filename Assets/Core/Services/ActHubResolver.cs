using System;

namespace DVBARPG.Core.Services
{
    /// <summary>
    /// Сопоставляет коды карт вида actN_* с хабом акта: actN_hub и именем сцены Unity (то же имя, что mapId).
    /// </summary>
    public static class ActHubResolver
    {
        public static bool TryParseActFromMapCode(string mapCode, out int act)
        {
            act = 0;
            if (string.IsNullOrWhiteSpace(mapCode)) return false;

            var s = mapCode.Trim();
            if (s.Length < 6) return false;
            if (!s.StartsWith("act", StringComparison.OrdinalIgnoreCase)) return false;

            var i = 3;
            while (i < s.Length && char.IsDigit(s[i])) i++;
            if (i == 3) return false;
            if (i >= s.Length || s[i] != '_') return false;

            if (!int.TryParse(s.Substring(3, i - 3), out act) || act < 1) return false;
            return true;
        }

        public static string GetHubMapCode(int act)
        {
            if (act < 1) act = 1;
            return $"act{act}_hub";
        }

        /// <summary>Имя сцены в Build Settings совпадает с кодом карты-хаба (act1_hub, act2_hub, …).</summary>
        public static string GetHubSceneName(int act) => GetHubMapCode(act);

        public static int ResolveAct(SessionState session, CampaignState campaign = null)
        {
            if (session == null) return 1;

            if (session.ActiveActNumber > 0)
                return session.ActiveActNumber;

            if (TryParseActFromMapCode(session.MapId, out var actFromSession))
                return actFromSession;

            if (campaign != null && TryParseActFromMapCode(campaign.CurrentMapCode, out var actFromCampaign))
                return actFromCampaign;

            return 1;
        }

        public static string ResolveHubMapCode(SessionState session, CampaignState campaign = null)
        {
            return GetHubMapCode(ResolveAct(session, campaign));
        }

        public static string ResolveHubSceneName(SessionState session, CampaignState campaign = null)
        {
            return GetHubSceneName(ResolveAct(session, campaign));
        }

        /// <summary>При переходе на карту обновляет номер акта по префиксу actN_.</summary>
        public static void ApplyDestinationMap(SessionState session, string toMapCode)
        {
            if (session == null || string.IsNullOrWhiteSpace(toMapCode)) return;
            session.MapId = toMapCode;
            if (TryParseActFromMapCode(toMapCode, out var act))
                session.ActiveActNumber = act;
        }
    }
}
