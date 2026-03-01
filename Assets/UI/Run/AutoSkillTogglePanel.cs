using System;
using System.Collections.Generic;
using DVBARPG.Core;
using DVBARPG.Core.Services;
using DVBARPG.Data;
using DVBARPG.Game.Combat;
using DVBARPG.Game.Player;
using DVBARPG.Net.Network;
using UnityEngine;
using UnityEngine.UI;

namespace DVBARPG.UI.Run
{
    public sealed class AutoSkillTogglePanel : MonoBehaviour
    {
        [Header("Связи")]
        [Tooltip("Контроллер авто-использования скиллов.")]
        [SerializeField] private AutoSkillToggleController controller;

        [Header("Кнопки")]
        [Tooltip("Кнопка атаки (вкл/выкл).")]
        [SerializeField] private Button attackButton;
        [Tooltip("Кнопка поддержки A (вкл/выкл).")]
        [SerializeField] private Button supportAButton;
        [Tooltip("Кнопка поддержки B (вкл/выкл).")]
        [SerializeField] private Button supportBButton;

        [Header("Визуал")]
        [Tooltip("Фон атаки (меняем цвет по состоянию).")]
        [SerializeField] private Image attackImage;
        [Tooltip("Фон поддержки A (меняем цвет по состоянию).")]
        [SerializeField] private Image supportAImage;
        [Tooltip("Фон поддержки B (меняем цвет по состоянию).")]
        [SerializeField] private Image supportBImage;
        [Tooltip("Цвет для включённого состояния.")]
        [SerializeField] private Color enabledColor = new(0.25f, 0.85f, 0.35f, 0.95f);
        [Tooltip("Цвет для выключенного состояния.")]
        [SerializeField] private Color disabledColor = new(0.2f, 0.2f, 0.2f, 0.85f);

        [Header("Кулдауны")]
        [Tooltip("SkillId поддержки A для кулдауна (серверный).")]
        [SerializeField] private string supportASkillId = "fireball";
        [Tooltip("SkillId поддержки B для кулдауна (серверный).")]
        [SerializeField] private string supportBSkillId = "ice_nova";
        [Tooltip("Заливка кулдауна поддержки A.")]
        [SerializeField] private Image supportACooldownFill;
        [Tooltip("Текст кулдауна поддержки A.")]
        [SerializeField] private Text supportACooldownText;
        [Tooltip("Заливка кулдауна поддержки B.")]
        [SerializeField] private Image supportBCooldownFill;
        [Tooltip("Текст кулдауна поддержки B.")]
        [SerializeField] private Text supportBCooldownText;

        [Header("Данные классов")]
        [Tooltip("Использовать SkillId из ClassData выбранного класса.")]
        [SerializeField] private bool useClassData = true;
        [Tooltip("ClassData для класса Melee.")]
        [SerializeField] private ClassData meleeClass;
        [Tooltip("ClassData для класса Ranged.")]
        [SerializeField] private ClassData rangedClass;
        [Tooltip("ClassData для класса Mage.")]
        [SerializeField] private ClassData mageClass;

        private NetworkSessionRunner _net;
        private readonly Dictionary<string, float> _maxCooldownBySkill = new(StringComparer.OrdinalIgnoreCase);

        private void Awake()
        {
            if (controller == null)
            {
                controller = FindObjectOfType<AutoSkillToggleController>();
            }
        }

        private void OnEnable()
        {
            var session = DVBARPG.Core.GameRoot.Instance.Services.Get<DVBARPG.Core.Services.ISessionService>();
            _net = session as NetworkSessionRunner;
            if (_net != null)
            {
                _net.Snapshot += OnSnapshot;
            }

            ResolveSkillIdsFromClass();

            if (attackButton != null) attackButton.onClick.AddListener(OnAttackPressed);
            if (supportAButton != null) supportAButton.onClick.AddListener(OnSupportAPressed);
            if (supportBButton != null) supportBButton.onClick.AddListener(OnSupportBPressed);

            if (controller != null)
            {
                controller.SlotChanged += OnSlotChanged;
            }

            RefreshAll();
        }

        private void OnDisable()
        {
            if (attackButton != null) attackButton.onClick.RemoveListener(OnAttackPressed);
            if (supportAButton != null) supportAButton.onClick.RemoveListener(OnSupportAPressed);
            if (supportBButton != null) supportBButton.onClick.RemoveListener(OnSupportBPressed);

            if (controller != null)
            {
                controller.SlotChanged -= OnSlotChanged;
            }

            if (_net != null)
            {
                _net.Snapshot -= OnSnapshot;
                _net = null;
            }
        }

