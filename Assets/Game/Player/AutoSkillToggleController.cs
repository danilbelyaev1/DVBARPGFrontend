using System;
using DVBARPG.Core;
using DVBARPG.Core.Services;
using DVBARPG.Game.Combat;
using DVBARPG.Net.Commands;
using DVBARPG.Net.Network;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DVBARPG.Game.Player
{
    public sealed class AutoSkillToggleController : MonoBehaviour
    {
        public event Action<string, bool> SlotChanged;

        [Header("Слоты")]
        [Tooltip("Идентификатор слота атаки (серверный).")]
        [SerializeField] private string attackSlotId = CombatSlots.Attack;
        [Tooltip("Идентификатор слота поддержки A (серверный).")]
        [SerializeField] private string supportASlotId = CombatSlots.SupportA;
        [Tooltip("Идентификатор слота поддержки B (серверный).")]
        [SerializeField] private string supportBSlotId = CombatSlots.SupportB;

        [Header("Горячие клавиши")]
        [Tooltip("Клавиша переключения автоатаки (ПК).")]
        [SerializeField] private KeyCode attackToggleKey = KeyCode.Q;
        [Tooltip("Клавиша переключения поддержки A (ПК).")]
        [SerializeField] private KeyCode supportAToggleKey = KeyCode.W;
        [Tooltip("Клавиша переключения поддержки B (ПК).")]
        [SerializeField] private KeyCode supportBToggleKey = KeyCode.E;

        [Header("Состояние")]
        [Tooltip("Автоатака включена при старте.")]
        [SerializeField] private bool attackEnabled = true;
        [Tooltip("Поддержка A включена при старте.")]
        [SerializeField] private bool supportAEnabled = true;
        [Tooltip("Поддержка B включена при старте.")]
        [SerializeField] private bool supportBEnabled = true;

        [Header("Синхронизация")]
        [Tooltip("Отправлять состояние слотов на сервер при первом подключении.")]
        [SerializeField] private bool sendStateOnConnect = true;

        private ISessionService _session;
        private NetworkSessionRunner _net;
        private bool _sentInitialState;

        public bool AttackEnabled => attackEnabled;
        public bool SupportAEnabled => supportAEnabled;
        public bool SupportBEnabled => supportBEnabled;

        private void Awake()
        {
            _session = GameRoot.Instance.Services.Get<ISessionService>();
        }

        private void OnEnable()
        {
            _net = _session as NetworkSessionRunner;
            if (_net != null)
            {
                _net.Snapshot += OnSnapshot;
            }
        }

        private void OnDisable()
        {
            if (_net != null)
            {
                _net.Snapshot -= OnSnapshot;
            }
        }

        private void Update()
        {
            if (_session == null) return;

            if (sendStateOnConnect && _session.IsConnected && !_sentInitialState)
            {
                SendSlot(attackSlotId, attackEnabled);
                SendSlot(supportASlotId, supportAEnabled);
                SendSlot(supportBSlotId, supportBEnabled);
                _sentInitialState = true;
            }

            if (WasPressed(attackToggleKey)) ToggleAttack();
            if (WasPressed(supportAToggleKey)) ToggleSupportA();
            if (WasPressed(supportBToggleKey)) ToggleSupportB();
        }

        public void ToggleAttack() => SetSlotEnabled(attackSlotId, !attackEnabled);
        public void ToggleSupportA() => SetSlotEnabled(supportASlotId, !supportAEnabled);
        public void ToggleSupportB() => SetSlotEnabled(supportBSlotId, !supportBEnabled);

        public void SetSlotEnabled(string slot, bool enabled)
        {
            if (string.IsNullOrWhiteSpace(slot)) return;

            if (string.Equals(slot, attackSlotId, StringComparison.OrdinalIgnoreCase))
            {
                if (attackEnabled == enabled) return;
                attackEnabled = enabled;
            }
            else if (string.Equals(slot, supportASlotId, StringComparison.OrdinalIgnoreCase))
            {
                if (supportAEnabled == enabled) return;
                supportAEnabled = enabled;
            }
            else if (string.Equals(slot, supportBSlotId, StringComparison.OrdinalIgnoreCase))
            {
                if (supportBEnabled == enabled) return;
                supportBEnabled = enabled;
            }
            else
            {
                return;
            }

            SendSlot(slot, enabled);
            SlotChanged?.Invoke(slot, enabled);
        }

        public bool TryGetSlotEnabled(string slot, out bool enabled)
        {
            if (string.Equals(slot, attackSlotId, StringComparison.OrdinalIgnoreCase))
            {
                enabled = attackEnabled;
                return true;
            }
            if (string.Equals(slot, supportASlotId, StringComparison.OrdinalIgnoreCase))
            {
                enabled = supportAEnabled;
                return true;
            }
            if (string.Equals(slot, supportBSlotId, StringComparison.OrdinalIgnoreCase))
            {
                enabled = supportBEnabled;
                return true;
            }

            enabled = false;
            return false;
        }

        private void SendSlot(string slot, bool enabled)
        {
            if (_session == null || !_session.IsConnected) return;
            _session.Send(new CmdSlotToggle { Slot = slot, Enabled = enabled });
        }

        private void OnSnapshot(SnapshotEnvelope snap)
        {
            ApplyServerState(snap.Player.AttackEnabled, snap.Player.SupportAEnabled, snap.Player.SupportBEnabled);
        }

        private void ApplyServerState(bool attack, bool supportA, bool supportB)
        {
            var changed = false;

            if (attackEnabled != attack)
            {
                attackEnabled = attack;
                SlotChanged?.Invoke(attackSlotId, attackEnabled);
                changed = true;
            }

            if (supportAEnabled != supportA)
            {
                supportAEnabled = supportA;
                SlotChanged?.Invoke(supportASlotId, supportAEnabled);
                changed = true;
            }

            if (supportBEnabled != supportB)
            {
                supportBEnabled = supportB;
                SlotChanged?.Invoke(supportBSlotId, supportBEnabled);
                changed = true;
            }

            if (changed)
            {
                _sentInitialState = true;
            }
        }

        private static bool WasPressed(KeyCode key)
        {
            if (key == KeyCode.None) return false;
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return false;
            return key switch
            {
                KeyCode.Q => k.qKey.wasPressedThisFrame,
                KeyCode.W => k.wKey.wasPressedThisFrame,
                KeyCode.E => k.eKey.wasPressedThisFrame,
                KeyCode.A => k.aKey.wasPressedThisFrame,
                KeyCode.S => k.sKey.wasPressedThisFrame,
                KeyCode.D => k.dKey.wasPressedThisFrame,
                KeyCode.Z => k.zKey.wasPressedThisFrame,
                KeyCode.X => k.xKey.wasPressedThisFrame,
                KeyCode.C => k.cKey.wasPressedThisFrame,
                _ => false
            };
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(key);
#else
            return false;
#endif
        }
    }
}
