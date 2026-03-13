using System;
using System.Collections;
using System.Text;
using DVBARPG.Core.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace DVBARPG.Net.Network
{
    public sealed class RuntimeMetaService : MonoBehaviour, IRuntimeMetaService
    {
        [Header("HTTP")]
        [Tooltip("Базовый URL Laravel backend.")]
        [SerializeField] private string backendBaseUrl = "http://127.0.0.1:8000";
        [Tooltip("API key для runtime.auth (если требуется).")]
        [SerializeField] private string apiKey = "dev-backend-key";
        [Tooltip("Контракт API.")]
        [SerializeField] private string contractVersion = "1.1";
        [Tooltip("Таймаут запросов (сек).")]
        [SerializeField] private int timeoutSec = 10;
        [Header("Debug")]
        [Tooltip("Логировать все HTTP‑запросы/ответы в dev‑режиме.")]
        [SerializeField] private bool logHttpTraffic = true;

        public void FetchCurrentSeason(AuthSession session, Action<RuntimeSeasonSnapshot> onDone)
        {
            StartCoroutine(WithOverlay(FetchCurrentSeasonRoutine(session, onDone)));
        }

        public void FetchCharacters(AuthSession session, Action<RuntimeCharactersSnapshot> onDone)
        {
            StartCoroutine(WithOverlay(FetchCharactersRoutine(session, onDone)));
        }

        public void ValidateAuth(AuthSession session, string characterId, string seasonId, Action<RuntimeAuthSnapshot> onDone)
        {
            StartCoroutine(WithOverlay(ValidateAuthRoutine(session, characterId, seasonId, onDone)));
        }

        public void FetchProfile(AuthSession session, string characterId, string seasonId, Action<RuntimeProfileSnapshot> onDone)
        {
            StartCoroutine(WithOverlay(FetchProfileRoutine(session, characterId, seasonId, onDone)));
        }

        public void SetLoadout(AuthSession session, string characterId, string seasonId, RuntimeLoadoutPayload loadout, Action<SetLoadoutResult> onDone)
        {
            StartCoroutine(WithOverlay(SetLoadoutRoutine(session, characterId, seasonId, loadout, onDone)));
        }

        public void AllocateTalent(AuthSession session, string characterId, string seasonId, string talentCode, string requestId, Action<AllocateTalentResult> onDone)
        {
            StartCoroutine(WithOverlay(AllocateTalentRoutine(session, characterId, seasonId, talentCode, requestId, onDone)));
        }

        public void CreateCharacter(AuthSession session, string name, string classId, string gender, Action<CreateCharacterResult> onDone)
        {
            StartCoroutine(WithOverlay(CreateCharacterRoutine(session, name, classId, gender, onDone)));
        }

        public void DeleteCharacter(AuthSession session, string characterId, Action<DeleteCharacterResult> onDone)
        {
            StartCoroutine(WithOverlay(DeleteCharacterRoutine(session, characterId, onDone)));
        }

        private static IEnumerator WithOverlay(IEnumerator inner)
        {
            var overlay = Core.GameRoot.Instance?.Services?.Get<ILoadingOverlayService>();
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

        private IEnumerator FetchCurrentSeasonRoutine(AuthSession session, Action<RuntimeSeasonSnapshot> onDone)
        {
            var url = BuildUrl("/api/runtime/seasons/current");
            if (string.IsNullOrWhiteSpace(url))
            {
                onDone?.Invoke(new RuntimeSeasonSnapshot { Ok = false, Error = "missing_backend_url" });
                yield break;
            }

            using var req = UnityWebRequest.Get(url);
            ApplyHeaders(req, session);
            yield return SendWithLogging(req, "GET", url, null);

            if (req.result != UnityWebRequest.Result.Success)
            {
                onDone?.Invoke(new RuntimeSeasonSnapshot { Ok = false, Error = req.error });
                yield break;
            }

            RuntimeSeasonSnapshot snapshot;
            try
            {
                var response = JsonConvert.DeserializeObject<SeasonResponse>(req.downloadHandler.text);
                snapshot = response != null
                    ? new RuntimeSeasonSnapshot { Ok = response.Ok, Error = response.Error, SeasonId = response.SeasonId }
                    : new RuntimeSeasonSnapshot { Ok = false, Error = "empty_response" };
            }
            catch (Exception)
            {
                snapshot = new RuntimeSeasonSnapshot { Ok = false, Error = "parse_error" };
            }

            onDone?.Invoke(snapshot);
        }

        private IEnumerator FetchCharactersRoutine(AuthSession session, Action<RuntimeCharactersSnapshot> onDone)
        {
            var url = BuildUrl("/api/runtime/characters");
            if (string.IsNullOrWhiteSpace(url))
            {
                onDone?.Invoke(new RuntimeCharactersSnapshot { Ok = false, Error = "missing_backend_url" });
                yield break;
            }

            using var req = UnityWebRequest.Get(url);
            ApplyHeaders(req, session);
            yield return SendWithLogging(req, "GET", url, null);

            if (req.result != UnityWebRequest.Result.Success)
            {
                onDone?.Invoke(new RuntimeCharactersSnapshot { Ok = false, Error = req.error });
                yield break;
            }

            RuntimeCharactersSnapshot snapshot;
            try
            {
                var body = req.downloadHandler.text;
                var response = JsonConvert.DeserializeObject<CharactersResponse>(body);
                if (response == null)
                {
                    snapshot = new RuntimeCharactersSnapshot { Ok = false, Error = "empty_response" };
                }
                else
                {
                    var list = response.Characters ?? Array.Empty<CharacterRow>();
                    var mapped = new RuntimeCharacterSummary[list.Length];
                    for (int i = 0; i < list.Length; i++)
                    {
                        mapped[i] = new RuntimeCharacterSummary
                        {
                            Id = list[i].Id,
                            Name = list[i].Name,
                            Gender = list[i].Gender,
                            Seasons = list[i].Seasons ?? Array.Empty<string>()
                        };
                    }

                    snapshot = new RuntimeCharactersSnapshot
                    {
                        Ok = response.Ok,
                        Error = response.Error,
                        CurrentSeasonId = response.CurrentSeasonId,
                        Characters = mapped
                    };
                }
            }
            catch (Exception)
            {
                snapshot = new RuntimeCharactersSnapshot { Ok = false, Error = "parse_error" };
            }

            onDone?.Invoke(snapshot);
        }

        private IEnumerator ValidateAuthRoutine(AuthSession session, string characterId, string seasonId, Action<RuntimeAuthSnapshot> onDone)
        {
            var url = BuildUrl("/api/runtime/auth/validate");
            if (string.IsNullOrWhiteSpace(url))
            {
                onDone?.Invoke(new RuntimeAuthSnapshot { Ok = false, Error = "missing_backend_url" });
                yield break;
            }

            var payload = new ValidateAuthRequest
            {
                CharacterId = characterId,
                SeasonId = seasonId
            };

            var json = JsonConvert.SerializeObject(payload);
            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            ApplyHeaders(req, session);
            yield return SendWithLogging(req, "POST", url, json);

            if (req.result != UnityWebRequest.Result.Success)
            {
                onDone?.Invoke(new RuntimeAuthSnapshot { Ok = false, Error = req.error });
                yield break;
            }

            RuntimeAuthSnapshot snapshot;
            try
            {
                var body = req.downloadHandler.text;
                var response = JsonConvert.DeserializeObject<ValidateAuthResponse>(body);
                if (response == null)
                {
                    snapshot = new RuntimeAuthSnapshot { Ok = false, Error = "empty_response" };
                }
                else
                {
                    snapshot = new RuntimeAuthSnapshot
                    {
                        Ok = response.Ok,
                        Error = response.Error,
                        Loadout = ParseCombatLoadout(response.CombatLoadout),
                        Skills = MapSkills(response.Skills),
                        MoveSpeed = response.Stats != null ? response.Stats.MoveSpeed : 0f,
                        Level = response.Level,
                        XpTotal = response.XpTotal,
                        XpToNextLevel = response.XpToNextLevel,
                        UnspentTalentPoints = response.UnspentTalentPoints
                    };
                }
            }
            catch (Exception)
            {
                snapshot = new RuntimeAuthSnapshot { Ok = false, Error = "parse_error" };
            }

            onDone?.Invoke(snapshot);
        }

        private IEnumerator FetchProfileRoutine(AuthSession session, string characterId, string seasonId, Action<RuntimeProfileSnapshot> onDone)
        {
            var url = BuildUrl($"/api/runtime/characters/{characterId}/profile?seasonId={seasonId}");
            if (string.IsNullOrWhiteSpace(url))
            {
                onDone?.Invoke(new RuntimeProfileSnapshot { Ok = false, Error = "missing_backend_url" });
                yield break;
            }

            using var req = UnityWebRequest.Get(url);
            ApplyHeaders(req, session);
            yield return SendWithLogging(req, "GET", url, null);

            if (req.result != UnityWebRequest.Result.Success)
            {
                onDone?.Invoke(new RuntimeProfileSnapshot { Ok = false, Error = req.error });
                yield break;
            }

            RuntimeProfileSnapshot snapshot;
            try
            {
                var body = req.downloadHandler.text;
                var response = JsonConvert.DeserializeObject<ProfileResponse>(body);
                if (response == null)
                {
                    snapshot = new RuntimeProfileSnapshot { Ok = false, Error = "empty_response" };
                }
                else
                {
                    RuntimeProgressionSnapshot progression = null;
                    if (response.Progression != null)
                    {
                        progression = new RuntimeProgressionSnapshot
                        {
                            Level = response.Progression.Level,
                            XpTotal = response.Progression.XpTotal,
                            XpToNextLevel = response.Progression.XpToNextLevel,
                            XpCurrentLevelBase = response.Progression.XpCurrentLevelBase,
                            XpNextLevelTotal = response.Progression.XpNextLevelTotal
                        };
                    }

                    snapshot = new RuntimeProfileSnapshot
                    {
                        Ok = response.Ok,
                        Error = response.Error,
                        Progression = progression
                    };
                }
            }
            catch (Exception)
            {
                snapshot = new RuntimeProfileSnapshot { Ok = false, Error = "parse_error" };
            }

            onDone?.Invoke(snapshot);
        }

        private IEnumerator SetLoadoutRoutine(AuthSession session, string characterId, string seasonId, RuntimeLoadoutPayload loadout, Action<SetLoadoutResult> onDone)
        {
            var url = BuildUrl($"/api/runtime/characters/{characterId}/loadout");
            if (string.IsNullOrWhiteSpace(url) || loadout == null)
            {
                onDone?.Invoke(new SetLoadoutResult { Ok = false, Error = "missing_url_or_loadout" });
                yield break;
            }

            var payload = new SetLoadoutRequest
            {
                SeasonId = seasonId,
                CombatLoadout = new CombatLoadoutPayload
                {
                    AttackSkillId = loadout.AttackSkillId ?? "",
                    SupportASkillId = loadout.SupportASkillId ?? "",
                    SupportBSkillId = loadout.SupportBSkillId ?? "",
                    MovementSlot = string.IsNullOrWhiteSpace(loadout.MovementSlot) ? "supportB" : loadout.MovementSlot,
                    AttackEnabled = true,
                    SupportAEnabled = true,
                    SupportBEnabled = true
                }
            };

            var json = JsonConvert.SerializeObject(payload);
            using var req = new UnityWebRequest(url, "PUT");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            ApplyHeaders(req, session);
            yield return SendWithLogging(req, "PUT", url, json);

            if (req.result != UnityWebRequest.Result.Success)
            {
                onDone?.Invoke(new SetLoadoutResult { Ok = false, Error = req.error });
                yield break;
            }

            SetLoadoutResult result;
            try
            {
                var response = JsonConvert.DeserializeObject<SetLoadoutResponse>(req.downloadHandler.text);
                result = response != null
                    ? new SetLoadoutResult { Ok = response.Ok, Error = response.Error }
                    : new SetLoadoutResult { Ok = false, Error = "empty_response" };
            }
            catch (Exception)
            {
                result = new SetLoadoutResult { Ok = false, Error = "parse_error" };
            }

            onDone?.Invoke(result);
        }

        private IEnumerator AllocateTalentRoutine(AuthSession session, string characterId, string seasonId, string talentCode, string requestId, Action<AllocateTalentResult> onDone)
        {
            var url = BuildUrl($"/api/runtime/characters/{characterId}/talents/allocate");
            if (string.IsNullOrWhiteSpace(url))
            {
                onDone?.Invoke(new AllocateTalentResult { Ok = false, Error = "missing_url" });
                yield break;
            }

            var payload = new { seasonId, talentCode, requestId };
            var json = JsonConvert.SerializeObject(payload);
            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            ApplyHeaders(req, session);
            yield return SendWithLogging(req, "POST", url, json);

            if (req.result != UnityWebRequest.Result.Success)
            {
                onDone?.Invoke(new AllocateTalentResult { Ok = false, Error = req.error });
                yield break;
            }

            AllocateTalentResult result;
            try
            {
                var response = JsonConvert.DeserializeObject<AllocateTalentResponse>(req.downloadHandler.text);
                result = response != null
                    ? new AllocateTalentResult { Ok = response.Ok, Error = response.Error }
                    : new AllocateTalentResult { Ok = false, Error = "empty_response" };
            }
            catch (Exception)
            {
                result = new AllocateTalentResult { Ok = false, Error = "parse_error" };
            }

            onDone?.Invoke(result);
        }

        private IEnumerator CreateCharacterRoutine(AuthSession session, string name, string classId, string gender, Action<CreateCharacterResult> onDone)
        {
            var url = BuildUrl("/api/runtime/characters");
            if (string.IsNullOrWhiteSpace(url))
            {
                onDone?.Invoke(new CreateCharacterResult { Ok = false, Error = "missing_backend_url" });
                yield break;
            }

            var payload = new CreateCharacterRequest
            {
                Name = name ?? "",
                ClassId = classId ?? "vanguard",
                Gender = gender ?? "male"
            };
            var json = JsonConvert.SerializeObject(payload);
            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            ApplyHeaders(req, session);
            yield return SendWithLogging(req, "POST", url, json);

            if (req.result != UnityWebRequest.Result.Success)
            {
                onDone?.Invoke(new CreateCharacterResult { Ok = false, Error = req.error });
                yield break;
            }

            CreateCharacterResult result;
            try
            {
                var response = JsonConvert.DeserializeObject<CreateCharacterResponse>(req.downloadHandler.text);
                result = response != null
                    ? new CreateCharacterResult { Ok = response.Ok, Error = response.Error, CharacterId = response.CharacterId }
                    : new CreateCharacterResult { Ok = false, Error = "empty_response" };
            }
            catch (Exception)
            {
                result = new CreateCharacterResult { Ok = false, Error = "parse_error" };
            }

            onDone?.Invoke(result);
        }

        private IEnumerator DeleteCharacterRoutine(AuthSession session, string characterId, Action<DeleteCharacterResult> onDone)
        {
            var url = BuildUrl($"/api/runtime/characters/{characterId}");
            if (string.IsNullOrWhiteSpace(url))
            {
                onDone?.Invoke(new DeleteCharacterResult { Ok = false, Error = "missing_backend_url" });
                yield break;
            }

            using var req = new UnityWebRequest(url, "DELETE")
            {
                downloadHandler = new DownloadHandlerBuffer()
            };
            ApplyHeaders(req, session);
            yield return SendWithLogging(req, "DELETE", url, null);

            if (req.result != UnityWebRequest.Result.Success)
            {
                onDone?.Invoke(new DeleteCharacterResult { Ok = false, Error = req.error });
                yield break;
            }

            DeleteCharacterResult result;
            try
            {
                var body = req.downloadHandler?.text ?? "";
                var response = body.Length > 0 ? JsonConvert.DeserializeObject<DeleteCharacterResponse>(body) : null;
                if (response != null)
                    result = new DeleteCharacterResult { Ok = response.Ok, Error = response.Error };
                else
                    result = new DeleteCharacterResult { Ok = req.responseCode >= 200 && req.responseCode < 300, Error = null };
            }
            catch (Exception)
            {
                result = new DeleteCharacterResult { Ok = false, Error = "parse_error" };
            }

            onDone?.Invoke(result);
        }

        private string BuildUrl(string path)
        {
            var baseUrl = backendBaseUrl;
            if (string.IsNullOrWhiteSpace(baseUrl)) return null;
            return $"{baseUrl.TrimEnd('/')}{path}";
        }

        private void ApplyHeaders(UnityWebRequest req, AuthSession session)
        {
            req.timeout = timeoutSec;
            req.SetRequestHeader("Authorization", $"Bearer {session.Token}");
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                req.SetRequestHeader("X-Api-Key", apiKey);
            }
            if (!string.IsNullOrWhiteSpace(contractVersion))
            {
                req.SetRequestHeader("X-Contract-Version", contractVersion);
            }
        }

        private IEnumerator SendWithLogging(UnityWebRequest req, string method, string url, string body)
        {
            if (logHttpTraffic)
            {
                var shortBody = body;
                if (!string.IsNullOrEmpty(shortBody) && shortBody.Length > 512)
                {
                    shortBody = shortBody.Substring(0, 512) + "...";
                }

                var apiKeyLabel = string.IsNullOrWhiteSpace(apiKey) ? "<none>" : "***";
                Debug.Log($"[HTTP] {method} {url}\n" +
                          $"Headers: Authorization=Bearer ****, X-Api-Key={apiKeyLabel}, Contract={contractVersion}\n" +
                          $"Body: {shortBody}");
            }

            yield return req.SendWebRequest();

            if (logHttpTraffic)
            {
                var text = req.downloadHandler != null ? req.downloadHandler.text : "";
                string shortText = text;
                if (!string.IsNullOrEmpty(shortText) && shortText.Length > 1024)
                {
                    shortText = shortText.Substring(0, 1024) + "...";
                }

                Debug.Log($"[HTTP] RESPONSE {method} {url}\n" +
                          $"Status: {(int)req.responseCode} {req.result}\n" +
                          $"Body: {shortText}");
            }
        }


        private sealed class SeasonResponse
        {
            [JsonProperty("ok")] public bool Ok { get; set; }
            [JsonProperty("error")] public string Error { get; set; }
            [JsonProperty("seasonId")] public string SeasonId { get; set; }
        }

        private sealed class CharactersResponse
        {
            [JsonProperty("ok")] public bool Ok { get; set; }
            [JsonProperty("error")] public string Error { get; set; }
            [JsonProperty("currentSeasonId")] public string CurrentSeasonId { get; set; }
            [JsonProperty("characters")] public CharacterRow[] Characters { get; set; }
        }

        private sealed class CharacterRow
        {
            [JsonProperty("id")] public string Id { get; set; }
            [JsonProperty("name")] public string Name { get; set; }
            [JsonProperty("gender")] public string Gender { get; set; }
            [JsonProperty("seasons")] public string[] Seasons { get; set; }
        }

        private sealed class ValidateAuthRequest
        {
            [JsonProperty("characterId")] public string CharacterId { get; set; }
            [JsonProperty("seasonId")] public string SeasonId { get; set; }
        }

        private sealed class ValidateAuthResponse
        {
            [JsonProperty("ok")] public bool Ok { get; set; }
            [JsonProperty("error")] public string Error { get; set; }
            [JsonProperty("combatLoadout")] public JObject CombatLoadout { get; set; }
            [JsonProperty("skills")] public SkillRow[] Skills { get; set; }
            [JsonProperty("stats")] public StatsRow Stats { get; set; }
            [JsonProperty("level")] public int Level { get; set; }
            [JsonProperty("xpTotal")] public int XpTotal { get; set; }
            [JsonProperty("xpToNextLevel")] public int XpToNextLevel { get; set; }
            [JsonProperty("unspentTalentPoints")] public int UnspentTalentPoints { get; set; }
        }

        private sealed class ProfileResponse
        {
            [JsonProperty("ok")] public bool Ok { get; set; }
            [JsonProperty("error")] public string Error { get; set; }
            [JsonProperty("progression")] public ProgressionRow Progression { get; set; }
        }

        private sealed class ProgressionRow
        {
            [JsonProperty("level")] public int Level { get; set; }
            [JsonProperty("xpTotal")] public int XpTotal { get; set; }
            [JsonProperty("xpToNextLevel")] public int XpToNextLevel { get; set; }
            [JsonProperty("xpCurrentLevelBase")] public int XpCurrentLevelBase { get; set; }
            [JsonProperty("xpNextLevelTotal")] public int XpNextLevelTotal { get; set; }
        }

        private sealed class SkillRow
        {
            [JsonProperty("skillId")] public string SkillId { get; set; }
            [JsonProperty("level")] public int Level { get; set; }
            [JsonProperty("modifiers")] public JToken Modifiers { get; set; }
        }

        private sealed class StatsRow
        {
            [JsonProperty("moveSpeed")] public float MoveSpeed { get; set; }
        }

        private sealed class SetLoadoutRequest
        {
            [JsonProperty("seasonId")] public string SeasonId { get; set; }
            [JsonProperty("combatLoadout")] public CombatLoadoutPayload CombatLoadout { get; set; }
        }

        private sealed class CombatLoadoutPayload
        {
            [JsonProperty("attackSkillId")] public string AttackSkillId { get; set; }
            [JsonProperty("supportASkillId")] public string SupportASkillId { get; set; }
            [JsonProperty("supportBSkillId")] public string SupportBSkillId { get; set; }
            [JsonProperty("movementSlot")] public string MovementSlot { get; set; }
            [JsonProperty("attackEnabled")] public bool AttackEnabled { get; set; }
            [JsonProperty("supportAEnabled")] public bool SupportAEnabled { get; set; }
            [JsonProperty("supportBEnabled")] public bool SupportBEnabled { get; set; }
        }

        private sealed class SetLoadoutResponse
        {
            [JsonProperty("ok")] public bool Ok { get; set; }
            [JsonProperty("error")] public string Error { get; set; }
        }

        private sealed class AllocateTalentResponse
        {
            [JsonProperty("ok")] public bool Ok { get; set; }
            [JsonProperty("error")] public string Error { get; set; }
        }

        private sealed class CreateCharacterRequest
        {
            [JsonProperty("name")] public string Name { get; set; }
            [JsonProperty("classId")] public string ClassId { get; set; }
            [JsonProperty("gender")] public string Gender { get; set; }
        }

        private sealed class CreateCharacterResponse
        {
            [JsonProperty("ok")] public bool Ok { get; set; }
            [JsonProperty("error")] public string Error { get; set; }
            [JsonProperty("characterId")] public string CharacterId { get; set; }
        }

        private sealed class DeleteCharacterResponse
        {
            [JsonProperty("ok")] public bool Ok { get; set; }
            [JsonProperty("error")] public string Error { get; set; }
        }

        private static RuntimeSkillSnapshot[] MapSkills(SkillRow[] rows)
        {
            if (rows == null || rows.Length == 0) return Array.Empty<RuntimeSkillSnapshot>();
            var mapped = new RuntimeSkillSnapshot[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                mapped[i] = new RuntimeSkillSnapshot
                {
                    SkillId = row?.SkillId,
                    Level = row?.Level ?? 0,
                    ModifiersJson = row?.Modifiers == null ? null : row.Modifiers.ToString(Formatting.None)
                };
            }
            return mapped;
        }

        private static RuntimeLoadout ParseCombatLoadout(JObject loadout)
        {
            if (loadout == null) return null;

            var result = new RuntimeLoadout();
            ReadSkill(loadout, "attack", "attackSkillId", ref result.AttackSkillId);
            ReadSkill(loadout, "supportA", "supportASkillId", ref result.SupportASkillId);
            ReadSkill(loadout, "supportB", "supportBSkillId", ref result.SupportBSkillId);
            ReadSkill(loadout, "movement", "movementSkillId", ref result.MovementSkillId);
            return result;
        }

        private static void ReadSkill(JObject loadout, string keyA, string keyB, ref string target)
        {
            if (loadout == null) return;
            if (TryReadSkill(loadout, keyA, ref target)) return;
            TryReadSkill(loadout, keyB, ref target);
        }

        private static bool TryReadSkill(JObject loadout, string key, ref string target)
        {
            if (loadout == null || string.IsNullOrWhiteSpace(key)) return false;
            if (!loadout.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out var token)) return false;
            if (TryReadSkillId(token, out var id))
            {
                target = id;
                return true;
            }
            return false;
        }

        private static bool TryReadSkillId(JToken token, out string id)
        {
            id = "";
            if (token == null) return false;

            if (token.Type == JTokenType.String)
            {
                id = token.Value<string>();
                return !string.IsNullOrWhiteSpace(id);
            }

            if (token.Type == JTokenType.Object)
            {
                var obj = (JObject)token;
                if (obj.TryGetValue("skillId", StringComparison.OrdinalIgnoreCase, out var skillToken))
                {
                    id = skillToken.Value<string>();
                    return !string.IsNullOrWhiteSpace(id);
                }
            }

            return false;
        }

    }
}
