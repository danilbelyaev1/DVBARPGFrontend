using System;

namespace DVBARPG.Core.Services
{
    [Serializable]
    public sealed class RuntimeCampaignSnapshot
    {
        public bool Ok;
        public string Error;
        public string[] UnlockedMapCodes = Array.Empty<string>();
        public string[] VisitedMapCodes = Array.Empty<string>();
        public CampaignTravelMapOptions[] TravelOptionsByMap = Array.Empty<CampaignTravelMapOptions>();
        public CampaignQuestInfo[] Quests = Array.Empty<CampaignQuestInfo>();
    }

    [Serializable]
    public sealed class CampaignTravelMapOptions
    {
        public string MapCode;
        public CampaignTravelOption[] Options = Array.Empty<CampaignTravelOption>();
    }

    [Serializable]
    public sealed class RuntimeTravelValidateResult
    {
        public bool Ok;
        public string Error;
    }

    [Serializable]
    public sealed class RuntimeNpcListSnapshot
    {
        public bool Ok;
        public string Error;
        public NpcInfo[] Npcs = Array.Empty<NpcInfo>();
    }

    [Serializable]
    public sealed class RuntimeShopSnapshot
    {
        public bool Ok;
        public string Error;
        public NpcInfo Npc;
        public ShopOfferInfo[] Offers = Array.Empty<ShopOfferInfo>();
    }

    [Serializable]
    public sealed class RuntimeShopBuyResult
    {
        public bool Ok;
        public string Error;
        public int NewBalance;
    }

    public interface IRuntimeMvpService
    {
        void FetchCampaign(AuthSession session, string characterId, string seasonId, Action<RuntimeCampaignSnapshot> onDone);
        void ValidateTravel(AuthSession session, string characterId, string seasonId, string fromMapCode, string toMapCode, string travelType, Action<RuntimeTravelValidateResult> onDone);
        void FetchNpcs(AuthSession session, string mapId, Action<RuntimeNpcListSnapshot> onDone);
        void FetchShop(AuthSession session, string characterId, string npcCode, string seasonId, Action<RuntimeShopSnapshot> onDone);
        void BuyShopOffer(AuthSession session, string characterId, string npcCode, string seasonId, int offerId, int quantity, Action<RuntimeShopBuyResult> onDone);

        /// <summary>Квест-события вне UDP-инстанса (хаб UI). Только interact; см. Laravel POST .../campaign/quest-events/batch.</summary>
        void PostCampaignQuestEventsBatch(AuthSession session, string characterId, string seasonId, string mapId, string requestId, object[] events, Action<RuntimeCampaignSnapshot> onDone);
    }
}
