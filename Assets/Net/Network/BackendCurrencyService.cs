using System;
using System.Collections;
using DVBARPG.Core;
using DVBARPG.Core.Services;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace DVBARPG.Net.Network
{
    public sealed class BackendCurrencyService : MonoBehaviour, ICurrencyService
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

        public void GetBalance(string characterId, string seasonId, string currencyCode, Action<CurrencyBalanceResult> onDone)
        {
            StartCoroutine(GetBalanceRoutine(characterId, seasonId, currencyCode ?? "gold", onDone));
        }

        private IEnumerator GetBalanceRoutine(string characterId, string seasonId, string currencyCode, Action<CurrencyBalanceResult> onDone)
        {
            var url = BuildUrl($"/api/runtime/characters/{characterId}/currency/balance?seasonId={Uri.EscapeDataString(seasonId ?? "")}&currencyCode={Uri.EscapeDataString(currencyCode)}");
            if (string.IsNullOrWhiteSpace(url)) { onDone?.Invoke(new CurrencyBalanceResult { Ok = false, Error = "missing_url" }); yield break; }
            using var req = UnityWebRequest.Get(url);
            ApplyHeaders(req, GetAuth());
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) { onDone?.Invoke(new CurrencyBalanceResult { Ok = false, Error = req.error }); yield break; }
            try
            {
                var response = JsonConvert.DeserializeObject<CurrencyBalanceResponse>(req.downloadHandler.text, NetProtocol.JsonSettings);
                onDone?.Invoke(response != null && response.Ok
                    ? new CurrencyBalanceResult { Ok = true, CurrencyCode = response.CurrencyCode, Balance = response.Balance }
                    : new CurrencyBalanceResult { Ok = false, Error = response?.Error ?? "parse_error" });
            }
            catch (Exception e) { onDone?.Invoke(new CurrencyBalanceResult { Ok = false, Error = e.Message }); }
        }

        public void GetLedger(string characterId, string seasonId, int limit, int offset, Action<CurrencyLedgerResult> onDone)
        {
            StartCoroutine(GetLedgerRoutine(characterId, seasonId, limit, offset, onDone));
        }

        private IEnumerator GetLedgerRoutine(string characterId, string seasonId, int limit, int offset, Action<CurrencyLedgerResult> onDone)
        {
            var url = BuildUrl($"/api/runtime/characters/{characterId}/currency/ledger?seasonId={Uri.EscapeDataString(seasonId ?? "")}&limit={limit}&offset={offset}");
            if (string.IsNullOrWhiteSpace(url)) { onDone?.Invoke(new CurrencyLedgerResult { Ok = false, Error = "missing_url" }); yield break; }
            using var req = UnityWebRequest.Get(url);
            ApplyHeaders(req, GetAuth());
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) { onDone?.Invoke(new CurrencyLedgerResult { Ok = false, Error = req.error }); yield break; }
            try
            {
                var response = JsonConvert.DeserializeObject<CurrencyLedgerResponse>(req.downloadHandler.text, NetProtocol.JsonSettings);
                onDone?.Invoke(response != null && response.Ok
                    ? new CurrencyLedgerResult { Ok = true, Balances = response.Balances, Events = response.Events, Pagination = response.Pagination }
                    : new CurrencyLedgerResult { Ok = false, Error = response?.Error ?? "parse_error" });
            }
            catch (Exception e) { onDone?.Invoke(new CurrencyLedgerResult { Ok = false, Error = e.Message }); }
        }

        private sealed class CurrencyBalanceResponse
        {
            public bool Ok { get; set; }
            public string Error { get; set; }
            [JsonProperty("currency_code")] public string CurrencyCode { get; set; }
            public int Balance { get; set; }
        }

        private sealed class CurrencyLedgerResponse
        {
            public bool Ok { get; set; }
            public string Error { get; set; }
            [JsonProperty("balances")] public CurrencyBalanceEntryDto[] Balances { get; set; }
            [JsonProperty("events")] public CurrencyEventDto[] Events { get; set; }
            [JsonProperty("pagination")] public MarketPaginationDto Pagination { get; set; }
        }
    }
}
