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

        public void FetchCurrentSeason(AuthSession session, Action<RuntimeSeasonSnapshot> onDone)
        {
            StartCoroutine(FetchCurrentSeasonRoutine(session, onDone));
        }

        public void FetchCharacters(AuthSession session, Action<RuntimeCharactersSnapshot> onDone)
        {
            StartCoroutine(FetchCharactersRoutine(session, onDone));
        }

        public void ValidateAuth(AuthSession session, string characterId, string seasonId, Action<RuntimeAuthSnapshot> onDone)
        {
            StartCoroutine(ValidateAuthRoutine(session, characterId, seasonId, onDone));
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
            yield return req.SendWebRequest();

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
            yield return req.SendWebRequest();

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
            yield return req.SendWebRequest();

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
                        MoveSpeed = response.Stats != null ? response.Stats.MoveSpeed : 0f
                    };
                }
            }
            catch (Exception)
            {
                snapshot = new RuntimeAuthSnapshot { Ok = false, Error = "parse_error" };
            }

            onDone?.Invoke(snapshot);
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
