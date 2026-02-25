using DVBARPG.Core;
using DVBARPG.Core.Combat;
using DVBARPG.Core.Services;
using UnityEngine;
using UnityEngine.AI;

namespace DVBARPG.Game.Enemies
{
    public enum EnemyType
    {
        Melee,
        Ranged
    }

    public sealed class EnemyController : MonoBehaviour
    {
        [SerializeField] private EnemyType type = EnemyType.Melee;
        [SerializeField] private Transform target;
        [SerializeField] private float meleeAttackRange = 1.6f;
        [SerializeField] private float rangedMinRange = 3.0f;
        [SerializeField] private float rangedMaxRange = 7.0f;
        [SerializeField] private string skillId = "basic";

        private NavMeshAgent _agent;
        private ICombatService _combat;
        private ICombatEntity _self;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _self = GetComponent<ICombatEntity>();
            _combat = GameRoot.Instance.Services.Get<ICombatService>();
        }

        private void Update()
        {
            if (target == null || _agent == null || _self == null) return;

            var toTarget = target.position - transform.position;
            var dist = toTarget.magnitude;

            if (type == EnemyType.Melee)
            {
                UpdateMelee(dist);
            }
            else
            {
                UpdateRanged(dist, toTarget);
            }
        }

        private void UpdateMelee(float dist)
        {
            if (dist <= meleeAttackRange)
            {
                _agent.ResetPath();
                _combat.RequestHit(_self.EntityId, GetTargetId(), skillId);
            }
            else
            {
                _agent.SetDestination(target.position);
            }
        }

        private void UpdateRanged(float dist, Vector3 toTarget)
        {
            if (dist < rangedMinRange)
            {
                var away = transform.position - toTarget.normalized * rangedMinRange;
                _agent.SetDestination(away);
                return;
            }

            if (dist > rangedMaxRange)
            {
                _agent.SetDestination(target.position);
                return;
            }

            _agent.ResetPath();
            _combat.RequestHit(_self.EntityId, GetTargetId(), skillId);
        }

        private string GetTargetId()
        {
            var ce = target.GetComponent<ICombatEntity>();
            return ce != null ? ce.EntityId : null;
        }
    }
}
