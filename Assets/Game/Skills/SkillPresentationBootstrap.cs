using System;
using System.Collections;
using System.Collections.Generic;
using DVBARPG.Core.Services;
using DVBARPG.Game.Network;
using DVBARPG.Game.Skills;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace DVBARPG.Game.Skills.Presentation
{
    public sealed class SkillPresentationBootstrap : MonoBehaviour
    {
        [Header("HTTP")]
        [Tooltip("Переопределить базовый URL runtime (например http://127.0.0.1:8080). Если пусто — берём из NetworkRunConnector.")]
        [SerializeField] private string runtimeBaseUrlOverride = "";
        [Tooltip("Таймаут запросов (сек).")]
        [SerializeField] private int timeoutSec = 10;
        [Tooltip("Логировать успешные ответы.")]
        [SerializeField] private bool logOnSuccess = true;
        [Tooltip("Логировать ошибки запросов.")]
        [SerializeField] private bool logErrors = true;

        [Header("Логи")]
        [Tooltip("Логировать ответ /skills/catalog.")]
        [SerializeField] private bool logCatalogResponse = true;

        [Header("Авторизация")]
        [Tooltip("Брать токен/CharacterId из ProfileService.")]
        [SerializeField] private bool useProfileAuth = true;
        [Tooltip("Переопределить токен (если не используем ProfileService).")]
        [SerializeField] private string tokenOverride = "";
        [Tooltip("Переопределить CharacterId (если не используем ProfileService).")]
        [SerializeField] private string characterIdOverride = "";
        [Tooltip("Переопределить SeasonId (если не используем ProfileService).")]
        [SerializeField] private string seasonIdOverride = "";

        [Header("Каталог")]
        [Tooltip("Базовый каталог (ассет). Может содержать ручные презентации.")]
        [SerializeField] private SkillPresentationCatalog baseCatalog;
        [Tooltip("Создать runtime-каталог поверх baseCatalog.")]
        [SerializeField] private bool createRuntimeCatalog = true;
        [Tooltip("Добавлять в каталог все скиллы из /skills/catalog.")]
        [SerializeField] private bool includeAllCatalogSkills = true;

        [Header("Драйвер")]
        [Tooltip("Драйвер презентации скиллов.")]
        [SerializeField] private SkillPresentationDriver driver;

        private void Start()
        {
            StartCoroutine(BootstrapRoutine());
        }

        private IEnumerator BootstrapRoutine()
        {
            var auth = ResolveAuth();
            if (!auth.Valid)
            {
                if (logErrors) Debug.LogWarning("SkillPresentationBootstrap: auth is missing.");
                yield break;
            }

            var baseUrl = ResolveRuntimeBaseUrl();
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                if (logErrors) Debug.LogWarning("SkillPresentationBootstrap: runtime base url is empty.");
                yield break;
            }

            var skillsUrl = $"{baseUrl.TrimEnd('/')}/skills/catalog";
            var characterUrl = $"{baseUrl.TrimEnd('/')}/characters/{auth.CharacterId}";

            var skillIds = new List<string>();
            yield return FetchSkillCatalog(skillsUrl, auth.Token, skillIds);

            if (skillIds.Count == 0)
            {
                if (logErrors) Debug.LogWarning("SkillPresentationBootstrap: skills catalog is empty.");
            }

            var equipped = new EquippedSkills();
            yield return FetchCharacter(characterUrl, auth.Token, auth.SeasonId, equipped, skillIds);
            ApplyCatalogAndLoadout(skillIds, equipped);
        }

        
        private void ApplyCatalogAndLoadout(List<string> skillIds, EquippedSkills equipped)
        {
            if (driver == null)
            {
                if (logErrors) Debug.LogWarning("SkillPresentationBootstrap: SkillPresentationDriver is not assigned.");
                return;
            }

            var catalog = baseCatalog;
            if (createRuntimeCatalog || catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<SkillPresentationCatalog>();
            }

            var items = new List<SkillPresentation>();
            if (baseCatalog != null)
            {
                var existing = baseCatalog.Items;
                if (existing != null)
                {
                    items.AddRange(existing);
                }
            }

            var desired = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (includeAllCatalogSkills)
            {
                for (int i = 0; i < skillIds.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(skillIds[i])) desired.Add(skillIds[i]);
                }
            }
            else
            {
                AddIfNotEmpty(desired, equipped.AttackSkillId);
                AddIfNotEmpty(desired, equipped.SupportASkillId);
                AddIfNotEmpty(desired, equipped.SupportBSkillId);
                AddIfNotEmpty(desired, equipped.MovementSkillId);
            }

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null || string.IsNullOrWhiteSpace(item.SkillId)) continue;
                desired.Remove(item.SkillId);
            }

            foreach (var id in desired)
            {
                var pres = ScriptableObject.CreateInstance<SkillPresentation>();
                pres.Initialize(id);
                items.Add(pres);
            }

            catalog.SetItems(items);
            driver.SetCatalog(catalog);

            driver.SetEquippedSkills(
                equipped.AttackSkillId,
                equipped.SupportASkillId,
                equipped.SupportBSkillId,
                equipped.MovementSkillId);
        }

        private static void AddIfNotEmpty(HashSet<string> set, string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) set.Add(value);
        }

        private IEnumerator FetchSkillCatalog(string url, string token, List<string> outSkillIds)
        {
            using var req = UnityWebRequest.Get(url);
            req.timeout = timeoutSec;
            req.SetRequestHeader("Authorization", $"Bearer {token}");
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                if (logErrors) LogRequestFailure("skills/catalog", req, url);
                yield break;
            }

            var json = req.downloadHandler.text;
            if (logCatalogResponse)
            {
                Debug.Log($"SkillPresentationBootstrap: skills/catalog response={json}");
            }
            SkillCatalogResponse response;
            try
            {
                response = JsonConvert.DeserializeObject<SkillCatalogResponse>(json);
            }
            catch (Exception e)
            {
                if (logErrors) Debug.LogWarning($"SkillPresentationBootstrap: skills/catalog parse error: {e.Message}");
                yield break;
            }

            if (response?.Skills == null)
            {
                if (logErrors) Debug.LogWarning("SkillPresentationBootstrap: skills/catalog response is empty.");
                yield break;
            }

            outSkillIds.Clear();
            SkillRangeCatalog.Clear();
            SkillCastModeCatalog.Clear();
            for (int i = 0; i < response.Skills.Length; i++)
            {
                var def = response.Skills[i];
                var id = def?.Id;
                if (string.IsNullOrWhiteSpace(id)) continue;
                outSkillIds.Add(id);
                SkillRangeCatalog.SetRange(id, def.Range);
                var mode = !string.IsNullOrWhiteSpace(def.CastMode) ? def.CastMode : def.CastModeAlt;
                SkillCastModeCatalog.SetCastMode(id, MapCastMode(mode));
            }

            if (logOnSuccess)
            {
                Debug.Log($"SkillPresentationBootstrap: loaded {outSkillIds.Count} skills.");
            }
        }

        private IEnumerator FetchCharacter(string url, string token, Guid? seasonId, EquippedSkills outEquipped, List<string> catalogIds)
        {
            using var req = UnityWebRequest.Get(url);
            req.timeout = timeoutSec;
            req.SetRequestHeader("Authorization", $"Bearer {token}");
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                if (logErrors) LogRequestFailure("character", req, url);
                FallbackEquipped(outEquipped, catalogIds);
                yield break;
            }

            var json = req.downloadHandler.text;
            CharacterGraphResponse response;
            try
            {
                response = JsonConvert.DeserializeObject<CharacterGraphResponse>(json);
            }
            catch (Exception e)
            {
                if (logErrors) Debug.LogWarning($"SkillPresentationBootstrap: character parse error: {e.Message}");
                FallbackEquipped(outEquipped, catalogIds);
                yield break;
            }

            var profile = FindProfile(response, seasonId);
            if (profile == null)
            {
                // Нормальная ситуация: у персонажа ещё нет runtime-профиля для этого сезона (первый заход в сезон / новый персонаж). Используем дефолтный лоадаут.
                if (logErrors) Debug.Log("SkillPresentationBootstrap: no runtime profile for current season; using default loadout.");
                FallbackEquipped(outEquipped, catalogIds);
                yield break;
            }

            ResolveEquippedFromProfile(profile, outEquipped);
            EnsureEquippedFilled(outEquipped, profile, catalogIds);

            if (logOnSuccess)
            {
                Debug.Log($"SkillPresentationBootstrap: character loaded. url={url} characterId={profile.CharacterId} seasonId={profile.SeasonId}");
                Debug.Log($"SkillPresentationBootstrap: equipped skills attack={outEquipped.AttackSkillId} supportA={outEquipped.SupportASkillId} supportB={outEquipped.SupportBSkillId} movement={outEquipped.MovementSkillId}");
            }

        }

        private static void ResolveEquippedFromProfile(CharacterRuntimeProfile profile, EquippedSkills outEquipped)
        {
            if (profile?.CombatLoadout != null)
            {
                ReadLoadout(profile.CombatLoadout, "attack", ref outEquipped.AttackSkillId);
                ReadLoadout(profile.CombatLoadout, "supportA", ref outEquipped.SupportASkillId);
                ReadLoadout(profile.CombatLoadout, "supportB", ref outEquipped.SupportBSkillId);
                ReadLoadout(profile.CombatLoadout, "movement", ref outEquipped.MovementSkillId);
            }
        }

        private static void ReadLoadout(JObject loadout, string key, ref string target)
        {
            if (loadout == null || string.IsNullOrWhiteSpace(key)) return;
            if (!loadout.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out var token)) return;
            if (TryReadSkillId(token, out var id))
            {
                target = id;
            }
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

        private static void EnsureEquippedFilled(EquippedSkills outEquipped, CharacterRuntimeProfile profile, List<string> catalogIds)
        {
            var fallback = new List<string>();
            if (profile?.Skills != null)
            {
                for (int i = 0; i < profile.Skills.Length; i++)
                {
                    var id = profile.Skills[i]?.SkillId;
                    if (!string.IsNullOrWhiteSpace(id)) fallback.Add(id);
                }
            }

            if (fallback.Count == 0 && catalogIds != null)
            {
                fallback.AddRange(catalogIds);
            }

            FillMissing(outEquipped, fallback);
        }

        private static void FillMissing(EquippedSkills outEquipped, List<string> ids)
        {
            var idx = 0;
            if (string.IsNullOrWhiteSpace(outEquipped.AttackSkillId) && idx < ids.Count) outEquipped.AttackSkillId = ids[idx++];
            if (string.IsNullOrWhiteSpace(outEquipped.SupportASkillId) && idx < ids.Count) outEquipped.SupportASkillId = ids[idx++];
            if (string.IsNullOrWhiteSpace(outEquipped.SupportBSkillId) && idx < ids.Count) outEquipped.SupportBSkillId = ids[idx++];
        }

        private static void FallbackEquipped(EquippedSkills outEquipped, List<string> catalogIds)
        {
            FillMissing(outEquipped, catalogIds ?? new List<string>());
        }

        private static CharacterRuntimeProfile FindProfile(CharacterGraphResponse response, Guid? seasonId)
        {
            var profiles = response?.Character?.RuntimeProfiles;
            if (profiles == null || profiles.Length == 0) return null;

            if (seasonId.HasValue)
            {
                var seasonStr = seasonId.Value.ToString();
                for (int i = 0; i < profiles.Length; i++)
                {
                    if (string.Equals(profiles[i].SeasonId, seasonStr, StringComparison.OrdinalIgnoreCase))
                    {
                        return profiles[i];
                    }
                }
            }

            return profiles[0];
        }

        private AuthContext ResolveAuth()
        {
            if (useProfileAuth)
            {
                var profile = DVBARPG.Core.GameRoot.Instance.Services.Get<IProfileService>();
                if (profile == null || profile.CurrentAuth == null) return default;

                var auth = profile.CurrentAuth;
                return new AuthContext
                {
                    Token = auth.Token,
                    CharacterId = auth.CharacterId.ToString(),
                    SeasonId = auth.SeasonId,
                    Valid = !string.IsNullOrWhiteSpace(auth.Token) && auth.CharacterId != Guid.Empty
                };
            }

            var token = tokenOverride;
            var characterId = characterIdOverride;
            var seasonId = ParseGuid(seasonIdOverride);
            return new AuthContext
            {
                Token = token,
                CharacterId = characterId,
                SeasonId = seasonId,
                Valid = !string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(characterId)
            };
        }

        private string ResolveRuntimeBaseUrl()
        {
            if (!string.IsNullOrWhiteSpace(runtimeBaseUrlOverride)) return runtimeBaseUrlOverride;

            var connector = FindFirstObjectByType<NetworkRunConnector>();
            if (connector == null) return "http://127.0.0.1:8080";

            var serverUrlField = typeof(NetworkRunConnector).GetField("serverUrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var raw = serverUrlField?.GetValue(connector) as string;
            if (string.IsNullOrWhiteSpace(raw)) return "http://127.0.0.1:8080";

            if (raw.StartsWith("udp://", StringComparison.OrdinalIgnoreCase))
            {
                raw = "http://" + raw.Substring("udp://".Length);
            }
            raw = raw.TrimEnd('/');

            if (raw.EndsWith(":8081", StringComparison.OrdinalIgnoreCase))
            {
                raw = raw.Substring(0, raw.Length - 5) + ":8080";
            }

            return raw;
        }

        private static Guid? ParseGuid(string raw)
        {
            if (Guid.TryParse(raw, out var g)) return g;
            return null;
        }

        private void LogRequestFailure(string label, UnityWebRequest req, string url)
        {
            var status = req.responseCode;
            var error = req.error;
            var body = req.downloadHandler != null ? req.downloadHandler.text : "";
            if (string.IsNullOrWhiteSpace(body))
            {
                Debug.LogWarning($"SkillPresentationBootstrap: {label} request failed. url={url} status={status} error={error}");
                return;
            }

            Debug.LogWarning($"SkillPresentationBootstrap: {label} request failed. url={url} status={status} error={error} body={body}");
        }

        private struct AuthContext
        {
            public string Token;
            public string CharacterId;
            public Guid? SeasonId;
            public bool Valid;
        }

        private sealed class EquippedSkills
        {
            public string AttackSkillId;
            public string SupportASkillId;
            public string SupportBSkillId;
            public string MovementSkillId;
        }

        private sealed class SkillCatalogResponse
        {
            [JsonProperty("ok")] public bool Ok { get; set; }
            [JsonProperty("skills")] public SkillDef[] Skills { get; set; }
        }

        private sealed class SkillDef
        {
            [JsonProperty("id")] public string Id { get; set; }
            [JsonProperty("range")] public float Range { get; set; }
            [JsonProperty("castMode")] public string CastMode { get; set; }
            [JsonProperty("cast_mode")] public string CastModeAlt { get; set; }
        }

        private static int MapCastMode(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 2;
            if (string.Equals(value, "stand_only", System.StringComparison.OrdinalIgnoreCase)) return 0;
            if (string.Equals(value, "move_only", System.StringComparison.OrdinalIgnoreCase)) return 1;
            return 2;
        }

        private sealed class CharacterGraphResponse
        {
            [JsonProperty("ok")] public bool Ok { get; set; }
            [JsonProperty("character")] public CharacterGraphCharacter Character { get; set; }
        }

        private sealed class CharacterGraphCharacter
        {
            [JsonProperty("runtimeProfiles")] public CharacterRuntimeProfile[] RuntimeProfiles { get; set; }
        }

        private sealed class CharacterRuntimeProfile
        {
            [JsonProperty("characterId")] public string CharacterId { get; set; }
            [JsonProperty("seasonId")] public string SeasonId { get; set; }
            [JsonProperty("skills")] public CharacterSkill[] Skills { get; set; }
            [JsonProperty("combatLoadout")] public JObject CombatLoadout { get; set; }
        }

        private sealed class CharacterSkill
        {
            [JsonProperty("skillId")] public string SkillId { get; set; }
        }
    }
}
