using System;
using Newtonsoft.Json;

namespace DVBARPG.Core.Services
{
    public sealed class MarketListingDto
    {
        [JsonProperty("listing_id")] public string ListingId { get; set; }
        [JsonProperty("seller_character_id")] public string SellerCharacterId { get; set; }
        [JsonProperty("seller_name")] public string SellerName { get; set; }
        [JsonProperty("item")] public MarketItemDto Item { get; set; }
        [JsonProperty("currency_code")] public string CurrencyCode { get; set; }
        [JsonProperty("price")] public int Price { get; set; }
        [JsonProperty("created_at")] public string CreatedAt { get; set; }
    }

    public sealed class MarketItemDto
    {
        [JsonProperty("instance_id")] public string InstanceId { get; set; }
        [JsonProperty("item_definition_id")] public string ItemDefinitionId { get; set; }
        [JsonProperty("item_level")] public int ItemLevel { get; set; }
        [JsonProperty("rarity")] public string Rarity { get; set; }
        [JsonProperty("stack_count")] public int StackCount { get; set; }
        [JsonProperty("definition")] public MarketItemDefinitionDto Definition { get; set; }
    }

    public sealed class MarketItemDefinitionDto
    {
        [JsonProperty("code")] public string Code { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
    }

    public sealed class GetListingsResult
    {
        public bool Ok { get; set; }
        public string Error { get; set; }
        public MarketListingDto[] Listings { get; set; }
        public MarketPaginationDto Pagination { get; set; }
    }

    public sealed class MarketPaginationDto
    {
        [JsonProperty("offset")] public int Offset { get; set; }
        [JsonProperty("limit")] public int Limit { get; set; }
        [JsonProperty("count")] public int Count { get; set; }
    }

    public sealed class ListItemResult
    {
        public bool Ok { get; set; }
        public string Error { get; set; }
        public string ListingId { get; set; }
    }

    public sealed class BuyListingResult
    {
        public bool Ok { get; set; }
        public string Error { get; set; }
        public string ListingId { get; set; }
        public int Price { get; set; }
        public int FeeAmount { get; set; }
        public bool Replayed { get; set; }
    }

    public sealed class CurrencyBalanceResult
    {
        public bool Ok { get; set; }
        public string Error { get; set; }
        public string CurrencyCode { get; set; }
        public int Balance { get; set; }
    }

    public sealed class CurrencyLedgerResult
    {
        public bool Ok { get; set; }
        public string Error { get; set; }
        public CurrencyBalanceEntryDto[] Balances { get; set; }
        public CurrencyEventDto[] Events { get; set; }
        public MarketPaginationDto Pagination { get; set; }
    }

    public sealed class CurrencyBalanceEntryDto
    {
        [JsonProperty("currency_code")] public string CurrencyCode { get; set; }
        [JsonProperty("amount")] public int Amount { get; set; }
    }

    public sealed class CurrencyEventDto
    {
        [JsonProperty("event_type")] public string EventType { get; set; }
        [JsonProperty("payload")] public object Payload { get; set; }
        [JsonProperty("created_at")] public string CreatedAt { get; set; }
    }
}
