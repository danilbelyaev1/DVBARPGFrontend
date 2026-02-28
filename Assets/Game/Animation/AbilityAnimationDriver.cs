using System.Collections.Generic;
using UnityEngine;

namespace DVBARPG.Game.Animation
{
    public sealed class AbilityAnimationDriver : MonoBehaviour
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

        [Header("Способности")]
        [Tooltip("Сопоставление SkillId -> Trigger.")]
        [SerializeField] private List<AbilityEntry> abilities = new();
        [Tooltip("Триггер по умолчанию, если SkillId не найден.")]
        [SerializeField] private string fallbackTrigger = "Attack";
        [Tooltip("Режим выбора вариации анимации.")]
        [SerializeField] private VariantMode variantMode = VariantMode.Random;

        [Header("Слой атак")]
        [Tooltip("Управлять весом слоя атак вручную.")]
        [SerializeField] private bool controlAttackLayerWeight = true;
        [Tooltip("Имя слоя атак в Animator.")]
        [SerializeField] private string attackLayerName = "AttackHumanoid";
        [Tooltip("Вес слоя во время атаки.")]
        [Range(0f, 1f)]
        [SerializeField] private float attackLayerWeight = 1f;
        [Tooltip("Скорость возврата веса слоя к 0.")]
        [SerializeField] private float layerFadeSpeed = 6f;

        [Header("Сетевое состояние")]
        [Tooltip("Какое состояние считается атакой (из снапшота).")]
        [SerializeField] private string attackStateName = "attack";
        [Tooltip("SkillId для ближней атаки по умолчанию.")]
        [SerializeField] private string meleeSkillId = "humanoid_melee";
        [Tooltip("SkillId для дальней атаки по умолчанию.")]
        [SerializeField] private string rangedSkillId = "humanoid_ranged";

        private readonly Dictionary<string, int[]> _map = new();
        private readonly Dictionary<string, int> _rrIndex = new();
        private readonly HashSet<int> _attackStateHashes = new();
        private int _fallbackHash;
        private int _attackLayerIndex = -1;
        private bool _attackLayerReady;
        private float _lastAttackTime;
        private string _lastState = "";

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            _fallbackHash = Animator.StringToHash(fallbackTrigger);
            if (animator != null)
            {
                _attackLayerIndex = animator.GetLayerIndex(attackLayerName);
                _attackLayerReady = _attackLayerIndex >= 0;
                if (_attackLayerReady && controlAttackLayerWeight)
                {
                    animator.SetLayerWeight(_attackLayerIndex, 0f);
                }
            }

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

        public void ForceAttackLayerWeight()
        {
            if (!controlAttackLayerWeight || animator == null || !_attackLayerReady) return;
            _lastAttackTime = Time.time;
            animator.SetLayerWeight(_attackLayerIndex, attackLayerWeight);
        }

        public void ApplyNetworkState(string state, string type)
        {
            if (string.IsNullOrWhiteSpace(state)) return;
            if (string.Equals(state, _lastState, System.StringComparison.OrdinalIgnoreCase)) return;

            if (string.Equals(state, attackStateName, System.StringComparison.OrdinalIgnoreCase))
            {
                var isRanged = string.Equals(type, "ranged", System.StringComparison.OrdinalIgnoreCase);
                var skillId = isRanged ? rangedSkillId : meleeSkillId;
                ForceAttackLayerWeight();
                PlaySkill(skillId);
            }

            _lastState = state;
        }

        private void Update()
        {
            if (!controlAttackLayerWeight || animator == null || !_attackLayerReady) return;

            // Держим вес, пока активен атакующий стейт на слое.
            var state = animator.GetCurrentAnimatorStateInfo(_attackLayerIndex);
            var inAttackState = _attackStateHashes.Contains(state.shortNameHash);
            if (!inAttackState && !animator.IsInTransition(_attackLayerIndex))
            {
                var w = animator.GetLayerWeight(_attackLayerIndex);
                if (w > 0f)
                {
                    var next = Mathf.MoveTowards(w, 0f, layerFadeSpeed * Time.deltaTime);
                    animator.SetLayerWeight(_attackLayerIndex, next);
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
