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
    /// <summary>
    /// HTTP-клиент к Laravel API инвентаря: GET inventory, equip, unequip, move, split, merge.
    /// </summary>
    public sealed class BackendInventoryService : MonoBehaviour, IInventoryService
    {
        [Header("HTTP")]
        [Tooltip("Базовый URL Laravel backend.")]
        [SerializeField] private string backendBaseUrl = "http://127.0.0.1:8000";
        [Tooltip("API key для runtime.auth.")]
        [SerializeField] private string apiKey = "dev-backend-key";
        [Tooltip("Контракт API.")]
        [SerializeField] private string contractVersion = "1.1";
        [Tooltip("Таймаут запросов (сек).")]
        [SerializeField] private int timeoutSec = 30;

        private string BuildUrl(string path)
        {
            if (string.IsNullOrWhiteSpace(backendBaseUrl)) return null;
            return $"{backendBaseUrl.TrimEnd('/')}{path}";
        }

        private void ApplyHeaders(UnityWebRequest req, AuthSession session)
        {
            req.timeout = timeoutSec;
            req.SetRequestHeader("Authorization", $"Bearer {session?.Token}");
            if (!string.IsNullOrWhiteSpace(apiKey)) req.SetRequestHeader("X-Api-Key", apiKey);
            if (!string.IsNullOrWhiteSpace(contractVersion)) req.SetRequestHeader("X-Contract-Version", contractVersion);
        }

        private AuthSession GetAuth()
        {
            var profile = GameRoot.Instance?.Services?.Get<IProfileService>();
            return profile?.CurrentAuth;
        }

        private static IEnumerator WithOverlay(IEnumerator inner)
        {
            var overlay = GameRoot.Instance?.Services?.Get<ILoadingOverlayService>();
            overlay?.BeginRequest();
            try
            {
                while (inner.MoveNext())
                    yield return inner.Current;
            }
            finally
            {
                overlay?.EndRequest();
            }
        }

        public void GetInventory(string characterId, string seasonId, Action<InventoryResult> onDone)
        {
            StartCoroutine(WithOverlay(GetInventoryRoutine(characterId, seasonId, onDone)));
        }

        private IEnumerator GetInventoryRoutine(string characterId, string seasonId, Action<InventoryResult> onDone)
        {
            var url = BuildUrl($"/api/runtime/characters/{characterId}/inventory?seasonId={Uri.EscapeDataString(seasonId ?? "")}");
            if (string.IsNullOrWhiteSpace(url)) { onDone?.Invoke(new InventoryResult { Ok = false, Error = "missing_url" }); yield break; }

            var auth = GetAuth();
            using var req = UnityWebRequest.Get(url);
            ApplyHeaders(req, auth);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                var err = req.error;
                var body = req.downloadHandler?.text;
                if (!string.IsNullOrWhiteSpace(body))
                {
                    try
                    {
                        var errObj = JsonConvert.DeserializeObject<ErrorBody>(body, NetProtocol.JsonSettings);
                        var serverErr = !string.IsNullOrWhiteSpace(errObj?.error) ? errObj.error : errObj?.message;
                        if (!string.IsNullOrWhiteSpace(serverErr))
                            err = $"{err} | {serverErr}";
                    }
                    catch { /* use req.error */ }
                }
                if (req.result == UnityWebRequest.Result.ConnectionError && (err?.Contains("timeout") == true || err?.Contains("Timeout") == true))
                    err = $"{err} (Laravel запущен на {backendBaseUrl}?)";
                onDone?.Invoke(new InventoryResult { Ok = false, Error = err });
                yield break;
            }

            try
            {
                var body = req.downloadHandler?.text ?? "";
                var response = JsonConvert.DeserializeObject<InventoryResponse>(body, NetProtocol.JsonSettings);
                onDone?.Invoke(response != null
                    ? new InventoryResult
                    {
                        Ok = response.Ok,
                        Error = response.Error,
                        Items = response.Items,
                        EquipmentSlots = response.EquipmentSlots,
                        BagCapacity = response.BagCapacity,
                        StashCapacity = response.StashCapacity,
                        BagUsage = response.BagUsage,
                        StashUsage = response.StashUsage
                    }
                    : new InventoryResult { Ok = false, Error = "parse_error" });
            }
            catch (Exception e)
            {
                onDone?.Invoke(new InventoryResult { Ok = false, Error = e.Message });
            }
        }

        public void Equip(string characterId, string seasonId, string instanceId, string slot, string requestId, Action<InventoryActionResult> onDone)
        {
            StartCoroutine(WithOverlay(PostRoutine($"/api/runtime/characters/{characterId}/inventory/equip", new { seasonId, instanceId, slot, requestId }, onDone)));
        }

        public void Unequip(string characterId, string seasonId, string slot, string requestId, Action<InventoryActionResult> onDone)
        {
            StartCoroutine(WithOverlay(PostRoutine($"/api/runtime/characters/{characterId}/inventory/unequip", new { seasonId, slot, requestId }, onDone)));
        }

        public void Move(string characterId, string seasonId, string instanceId, string targetContainer, int? targetSlot, string requestId, Action<InventoryActionResult> onDone)
        {
            var body = new { seasonId, instanceId, targetContainer, targetSlot = targetSlot.HasValue ? targetSlot.Value : (int?)null, requestId };
            StartCoroutine(WithOverlay(PostRoutine($"/api/runtime/characters/{characterId}/inventory/move", body, onDone)));
        }

        public void SplitStack(string characterId, string seasonId, string instanceId, int splitAmount, string requestId, Action<SplitStackResult> onDone)
        {
            StartCoroutine(WithOverlay(SplitStackRoutine(characterId, seasonId, instanceId, splitAmount, requestId, onDone)));
        }

        private IEnumerator SplitStackRoutine(string characterId, string seasonId, string instanceId, int splitAmount, string requestId, Action<SplitStackResult> onDone)
        {
            var url = BuildUrl($"/api/runtime/characters/{characterId}/inventory/split");
            if (string.IsNullOrWhiteSpace(url)) { onDone?.Invoke(new SplitStackResult { Ok = false, Error = "missing_url" }); yield break; }

            var body = JsonConvert.SerializeObject(new { seasonId, instanceId, splitAmount, requestId }, NetProtocol.JsonSettings);
            var auth = GetAuth();
            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            ApplyHeaders(req, auth);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onDone?.Invoke(new SplitStackResult { Ok = false, Error = req.error });
                yield break;
            }

            try
            {
                var response = JsonConvert.DeserializeObject<SplitStackResponse>(req.downloadHandler.text, NetProtocol.JsonSettings);
                onDone?.Invoke(response != null && response.Ok
                    ? new SplitStackResult { Ok = true, OriginalItem = response.OriginalItem, NewItem = response.NewItem }
                    : new SplitStackResult { Ok = false, Error = response?.Error ?? "parse_error" });
            }
            catch (Exception e)
            {
                onDone?.Invoke(new SplitStackResult { Ok = false, Error = e.Message });
            }
        }

        public void MergeStacks(string characterId, string seasonId, string sourceInstanceId, string targetInstanceId, string requestId, Action<MergeStacksResult> onDone)
        {
            StartCoroutine(WithOverlay(MergeStacksRoutine(characterId, seasonId, sourceInstanceId, targetInstanceId, requestId, onDone)));
        }

        private IEnumerator MergeStacksRoutine(string characterId, string seasonId, string sourceInstanceId, string targetInstanceId, string requestId, Action<MergeStacksResult> onDone)
        {
            var url = BuildUrl($"/api/runtime/characters/{characterId}/inventory/merge");
            if (string.IsNullOrWhiteSpace(url)) { onDone?.Invoke(new MergeStacksResult { Ok = false, Error = "missing_url" }); yield break; }

            var body = JsonConvert.SerializeObject(new { seasonId, sourceInstanceId, targetInstanceId, requestId }, NetProtocol.JsonSettings);
            var auth = GetAuth();
            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            ApplyHeaders(req, auth);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onDone?.Invoke(new MergeStacksResult { Ok = false, Error = req.error });
                yield break;
            }

            try
            {
                var response = JsonConvert.DeserializeObject<MergeStacksResponse>(req.downloadHandler.text, NetProtocol.JsonSettings);
                onDone?.Invoke(response != null && response.Ok
                    ? new MergeStacksResult { Ok = true, MergedItem = response.MergedItem, DeletedInstanceId = response.DeletedInstanceId }
                    : new MergeStacksResult { Ok = false, Error = response?.Error ?? "parse_error" });
            }
            catch (Exception e)
            {
                onDone?.Invoke(new MergeStacksResult { Ok = false, Error = e.Message });
            }
        }

        private IEnumerator PostRoutine(string path, object payload, Action<InventoryActionResult> onDone)
        {
            var url = BuildUrl(path);
            if (string.IsNullOrWhiteSpace(url)) { onDone?.Invoke(new InventoryActionResult { Ok = false, Error = "missing_url" }); yield break; }

            var body = JsonConvert.SerializeObject(payload, NetProtocol.JsonSettings);
            var auth = GetAuth();
            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            ApplyHeaders(req, auth);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onDone?.Invoke(new InventoryActionResult { Ok = false, Error = req.error });
                yield break;
            }

            try
            {
                var response = JsonConvert.DeserializeObject<InventoryActionResponse>(req.downloadHandler.text, NetProtocol.JsonSettings);
                onDone?.Invoke(response != null && response.Ok
                    ? new InventoryActionResult { Ok = true, Item = response.Item }
                    : new InventoryActionResult { Ok = false, Error = response?.Error ?? "parse_error" });
            }
            catch (Exception e)
            {
                onDone?.Invoke(new InventoryActionResult { Ok = false, Error = e.Message });
            }
        }

        private sealed class ErrorBody
        {
            [JsonProperty("error")] public string error { get; set; }
            [JsonProperty("message")] public string message { get; set; }
        }

        private sealed class InventoryResponse
        {
            [JsonProperty("ok")] public bool Ok { get; set; }
            [JsonProperty("error")] public string Error { get; set; }
            [JsonProperty("items")] public InventoryItemDto[] Items { get; set; }
            [JsonProperty("equipmentSlots")] public string[] EquipmentSlots { get; set; }
            [JsonProperty("bagCapacity")] public int BagCapacity { get; set; }
            [JsonProperty("bag_capacity")] public int BagCapacitySnake { set => BagCapacity = value; }
            [JsonProperty("stashCapacity")] public int StashCapacity { get; set; }
            [JsonProperty("stash_capacity")] public int StashCapacitySnake { set => StashCapacity = value; }
            [JsonProperty("bagUsage")] public int BagUsage { get; set; }
            [JsonProperty("bag_usage")] public int BagUsageSnake { set => BagUsage = value; }
            [JsonProperty("stashUsage")] public int StashUsage { get; set; }
            [JsonProperty("stash_usage")] public int StashUsageSnake { set => StashUsage = value; }
        }

        private sealed class InventoryActionResponse
        {
            [JsonProperty("ok")] public bool Ok { get; set; }
            [JsonProperty("error")] public string Error { get; set; }
            [JsonProperty("item")] public InventoryItemDto Item { get; set; }
            [JsonProperty("profile")] public object Profile { get; set; }
        }

        private sealed class SplitStackResponse
        {
            public bool Ok { get; set; }
            public string Error { get; set; }
            [JsonProperty("original_item")] public InventoryItemDto OriginalItem { get; set; }
            [JsonProperty("new_item")] public InventoryItemDto NewItem { get; set; }
        }

        private sealed class MergeStacksResponse
        {
            public bool Ok { get; set; }
            public string Error { get; set; }
            [JsonProperty("merged_item")] public InventoryItemDto MergedItem { get; set; }
            [JsonProperty("deleted_instance_id")] public string DeletedInstanceId { get; set; }
        }
    }
}
