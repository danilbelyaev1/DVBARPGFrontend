using System;
using System.Collections.Generic;
using DVBARPG.Core.Services;
using DVBARPG.Game.Skills;
using UnityEngine;

namespace DVBARPG.Game.Animation
{
    /// <summary>
    /// Драйвер анимаций только для атакующего скилла и мувмент-скилла.
    /// Support A/B анимации здесь не вызываются.
    /// </summary>
    public sealed class PlayerAbilityAnimationDriver : MonoBehaviour
    {
        /// <summary>Встроенные маппинги SkillId → триггер Animator (атака + мувмент). Inspector переопределяет.</summary>
        private static readonly Dictionary<string, string> DefaultSkillToTrigger = new(StringComparer.OrdinalIgnoreCase)
        {
            { "slash", "Slash" },
            { "quick_shot", "QuickShot" },
            { "arc_bolt", "ArcBolt" },
            { "unarmed_strike", "UnarmedStrike" },
            { "dash", "Dash" },
            { "combat_roll", "CombatRoll" },
            { "rift_step", "RiftStep" }
        };

        public enum VariantMode
        {
            Random,
            RoundRobin
        }

        [System.Serializable]
        private sealed class AbilityEntry
        {
            [Tooltip("SkillId, который приходит из логики/снапшота.")]
            public string SkillId;
            [Tooltip("Триггеры в Animator, которые можно дернуть для этого SkillId.")]
            public List<string> Triggers = new();
        }

        [System.Serializable]
        private sealed class WeaponDefaultEntry
        {
            [Tooltip("Тип оружия (sword, bow, staff, unarmed или свой id).")]
            public string WeaponType;
            [Tooltip("Триггер анимации по умолчанию для этого оружия, если скилла нет в слое.")]
            public string TriggerName;
        }

        [System.Serializable]
        private sealed class SkillToWeaponEntry
        {
            [Tooltip("SkillId атаки из лоадута (attack).")]
            public string SkillId;
            [Tooltip("Тип оружия для этого скилла (sword, bow, staff, unarmed).")]
            public string WeaponType;
        }

        [System.Serializable]
        private sealed class ClassToWeaponEntry
        {
            [Tooltip("Класс (vanguard, hunter, mystic) — используется, если лоадут ещё не пришёл.")]
            public string ClassId;
            [Tooltip("Тип оружия для этого класса.")]
            public string WeaponType;
        }

        [Header("Animator")]
        [Tooltip("Animator на этом объекте.")]
        [SerializeField] private Animator animator;

        [Header("Способности игрока")]
        [Tooltip("Сопоставление SkillId -> Trigger.")]
        [SerializeField] private List<AbilityEntry> abilities = new();
        [Tooltip("Триггер по умолчанию, если SkillId не найден и нет дефолта по оружию.")]
        [SerializeField] private string fallbackTrigger = "Attack";
        [Tooltip("Скилл атаки → тип оружия. Оружие берётся из ServerLoadout.AttackSkillId.")]
        [SerializeField] private List<SkillToWeaponEntry> skillToWeaponType = new()
        {
            new SkillToWeaponEntry { SkillId = "slash", WeaponType = "sword" },
            new SkillToWeaponEntry { SkillId = "quick_shot", WeaponType = "bow" },
            new SkillToWeaponEntry { SkillId = "arc_bolt", WeaponType = "staff" },
            new SkillToWeaponEntry { SkillId = "unarmed_strike", WeaponType = "unarmed" }
        };
        [Tooltip("Класс → тип оружия. Используется, если лоадут ещё не пришёл с сервера.")]
        [SerializeField] private List<ClassToWeaponEntry> classToWeaponType = new()
        {
            new ClassToWeaponEntry { ClassId = "vanguard", WeaponType = "sword" },
            new ClassToWeaponEntry { ClassId = "hunter", WeaponType = "bow" },
            new ClassToWeaponEntry { ClassId = "mystic", WeaponType = "staff" }
        };
        [Tooltip("Дефолтная анимация по типу оружия. Если скилла нет в слое — используется этот триггер.")]
        [SerializeField] private List<WeaponDefaultEntry> weaponDefaultTriggers = new()
        {
            new WeaponDefaultEntry { WeaponType = "sword", TriggerName = "Slash" },
            new WeaponDefaultEntry { WeaponType = "bow", TriggerName = "QuickShot" },
            new WeaponDefaultEntry { WeaponType = "staff", TriggerName = "ArcBolt" },
            new WeaponDefaultEntry { WeaponType = "unarmed", TriggerName = "UnarmedStrike" }
        };
        [Tooltip("SkillId, для которых в слое AttackStanding (индекс 0) есть своя анимация. Пусто = все скиллы. Иначе в стоячем слое для остальных — дефолт по оружию.")]
        [SerializeField] private List<string> attackStandingSkillIds = new();
        [Tooltip("SkillId, для которых в слое AttackMoving (индекс 1) есть своя анимация. Пусто = все.")]
        [SerializeField] private List<string> attackMovingSkillIds = new();
        [Tooltip("SkillId, для которых в слое AttackAny (индекс 2) есть своя анимация. Пусто = все.")]
        [SerializeField] private List<string> attackAnySkillIds = new();
        [Tooltip("Режим выбора вариации анимации.")]
        [SerializeField] private VariantMode variantMode = VariantMode.Random;

