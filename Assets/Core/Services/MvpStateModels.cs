using System;
using System.Collections.Generic;

namespace DVBARPG.Core.Services
{
    /// <summary>Что делает сцена RunLoading перед следующей загрузкой.</summary>
    public enum RunLoadingIntent
    {
        /// <summary>Полный вход в забег (мета + UDP + сцена Run).</summary>
        EnterRun = 0,
        /// <summary>Только асинхронная смена сцены на хаб (после рана и т.п.).</summary>
        LoadHubOnly = 1
    }

    [Serializable]
    public sealed class SessionState
    {
        /// <summary>Перед <see cref="FlowRoute.RunLoading"/> выставить нужный режим; RunLoading сбрасывает на <see cref="RunLoadingIntent.EnterRun"/> после обработки.</summary>
        public RunLoadingIntent RunLoadingIntent = RunLoadingIntent.EnterRun;

        public string Token;
        public string SeasonId;
        public string CharacterId;
        public string MapId;
        /// <summary>1-based номер акта; 0 = вывести из MapId (префикс actN_).</summary>
        public int ActiveActNumber;
        public string LastApiError;
        public bool HubPortalOpen;
        public bool HubTeleportMenuOpen;
        public string PendingTravelMapCode;
        public bool ReturnPortalPlaced;

        /// <summary>Блокировать игровой ввод (движение в хабе), пока открыт UI выбора локации или ожидается вход в портал.</summary>
        public bool HubBlocksWorldInput =>
            HubTeleportMenuOpen
            || (HubPortalOpen && !string.IsNullOrWhiteSpace(PendingTravelMapCode));
    }

    [Serializable]
    public sealed class CampaignTravelOption
    {
        public string ToMapCode;
        public string TravelType;
        public bool Visited;
        public bool Teleportable;
        public bool CanFirstVisit;
        public string RequiredQuestCode;
        public int? RequiredLevel;
    }

    [Serializable]
    public sealed class CampaignQuestInfo
    {
        public string QuestCode;
        public string Category;
        public string Title;
        public string Status;
        public string ShortObjective;
    }

    [Serializable]
    public sealed class CampaignState
    {
        public string CurrentMapCode = "act1_hub";
        public string[] UnlockedMapCodes = Array.Empty<string>();
        public string[] VisitedMapCodes = Array.Empty<string>();
        public Dictionary<string, CampaignTravelOption[]> TravelOptionsByMap = new Dictionary<string, CampaignTravelOption[]>();
        public CampaignQuestInfo[] Quests = Array.Empty<CampaignQuestInfo>();
    }

    [Serializable]
    public sealed class NpcInfo
    {
        public string Code;
        public string Name;
        public string MapCode;
        public string NpcType;
    }

    [Serializable]
    public sealed class ShopOfferInfo
    {
        public int Id;
        public string ItemCode;
        public string ItemName;
        public int Price;
        public string CurrencyCode;
        public int RequiredLevel;
        public string RequiredQuestCode;
        public string StockMode;
    }

    [Serializable]
    public sealed class ShopState
    {
        public NpcInfo[] Npcs = Array.Empty<NpcInfo>();
        public ShopOfferInfo[] Offers = Array.Empty<ShopOfferInfo>();
        public bool PendingBuy;
        public string ActiveNpcCode;
    }
}
