using System.Collections.Generic;
using UnityEngine;

namespace DVBARPG.Game.Animation
{
    public sealed class MonsterAnimationDriver : MonoBehaviour
    {
        public enum VariantMode
        {
            Random,
            RoundRobin
        }

        [Header("Animator")]
        [Tooltip("Animator на этом объекте.")]
        [SerializeField] private Animator animator;

        [Header("Атаки монстров")]
        [Tooltip("Триггеры для ближней атаки монстров.")]
        [SerializeField] private List<string> meleeTriggers = new();
        [Tooltip("Триггеры для дальней атаки монстров.")]
        [SerializeField] private List<string> rangedTriggers = new();
        [Tooltip("Триггер по умолчанию.")]
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
        [Tooltip("Параметр Animator для скорости атаки.")]
        [SerializeField] private string attackSpeedParam = "MonsterAttackSpeed";
        [Tooltip("Автоматически определять длительность атак по клипам (по имени триггера).")]
        [SerializeField] private bool autoDetectAnimLength = true;
        [Tooltip("Базовый кулдаун ближней атаки (сек).")]
        [SerializeField] private float defaultMeleeCooldown = 1.2f;
        [Tooltip("Базовый кулдаун дальней атаки (сек).")]
        [SerializeField] private float defaultRangedCooldown = 1.4f;
        [Tooltip("Длительность анимации ближней атаки (сек) для синхронизации удара.")]
        [SerializeField] private float meleeAttackAnimSec = 0.6f;
        [Tooltip("Длительность анимации дальней атаки (сек) для синхронизации вылета.")]
        [SerializeField] private float rangedAttackAnimSec = 0.7f;
        [Tooltip("Мин. множитель скорости атаки.")]
        [SerializeField] private float minAttackSpeed = 0.5f;
        [Tooltip("Макс. множитель скорости атаки.")]
        [SerializeField] private float maxAttackSpeed = 10.0f;
        [Tooltip("Минимальная задержка между повторными атаками (сек).")]
        [SerializeField] private float minRetriggerSec = 0.05f;

        [Header("Поворот")]
        [Tooltip("Поворачивать монстра к игроку при атаке.")]
        [SerializeField] private bool rotateToPlayerOnAttack = true;
        [Tooltip("Скорость разворота к игроку.")]
        [SerializeField] private float rotationLerp = 12f;

        private MovementAnimator _movementAnimator;
        private readonly HashSet<int> _attackStateHashes = new();
        private readonly Dictionary<string, int> _rrIndex = new();
        private readonly Dictionary<string, float> _clipLengths = new();
        private int _fallbackHash;
        private int _attackLayerIndex = -1;
        private bool _attackLayerReady;
        private string _lastState = "";
        private int _lastAttackStateHash;
        private float _lastAttackTime;
        
        private string _resolvedAttackSpeedParam;
        private bool _pendingAttackSpeed;
        private float _pendingCooldown;

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            _movementAnimator = GetComponent<MovementAnimator>();
            if (_movementAnimator != null)
            {
                _movementAnimator.SetRotateToMovement(true);
            }
            _fallbackHash = Animator.StringToHash(fallbackTrigger);
            if (animator != null)
            {
                _attackLayerIndex = animator.GetLayerIndex(attackLayerName);
                _attackLayerReady = _attackLayerIndex >= 0;
                if (_attackLayerReady && controlAttackLayerWeight)
                {
                    animator.SetLayerWeight(_attackLayerIndex, 0f);
                }
                _resolvedAttackSpeedParam = ResolveAttackSpeedParam();
            }

            _attackStateHashes.Clear();
            RegisterAttackTriggers(meleeTriggers);
            RegisterAttackTriggers(rangedTriggers);
            if (!string.IsNullOrWhiteSpace(fallbackTrigger))
            {
                _attackStateHashes.Add(_fallbackHash);
            }

            if (autoDetectAnimLength)
            {
                CacheClipLengths();
                meleeAttackAnimSec = ResolveAnimLength(meleeTriggers, meleeAttackAnimSec);
                rangedAttackAnimSec = ResolveAnimLength(rangedTriggers, rangedAttackAnimSec);
            }
        }

