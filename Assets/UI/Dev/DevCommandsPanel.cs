using System;
using System.Collections.Generic;
using DVBARPG.Net.Commands;
using DVBARPG.Net.Network;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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

        private NetworkSessionRunner _net;

        private void Awake()
        {
            SetVisible(showOnStart);
        }

        private void OnEnable()
        {
            var session = DVBARPG.Core.GameRoot.Instance.Services.Get<DVBARPG.Core.Services.ISessionService>();
            _net = session as NetworkSessionRunner;

            if (toggleButton != null) toggleButton.onClick.AddListener(ToggleVisible);
            if (clearMobsButton != null) clearMobsButton.onClick.AddListener(OnClearMobs);
            if (spawnMeleeButton != null) spawnMeleeButton.onClick.AddListener(OnSpawnMelee);
            if (spawnRangedButton != null) spawnRangedButton.onClick.AddListener(OnSpawnRanged);
            if (spawnDummyButton != null) spawnDummyButton.onClick.AddListener(OnSpawnDummy);
            if (immortalOnButton != null) immortalOnButton.onClick.AddListener(OnImmortalOn);
            if (immortalOffButton != null) immortalOffButton.onClick.AddListener(OnImmortalOff);
            if (patchPlayerButton != null) patchPlayerButton.onClick.AddListener(OnPatchPlayer);
        }

        private void OnDisable()
        {
            if (toggleButton != null) toggleButton.onClick.RemoveListener(ToggleVisible);
            if (clearMobsButton != null) clearMobsButton.onClick.RemoveListener(OnClearMobs);
            if (spawnMeleeButton != null) spawnMeleeButton.onClick.RemoveListener(OnSpawnMelee);
            if (spawnRangedButton != null) spawnRangedButton.onClick.RemoveListener(OnSpawnRanged);
            if (spawnDummyButton != null) spawnDummyButton.onClick.RemoveListener(OnSpawnDummy);
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
        private void OnImmortalOn() => SendDebug("debug_immortal_on");
        private void OnImmortalOff() => SendDebug("debug_immortal_off");
        private void OnPatchPlayer() => SendDebug(BuildPatchCommand());

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