        [Header("Слои атак")]
        [Tooltip("Управлять весом слоёв атак вручную.")]
        [SerializeField] private bool controlAttackLayerWeight = true;
        [Tooltip("Имена слоёв атак в Animator.")]
        [SerializeField] private List<string> attackLayerNames = new() { "AttackStanding", "AttackMoving", "AttackAny" };
        [Tooltip("Вес слоя во время атаки.")]
        [Range(0f, 1f)]
        [SerializeField] private float attackLayerWeight = 1f;
        [Tooltip("Скорость возврата веса слоя к 0.")]
        [SerializeField] private float layerFadeSpeed = 6f;
        [Tooltip("Имя bool-параметра IsMoving. При движении вес слоя AttackStanding (индекс 0) сбрасывается — анимация стоя прерывается.")]
        [SerializeField] private string isMovingParam = "IsMoving";

        [Header("Параметры скиллов")]
        [Tooltip("Имя int-параметра CastMode.")]
        [SerializeField] private string castModeParam = "CastMode";
        [Tooltip("Имя trigger-параметра UseSkill.")]
        [SerializeField] private string useSkillParam = "UseSkill";
        [Tooltip("Имя float-параметра скорости анимации скилла (например AttackSpeed). Скорость = длина_клипа / cooldown, чтобы анимация успевала за кулдаун.")]
        [SerializeField] private string attackSpeedParam = "AttackSpeed";
        [Tooltip("Мин. множитель скорости (анимация не замедляется ниже этого).")]
        [SerializeField] private float minAttackSpeed = 0.25f;
        [Tooltip("Макс. множитель скорости. Задай достаточно большим (например 50), чтобы длинная анимация успевала отработать за короткий кулдаун.")]
        [SerializeField] private float maxAttackSpeed = 50f;
        [Tooltip("Доля кулдауна, в которую должна уложиться анимация (0.95 = закончить за 95% кулдауна, небольшой запас).")]
        [Range(0.5f, 1f)]
        [SerializeField] private float cooldownFractionForAnim = 0.98f;

        private readonly Dictionary<string, int[]> _map = new();
        private readonly Dictionary<string, int> _rrIndex = new();
        private readonly HashSet<int> _attackStateHashes = new();
        private readonly Dictionary<string, string> _skillToWeapon = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _classToWeapon = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _weaponDefaultByType = new(StringComparer.OrdinalIgnoreCase);
        private int _fallbackHash;
        private readonly List<int> _attackLayerIndexes = new();
        private readonly List<bool> _attackLayerReady = new();
        private int _castModeHash;
        private int _useSkillHash;
        private int _attackSpeedHash;
        private string _resolvedAttackSpeedParam;
        private float _pendingCooldownSec;
        private bool _pendingAttackSpeed;
        private int _lastPlayedStateHash;
        private int _isMovingHash;
        private bool _isPlayingAttack;

