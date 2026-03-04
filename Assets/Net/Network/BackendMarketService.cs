using System;
using System.Collections;
using System.Text;
using DVBARPG.Core;
using DVBARPG.Core.Services;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace DVBARPG.Net.Network
{
    public sealed class BackendMarketService : MonoBehaviour, IMarketService
    {
        [Header("HTTP")]
        [SerializeField] private string backendBaseUrl = "http://127.0.0.1:8000";
        [SerializeField] private string apiKey = "dev-backend-key";
        [SerializeField] private string contractVersion = "1.1";
        [SerializeField] private int timeoutSec = 10;

        private string BuildUrl(string path) => string.IsNullOrWhiteSpace(backendBaseUrl) ? null : $"{backendBaseUrl.TrimEnd('/')}{path}";
        private AuthSession GetAuth() => GameRoot.Instance?.Services?.Get<IProfileService>()?.CurrentAuth;

        private void ApplyHeaders(UnityWebRequest req, AuthSession session)
        {
            req.timeout = timeoutSec;
            req.SetRequestHeader("Authorization", $"Bearer {session?.Token}");
            if (!string.IsNullOrWhiteSpace(apiKey)) req.SetRequestHeader("X-Api-Key", apiKey);
            if (!string.IsNullOrWhiteSpace(contractVersion)) req.SetRequestHeader("X-Contract-Version", contractVersion);
        }

        public void GetListings(string seasonId, int limit, int offset, Action<GetListingsResult> onDone)
        {
            StartCoroutine(GetListingsRoutine(seasonId, limit, offset, onDone));
        }

        private IEnumerator GetListingsRoutine(string seasonId, int limit, int offset, Action<GetListingsResult> onDone)
        {
            var url = BuildUrl($"/api/runtime/market/listings?seasonId={Uri.EscapeDataString(seasonId ?? "")}&limit={limit}&offset={offset}");
            if (string.IsNullOrWhiteSpace(url)) { onDone?.Invoke(new GetListingsResult { Ok = false, Error = "missing_url" }); yield break; }
            using var req = UnityWebRequest.Get(url);
            ApplyHeaders(req, GetAuth());
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) { onDone?.Invoke(new GetListingsResult { Ok = false, Error = req.error }); yield break; }
            try
            {
                var response = JsonConvert.DeserializeObject<GetListingsResponse>(req.downloadHandler.text, NetProtocol.JsonSettings);
                onDone?.Invoke(response != null && response.Ok
                    ? new GetListingsResult { Ok = true, Listings = response.Listings, Pagination = response.Pagination }
                    : new GetListingsResult { Ok = false, Error = response?.Error ?? "parse_error" });
            }
            catch (Exception e) { onDone?.Invoke(new GetListingsResult { Ok = false, Error = e.Message }); }
        }

        public void ListItem(string characterId, string seasonId, string itemInstanceId, int price, string currencyCode, string requestId, Action<ListItemResult> onDone)
        {
            StartCoroutine(PostMarketRoutine($"/api/runtime/characters/{characterId}/market/list", new { seasonId, itemInstanceId, price, currencyCode = currencyCode ?? "gold", requestId }, onDone));
        }

        public void CancelListing(string characterId, string seasonId, string listingId, string requestId, Action<ListItemResult> onDone)
        {
            StartCoroutine(PostMarketRoutine($"/api/runtime/characters/{characterId}/market/cancel", new { seasonId, listingId, requestId }, onDone));
        }

        public void BuyListing(string characterId, string seasonId, string listingId, string requestId, Action<BuyListingResult> onDone)
        {
            StartCoroutine(BuyListingRoutine(characterId, seasonId, listingId, requestId, onDone));
        }

        private IEnumerator PostMarketRoutine(string path, object payload, Action<ListItemResult> onDone)
        {
            var url = BuildUrl(path);
            if (string.IsNullOrWhiteSpace(url)) { onDone?.Invoke(new ListItemResult { Ok = false, Error = "missing_url" }); yield break; }
            var body = JsonConvert.SerializeObject(payload, NetProtocol.JsonSettings);
            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            ApplyHeaders(req, GetAuth());
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) { onDone?.Invoke(new ListItemResult { Ok = false, Error = req.error }); yield break; }
            try
            {
                var response = JsonConvert.DeserializeObject<ListItemResponse>(req.downloadHandler.text, NetProtocol.JsonSettings);
                onDone?.Invoke(response != null && response.Ok
                    ? new ListItemResult { Ok = true, ListingId = response.ListingId }
                    : new ListItemResult { Ok = false, Error = response?.Error ?? "parse_error" });
            }
            catch (Exception e) { onDone?.Invoke(new ListItemResult { Ok = false, Error = e.Message }); }
        }

        private IEnumerator BuyListingRoutine(string characterId, string seasonId, string listingId, string requestId, Action<BuyListingResult> onDone)
        {
            var url = BuildUrl($"/api/runtime/characters/{characterId}/market/buy");
            if (string.IsNullOrWhiteSpace(url)) { onDone?.Invoke(new BuyListingResult { Ok = false, Error = "missing_url" }); yield break; }
            var body = JsonConvert.SerializeObject(new { seasonId, listingId, requestId }, NetProtocol.JsonSettings);
            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            ApplyHeaders(req, GetAuth());
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) { onDone?.Invoke(new BuyListingResult { Ok = false, Error = req.error }); yield break; }
            try
            {
                var response = JsonConvert.DeserializeObject<BuyListingResponse>(req.downloadHandler.text, NetProtocol.JsonSettings);
                onDone?.Invoke(response != null && response.Ok
                    ? new BuyListingResult { Ok = true, ListingId = response.ListingId, Price = response.Price, FeeAmount = response.FeeAmount, Replayed = response.Replayed }
                    : new BuyListingResult { Ok = false, Error = response?.Error ?? "parse_error" });
            }
            catch (Exception e) { onDone?.Invoke(new BuyListingResult { Ok = false, Error = e.Message }); }
        }

        private sealed class GetListingsResponse
        {
            public bool Ok { get; set; }
            public string Error { get; set; }
            public MarketListingDto[] Listings { get; set; }
            public MarketPaginationDto Pagination { get; set; }
        }

        private sealed class ListItemResponse
        {
            public bool Ok { get; set; }
            public string Error { get; set; }
            [JsonProperty("listing_id")] public string ListingId { get; set; }
        }

        private sealed class BuyListingResponse
        {
            public bool Ok { get; set; }
            public string Error { get; set; }
            [JsonProperty("listing_id")] public string ListingId { get; set; }
            public int Price { get; set; }
            [JsonProperty("fee_amount")] public int FeeAmount { get; set; }
            public bool Replayed { get; set; }
        }
    }
}
