using System;
using System.Collections.Generic;
using DVBARPG.Core.Combat;
using DVBARPG.Core.Services;
using DVBARPG.Net.Events;
using UnityEngine;

namespace DVBARPG.Net.Local
{
    public sealed class LocalCombatService : ICombatService
    {
        public event Action<EvtDamage> Damage;
        public event Action<EvtDeath> Death;
        public event Action<EvtDrop> Drop;

        private readonly Dictionary<string, ICombatEntity> _entities = new();
        private readonly Dictionary<string, float> _nextAttackTime = new();

        public void RegisterEntity(ICombatEntity entity)
        {
            if (entity == null || string.IsNullOrEmpty(entity.EntityId)) return;
            _entities[entity.EntityId] = entity;
        }

        public void UnregisterEntity(string entityId)
        {
            if (string.IsNullOrEmpty(entityId)) return;
            _entities.Remove(entityId);
            _nextAttackTime.Remove(entityId);
        }

        public void RequestHit(string attackerId, string targetId, string skillId)
        {
            if (!_entities.TryGetValue(attackerId, out var attacker))
            {
                Debug.LogWarning($"LocalCombatService: attacker not found '{attackerId}'.");
                return;
            }
            if (!_entities.TryGetValue(targetId, out var target))
            {
                Debug.LogWarning($"LocalCombatService: target not found '{targetId}'.");
                return;
            }

            var atkSpeed = Mathf.Max(0.01f, attacker.Stats.AttackSpeed);
            var now = Time.time;
            if (_nextAttackTime.TryGetValue(attackerId, out var next) && now < next) return;
            _nextAttackTime[attackerId] = now + (1f / atkSpeed);

            var result = CalcDamage(attacker, target);
            target.ApplyDamage(result);

            Damage?.Invoke(new EvtDamage
            {
                AttackerId = attackerId,
                TargetId = targetId,
                Amount = result.Amount,
                IsCrit = result.IsCrit,
                TargetRemainingHp = result.TargetRemainingHp
            });

            if (result.TargetRemainingHp <= 0)
            {
                Death?.Invoke(new EvtDeath
                {
                    EntityId = targetId,
                    KillerId = attackerId
                });

                Drop?.Invoke(new EvtDrop
                {
                    EntityId = targetId
                });
            }
        }

        private static DamageResult CalcDamage(ICombatEntity attacker, ICombatEntity target)
        {
            var baseDamage = Mathf.Max(1, attacker.Stats.Damage);
            var isCrit = UnityEngine.Random.value < Mathf.Clamp01(attacker.Stats.CritChance);

            var critMulti = Mathf.Max(1f, attacker.Stats.CritMulti);
            var raw = isCrit ? Mathf.RoundToInt(baseDamage * critMulti) : baseDamage;

            var armor = Mathf.Max(0, target.Stats.Armor);
            var reduced = Mathf.RoundToInt(raw * (100f / (100f + armor)));
            var final = Mathf.Max(1, reduced);

            var remaining = Mathf.Max(0, target.CurrentHp - final);

            return new DamageResult
            {
                Amount = final,
                IsCrit = isCrit,
                TargetRemainingHp = remaining
            };
        }
    }
}