        /// <summary>True, если сейчас проигрывается атака на одном из слоёв (поворот к цели не применяют).</summary>
        public bool IsPlayingAttackAnimation => _isPlayingAttack;

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            _fallbackHash = Animator.StringToHash(fallbackTrigger);
            if (animator != null)
            {
                animator.applyRootMotion = false; // поворот/сдвиг корня только из кода
                _attackLayerIndexes.Clear();
                _attackLayerReady.Clear();
                for (int i = 0; i < attackLayerNames.Count; i++)
                {
                    var idx = animator.GetLayerIndex(attackLayerNames[i]);
                    _attackLayerIndexes.Add(idx);
                    _attackLayerReady.Add(idx >= 0);
                    if (idx >= 0 && controlAttackLayerWeight)
                    {
                        animator.SetLayerWeight(idx, 0f);
                    }
                }
            }

            _castModeHash = Animator.StringToHash(castModeParam);
            _useSkillHash = Animator.StringToHash(useSkillParam);
            _resolvedAttackSpeedParam = ResolveAttackSpeedParam();
            _attackSpeedHash = string.IsNullOrEmpty(_resolvedAttackSpeedParam) ? 0 : Animator.StringToHash(_resolvedAttackSpeedParam);
            _isMovingHash = string.IsNullOrEmpty(isMovingParam) ? 0 : Animator.StringToHash(isMovingParam);

            _map.Clear();
            _attackStateHashes.Clear();
            for (int i = 0; i < abilities.Count; i++)
            {
                var a = abilities[i];
                if (string.IsNullOrWhiteSpace(a?.SkillId) || a.Triggers == null || a.Triggers.Count == 0)
                {
                    continue;
                }
                if (_map.ContainsKey(a.SkillId)) continue;

                var list = new List<int>();
                for (int t = 0; t < a.Triggers.Count; t++)
                {
                    var trig = a.Triggers[t];
                    if (string.IsNullOrWhiteSpace(trig)) continue;
                    var hash = Animator.StringToHash(trig);
                    list.Add(hash);
                    _attackStateHashes.Add(hash);
                }

                if (list.Count == 0) continue;
                _map[a.SkillId] = list.ToArray();
                _rrIndex[a.SkillId] = 0;
            }

            // Добавляем встроенные маппинги только для атаки и мувмента (если не заданы в Inspector).
            foreach (var kv in DefaultSkillToTrigger)
            {
                if (_map.ContainsKey(kv.Key)) continue;
                var hash = Animator.StringToHash(kv.Value);
                _map[kv.Key] = new[] { hash };
                _attackStateHashes.Add(hash);
                _rrIndex[kv.Key] = 0;
            }

            if (!string.IsNullOrWhiteSpace(fallbackTrigger))
            {
                _attackStateHashes.Add(Animator.StringToHash(fallbackTrigger));
            }

            _skillToWeapon.Clear();
            for (int i = 0; i < skillToWeaponType.Count; i++)
            {
                var e = skillToWeaponType[i];
                if (string.IsNullOrWhiteSpace(e?.SkillId) || string.IsNullOrWhiteSpace(e.WeaponType)) continue;
                _skillToWeapon[e.SkillId] = e.WeaponType;
            }
            _classToWeapon.Clear();
            for (int i = 0; i < classToWeaponType.Count; i++)
            {
                var e = classToWeaponType[i];
                if (string.IsNullOrWhiteSpace(e?.ClassId) || string.IsNullOrWhiteSpace(e.WeaponType)) continue;
                _classToWeapon[e.ClassId] = e.WeaponType;
            }
            _weaponDefaultByType.Clear();
            for (int i = 0; i < weaponDefaultTriggers.Count; i++)
            {
                var e = weaponDefaultTriggers[i];
                if (string.IsNullOrWhiteSpace(e?.WeaponType) || string.IsNullOrWhiteSpace(e.TriggerName)) continue;
                _weaponDefaultByType[e.WeaponType] = e.TriggerName;
                _attackStateHashes.Add(Animator.StringToHash(e.TriggerName));
            }
        }

