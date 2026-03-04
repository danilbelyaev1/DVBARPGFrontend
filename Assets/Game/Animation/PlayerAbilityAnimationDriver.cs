using System.Collections.Generic;
using DVBARPG.Game.Skills;
using UnityEngine;

namespace DVBARPG.Game.Animation
{
    public sealed class PlayerAbilityAnimationDriver : MonoBehaviour
    {
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

        [Header("Animator")]
        [Tooltip("Animator на этом объекте.")]
        [SerializeField] private Animator animator;

        [Header("Способности игрока")]
        [Tooltip("Сопоставление SkillId -> Trigger.")]
        [SerializeField] private List<AbilityEntry> abilities = new();
        [Tooltip("Триггер по умолчанию, если SkillId не найден.")]
        [SerializeField] private string fallbackTrigger = "Attack";
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

        [Header("Параметры скиллов")]
        [Tooltip("Имя int-параметра CastMode.")]
        [SerializeField] private string castModeParam = "CastMode";
        [Tooltip("Имя trigger-параметра UseSkill.")]
        [SerializeField] private string useSkillParam = "UseSkill";

        private readonly Dictionary<string, int[]> _map = new();
        private readonly Dictionary<string, int> _rrIndex = new();
        private readonly HashSet<int> _attackStateHashes = new();
        private int _fallbackHash;
        private readonly List<int> _attackLayerIndexes = new();
        private readonly List<bool> _attackLayerReady = new();
        private int _castModeHash;
        private int _useSkillHash;

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            _fallbackHash = Animator.StringToHash(fallbackTrigger);
            if (animator != null)
            {
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

            if (!string.IsNullOrWhiteSpace(fallbackTrigger))
            {
                _attackStateHashes.Add(Animator.StringToHash(fallbackTrigger));
            }
        }

        public void PlaySkill(string skillId)
        {
            if (animator == null) return;
            var castMode = 2;
            SkillCastModeCatalog.TryGetCastMode(skillId, out castMode);
            animator.SetInteger(_castModeHash, castMode);
            animator.SetTrigger(_useSkillHash);
            if (!string.IsNullOrWhiteSpace(skillId) && _map.TryGetValue(skillId, out var hashes))
            {
                var hash = PickVariant(skillId, hashes);
                ForceAttackLayerWeight();
                animator.SetTrigger(hash);
                return;
            }

            if (_fallbackHash != 0)
            {
                ForceAttackLayerWeight();
                animator.SetTrigger(_fallbackHash);
            }
        }

        public void PlayTrigger(string triggerName)
        {
            if (animator == null) return;
            if (string.IsNullOrWhiteSpace(triggerName)) return;

            ForceAttackLayerWeight();
            animator.SetTrigger(Animator.StringToHash(triggerName));
        }

        public void ForceAttackLayerWeight()
        {
            if (!controlAttackLayerWeight || animator == null) return;
            for (int i = 0; i < _attackLayerIndexes.Count; i++)
            {
                if (!_attackLayerReady[i]) continue;
                animator.SetLayerWeight(_attackLayerIndexes[i], attackLayerWeight);
            }
        }

        private void Update()
        {
            if (!controlAttackLayerWeight || animator == null) return;

            for (int i = 0; i < _attackLayerIndexes.Count; i++)
            {
                if (!_attackLayerReady[i]) continue;
                var idx = _attackLayerIndexes[i];
                var state = animator.GetCurrentAnimatorStateInfo(idx);
                var inAttackState = _attackStateHashes.Contains(state.shortNameHash);
                if (!inAttackState && !animator.IsInTransition(idx))
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

            var r = Random.Range(0, hashes.Length);
            return hashes[r];
        }
    }
}