        private void OnAttackPressed()
        {
            if (controller == null) return;
            controller.ToggleAttack();
            RefreshSlot(CombatSlots.Attack);
        }

        private void OnSupportAPressed()
        {
            if (controller == null) return;
            controller.ToggleSupportA();
            RefreshSlot(CombatSlots.SupportA);
        }

        private void OnSupportBPressed()
        {
            if (controller == null) return;
            controller.ToggleSupportB();
            RefreshSlot(CombatSlots.SupportB);
        }

        private void OnSlotChanged(string slot, bool enabled)
        {
            RefreshSlot(slot);
        }

        private void RefreshAll()
        {
            RefreshSlot(CombatSlots.Attack);
            RefreshSlot(CombatSlots.SupportA);
            RefreshSlot(CombatSlots.SupportB);
        }

        private void RefreshSlot(string slot)
        {
            if (controller == null) return;
            if (!controller.TryGetSlotEnabled(slot, out var enabled)) return;

            if (string.Equals(slot, CombatSlots.Attack, System.StringComparison.OrdinalIgnoreCase))
            {
                ApplyState(attackImage, enabled);
                return;
            }

            if (string.Equals(slot, CombatSlots.SupportA, System.StringComparison.OrdinalIgnoreCase))
            {
                ApplyState(supportAImage, enabled);
                return;
            }

            if (string.Equals(slot, CombatSlots.SupportB, System.StringComparison.OrdinalIgnoreCase))
            {
                ApplyState(supportBImage, enabled);
            }
        }

        private void ApplyState(Image image, bool enabled)
        {
            if (image == null) return;
            image.color = enabled ? enabledColor : disabledColor;
        }

        private void OnSnapshot(SnapshotEnvelope snap)
        {
            var cds = snap.Cooldowns;
            if (cds == null || cds.Count == 0)
            {
                ApplyCooldown(supportACooldownFill, supportACooldownText, 0f, 0f);
                ApplyCooldown(supportBCooldownFill, supportBCooldownText, 0f, 0f);
                return;
            }

            var a = GetCooldownRemaining(cds, supportASkillId, out var aMax);
            var b = GetCooldownRemaining(cds, supportBSkillId, out var bMax);
            ApplyCooldown(supportACooldownFill, supportACooldownText, a, aMax);
            ApplyCooldown(supportBCooldownFill, supportBCooldownText, b, bMax);
        }

        private float GetCooldownRemaining(Dictionary<string, float> cds, string skillId, out float max)
        {
            max = 0f;
            if (string.IsNullOrWhiteSpace(skillId)) return 0f;

            cds.TryGetValue(skillId, out var remaining);
            if (!_maxCooldownBySkill.TryGetValue(skillId, out max) || remaining > max + 0.01f)
            {
                max = remaining;
                _maxCooldownBySkill[skillId] = max;
            }

            if (remaining <= 0.01f)
            {
                _maxCooldownBySkill[skillId] = 0f;
                max = 0f;
            }

            return remaining;
        }

        private void ApplyCooldown(Image fill, Text label, float remaining, float max)
        {
            if (fill != null)
            {
                if (remaining <= 0.01f || max <= 0.01f)
                {
                    fill.gameObject.SetActive(false);
                }
                else
                {
                    fill.gameObject.SetActive(true);
                    fill.fillAmount = Mathf.Clamp01(remaining / max);
                }
            }

            if (label != null)
            {
                if (remaining <= 0.01f)
                {
                    label.text = "";
                }
                else
                {
                    var seconds = Mathf.CeilToInt(remaining);
                    label.text = seconds.ToString();
                }
            }
        }

        private void ResolveSkillIdsFromClass()
        {
            if (!useClassData) return;

            var profile = GameRoot.Instance.Services.Get<IProfileService>();
            if (profile == null || string.IsNullOrWhiteSpace(profile.SelectedClassId)) return;

            var data = GetClassData(profile.SelectedClassId);
            if (data == null) return;

            if (!string.IsNullOrWhiteSpace(data.Skill2Id))
            {
                supportASkillId = data.Skill2Id;
            }

            if (!string.IsNullOrWhiteSpace(data.Skill3Id))
            {
                supportBSkillId = data.Skill3Id;
            }
        }

        private ClassData GetClassData(string classId)
        {
            if (string.Equals(classId, ClassId.Melee.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return meleeClass;
            }

            if (string.Equals(classId, ClassId.Ranged.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return rangedClass;
            }

            if (string.Equals(classId, ClassId.Mage.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return mageClass;
            }

            return null;
        }
    }
}
