using System.Collections.Generic;
using DVBARPG.Core;
using DVBARPG.Core.Services;
using DVBARPG.Game.Animation;
using DVBARPG.Game.Network;
using DVBARPG.Game.Skills;
using UnityEngine;

namespace DVBARPG.Game.Player
{
    public sealed class PlayerTargetFacing : MonoBehaviour
    {
        [Header("Параметры")]
        [Tooltip("Скорость поворота к цели.")]
        [SerializeField] private float rotationLerp = 12f;
        [Tooltip("Поворот к цели только при наличии цели в радиусе.")]
        [SerializeField] private bool onlyWhenTargetInRange = true;
        [Tooltip("При отсутствии цели — поворачивать по направлению движения.")]
        [SerializeField] private bool rotateToMovementWhenNoTarget = true;
        [Tooltip("Фоллбек радиуса, если у скиллов нет Range.")]
        [SerializeField] private float fallbackRange = 6f;

        private MovementAnimator _movementAnimator;
        private PlayerAbilityAnimationDriver _abilityDriver;

        private void Awake()
        {
            _movementAnimator = GetComponent<MovementAnimator>();
            if (_movementAnimator == null) _movementAnimator = GetComponentInChildren<MovementAnimator>();
            _abilityDriver = GetComponent<PlayerAbilityAnimationDriver>();
            if (_abilityDriver == null) _abilityDriver = GetComponentInChildren<PlayerAbilityAnimationDriver>();
        }

        private void Update()
        {
            var maxRange = ResolveMaxRange();
            if (maxRange <= 0f)
            {
                ApplyNoTarget();
                return;
            }

            var target = FindNearestTarget(maxRange);
            if (target == null)
            {
                ApplyNoTarget();
                return;
            }

            // Не поворачивать к цели во время атаки — только между анимациями
            if (_abilityDriver != null && _abilityDriver.IsPlayingAttackAnimation)
            {
                if (_movementAnimator != null)
                {
                    _movementAnimator.SetRotationLocked(true);
                    _movementAnimator.SetRotateToMovement(false);
                }
                return;
            }

            RotateTo(target.position);
            if (_movementAnimator != null)
            {
                _movementAnimator.SetRotationLocked(true);
                _movementAnimator.SetRotateToMovement(false);
            }
        }

        private void ApplyNoTarget()
        {
            if (!onlyWhenTargetInRange)
            {
                return;
            }

            if (_movementAnimator != null)
            {
                _movementAnimator.SetRotationLocked(false);
                _movementAnimator.SetRotateToMovement(rotateToMovementWhenNoTarget);
            }
        }

        private void RotateTo(Vector3 targetPos)
        {
            var from = transform.position;
            var dir = new Vector3(targetPos.x - from.x, 0f, targetPos.z - from.z);
            if (dir.sqrMagnitude < 0.000001f) return;
            dir.Normalize();
            var targetRot = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationLerp * Time.deltaTime);
        }

        private float ResolveMaxRange()
        {
            var profile = GameRoot.Instance?.Services?.Get<IProfileService>();
            var loadout = profile?.ServerLoadout;
            float max = 0f;
            if (loadout != null)
            {
                TryMax(loadout.AttackSkillId, ref max);
                TryMax(loadout.SupportASkillId, ref max);
                TryMax(loadout.SupportBSkillId, ref max);
                TryMax(loadout.MovementSkillId, ref max);
            }

            if (max <= 0f && fallbackRange > 0f)
            {
                max = fallbackRange;
            }

            return max;
        }

        private static void TryMax(string skillId, ref float max)
        {
            if (SkillRangeCatalog.TryGetRange(skillId, out var range) && range > max)
            {
                max = range;
            }
        }

        private Transform FindNearestTarget(float range)
        {
            var origin = transform.position;

            Transform best = null;
            var bestSq = range * range;
            foreach (var tr in NetworkMonstersReplicator.AllTransforms)
            {
                if (tr == null || !tr.gameObject.activeInHierarchy) continue;
                var delta = tr.position - origin;
                delta.y = 0f;
                var distSq = delta.sqrMagnitude;
                if (distSq <= bestSq)
                {
                    bestSq = distSq;
                    best = tr;
                }
            }

            return best;
        }
    }
}
