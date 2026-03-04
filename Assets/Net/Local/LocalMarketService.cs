using DVBARPG.Core.Services;

namespace DVBARPG.Net.Local
{
    /// <summary>Заглушка маркета. Для работы с Laravel используется BackendMarketService.</summary>
    public sealed class LocalMarketService : IMarketService
    {
        public void GetListings(string seasonId, int limit, int offset, System.Action<GetListingsResult> onDone)
            => onDone?.Invoke(new GetListingsResult { Ok = false, Error = "use_backend" });

        public void ListItem(string characterId, string seasonId, string itemInstanceId, int price, string currencyCode, string requestId, System.Action<ListItemResult> onDone)
            => onDone?.Invoke(new ListItemResult { Ok = false, Error = "use_backend" });

        public void CancelListing(string characterId, string seasonId, string listingId, string requestId, System.Action<ListItemResult> onDone)
            => onDone?.Invoke(new ListItemResult { Ok = false, Error = "use_backend" });

        public void BuyListing(string characterId, string seasonId, string listingId, string requestId, System.Action<BuyListingResult> onDone)
            => onDone?.Invoke(new BuyListingResult { Ok = false, Error = "use_backend" });
    }
}