        /// <summary>Воспроизвести анимацию скилла. Если в текущем слое нет анимации для скилла — используется дефолт по типу оружия (классу).</summary>
        public void PlaySkill(string skillId, float cooldownSec = 0f)
        {
            if (animator == null) return;

            _pendingCooldownSec = cooldownSec > 0.001f ? cooldownSec : 0f;
            _pendingAttackSpeed = _pendingCooldownSec > 0 && !string.IsNullOrEmpty(_resolvedAttackSpeedParam);

            var castMode = 2;
            SkillCastModeCatalog.TryGetCastMode(skillId, out castMode);
            animator.SetInteger(_castModeHash, castMode);
            animator.SetTrigger(_useSkillHash);

            var layerIndex = Mathf.Clamp(castMode, 0, 2);
            var useWeaponDefault = ShouldUseWeaponDefault(skillId, layerIndex);

            if (!useWeaponDefault && !string.IsNullOrWhiteSpace(skillId) && _map.TryGetValue(skillId, out var hashes))
            {
                var hash = PickVariant(skillId, hashes);
                _lastPlayedStateHash = hash;
                ForceAttackLayerWeight(castMode);
                animator.SetTrigger(hash);
                return;
            }

            var defaultTrigger = GetWeaponDefaultTrigger();
            if (!string.IsNullOrWhiteSpace(defaultTrigger))
            {
                _lastPlayedStateHash = Animator.StringToHash(defaultTrigger);
                ForceAttackLayerWeight(castMode);
                animator.SetTrigger(_lastPlayedStateHash);
                return;
            }

            _lastPlayedStateHash = _fallbackHash;
            if (_fallbackHash != 0)
            {
                ForceAttackLayerWeight(castMode);
                animator.SetTrigger(_fallbackHash);
            }
        }

        private bool ShouldUseWeaponDefault(string skillId, int layerIndex)
        {
            var list = GetSkillIdsForLayer(layerIndex);
            if (list == null || list.Count == 0) return false;
            return string.IsNullOrWhiteSpace(skillId) || !list.Contains(skillId);
        }

        private List<string> GetSkillIdsForLayer(int layerIndex)
        {
            if (layerIndex == 0) return attackStandingSkillIds;
            if (layerIndex == 1) return attackMovingSkillIds;
            if (layerIndex == 2) return attackAnySkillIds;
            return null;
        }

        private string GetWeaponDefaultTrigger()
        {
            if (DVBARPG.Core.GameRoot.Instance?.Services == null) return null;
            if (!DVBARPG.Core.GameRoot.Instance.Services.TryGet<IProfileService>(out var profile)) return null;
            string weaponType = null;
            var attackSkillId = profile?.ServerLoadout?.AttackSkillId;
            if (!string.IsNullOrWhiteSpace(attackSkillId) && _skillToWeapon.TryGetValue(attackSkillId, out var wt))
                weaponType = wt;
            if (string.IsNullOrWhiteSpace(weaponType) && !string.IsNullOrWhiteSpace(profile?.SelectedClassId) && _classToWeapon.TryGetValue(profile.SelectedClassId, out var wt2))
                weaponType = wt2;
            if (string.IsNullOrWhiteSpace(weaponType)) return null;
            return _weaponDefaultByType.TryGetValue(weaponType, out var trigger) ? trigger : null;
        }

        public void PlayTrigger(string triggerName)
        {
            if (animator == null) return;
            if (string.IsNullOrWhiteSpace(triggerName)) return;

            ForceAttackLayerWeight(2);
            animator.SetTrigger(Animator.StringToHash(triggerName));
        }

