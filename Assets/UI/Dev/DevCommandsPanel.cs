using System;
using System.Collections.Generic;
using DVBARPG.Net.Commands;
using DVBARPG.Net.Network;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace DVBARPG.UI.Dev
{
    public sealed class DevCommandsPanel : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("Корень панели (включается/выключается).")]
        [SerializeField] private GameObject contentRoot;
        [Tooltip("Кнопка-переключатель панели.")]
        [SerializeField] private Button toggleButton;
        [Tooltip("Кнопка: очистить мобов.")]
        [SerializeField] private Button clearMobsButton;
        [Tooltip("Кнопка: спавн мили моба.")]
        [SerializeField] private Button spawnMeleeButton;
        [Tooltip("Кнопка: спавн рейндж моба.")]
        [SerializeField] private Button spawnRangedButton;
        [Tooltip("Кнопка: спавн манекена.")]
        [SerializeField] private Button spawnDummyButton;
        [Tooltip("Кнопка: спавн моба выбранного типа из dropdown.")]
        [SerializeField] private Button spawnSelectedButton;
        [Tooltip("Dropdown выбора тега мобов для спавна (например goblins/spiders).")]
        [SerializeField] private Dropdown spawnMonsterTypeDropdown;
        [Tooltip("Кнопка: бессмертие ВКЛ.")]
        [SerializeField] private Button immortalOnButton;
        [Tooltip("Кнопка: бессмертие ВЫКЛ.")]
        [SerializeField] private Button immortalOffButton;
        [Tooltip("Кнопка: применить патч игрока.")]
        [SerializeField] private Button patchPlayerButton;

        [Header("Patch Player")]
        [Tooltip("JSON для statPatch (например {\"moveSpeed\":6,\"attackPower\":20}).")]
        [SerializeField] private InputField statPatchInput;
        [Tooltip("JSON для skills (например [{\"skillId\":\"slash\",\"level\":1,\"modifiers\":{}}]).")]
        [SerializeField] private InputField skillsInput;
        [Tooltip("JSON для combatLoadout (например {\"attackSkillId\":\"slash\",\"supportASkillId\":\"guard_break\",\"supportBSkillId\":\"dash\"}).")]
        [SerializeField] private InputField loadoutInput;
        [Tooltip("Заменять весь список скиллов (true) или merge (false).")]
        [SerializeField] private Toggle replaceSkillsToggle;

        [Header("Поведение")]
        [Tooltip("Показывать панель при старте.")]
        [SerializeField] private bool showOnStart = true;
        [Tooltip("Горячая клавиша для показа/скрытия.")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;
        [Tooltip("Смещение точки спавна мобов от игрока вперёд.")]
        [SerializeField] private float spawnForwardOffset = 2.0f;
        [Tooltip("Базовый URL backend для загрузки типов мобов.")]
        [SerializeField] private string backendBaseUrl = "http://127.0.0.1:8000";
        [Tooltip("Шаблон эндпоинта монстров карты. {mapId} будет заменён на имя сцены.")]
        [SerializeField] private string mapMonstersEndpointTemplate = "/api/content/maps/{mapId}/monsters";
        [Tooltip("Шаблон эндпоинта геометрии карты (для получения enemyTags карты).")]
        [SerializeField] private string mapGeometryEndpointTemplate = "/api/content/maps/{mapId}";
        [Tooltip("Таймаут HTTP при загрузке типов мобов.")]
        [SerializeField] private int monsterTypesHttpTimeoutSec = 8;

        private NetworkSessionRunner _net;

        private void Awake()
        {
            SetVisible(showOnStart);
        }

        private void OnEnable()
        {
            Debug.Log("[DevCommandsPanel] OnEnable");
            var root = DVBARPG.Core.GameRoot.Instance;
            if (root == null || root.Services == null) return;
            if (!root.Services.TryGet<DVBARPG.Core.Services.ISessionService>(out var session)) return;
            _net = session as NetworkSessionRunner;

            if (toggleButton != null) toggleButton.onClick.AddListener(ToggleVisible);
            if (clearMobsButton != null) clearMobsButton.onClick.AddListener(OnClearMobs);
            if (spawnMeleeButton != null) spawnMeleeButton.onClick.AddListener(OnSpawnMelee);
            if (spawnRangedButton != null) spawnRangedButton.onClick.AddListener(OnSpawnRanged);
            if (spawnDummyButton != null) spawnDummyButton.onClick.AddListener(OnSpawnDummy);
            if (spawnSelectedButton != null) spawnSelectedButton.onClick.AddListener(OnSpawnSelected);
            if (immortalOnButton != null) immortalOnButton.onClick.AddListener(OnImmortalOn);
            if (immortalOffButton != null) immortalOffButton.onClick.AddListener(OnImmortalOff);
            if (patchPlayerButton != null) patchPlayerButton.onClick.AddListener(OnPatchPlayer);

            EnsureDefaultSpawnOptions();
            Debug.Log("[DevCommandsPanel] Starting monster IDs load from backend");
            StartCoroutine(LoadSpawnMonsterIdsFromBackend());
        }

        private void OnDisable()
        {
            if (toggleButton != null) toggleButton.onClick.RemoveListener(ToggleVisible);
            if (clearMobsButton != null) clearMobsButton.onClick.RemoveListener(OnClearMobs);
            if (spawnMeleeButton != null) spawnMeleeButton.onClick.RemoveListener(OnSpawnMelee);
            if (spawnRangedButton != null) spawnRangedButton.onClick.RemoveListener(OnSpawnRanged);
            if (spawnDummyButton != null) spawnDummyButton.onClick.RemoveListener(OnSpawnDummy);
            if (spawnSelectedButton != null) spawnSelectedButton.onClick.RemoveListener(OnSpawnSelected);
            if (immortalOnButton != null) immortalOnButton.onClick.RemoveListener(OnImmortalOn);
            if (immortalOffButton != null) immortalOffButton.onClick.RemoveListener(OnImmortalOff);
            if (patchPlayerButton != null) patchPlayerButton.onClick.RemoveListener(OnPatchPlayer);
        }

        private void Update()
        {
            if (toggleKey != KeyCode.None && Keyboard.current != null && Keyboard.current[ToInputKey(toggleKey)].wasPressedThisFrame)
            {
                ToggleVisible();
            }
        }

        private void OnClearMobs() => SendDebug("debug_clear_mobs");
        private void OnSpawnMelee() => SendDebug("debug_spawn_melee", usePlayerPos: true);
        private void OnSpawnRanged() => SendDebug("debug_spawn_ranged", usePlayerPos: true);
        private void OnSpawnDummy() => SendDebug("debug_spawn_dummy", usePlayerPos: true);
        private void OnSpawnSelected() => SpawnSelectedByMonsterId();
        private void OnImmortalOn() => SendDebug("debug_immortal_on");
        private void OnImmortalOff() => SendDebug("debug_immortal_off");
        private void OnPatchPlayer() => SendDebug(BuildPatchCommand());

        private string GetSelectedMonsterId()
        {
            if (spawnMonsterTypeDropdown == null || spawnMonsterTypeDropdown.options == null || spawnMonsterTypeDropdown.options.Count == 0)
                return string.Empty;
            var index = Mathf.Clamp(spawnMonsterTypeDropdown.value, 0, spawnMonsterTypeDropdown.options.Count - 1);
            return (spawnMonsterTypeDropdown.options[index]?.text ?? string.Empty).Trim().ToLowerInvariant();
        }

        private void SpawnSelectedByMonsterId()
        {
            var monsterId = GetSelectedMonsterId();
            if (string.IsNullOrWhiteSpace(monsterId))
            {
                SendDebug("debug_spawn_melee", usePlayerPos: true);
                return;
            }

            if (monsterId == "dummy" || monsterId == "training_dummy" || monsterId == "манекен" || monsterId == "maneken")
            {
                SendDebug("debug_spawn_dummy", usePlayerPos: true);
                return;
            }

            var cmd = new CmdDebug
            {
                Type = "debug_spawn_monster_id",
                MonsterId = monsterId
            };
            ApplyPosition(cmd, usePlayerPos: true);
            Debug.Log($"[DevCommandsPanel] Spawn by monsterId: {monsterId}");
            SendDebug(cmd);
        }

        private void SendDebug(string type, bool usePlayerPos = false)
        {
            if (_net == null) return;

            var cmd = new CmdDebug { Type = type };
            ApplyPosition(cmd, usePlayerPos);
            _net.Send(cmd);
        }

        private void SendDebug(CmdDebug cmd)
        {
            if (_net == null || cmd == null) return;
            _net.Send(cmd);
        }

        private CmdDebug BuildPatchCommand()
        {
            var cmd = new CmdDebug { Type = "debug_patch_player" };
            ApplyPosition(cmd, usePlayerPos: false);

            if (statPatchInput != null && !string.IsNullOrWhiteSpace(statPatchInput.text))
            {
                cmd.StatPatch = JsonConvert.DeserializeObject<Dictionary<string, float>>(statPatchInput.text);
            }

            if (skillsInput != null && !string.IsNullOrWhiteSpace(skillsInput.text))
            {
                cmd.Skills = JsonConvert.DeserializeObject<List<SkillInstance>>(skillsInput.text);
            }

            if (loadoutInput != null && !string.IsNullOrWhiteSpace(loadoutInput.text))
            {
                cmd.CombatLoadout = JsonConvert.DeserializeObject<CombatLoadout>(loadoutInput.text);
            }

            if (replaceSkillsToggle != null)
            {
                cmd.ReplaceSkills = replaceSkillsToggle.isOn;
            }

            return cmd;
        }

        private void ApplyPosition(CmdDebug cmd, bool usePlayerPos)
        {
            if (!usePlayerPos) return;
            var tr = DVBARPG.Game.Player.NetworkPlayerReplicator.PlayerTransform;
            if (tr != null)
            {
                var pos = tr.position + tr.forward * spawnForwardOffset;
                cmd.HasPosition = true;
                cmd.Position = new Vector2(pos.x, pos.z);
            }
        }

        private void ToggleVisible()
        {
            if (contentRoot == null) return;
            contentRoot.SetActive(!contentRoot.activeSelf);
        }

        private void SetVisible(bool visible)
        {
            if (contentRoot != null) contentRoot.SetActive(visible);
        }

        private void EnsureDefaultSpawnOptions()
        {
            if (spawnMonsterTypeDropdown == null) return;
            if (spawnMonsterTypeDropdown.options != null && spawnMonsterTypeDropdown.options.Count > 0) return;

            spawnMonsterTypeDropdown.ClearOptions();
            spawnMonsterTypeDropdown.AddOptions(new List<string> { "dummy" });
            spawnMonsterTypeDropdown.value = 0;
            spawnMonsterTypeDropdown.RefreshShownValue();
        }

        private System.Collections.IEnumerator LoadSpawnMonsterIdsFromBackend()
        {
            if (spawnMonsterTypeDropdown == null)
            {
                Debug.LogWarning("[DevCommandsPanel] spawnMonsterTypeDropdown is null");
                yield break;
            }

            var mapId = SceneManager.GetActiveScene().name;
            if (string.IsNullOrWhiteSpace(mapId))
                mapId = "default";

            var mapGeometryPath = (mapGeometryEndpointTemplate ?? string.Empty).Replace("{mapId}", mapId);
            var mapGeometryUrl = BuildUrl(backendBaseUrl, mapGeometryPath);
            if (string.IsNullOrWhiteSpace(mapGeometryUrl))
            {
                Debug.LogWarning("[DevCommandsPanel] mapGeometryUrl is empty");
                yield break;
            }

            using var mapReq = UnityWebRequest.Get(mapGeometryUrl);
            mapReq.timeout = Mathf.Max(1, monsterTypesHttpTimeoutSec);
            yield return mapReq.SendWebRequest();
            if (mapReq.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[DevCommandsPanel] Failed to load map geometry. url={mapGeometryUrl} code={mapReq.responseCode} err={mapReq.error} body={mapReq.downloadHandler?.text}");
                yield break;
            }

            MapGeometryResponse mapInfo;
            try
            {
                mapInfo = JsonConvert.DeserializeObject<MapGeometryResponse>(mapReq.downloadHandler.text);
            }
            catch
            {
                Debug.LogWarning("[DevCommandsPanel] Failed to parse map geometry JSON");
                yield break;
            }

            if (mapInfo == null || !mapInfo.ok || mapInfo.enemyTags == null || mapInfo.enemyTags.Count == 0)
            {
                Debug.LogWarning($"[DevCommandsPanel] Map geometry has no enemyTags. mapId={mapId} body={mapReq.downloadHandler?.text}");
                yield break;
            }

            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            unique.Add("dummy");

            var mapMonstersPath = (mapMonstersEndpointTemplate ?? string.Empty).Replace("{mapId}", mapId);
            var monstersUrl = BuildUrl(backendBaseUrl, mapMonstersPath);
            if (string.IsNullOrWhiteSpace(monstersUrl))
            {
                Debug.LogWarning("[DevCommandsPanel] monstersUrl is empty");
                yield break;
            }

            var tagsQuery = string.Join(",", mapInfo.enemyTags);
            monstersUrl += monstersUrl.Contains("?") ? "&" : "?";
            monstersUrl += "tags=" + UnityWebRequest.EscapeURL(tagsQuery);

            Debug.Log($"[DevCommandsPanel] Loading map monsters by id: {monstersUrl}");
            using var monstersReq = UnityWebRequest.Get(monstersUrl);
            monstersReq.timeout = Mathf.Max(1, monsterTypesHttpTimeoutSec);
            yield return monstersReq.SendWebRequest();
            if (monstersReq.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[DevCommandsPanel] Failed to load map monsters. url={monstersUrl} code={monstersReq.responseCode} err={monstersReq.error} body={monstersReq.downloadHandler?.text}");
                yield break;
            }

            MapMonstersResponse monsters;
            try
            {
                monsters = JsonConvert.DeserializeObject<MapMonstersResponse>(monstersReq.downloadHandler.text);
            }
            catch
            {
                Debug.LogWarning("[DevCommandsPanel] Failed to parse map monsters JSON");
                yield break;
            }

            if (monsters == null || !monsters.ok || monsters.monsters == null || monsters.monsters.Count == 0)
            {
                Debug.LogWarning($"[DevCommandsPanel] Monsters response invalid. body={monstersReq.downloadHandler?.text}");
                yield break;
            }

            var rawIds = new List<string>();
            for (var i = 0; i < monsters.monsters.Count; i++)
            {
                var id = monsters.monsters[i]?.id;
                if (string.IsNullOrWhiteSpace(id)) continue;
                var normalized = id.Trim().ToLowerInvariant();
                rawIds.Add(normalized);
                unique.Add(normalized);
            }

            if (unique.Count == 0)
                yield break;

            var options = new List<string>(unique);
            options.Sort(StringComparer.OrdinalIgnoreCase);

            Debug.Log($"[DevCommandsPanel] Monster IDs received: count={monsters.monsters.Count}, enemyTags=[{string.Join(", ", mapInfo.enemyTags)}], rawIds=[{string.Join(", ", rawIds)}], dropdownIds=[{string.Join(", ", options)}], mapId={mapId}");

            spawnMonsterTypeDropdown.ClearOptions();
            spawnMonsterTypeDropdown.AddOptions(options);
            spawnMonsterTypeDropdown.value = 0;
            spawnMonsterTypeDropdown.RefreshShownValue();
        }

        private static string BuildUrl(string baseUrl, string path)
        {
            if (string.IsNullOrWhiteSpace(baseUrl)) return string.Empty;
            if (string.IsNullOrWhiteSpace(path)) return baseUrl.TrimEnd('/');
            return $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
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

        [Serializable]
        private sealed class MapMonstersResponse
        {
            public bool ok;
            public List<MapMonsterRow> monsters;
        }

        [Serializable]
        private sealed class MapMonsterRow
        {
            public string id;
            public string type;
        }

        [Serializable]
        private sealed class MapGeometryResponse
        {
            public bool ok;
            public List<string> enemyTags;
        }
    }
}