        public void ApplyNetworkState(string state, string type, long serverTimeMs)
        {
            if (string.IsNullOrWhiteSpace(state)) return;

            if (string.Equals(state, attackStateName, System.StringComparison.OrdinalIgnoreCase))
            {
                var now = Time.time;
                var isRanged = string.Equals(type, "ranged", System.StringComparison.OrdinalIgnoreCase);
                var cooldown = GetAttackCooldownSec(type);
                var minDelay = Mathf.Max(minRetriggerSec, cooldown);
                if (!string.Equals(_lastState, attackStateName, System.StringComparison.OrdinalIgnoreCase))
                {
                    ApplyAttackSpeed(type, isRanged);
                    ForceAttackLayerWeight();
                    TriggerAttack(isRanged);
                    _lastAttackTime = now;
                }
                else if (now - _lastAttackTime >= minDelay)
                {
                    ApplyAttackSpeed(type, isRanged);
                    ForceAttackLayerWeight();
                    TriggerAttack(isRanged);
                    _lastAttackTime = now;
                }
            }

            _lastState = state;
        }

        private void ApplyAttackSpeed(string type, bool isRanged)
        {
            if (animator == null || string.IsNullOrWhiteSpace(_resolvedAttackSpeedParam)) return;

            var cooldown = GetAttackCooldownSec(type);

            var clipLen = isRanged ? rangedAttackAnimSec : meleeAttackAnimSec;
            var speed = clipLen > 0.001f ? clipLen / Mathf.Max(0.001f, cooldown) : 1f;
            speed = Mathf.Clamp(speed, minAttackSpeed, maxAttackSpeed);
            animator.SetFloat(_resolvedAttackSpeedParam, speed);
        }

        private float GetAttackCooldownSec(string type)
        {
            var cooldown = defaultMeleeCooldown;
            if (DVBARPG.Game.Network.MonsterCatalogClient.TryGetByType(type, out var stats))
            {
                if (stats.AttackCooldownSec > 0.001f)
                {
                    cooldown = stats.AttackCooldownSec;
                }
                else if (stats.ProjectileCooldownSec > 0.001f)
                {
                    cooldown = stats.ProjectileCooldownSec;
                }
            }
            return Mathf.Max(0.001f, cooldown);
        }

        public float GetRangedAttackAnimSeconds() => Mathf.Max(0f, rangedAttackAnimSec);
        public float GetMeleeAttackAnimSeconds() => Mathf.Max(0f, meleeAttackAnimSec);

        private void TriggerAttack(bool ranged)
        {
            if (animator == null) return;

            var list = ranged ? rangedTriggers : meleeTriggers;
            var hash = PickVariantFromList(list, ranged ? "__ranged" : "__melee");
            if (hash != 0)
            {
                _lastAttackStateHash = hash;
                _lastAttackTime = Time.time;
                animator.SetTrigger(hash);
                _pendingAttackSpeed = true;
                _pendingCooldown = GetAttackCooldownSec(ranged ? "ranged" : "melee");
                return;
            }

            if (_fallbackHash != 0)
            {
                _lastAttackStateHash = _fallbackHash;
                _lastAttackTime = Time.time;
                animator.SetTrigger(_fallbackHash);
                _pendingAttackSpeed = true;
                _pendingCooldown = GetAttackCooldownSec(ranged ? "ranged" : "melee");
            }
        }

        private string ResolveAttackSpeedParam()
        {
            if (animator == null) return string.Empty;

            if (HasParam(attackSpeedParam)) return attackSpeedParam;
            if (HasParam("MonsterAttackSpeed")) return "MonsterAttackSpeed";
            if (HasParam("AttackSpeed")) return "AttackSpeed";

            return string.Empty;
        }