        /// <summary>Поднимает вес только у слоя, соответствующего CastMode: 0 = AttackStanding, 1 = AttackMoving, 2 = AttackAny.</summary>
        public void ForceAttackLayerWeight(int castMode = 2)
        {
            if (!controlAttackLayerWeight || animator == null) return;
            var layerIndex = Mathf.Clamp(castMode, 0, _attackLayerIndexes.Count - 1);
            if (layerIndex >= 0 && layerIndex < _attackLayerReady.Count && _attackLayerReady[layerIndex])
            {
                animator.SetLayerWeight(_attackLayerIndexes[layerIndex], attackLayerWeight);
            }
        }

        private void Update()
        {
            if (animator == null) return;

            _isPlayingAttack = false;
            for (int i = 0; i < _attackLayerIndexes.Count; i++)
            {
                if (!_attackLayerReady[i]) continue;
                var idx = _attackLayerIndexes[i];
                var state = animator.GetCurrentAnimatorStateInfo(idx);
                var inAttackState = _attackStateHashes.Contains(state.shortNameHash);
                if (animator.GetLayerWeight(idx) > 0.01f && (inAttackState || animator.IsInTransition(idx)))
                    _isPlayingAttack = true;

                if (_pendingAttackSpeed && _lastPlayedStateHash != 0 && state.shortNameHash == _lastPlayedStateHash && !animator.IsInTransition(idx))
                {
                    var clipInfo = animator.GetCurrentAnimatorClipInfo(idx);
                    if (clipInfo != null && clipInfo.Length > 0 && clipInfo[0].clip != null)
                    {
                        var clipLen = clipInfo[0].clip.length;
                        var cooldown = Mathf.Max(0.001f, _pendingCooldownSec) * Mathf.Max(0.01f, cooldownFractionForAnim);
                        var speed = clipLen > 0.001f ? clipLen / cooldown : 1f;
                        speed = Mathf.Clamp(speed, minAttackSpeed, maxAttackSpeed);
                        animator.SetFloat(_attackSpeedHash, speed);
                    }
                    _pendingAttackSpeed = false;
                }

                if (!controlAttackLayerWeight) continue;

                var shouldFadeWeight = (!inAttackState && !animator.IsInTransition(idx))
                    || (i == 0 && _isMovingHash != 0 && animator.GetBool(_isMovingHash));

                if (shouldFadeWeight)
                {
                    var w = animator.GetLayerWeight(idx);
                    if (w > 0f)
                    {
                        var next = Mathf.MoveTowards(w, 0f, layerFadeSpeed * Time.deltaTime);
                        animator.SetLayerWeight(idx, next);
                    }
                }
            }
        }

        private string ResolveAttackSpeedParam()
        {
            if (animator == null || string.IsNullOrWhiteSpace(attackSpeedParam)) return string.Empty;
            if (HasParam(attackSpeedParam)) return attackSpeedParam;
            if (HasParam("AttackSpeed")) return "AttackSpeed";
            if (HasParam("SkillSpeed")) return "SkillSpeed";
            return string.Empty;
        }

        private bool HasParam(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || animator == null) return false;
            for (int i = 0; i < animator.parameterCount; i++)
            {
                var p = animator.GetParameter(i);
                if (p.name == name && p.type == AnimatorControllerParameterType.Float) return true;
            }
            return false;
        }

        private int PickVariant(string skillId, int[] hashes)
        {
            if (hashes == null || hashes.Length == 0) return _fallbackHash;

            if (variantMode == VariantMode.RoundRobin)
            {
                if (!_rrIndex.TryGetValue(skillId, out var idx)) idx = 0;
                var chosen = hashes[idx % hashes.Length];
                _rrIndex[skillId] = (idx + 1) % hashes.Length;
                return chosen;
            }

            var r = UnityEngine.Random.Range(0, hashes.Length);
            return hashes[r];
        }
    }
}
