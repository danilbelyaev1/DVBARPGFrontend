using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

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

        /// <summary>Открыто контекстное меню NPC (хаб): блокируем перемещение и клики по миру не уходят в движение.</summary>
        public bool HubNpcDialogOpen;

        /// <summary>Блокировать игровой ввод (движение в хабе), пока открыт UI выбора локации или ожидается вход в портал.</summary>
        public bool HubBlocksWorldInput =>
            HubTeleportMenuOpen
            || HubNpcDialogOpen
            || (HubPortalOpen && !string.IsNullOrWhiteSpace(PendingTravelMapCode));
    }

    [Serializable]
    public sealed class CampaignTravelOption
    {
        [JsonProperty("toMapCode")]
        public string ToMapCode;

        [JsonProperty("travelType")]
        public string TravelType;

        [JsonProperty("visited")]
        public bool Visited;

        [JsonProperty("teleportable")]
        public bool Teleportable;

        [JsonProperty("canFirstVisit")]
        public bool CanFirstVisit;

        [JsonProperty("requiredQuestCode")]
        public string RequiredQuestCode;

        [JsonProperty("requiredLevel")]
        public int? RequiredLevel;
    }

    [Serializable]
    public sealed class CampaignQuestObjectiveInfo
    {
        [JsonProperty("type")]
        public string Type;

        [JsonProperty("params")]
        public JObject Params;

        [JsonProperty("count")]
        public int Count;
    }

    [Serializable]
    public sealed class CampaignQuestInfo
    {
        [JsonProperty("code")]
        public string QuestCode;

        [JsonProperty("title")]
        public string Title;

        [JsonProperty("status")]
        public string Status;

        [JsonProperty("objectiveIndex")]
        public int ObjectiveIndex;

        [JsonProperty("counters")]
        public JObject Counters;

        [JsonProperty("currentObjective")]
        public CampaignQuestObjectiveInfo CurrentObjective;

        /// <summary>Для UI журнала: side_* считаем сайдом.</summary>
        public string Category =>
            !string.IsNullOrEmpty(QuestCode) && QuestCode.StartsWith("side_", StringComparison.OrdinalIgnoreCase)
                ? "side"
                : "main";

        public string ShortObjective => CampaignQuestFormatting.ShortObjective(this);

        public string TryGetInteractObjectiveId()
        {
            if (CurrentObjective == null || !string.Equals(CurrentObjective.Type, "interact", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return CurrentObjective.Params?["interactId"]?.Value<string>();
        }
    }

    public static class CampaignQuestFormatting
    {
        public static string ShortObjective(CampaignQuestInfo q)
        {
            if (q?.CurrentObjective == null)
            {
                return "";
            }

            var t = q.CurrentObjective.Type;
            if (string.Equals(t, "interact", StringComparison.OrdinalIgnoreCase))
            {
                var id = q.CurrentObjective.Params?["interactId"]?.Value<string>();
                return string.IsNullOrEmpty(id) ? "Interact" : $"Interact: {id}";
            }

            if (string.Equals(t, "complete_instance", StringComparison.OrdinalIgnoreCase))
            {
                var map = q.CurrentObjective.Params?["mapCode"]?.Value<string>();
                return string.IsNullOrEmpty(map) ? "Clear instance" : $"Clear: {map}";
            }

            if (string.Equals(t, "kill_archetype", StringComparison.OrdinalIgnoreCase))
            {
                var arch = q.CurrentObjective.Params?["archetypeId"]?.Value<string>();
                return string.IsNullOrEmpty(arch) ? "Defeat target" : $"Defeat: {arch}";
            }

            if (string.Equals(t, "kill_tag", StringComparison.OrdinalIgnoreCase))
            {
                var tag = q.CurrentObjective.Params?["tag"]?.Value<string>();
                var c = q.CurrentObjective.Count > 0 ? q.CurrentObjective.Count : q.CurrentObjective.Params?["count"]?.Value<int>() ?? 0;
                return string.IsNullOrEmpty(tag) ? "Hunt" : $"Hunt {tag} x{c}";
            }

            return t ?? "";
        }
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
        [JsonProperty("code")]
        public string Code;

        [JsonProperty("name")]
        public string Name;

        [JsonProperty("mapCode")]
        public string MapCode;

        [JsonProperty("npcType")]
        public string NpcType;

        /// <summary>Опционально: подсказки UI с бэка (например <c>hasShop</c>). Позиции NPC в сцене Unity, не в meta.</summary>
        [JsonProperty("meta")]
        public JObject Meta;

        public bool MetaHasShop =>
            Meta != null && Meta["hasShop"]?.Value<bool>() == true;

        public bool LikelyHasShop =>
            MetaHasShop
            || (!string.IsNullOrEmpty(NpcType) && NpcType.IndexOf("merchant", StringComparison.OrdinalIgnoreCase) >= 0)
            || (!string.IsNullOrEmpty(Code) && Code.IndexOf("merchant", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    [Serializable]
    public sealed class ShopOfferInfo
    {
        [JsonProperty("id")]
        public int Id;

        [JsonProperty("itemCode")]
        public string ItemCode;

        [JsonProperty("itemName")]
        public string ItemName;

        [JsonProperty("price")]
        public int Price;

        [JsonProperty("currencyCode")]
        public string CurrencyCode;

        [JsonProperty("requiredLevel")]
        public int RequiredLevel;

        [JsonProperty("requiredQuestCode")]
        public string RequiredQuestCode;

        [JsonProperty("stockMode")]
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
