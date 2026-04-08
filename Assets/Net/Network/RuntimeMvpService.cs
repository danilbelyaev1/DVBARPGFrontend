using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DVBARPG.Core.Services;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace DVBARPG.Net.Network
{
    public sealed class RuntimeMvpService : MonoBehaviour, IRuntimeMvpService
    {
        [SerializeField] private string backendBaseUrl = "http://127.0.0.1:8000";
        [SerializeField] private string contractVersion = "1.1";
        [SerializeField] private string apiKey = "dev-backend-key";
        [SerializeField] private int timeoutSec = 10;
        [SerializeField] private bool verboseNetworkLogs = true;

        public void FetchCampaign(AuthSession session, string characterId, string seasonId, Action<RuntimeCampaignSnapshot> onDone)
        {
            StartCoroutine(FetchCampaignRoutine(session, characterId, seasonId, onDone));
        }

        public void ValidateTravel(AuthSession session, string characterId, string seasonId, string fromMapCode, string toMapCode, string travelType, Action<RuntimeTravelValidateResult> onDone)
        {
            StartCoroutine(ValidateTravelRoutine(session, characterId, seasonId, fromMapCode, toMapCode, travelType, onDone));
        }

        public void FetchNpcs(AuthSession session, string mapId, Action<RuntimeNpcListSnapshot> onDone)
        {
            StartCoroutine(FetchNpcsRoutine(session, mapId, onDone));
        }

        public void FetchShop(AuthSession session, string characterId, string npcCode, string seasonId, Action<RuntimeShopSnapshot> onDone)
        {
            StartCoroutine(FetchShopRoutine(session, characterId, npcCode, seasonId, onDone));
        }

        public void BuyShopOffer(AuthSession session, string characterId, string npcCode, string seasonId, int offerId, int quantity, Action<RuntimeShopBuyResult> onDone)
        {
            StartCoroutine(BuyShopOfferRoutine(session, characterId, npcCode, seasonId, offerId, quantity, onDone));
        }

        public void PostCampaignQuestEventsBatch(AuthSession session, string characterId, string seasonId, string mapId, string requestId, object[] events, Action<RuntimeCampaignSnapshot> onDone)
        {
            StartCoroutine(PostCampaignQuestEventsBatchRoutine(session, characterId, seasonId, mapId, requestId, events, onDone));
        }

        private IEnumerator FetchCampaignRoutine(AuthSession session, string characterId, string seasonId, Action<RuntimeCampaignSnapshot> onDone)
        {
            if (string.IsNullOrWhiteSpace(characterId) || string.IsNullOrWhiteSpace(seasonId))
            {
                Debug.LogWarning($"[RuntimeMvpService] FetchCampaign aborted: characterId/seasonId missing. characterId='{characterId}', seasonId='{seasonId}'");
                onDone?.Invoke(new RuntimeCampaignSnapshot { Ok = false, Error = "character_or_season_missing" });
                yield break;
            }

            var url = BuildUrl($"/api/runtime/characters/{characterId}/campaign?seasonId={UnityWebRequest.EscapeURL(seasonId)}");
            LogRequestStart("FetchCampaign", "GET", url, null);
            using var req = UnityWebRequest.Get(url);
            ApplyHeaders(req, session);
            yield return req.SendWebRequest();
            LogRequestResult("FetchCampaign", req, url);

            if (req.result != UnityWebRequest.Result.Success)
            {
                onDone?.Invoke(new RuntimeCampaignSnapshot { Ok = false, Error = TryReadError(req.downloadHandler?.text) ?? req.error });
                yield break;
            }

            try
            {
                var response = JsonConvert.DeserializeObject<CampaignResponse>(req.downloadHandler.text);
                if (response == null)
                {
                    onDone?.Invoke(new RuntimeCampaignSnapshot { Ok = false, Error = "empty_response" });
                    yield break;
                }

                onDone?.Invoke(BuildRuntimeCampaignSnapshot(response));
            }
            catch (Exception)
            {
                onDone?.Invoke(new RuntimeCampaignSnapshot { Ok = false, Error = "parse_error" });
            }
        }

        private IEnumerator PostCampaignQuestEventsBatchRoutine(AuthSession session, string characterId, string seasonId, string mapId, string requestId, object[] events, Action<RuntimeCampaignSnapshot> onDone)
        {
            if (string.IsNullOrWhiteSpace(characterId) || string.IsNullOrWhiteSpace(seasonId) || string.IsNullOrWhiteSpace(mapId) || string.IsNullOrWhiteSpace(requestId))
            {
                onDone?.Invoke(new RuntimeCampaignSnapshot { Ok = false, Error = "character_or_season_missing" });
                yield break;
            }

            var url = BuildUrl($"/api/runtime/characters/{characterId}/campaign/quest-events/batch");
            var payload = JsonConvert.SerializeObject(new
            {
                requestId,
                seasonId,
                mapId,
                events
            });
            LogRequestStart("PostCampaignQuestEventsBatch", "POST", url, payload);

            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            ApplyHeaders(req, session);
            yield return req.SendWebRequest();
            LogRequestResult("PostCampaignQuestEventsBatch", req, url);

            if (req.result != UnityWebRequest.Result.Success)
            {
                onDone?.Invoke(new RuntimeCampaignSnapshot { Ok = false, Error = TryReadError(req.downloadHandler?.text) ?? req.error });
                yield break;
            }

            try
            {
                var outer = JsonConvert.DeserializeObject<CampaignQuestEventsBatchOuterResponse>(req.downloadHandler.text);
                if (outer == null)
                {
                    onDone?.Invoke(new RuntimeCampaignSnapshot { Ok = false, Error = "empty_response" });
                    yield break;
                }

                if (!outer.Ok)
                {
                    onDone?.Invoke(new RuntimeCampaignSnapshot { Ok = false, Error = outer.Error ?? "campaign_quest_batch_failed" });
                    yield break;
                }

                if (outer.Campaign == null)
                {
                    onDone?.Invoke(new RuntimeCampaignSnapshot { Ok = false, Error = "campaign_missing" });
                    yield break;
                }

                outer.Campaign.OkExplicit = true;
                onDone?.Invoke(BuildRuntimeCampaignSnapshot(outer.Campaign));
            }
            catch (Exception)
            {
                onDone?.Invoke(new RuntimeCampaignSnapshot { Ok = false, Error = "parse_error" });
            }
        }

        private static RuntimeCampaignSnapshot BuildRuntimeCampaignSnapshot(CampaignResponse response)
        {
            var mapOptions = new List<CampaignTravelMapOptions>();
            if (response.TravelOptionsByMap != null)
            {
                foreach (var pair in response.TravelOptionsByMap)
                {
                    mapOptions.Add(new CampaignTravelMapOptions
                    {
                        MapCode = pair.Key,
                        Options = pair.Value ?? Array.Empty<CampaignTravelOption>()
                    });
                }
            }

            var ok = response.OkExplicit ?? true;

            return new RuntimeCampaignSnapshot
            {
                Ok = ok,
                Error = response.Error,
                UnlockedMapCodes = response.UnlockedMapCodes ?? Array.Empty<string>(),
                VisitedMapCodes = response.VisitedMapCodes ?? Array.Empty<string>(),
                TravelOptionsByMap = mapOptions.ToArray(),
                Quests = response.Quests ?? Array.Empty<CampaignQuestInfo>()
            };
        }

        private IEnumerator ValidateTravelRoutine(AuthSession session, string characterId, string seasonId, string fromMapCode, string toMapCode, string travelType, Action<RuntimeTravelValidateResult> onDone)
        {
            if (string.IsNullOrWhiteSpace(characterId) || string.IsNullOrWhiteSpace(seasonId))
            {
                Debug.LogWarning($"[RuntimeMvpService] ValidateTravel aborted: characterId/seasonId missing. characterId='{characterId}', seasonId='{seasonId}'");
                onDone?.Invoke(new RuntimeTravelValidateResult { Ok = false, Error = "character_or_season_missing" });
                yield break;
            }

            var url = BuildUrl($"/api/runtime/characters/{characterId}/travel/validate");
            var payload = JsonConvert.SerializeObject(new
            {
                seasonId,
                fromMapCode,
                toMapCode,
                travelType
            });
            LogRequestStart("ValidateTravel", "POST", url, payload);

            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            ApplyHeaders(req, session);
            yield return req.SendWebRequest();
            LogRequestResult("ValidateTravel", req, url);

            if (req.result == UnityWebRequest.Result.Success)
            {
                onDone?.Invoke(new RuntimeTravelValidateResult { Ok = true });
                yield break;
            }

            var errorCode = TryReadError(req.downloadHandler?.text) ?? req.error;
            onDone?.Invoke(new RuntimeTravelValidateResult { Ok = false, Error = errorCode });
        }

        private IEnumerator FetchNpcsRoutine(AuthSession session, string mapId, Action<RuntimeNpcListSnapshot> onDone)
        {
            var url = BuildUrl($"/api/runtime/content/npcs?mapId={UnityWebRequest.EscapeURL(mapId)}");
            LogRequestStart("FetchNpcs", "GET", url, null);
            using var req = UnityWebRequest.Get(url);
            ApplyHeaders(req, session);
            yield return req.SendWebRequest();
            LogRequestResult("FetchNpcs", req, url);

            if (req.result != UnityWebRequest.Result.Success)
            {
                onDone?.Invoke(new RuntimeNpcListSnapshot { Ok = false, Error = TryReadError(req.downloadHandler?.text) ?? req.error });
                yield break;
            }

            try
            {
                var response = JsonConvert.DeserializeObject<NpcListResponse>(req.downloadHandler.text);
                onDone?.Invoke(new RuntimeNpcListSnapshot
                {
                    Ok = response != null && response.Ok,
                    Error = response?.Error,
                    Npcs = response?.Npcs ?? Array.Empty<NpcInfo>()
                });
            }
            catch (Exception)
            {
                onDone?.Invoke(new RuntimeNpcListSnapshot { Ok = false, Error = "parse_error" });
            }
        }

        private IEnumerator FetchShopRoutine(AuthSession session, string characterId, string npcCode, string seasonId, Action<RuntimeShopSnapshot> onDone)
        {
            var url = BuildUrl($"/api/runtime/characters/{characterId}/shops/{npcCode}?seasonId={UnityWebRequest.EscapeURL(seasonId)}");
            LogRequestStart("FetchShop", "GET", url, null);
            using var req = UnityWebRequest.Get(url);
            ApplyHeaders(req, session);
            yield return req.SendWebRequest();
            LogRequestResult("FetchShop", req, url);

            if (req.result != UnityWebRequest.Result.Success)
            {
                onDone?.Invoke(new RuntimeShopSnapshot { Ok = false, Error = TryReadError(req.downloadHandler?.text) ?? req.error });
                yield break;
            }

            try
            {
                var response = JsonConvert.DeserializeObject<ShopResponse>(req.downloadHandler.text);
                onDone?.Invoke(new RuntimeShopSnapshot
                {
                    Ok = response != null && response.Ok,
                    Error = response?.Error,
                    Npc = response?.Npc,
                    Offers = response?.Offers ?? Array.Empty<ShopOfferInfo>()
                });
            }
            catch (Exception)
            {
                onDone?.Invoke(new RuntimeShopSnapshot { Ok = false, Error = "parse_error" });
            }
        }

        private IEnumerator BuyShopOfferRoutine(AuthSession session, string characterId, string npcCode, string seasonId, int offerId, int quantity, Action<RuntimeShopBuyResult> onDone)
        {
            var url = BuildUrl($"/api/runtime/characters/{characterId}/shops/{npcCode}/buy");
            var payload = JsonConvert.SerializeObject(new
            {
                seasonId,
                offerId,
                quantity,
                requestId = Guid.NewGuid().ToString("N")
            });
            LogRequestStart("BuyShopOffer", "POST", url, payload);

            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            ApplyHeaders(req, session);
            yield return req.SendWebRequest();
            LogRequestResult("BuyShopOffer", req, url);

            if (req.result != UnityWebRequest.Result.Success)
            {
                onDone?.Invoke(new RuntimeShopBuyResult { Ok = false, Error = TryReadError(req.downloadHandler?.text) ?? req.error });
                yield break;
            }

            try
            {
                var response = JsonConvert.DeserializeObject<BuyResponse>(req.downloadHandler.text);
                onDone?.Invoke(new RuntimeShopBuyResult
                {
                    Ok = response != null && response.Ok,
                    Error = response?.Error,
                    NewBalance = response?.NewBalance ?? 0
                });
            }
            catch (Exception)
            {
                onDone?.Invoke(new RuntimeShopBuyResult { Ok = false, Error = "parse_error" });
            }
        }

        private string BuildUrl(string path) => $"{backendBaseUrl.TrimEnd('/')}{path}";

        private void ApplyHeaders(UnityWebRequest req, AuthSession session)
        {
            req.timeout = timeoutSec;
            if (!string.IsNullOrWhiteSpace(session?.Token))
            {
                req.SetRequestHeader("Authorization", $"Bearer {session.Token}");
            }
            if (!string.IsNullOrWhiteSpace(contractVersion))
            {
                req.SetRequestHeader("X-Contract-Version", contractVersion);
            }
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                req.SetRequestHeader("X-Api-Key", apiKey);
            }
        }

        private static string TryReadError(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            try
            {
                var response = JsonConvert.DeserializeObject<ErrorResponse>(body);
                return response?.Error;
            }
            catch
            {
                return null;
            }
        }

        private void LogRequestStart(string operation, string method, string url, string payload)
        {
            if (!verboseNetworkLogs) return;
            var body = Truncate(payload);
            Debug.Log($"[RuntimeMvpService] {operation} -> {method} {url}; payload={body ?? "<none>"}");
        }

        private void LogRequestResult(string operation, UnityWebRequest req, string url)
        {
            if (!verboseNetworkLogs) return;

            var body = Truncate(req.downloadHandler?.text);
            var status = (long)req.responseCode;
            var isOk = req.result == UnityWebRequest.Result.Success;
            var line = $"[RuntimeMvpService] {operation} <- status={status} result={req.result} error={req.error ?? "<none>"} url={url} body={body ?? "<empty>"}";
            if (isOk)
            {
                Debug.Log(line);
                return;
            }

            Debug.LogWarning(line);

            if (status == 404 && url != null && url.Contains("/api/runtime/", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"[RuntimeMvpService] 404 on runtime API route. Check backendBaseUrl='{backendBaseUrl}' (should point to Laravel host, usually :8000, with /api/runtime routes).");
            }
        }

        private static string Truncate(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            const int maxLen = 600;
            return value.Length <= maxLen ? value : value.Substring(0, maxLen) + "...<truncated>";
        }

        [Serializable]
        private sealed class CampaignResponse
        {
            [JsonProperty("ok")] public bool? OkExplicit { get; set; }

            [JsonProperty("error")] public string Error { get; set; }
            [JsonProperty("unlockedMapCodes")] public string[] UnlockedMapCodes { get; set; }
            [JsonProperty("visitedMapCodes")] public string[] VisitedMapCodes { get; set; }
            [JsonProperty("travelOptionsByMap")] public Dictionary<string, CampaignTravelOption[]> TravelOptionsByMap { get; set; }
            [JsonProperty("quests")] public CampaignQuestInfo[] Quests { get; set; }
        }

        [Serializable]
        private sealed class CampaignQuestEventsBatchOuterResponse
        {
            [JsonProperty("ok")] public bool Ok { get; set; }
            [JsonProperty("error")] public string Error { get; set; }
            [JsonProperty("replayed")] public bool Replayed { get; set; }
            [JsonProperty("completedQuestCodes")] public string[] CompletedQuestCodes { get; set; }
            [JsonProperty("campaign")] public CampaignResponse Campaign { get; set; }
        }

        [Serializable]
        private sealed class NpcListResponse
        {
            [JsonProperty("ok")] public bool Ok { get; set; }
            [JsonProperty("error")] public string Error { get; set; }
            [JsonProperty("npcs")] public NpcInfo[] Npcs { get; set; }
        }

        [Serializable]
        private sealed class ShopResponse
        {
            [JsonProperty("ok")] public bool Ok { get; set; }
            [JsonProperty("error")] public string Error { get; set; }
            [JsonProperty("npc")] public NpcInfo Npc { get; set; }
            [JsonProperty("offers")] public ShopOfferInfo[] Offers { get; set; }
        }

        [Serializable]
        private sealed class BuyResponse
        {
            [JsonProperty("ok")] public bool Ok { get; set; }
            [JsonProperty("error")] public string Error { get; set; }
            [JsonProperty("newBalance")] public int NewBalance { get; set; }
        }

        [Serializable]
        private sealed class ErrorResponse
        {
            [JsonProperty("error")] public string Error { get; set; }
        }
    }
}