        private bool HasParam(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || animator == null) return false;
            var parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == name) return true;
            }
            return false;
        }

        private int PickVariantFromList(List<string> list, string key)
        {
            if (list == null || list.Count == 0) return 0;

            if (variantMode == VariantMode.RoundRobin)
            {
                if (!_rrIndex.TryGetValue(key, out var idx)) idx = 0;
                var name = list[idx % list.Count];
                _rrIndex[key] = (idx + 1) % list.Count;
                return Animator.StringToHash(name);
            }

            var r = Random.Range(0, list.Count);
            return Animator.StringToHash(list[r]);
        }

        private void RegisterAttackTriggers(List<string> triggers)
        {
            if (triggers == null) return;
            for (int i = 0; i < triggers.Count; i++)
            {
                var trig = triggers[i];
                if (string.IsNullOrWhiteSpace(trig)) continue;
                _attackStateHashes.Add(Animator.StringToHash(trig));
            }
        }

        private void CacheClipLengths()
        {
            _clipLengths.Clear();
            if (animator == null || animator.runtimeAnimatorController == null) return;

            var clips = animator.runtimeAnimatorController.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                if (clip == null) continue;
                if (string.IsNullOrWhiteSpace(clip.name)) continue;
                if (_clipLengths.ContainsKey(clip.name)) continue;
                _clipLengths[clip.name] = clip.length;
            }
        }

        private float ResolveAnimLength(List<string> triggers, float fallback)
        {
            if (triggers == null || triggers.Count == 0) return fallback;

            for (int i = 0; i < triggers.Count; i++)
            {
                var name = triggers[i];
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (_clipLengths.TryGetValue(name, out var len) && len > 0.001f)
                {
                    return len;
                }
            }

            return fallback;
        }

        private void ForceAttackLayerWeight()
        {
            if (!controlAttackLayerWeight || animator == null || !_attackLayerReady) return;
            animator.SetLayerWeight(_attackLayerIndex, attackLayerWeight);
        }

        private void Update()
        {
            if (!controlAttackLayerWeight || animator == null || !_attackLayerReady) return;

            var state = animator.GetCurrentAnimatorStateInfo(_attackLayerIndex);
            var inAttackState = _attackStateHashes.Contains(state.shortNameHash);

            if (_pendingAttackSpeed && state.shortNameHash == _lastAttackStateHash && !animator.IsInTransition(_attackLayerIndex))
            {
                var clipInfo = animator.GetCurrentAnimatorClipInfo(_attackLayerIndex);
                if (clipInfo != null && clipInfo.Length > 0 && clipInfo[0].clip != null)
                {
                    var clipLen = clipInfo[0].clip.length;
                    var cooldown = Mathf.Max(0.001f, _pendingCooldown);
                    var speed = clipLen > 0.001f ? clipLen / cooldown : 1f;
                    speed = Mathf.Clamp(speed, minAttackSpeed, maxAttackSpeed);
                    if (!string.IsNullOrWhiteSpace(_resolvedAttackSpeedParam))
                    {
                        animator.SetFloat(_resolvedAttackSpeedParam, speed);
                    }
                }
                _pendingAttackSpeed = false;
            }

            // Держим вес, пока не закончим именно тот атакующий стейт, который запускали.
            if (_lastAttackStateHash != 0)
            {
                var inSameState = state.shortNameHash == _lastAttackStateHash;
                var inTransition = animator.IsInTransition(_attackLayerIndex);
                if (inSameState && !inTransition && state.normalizedTime < 1f)
                {
                    return;
                }
            }

            if (!animator.IsInTransition(_attackLayerIndex))
            {
                var attackFinished = inAttackState && state.normalizedTime >= 1f;
                if (!inAttackState || attackFinished)
                {
                    var w = animator.GetLayerWeight(_attackLayerIndex);
                    if (w > 0f)
                    {
                        var next = Mathf.MoveTowards(w, 0f, layerFadeSpeed * Time.deltaTime);
                        animator.SetLayerWeight(_attackLayerIndex, next);
                    }
                }
            }

            if (rotateToPlayerOnAttack && string.Equals(_lastState, attackStateName, System.StringComparison.OrdinalIgnoreCase))
            {
                if (_movementAnimator != null) _movementAnimator.SetRotationLocked(true);
                var player = DVBARPG.Game.Player.NetworkPlayerReplicator.PlayerTransform;
                if (player != null)
                {
                    var to = player.position - transform.position;
                    to.y = 0f;
                    if (to.sqrMagnitude > 0.0001f)
                    {
                        var targetRot = Quaternion.LookRotation(to.normalized, Vector3.up);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationLerp * Time.deltaTime);
                    }
                }
            }
            else
            {
                if (_movementAnimator != null) _movementAnimator.SetRotationLocked(false);
            }
        }

    }
}
