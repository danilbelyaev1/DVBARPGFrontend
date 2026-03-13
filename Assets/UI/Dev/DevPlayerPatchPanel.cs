using System;
using System.Collections;
using System.Collections.Generic;
using DVBARPG.Core;
using DVBARPG.Core.Services;
using DVBARPG.Net.Commands;
using DVBARPG.Net.Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using DVBARPG.UI.Skills;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace DVBARPG.UI.Dev
{
    /// <summary>
    /// Dev‑панель для отправки debug_patch_player.
    /// Показывается только в редакторе/DEV‑сборке.
    /// </summary>
    public sealed class DevPlayerPatchPanel : MonoBehaviour
    {
        [Header("Видимость")]
        [SerializeField] private GameObject contentRoot;
        [SerializeField] private Button toggleButton;
        [SerializeField] private KeyCode toggleKey = KeyCode.F2;

        [Header("Статы")]
        [SerializeField] private Toggle resetStatsToggle;
        [SerializeField] private Slider attackPowerSlider;
        [SerializeField] private Slider spellPowerSlider;
        [SerializeField] private Slider maxHpSlider;
        [SerializeField] private Slider moveSpeedSlider;
        [SerializeField] private Slider critChanceSlider;          // 0–1
        [SerializeField] private Slider critMultSlider;
        [SerializeField] private Slider cooldownReductionSlider;   // 0–1
        [SerializeField] private Slider statusChanceSlider;
        [SerializeField] private Slider resistPhysSlider;
        [SerializeField] private Slider resistElementalSlider;
        [SerializeField] private Slider projectileSpeedBonusSlider;
        [SerializeField] private Slider aoeRadiusBonusSlider;

        [Header("Tag Bonuses (пример)")]
        [SerializeField] private Slider tagBonusSpellSlider;
        [SerializeField] private Slider tagBonusProjectileSlider;

        [Header("Скиллы")]
        [SerializeField] private Toggle replaceSkillsToggle;
        [SerializeField] private SkillSlotUi[] skillSlots;

        [Header("Лоадаут")]
        [SerializeField] private TMP_Dropdown attackSkillDropdown;
        [SerializeField] private TMP_Dropdown supportASkillDropdown;
        [SerializeField] private TMP_Dropdown supportBSkillDropdown;
        [SerializeField] private Toggle attackEnabledToggle;
        [SerializeField] private Toggle supportAEnabledToggle;
        [SerializeField] private Toggle supportBEnabledToggle;
        [SerializeField] private Toggle movementSlotSupportA;
        [SerializeField] private Toggle movementSlotSupportB;

        [Header("Кнопки")]
        [SerializeField] private Button applyButton;

        [Header("Индикация")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI runtimeInfoText;

        [Header("Каталог скиллов")]
        [SerializeField] private bool autoLoadSkillCatalog = true;
        [SerializeField] private bool logSkillCatalogErrors = true;

        [Header("Dev snapshot (/debug/player)")]
        [SerializeField] private bool autoLoadDebugSnapshot = true;
        [SerializeField] private bool logDebugSnapshotErrors = true;

        private NetworkSessionRunner _net;
        private bool _hasPrefilledFromProfile;
        private bool _hasAppliedDebugSnapshot;

        [Serializable]
        public sealed class SkillSlotUi
        {
            [Tooltip("Dropdown с id скилла (первый элемент — <none>).")]
            public TMP_Dropdown skillDropdown;
            [Tooltip("Опциональное текстовое поле с id, если dropdown не используется.")]
            public TMP_InputField skillId;
            [Tooltip("Иконка скилла (Resources/SkillIcons/{skillId}).")]
            public Image skillIcon;
            public TMP_InputField level;
            [Tooltip("Доп. JSON‑модификаторы (projectile.count, base_damage.mult и т.п.).")]
            public TMP_InputField modifiersJson;
            [Header("Теги")]
            public Toggle tagSpell;
            public Toggle tagProjectile;
            public Toggle tagMelee;
        }

        private struct AuthContext
        {
            public string Token;
            public string CharacterId;
            public Guid? SeasonId;
            public bool Valid;
        }

        private sealed class SkillCatalogResponse
        {
            [JsonProperty("ok")] public bool Ok { get; set; }
            [JsonProperty("skills")] public SkillDef[] Skills { get; set; }
        }

        private sealed class SkillDef
        {
            [JsonProperty("id")] public string Id { get; set; }
        }

        private sealed class DebugPlayerResponse
        {
            [JsonProperty("ok")] public bool Ok { get; set; }
            [JsonProperty("error")] public string Error { get; set; }
            [JsonProperty("stats")] public Dictionary<string, float> Stats { get; set; }
            [JsonProperty("skills")] public DebugPlayerSkill[] Skills { get; set; }
            [JsonProperty("combatLoadout")] public DebugPlayerCombatLoadout CombatLoadout { get; set; }
        }

        private sealed class DebugPlayerSkill
        {
            [JsonProperty("skillId")] public string SkillId { get; set; }
            [JsonProperty("level")] public int Level { get; set; }
            [JsonProperty("modifiers")] public JObject Modifiers { get; set; }
        }

        private sealed class DebugPlayerCombatLoadout
        {
            [JsonProperty("attackSkillId")] public string AttackSkillId { get; set; }
            [JsonProperty("supportASkillId")] public string SupportASkillId { get; set; }
            [JsonProperty("supportBSkillId")] public string SupportBSkillId { get; set; }
            [JsonProperty("movementSlot")] public string MovementSlot { get; set; }
            [JsonProperty("attackEnabled")] public bool AttackEnabled { get; set; }
            [JsonProperty("supportAEnabled")] public bool SupportAEnabled { get; set; }
            [JsonProperty("supportBEnabled")] public bool SupportBEnabled { get; set; }
        }

        private void Awake()
        {
            if (contentRoot != null)
                contentRoot.SetActive(false);
        }

        private void OnEnable()
        {
            var sessionService = GameRoot.Instance?.Services?.Get<ISessionService>();
            _net = sessionService as NetworkSessionRunner;
            Debug.Log($"[DevDebug] DevPlayerPatchPanel.OnEnable netAssigned={_net != null}");

            if (toggleButton != null) toggleButton.onClick.AddListener(ToggleVisible);
            if (applyButton != null) applyButton.onClick.AddListener(ApplyDebugPatch);

            _hasPrefilledFromProfile = false;
            _hasAppliedDebugSnapshot = false;

            if (autoLoadSkillCatalog)
            {
                StartCoroutine(LoadSkillCatalogRoutine());
            }

            if (autoLoadDebugSnapshot)
            {
                StartCoroutine(LoadDebugPlayerSnapshotRoutine());
            }

            SubscribeSkillSlotDropdowns();
        }

        private void SubscribeSkillSlotDropdowns()
        {
            if (skillSlots == null) return;
            for (int i = 0; i < skillSlots.Length; i++)
            {
                var slot = skillSlots[i];
                if (slot?.skillDropdown == null) continue;
                slot.skillDropdown.onValueChanged.AddListener(OnSkillSlotDropdownChanged);
            }
        }

        private void OnSkillSlotDropdownChanged(int _)
        {
            if (skillSlots == null) return;
            for (int i = 0; i < skillSlots.Length; i++)
            {
                var slot = skillSlots[i];
                if (slot?.skillDropdown == null) continue;
                RefreshSkillSlotIcon(slot);
            }
        }

        private static void RefreshSkillSlotIcon(SkillSlotUi slot)
        {
            if (slot == null) return;
            string id = null;
            if (slot.skillDropdown != null && slot.skillDropdown.options != null && slot.skillDropdown.options.Count > 0)
            {
                var idx = slot.skillDropdown.value;
                if (idx > 0 && idx < slot.skillDropdown.options.Count)
                    id = slot.skillDropdown.options[idx].text?.Trim();
            }
            if (string.IsNullOrWhiteSpace(id) && slot.skillId != null)
                id = slot.skillId.text?.Trim();
            SkillIconProvider.ApplyIcon(slot.skillIcon, id);
        }

        private void OnDisable()
        {
            if (toggleButton != null) toggleButton.onClick.RemoveListener(ToggleVisible);
            if (applyButton != null) applyButton.onClick.RemoveListener(ApplyDebugPatch);
        }

        private void Update()
        {
            // Используем новый Input System, как в DevCommandsPanel.
            if (toggleKey != KeyCode.None && Keyboard.current != null)
            {
                var key = ToInputKey(toggleKey);
                if (Keyboard.current[key].wasPressedThisFrame)
                {
                    ToggleVisible();
                }
            }

            UpdateRuntimeInfo();

            // Ленивая подгрузка текущих значений, когда профиль уже успел получить ValidateAuth.
            if (!_hasPrefilledFromProfile)
            {
                var profile = GameRoot.Instance?.Services?.Get<IProfileService>();
                if (profile != null)
                {
                    var skillsLen = profile.ServerSkills != null ? profile.ServerSkills.Length : 0;
                    Debug.Log($"[DevDebug] DevPanel.Update prefill check: hasLoadout={profile.ServerLoadout != null} skills={skillsLen} baseMoveSpeed={profile.BaseMoveSpeed}");

                    if (profile.ServerLoadout != null || (profile.ServerSkills != null && profile.ServerSkills.Length > 0) || profile.BaseMoveSpeed > 0f)
                    {
                        PrefillFromProfile();
                    }
                }
            }
        }

        private void ToggleVisible()
        {
            if (contentRoot == null) return;
            contentRoot.SetActive(!contentRoot.activeSelf);
        }

        private void UpdateRuntimeInfo()
        {
            if (runtimeInfoText == null) return;

            var profile = GameRoot.Instance?.Services?.Get<IProfileService>();
            var auth = profile?.CurrentAuth;

            // AUTH_MODE=dev вы не узнаете напрямую из клиента,
            // но можно хотя бы подсветить, что это dev‑панель.
            runtimeInfoText.text =
                $"Dev panel\n" +
                $"AuthMode: dev (ожидается)\n" +
                $"CharacterId: {auth?.CharacterId}\n" +
                $"SeasonId: {auth?.SeasonId}";
        }

        private IEnumerator LoadSkillCatalogRoutine()
        {
            var auth = ResolveAuth();
            if (!auth.Valid)
            {
                if (logSkillCatalogErrors)
                {
                    Debug.LogWarning("DevPlayerPatchPanel: Profile auth is missing, cannot load skills catalog.");
                }
                yield break;
            }

            var baseUrl = ResolveRuntimeBaseUrl();
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                if (logSkillCatalogErrors)
                {
                    Debug.LogWarning("DevPlayerPatchPanel: runtime base url is empty, cannot load skills catalog.");
                }
                yield break;
            }

            var skillsUrl = $"{baseUrl.TrimEnd('/')}/skills/catalog";

            using var req = UnityWebRequest.Get(skillsUrl);
            req.timeout = 10;
            req.SetRequestHeader("Authorization", $"Bearer {auth.Token}");
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                if (logSkillCatalogErrors)
                {
                    Debug.LogWarning($"DevPlayerPatchPanel: skills/catalog request failed. url={skillsUrl} status={req.responseCode} error={req.error}");
                }
                yield break;
            }

            var json = req.downloadHandler.text;
            SkillCatalogResponse response;
            try
            {
                response = JsonConvert.DeserializeObject<SkillCatalogResponse>(json);
            }
            catch (Exception e)
            {
                if (logSkillCatalogErrors)
                {
                    Debug.LogWarning($"DevPlayerPatchPanel: skills/catalog parse error: {e.Message}");
                }
                yield break;
            }

            var skillIds = new List<string>();
            if (response?.Skills != null)
            {
                for (int i = 0; i < response.Skills.Length; i++)
                {
                    var def = response.Skills[i];
                    if (def == null) continue;
                    var id = def.Id;
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        skillIds.Add(id);
                    }
                }
            }

            if (skillIds.Count == 0)
            {
                if (logSkillCatalogErrors)
                {
                    Debug.LogWarning("DevPlayerPatchPanel: skills catalog is empty.");
                }
                yield break;
            }

            PopulateSkillDropdowns(skillIds);
            PrefillFromProfile();
        }

        private IEnumerator LoadDebugPlayerSnapshotRoutine()
        {
            // Ждём, пока есть активный ран (connect_ok + instance_start).
            while (_net != null && (!_net.IsConnected || !_net.HasInstance))
            {
                yield return null;
            }

            var profile = GameRoot.Instance?.Services?.Get<IProfileService>();
            if (profile == null || profile.CurrentAuth == null)
            {
                if (logDebugSnapshotErrors)
                {
                    Debug.LogWarning("DevPlayerPatchPanel: profile or auth is missing, cannot call /debug/player.");
                }
                yield break;
            }

            var auth = profile.CurrentAuth;
            var baseUrl = ResolveRuntimeBaseUrl();
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                if (logDebugSnapshotErrors)
                {
                    Debug.LogWarning("DevPlayerPatchPanel: runtime base url is empty, cannot call /debug/player.");
                }
                yield break;
            }

            var url = $"{baseUrl.TrimEnd('/')}/debug/player";
            if (!string.IsNullOrWhiteSpace(profile.SelectedCharacterId))
            {
                url += $"?characterId={profile.SelectedCharacterId}";
            }

            using var req = UnityWebRequest.Get(url);
            req.timeout = 10;
            req.SetRequestHeader("Authorization", $"Bearer {auth.Token}");

            Debug.Log($"[DevDebug] DevPlayerPatchPanel: GET {url} (/debug/player)");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                if (logDebugSnapshotErrors)
                {
                    Debug.LogWarning($"DevPlayerPatchPanel: /debug/player request failed. url={url} status={req.responseCode} error={req.error} body={req.downloadHandler?.text}");
                }
                yield break;
            }

            var json = req.downloadHandler.text;
            DebugPlayerResponse response;
            try
            {
                response = JsonConvert.DeserializeObject<DebugPlayerResponse>(json);
            }
            catch (Exception e)
            {
                if (logDebugSnapshotErrors)
                {
                    Debug.LogWarning($"DevPlayerPatchPanel: /debug/player parse error: {e.Message} body={json}");
                }
                yield break;
            }

            if (response == null || !response.Ok)
            {
                if (logDebugSnapshotErrors)
                {
                    Debug.LogWarning($"DevPlayerPatchPanel: /debug/player returned error ok={response?.Ok} error={response?.Error}");
                }
                yield break;
            }

            ApplyDebugPlayerSnapshot(response);
        }

        private AuthContext ResolveAuth()
        {
            var profile = GameRoot.Instance?.Services?.Get<IProfileService>();
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

        private string ResolveRuntimeBaseUrl()
        {
            var connector = UnityEngine.Object.FindFirstObjectByType<DVBARPG.Game.Network.NetworkRunConnector>();
            if (connector == null) return "http://127.0.0.1:8080";

            var serverUrlField = typeof(DVBARPG.Game.Network.NetworkRunConnector)
                .GetField("serverUrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
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

        private void PopulateSkillDropdowns(List<string> skillIds)
        {
            PopulateDropdown(attackSkillDropdown, skillIds);
            PopulateDropdown(supportASkillDropdown, skillIds);
            PopulateDropdown(supportBSkillDropdown, skillIds);

            // Заполняем тем же каталогом дропдауны в трёх скилл‑слотах.
            if (skillSlots != null)
            {
                for (int i = 0; i < skillSlots.Length; i++)
                {
                    var slot = skillSlots[i];
                    if (slot == null || slot.skillDropdown == null) continue;
                    PopulateDropdown(slot.skillDropdown, skillIds);
                }
            }
        }

        private static void PopulateDropdown(TMP_Dropdown dropdown, List<string> skillIds)
        {
            if (dropdown == null || skillIds == null) return;

            dropdown.ClearOptions();

            var options = new List<TMP_Dropdown.OptionData>
            {
                new TMP_Dropdown.OptionData("<none>")
            };

            for (int i = 0; i < skillIds.Count; i++)
            {
                options.Add(new TMP_Dropdown.OptionData(skillIds[i]));
            }

            dropdown.AddOptions(options);
            dropdown.value = 0;
        }

        private void PrefillFromProfile()
        {
            var profile = GameRoot.Instance?.Services?.Get<IProfileService>();
            if (profile == null) return;

            var skillsLen = profile.ServerSkills != null ? profile.ServerSkills.Length : 0;
            var hasLoadout = profile.ServerLoadout != null;
            var hasSpeed = profile.BaseMoveSpeed > 0f;
            Debug.Log($"[DevDebug] PrefillFromProfile: hasLoadout={hasLoadout} skills={skillsLen} baseMoveSpeed={profile.BaseMoveSpeed}");

            // Если данных ещё нет — не помечаем как префиллнутый, чтобы повторить попытку позже.
            if (!hasLoadout && skillsLen == 0 && !hasSpeed)
            {
                return;
            }

            _hasPrefilledFromProfile = true;

            // 1) moveSpeed из профиля
            if (moveSpeedSlider != null && profile.BaseMoveSpeed > 0f)
            {
                moveSpeedSlider.value = profile.BaseMoveSpeed;
            }

            var loadout = profile.ServerLoadout;
            var serverSkills = profile.ServerSkills ?? Array.Empty<RuntimeSkillSnapshot>();
            if (loadout == null && serverSkills.Length == 0)
            {
                return;
            }

            // 2) Заполнить dropdown'ы лоадаута по ServerLoadout (если есть)
            if (loadout != null)
            {
                SetDropdownToSkillId(attackSkillDropdown, loadout.AttackSkillId);
                SetDropdownToSkillId(supportASkillDropdown, loadout.SupportASkillId);
                SetDropdownToSkillId(supportBSkillDropdown, loadout.SupportBSkillId);

                if (attackEnabledToggle != null) attackEnabledToggle.isOn = true;
                if (supportAEnabledToggle != null) supportAEnabledToggle.isOn = true;
                if (supportBEnabledToggle != null) supportBEnabledToggle.isOn = true;

                if (movementSlotSupportA != null || movementSlotSupportB != null)
                {
                    if (string.Equals(loadout.MovementSkillId, loadout.SupportASkillId, StringComparison.OrdinalIgnoreCase))
                    {
                        if (movementSlotSupportA != null) movementSlotSupportA.isOn = true;
                        if (movementSlotSupportB != null) movementSlotSupportB.isOn = false;
                    }
                    else if (string.Equals(loadout.MovementSkillId, loadout.SupportBSkillId, StringComparison.OrdinalIgnoreCase))
                    {
                        if (movementSlotSupportA != null) movementSlotSupportA.isOn = false;
                        if (movementSlotSupportB != null) movementSlotSupportB.isOn = true;
                    }
                    else
                    {
                        if (movementSlotSupportA != null) movementSlotSupportA.isOn = false;
                        if (movementSlotSupportB != null) movementSlotSupportB.isOn = false;
                    }
                }
            }

            // 3) Предзаполнить три ячейки skills текущим билдом (attack / supportA / supportB) + уровнями/модификаторами
            if (skillSlots == null || skillSlots.Length == 0) return;

            RuntimeSkillSnapshot FindSkill(string skillId)
            {
                if (string.IsNullOrWhiteSpace(skillId)) return null;
                for (int i = 0; i < serverSkills.Length; i++)
                {
                    var s = serverSkills[i];
                    if (s == null) continue;
                    if (string.Equals(s.SkillId, skillId, StringComparison.OrdinalIgnoreCase))
                        return s;
                }
                return null;
            }

            void SetSkillSlot(SkillSlotUi slot, string skillId)
            {
                if (slot == null || string.IsNullOrWhiteSpace(skillId)) return;

                // сначала пробуем выбрать в dropdown
                if (slot.skillDropdown != null && slot.skillDropdown.options != null && slot.skillDropdown.options.Count > 0)
                {
                    var idx = FindOptionIndex(slot.skillDropdown, skillId);
                    if (idx >= 0)
                    {
                        slot.skillDropdown.value = idx;
                    }
                    else if (slot.skillId != null)
                    {
                        slot.skillId.text = skillId;
                    }
                }
                else if (slot.skillId != null)
                {
                    slot.skillId.text = skillId;
                }

                var snap = FindSkill(skillId);
                if (snap != null)
                {
                    if (slot.level != null)
                    {
                        slot.level.text = snap.Level > 0 ? snap.Level.ToString() : "1";
                    }

                    if (slot.modifiersJson != null && !string.IsNullOrWhiteSpace(snap.ModifiersJson))
                    {
                        slot.modifiersJson.text = snap.ModifiersJson;

                        try
                        {
                            var obj = JObject.Parse(snap.ModifiersJson);
                            SetTagFromModifiers(slot.tagSpell, obj, "tag.spell");
                            SetTagFromModifiers(slot.tagProjectile, obj, "tag.projectile");
                            SetTagFromModifiers(slot.tagMelee, obj, "tag.melee");
                        }
                        catch (Exception)
                        {
                            // ignore parse errors
                        }
                    }
                }
                else
                {
                    if (slot.level != null)
                    {
                        slot.level.text = "1";
                    }
                }

                SkillIconProvider.ApplyIcon(slot.skillIcon, skillId);
            }

            if (loadout != null)
            {
                if (skillSlots.Length > 0) SetSkillSlot(skillSlots[0], loadout.AttackSkillId);
                if (skillSlots.Length > 1) SetSkillSlot(skillSlots[1], loadout.SupportASkillId);
                if (skillSlots.Length > 2) SetSkillSlot(skillSlots[2], loadout.SupportBSkillId);
            }
            else if (serverSkills.Length > 0)
            {
                // fallback: берём первые три скилла из snapshot
                for (int i = 0; i < skillSlots.Length && i < serverSkills.Length; i++)
                {
                    SetSkillSlot(skillSlots[i], serverSkills[i]?.SkillId);
                }
            }
        }

        private static void SetTagFromModifiers(Toggle toggle, JObject obj, string key)
        {
            if (toggle == null || obj == null || string.IsNullOrWhiteSpace(key)) return;
            if (!obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out var value)) return;

            if (value.Type == JTokenType.Boolean)
            {
                toggle.isOn = value.Value<bool>();
            }
            else if (value.Type == JTokenType.Integer || value.Type == JTokenType.Float)
            {
                toggle.isOn = value.Value<float>() != 0f;
            }
        }

        private static void SetDropdownToSkillId(TMP_Dropdown dropdown, string skillId)
        {
            if (dropdown == null) return;
            if (string.IsNullOrWhiteSpace(skillId)) return;

            var idx = FindOptionIndex(dropdown, skillId);
            if (idx >= 0)
            {
                dropdown.value = idx;
            }
        }

        private static int FindOptionIndex(TMP_Dropdown dropdown, string skillId)
        {
            if (dropdown == null || dropdown.options == null) return -1;
            for (int i = 0; i < dropdown.options.Count; i++)
            {
                var text = dropdown.options[i].text;
                if (string.Equals(text, skillId, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        private void ApplyDebugPatch()
        {
            if (_net == null || !_net.IsConnected)
            {
                SetStatus("Нет активной UDP‑сессии (connect/start не выполнены).");
                return;
            }

            // NetworkSessionRunner сам проверяет _connectOk/_instanceStarted в Send,
            // но для UX можно показать сообщение заранее.
            var cmd = new CmdDebug
            {
                Type = "debug_patch_player",
                StatPatch = BuildStatPatchFromUi(),
                Skills = BuildSkillsFromUi(),
                CombatLoadout = BuildCombatLoadoutFromUi(),
                ReplaceSkills = replaceSkillsToggle != null ? replaceSkillsToggle.isOn : (bool?)null
            };

            var statCount = cmd.StatPatch != null ? cmd.StatPatch.Count : 0;
            var skillsCount = cmd.Skills != null ? cmd.Skills.Count : 0;
            Debug.Log($"[DevDebug] ApplyDebugPatch: statPatchCount={statCount} skillsCount={skillsCount} hasLoadout={cmd.CombatLoadout != null} replaceSkills={cmd.ReplaceSkills}");

            _net.Send(cmd);
            SetStatus("Debug patch отправлен. Проверь снапшот (урон/кд/скиллы).");
        }

        private Dictionary<string, float> BuildStatPatchFromUi()
        {
            // Если "Сбросить в дефолт" — вернём пустой объект, чтобы сервер сбросил baseline.
            if (resetStatsToggle != null && resetStatsToggle.isOn)
            {
                return new Dictionary<string, float>();
            }

            var dict = new Dictionary<string, float>();

            AddIfAssigned(dict, "attackPower", attackPowerSlider);
            AddIfAssigned(dict, "spellPower", spellPowerSlider);
            AddIfAssigned(dict, "maxHp", maxHpSlider);
            AddIfAssigned(dict, "moveSpeed", moveSpeedSlider);
            AddIfAssigned(dict, "critChance", critChanceSlider);
            AddIfAssigned(dict, "critMult", critMultSlider);
            AddIfAssigned(dict, "cooldownReduction", cooldownReductionSlider);
            AddIfAssigned(dict, "statusChance", statusChanceSlider);
            AddIfAssigned(dict, "resistPhys", resistPhysSlider);
            AddIfAssigned(dict, "resistElemental", resistElementalSlider);
            AddIfAssigned(dict, "projectileSpeedBonus", projectileSpeedBonusSlider);
            AddIfAssigned(dict, "aoeRadiusBonus", aoeRadiusBonusSlider);

            // Теговые бонусы
            AddIfAssigned(dict, "tagBonus.spell", tagBonusSpellSlider);
            AddIfAssigned(dict, "tagBonus.projectile", tagBonusProjectileSlider);

            return dict.Count > 0 ? dict : null;
        }

        private static void AddIfAssigned(Dictionary<string, float> dict, string key, Slider slider)
        {
            if (slider == null) return;
            dict[key] = slider.value;
        }

        private void ApplyDebugPlayerSnapshot(DebugPlayerResponse snap)
        {
            if (snap == null || _hasAppliedDebugSnapshot) return;

            _hasAppliedDebugSnapshot = true;

            // 1) Статы
            var stats = snap.Stats;
            if (stats != null)
            {
                SetSliderFromStats(stats, "attackPower", attackPowerSlider);
                SetSliderFromStats(stats, "spellPower", spellPowerSlider);
                SetSliderFromStats(stats, "maxHp", maxHpSlider);
                SetSliderFromStats(stats, "moveSpeed", moveSpeedSlider);
                SetSliderFromStats(stats, "critChance", critChanceSlider);
                SetSliderFromStats(stats, "critMult", critMultSlider);
                SetSliderFromStats(stats, "cooldownReduction", cooldownReductionSlider);
                SetSliderFromStats(stats, "statusChance", statusChanceSlider);
                SetSliderFromStats(stats, "resistPhys", resistPhysSlider);
                SetSliderFromStats(stats, "resistElemental", resistElementalSlider);
                SetSliderFromStats(stats, "projectileSpeedBonus", projectileSpeedBonusSlider);
                SetSliderFromStats(stats, "aoeRadiusBonus", aoeRadiusBonusSlider);

                SetSliderFromStats(stats, "tagBonus.spell", tagBonusSpellSlider);
                SetSliderFromStats(stats, "tagBonus.projectile", tagBonusProjectileSlider);
            }

            // 2) Лоадаут из debug snapshot (если есть)
            var loadout = snap.CombatLoadout;
            if (loadout != null)
            {
                SetDropdownToSkillId(attackSkillDropdown, loadout.AttackSkillId);
                SetDropdownToSkillId(supportASkillDropdown, loadout.SupportASkillId);
                SetDropdownToSkillId(supportBSkillDropdown, loadout.SupportBSkillId);

                if (attackEnabledToggle != null) attackEnabledToggle.isOn = loadout.AttackEnabled;
                if (supportAEnabledToggle != null) supportAEnabledToggle.isOn = loadout.SupportAEnabled;
                if (supportBEnabledToggle != null) supportBEnabledToggle.isOn = loadout.SupportBEnabled;

                if (movementSlotSupportA != null || movementSlotSupportB != null)
                {
                    if (string.Equals(loadout.MovementSlot, "supportA", StringComparison.OrdinalIgnoreCase))
                    {
                        if (movementSlotSupportA != null) movementSlotSupportA.isOn = true;
                        if (movementSlotSupportB != null) movementSlotSupportB.isOn = false;
                    }
                    else if (string.Equals(loadout.MovementSlot, "supportB", StringComparison.OrdinalIgnoreCase))
                    {
                        if (movementSlotSupportA != null) movementSlotSupportA.isOn = false;
                        if (movementSlotSupportB != null) movementSlotSupportB.isOn = true;
                    }
                    else
                    {
                        if (movementSlotSupportA != null) movementSlotSupportA.isOn = false;
                        if (movementSlotSupportB != null) movementSlotSupportB.isOn = false;
                    }
                }
            }

            // 3) Три ячейки skills из snapshot
            if (skillSlots == null || skillSlots.Length == 0) return;

            var skills = snap.Skills ?? Array.Empty<DebugPlayerSkill>();

            void SetSkillSlotFromDebug(SkillSlotUi slot, DebugPlayerSkill s)
            {
                if (slot == null || s == null || string.IsNullOrWhiteSpace(s.SkillId)) return;

                // dropdown или текстовый id
                if (slot.skillDropdown != null && slot.skillDropdown.options != null && slot.skillDropdown.options.Count > 0)
                {
                    var idx = FindOptionIndex(slot.skillDropdown, s.SkillId);
                    if (idx >= 0)
                    {
                        slot.skillDropdown.value = idx;
                    }
                    else if (slot.skillId != null)
                    {
                        slot.skillId.text = s.SkillId;
                    }
                }
                else if (slot.skillId != null)
                {
                    slot.skillId.text = s.SkillId;
                }

                if (slot.level != null)
                {
                    var lvl = s.Level > 0 ? s.Level : 1;
                    slot.level.text = lvl.ToString();
                }

                if (slot.modifiersJson != null && s.Modifiers != null)
                {
                    var json = s.Modifiers.ToString(Formatting.None);
                    slot.modifiersJson.text = json;

                    try
                    {
                        SetTagFromModifiers(slot.tagSpell, s.Modifiers, "tag.spell");
                        SetTagFromModifiers(slot.tagProjectile, s.Modifiers, "tag.projectile");
                        SetTagFromModifiers(slot.tagMelee, s.Modifiers, "tag.melee");
                    }
                    catch (Exception)
                    {
                        // ignore
                    }
                }

                SkillIconProvider.ApplyIcon(slot.skillIcon, s.SkillId);
            }

            for (int i = 0; i < skillSlots.Length && i < skills.Length && i < 3; i++)
            {
                SetSkillSlotFromDebug(skillSlots[i], skills[i]);
            }
        }

        private static void SetSliderFromStats(Dictionary<string, float> stats, string key, Slider slider)
        {
            if (slider == null || stats == null || string.IsNullOrWhiteSpace(key)) return;
            if (!stats.TryGetValue(key, out var value)) return;
            slider.value = value;
        }

        private List<SkillInstance> BuildSkillsFromUi()
        {
            if (skillSlots == null || skillSlots.Length == 0)
                return null;

            var list = new List<SkillInstance>();

            foreach (var slot in skillSlots)
            {
                if (slot == null) continue;

                // 1) id скилла — сначала из dropdown, если он есть, иначе из текстового поля
                string id = null;
                if (slot.skillDropdown != null && slot.skillDropdown.options != null && slot.skillDropdown.options.Count > 0)
                {
                    var idx = slot.skillDropdown.value;
                    if (idx > 0 && idx < slot.skillDropdown.options.Count) // 0 — <none>
                    {
                        id = slot.skillDropdown.options[idx].text?.Trim();
                    }
                }

                if (string.IsNullOrWhiteSpace(id) && slot.skillId != null)
                {
                    id = slot.skillId.text?.Trim();
                }

                if (string.IsNullOrWhiteSpace(id))
                    continue;

                // 2) уровень
                int level = 1;
                if (slot.level != null && !string.IsNullOrWhiteSpace(slot.level.text))
                {
                    int.TryParse(slot.level.text, out level);
                    if (level <= 0) level = 1;
                }

                // 3) модификаторы: сначала теги, потом доп. JSON
                JObject modifiersObj = null;

                void AddTagIfOn(Toggle toggle, string tagKey)
                {
                    if (toggle != null && toggle.isOn)
                    {
                        modifiersObj ??= new JObject();
                        // Семантика: включённый тег = 1.0 (флаг)
                        modifiersObj[tagKey] = 1.0f;
                    }
                }

                AddTagIfOn(slot.tagSpell, "tag.spell");
                AddTagIfOn(slot.tagProjectile, "tag.projectile");
                AddTagIfOn(slot.tagMelee, "tag.melee");

                if (slot.modifiersJson != null && !string.IsNullOrWhiteSpace(slot.modifiersJson.text))
                {
                    try
                    {
                        var extra = JToken.Parse(slot.modifiersJson.text);
                        if (extra is JObject extraObj)
                        {
                            if (modifiersObj == null)
                            {
                                modifiersObj = extraObj;
                            }
                            else
                            {
                                // merge без перезаписи уже выставленных тегов
                                foreach (var prop in extraObj)
                                {
                                    if (!modifiersObj.ContainsKey(prop.Key))
                                    {
                                        modifiersObj[prop.Key] = prop.Value;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Некорректный JSON — просто игнорируем модификаторы из текстового поля
                    }
                }

                list.Add(new SkillInstance
                {
                    SkillId = id,
                    Level = level,
                    Modifiers = modifiersObj
                });
            }

            return list.Count > 0 ? list : null;
        }

        private CombatLoadout BuildCombatLoadoutFromUi()
        {
            if (attackSkillDropdown == null &&
                supportASkillDropdown == null &&
                supportBSkillDropdown == null &&
                attackEnabledToggle == null &&
                supportAEnabledToggle == null &&
                supportBEnabledToggle == null &&
                movementSlotSupportA == null &&
                movementSlotSupportB == null)
            {
                return null;
            }

            var loadout = new CombatLoadout();

            if (attackSkillDropdown != null)
                loadout.AttackSkillId = GetDropdownValue(attackSkillDropdown);

            if (supportASkillDropdown != null)
                loadout.SupportASkillId = GetDropdownValue(supportASkillDropdown);

            if (supportBSkillDropdown != null)
                loadout.SupportBSkillId = GetDropdownValue(supportBSkillDropdown);

            if (attackEnabledToggle != null)
                loadout.AttackEnabled = attackEnabledToggle.isOn;

            if (supportAEnabledToggle != null)
                loadout.SupportAEnabled = supportAEnabledToggle.isOn;

            if (supportBEnabledToggle != null)
                loadout.SupportBEnabled = supportBEnabledToggle.isOn;

            string movementSlot = null;
            if (movementSlotSupportA != null && movementSlotSupportA.isOn)
                movementSlot = "supportA";
            else if (movementSlotSupportB != null && movementSlotSupportB.isOn)
                movementSlot = "supportB";

            loadout.MovementSlot = movementSlot;

            // Если вообще ничего не заполнено — вернём null
            if (string.IsNullOrWhiteSpace(loadout.AttackSkillId) &&
                string.IsNullOrWhiteSpace(loadout.SupportASkillId) &&
                string.IsNullOrWhiteSpace(loadout.SupportBSkillId) &&
                loadout.AttackEnabled == null &&
                loadout.SupportAEnabled == null &&
                loadout.SupportBEnabled == null &&
                string.IsNullOrWhiteSpace(loadout.MovementSlot))
            {
                return null;
            }

            return loadout;
        }

        private static string GetDropdownValue(TMP_Dropdown dropdown)
        {
            if (dropdown == null || dropdown.options == null || dropdown.options.Count == 0)
                return null;
            var idx = dropdown.value;
            if (idx < 0 || idx >= dropdown.options.Count) return null;
            var text = dropdown.options[idx].text;
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        private void SetStatus(string msg)
        {
            if (statusText != null) statusText.text = msg;
        }

        private static Key ToInputKey(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.F1: return Key.F1;
                case KeyCode.F2: return Key.F2;
                case KeyCode.F3: return Key.F3;
                case KeyCode.F4: return Key.F4;
                case KeyCode.F5: return Key.F5;
                case KeyCode.F6: return Key.F6;
                case KeyCode.F7: return Key.F7;
                case KeyCode.F8: return Key.F8;
                case KeyCode.F9: return Key.F9;
                case KeyCode.F10: return Key.F10;
                case KeyCode.F11: return Key.F11;
                case KeyCode.F12: return Key.F12;
                default: return Key.F1;
            }
        }
    }
}
#endif