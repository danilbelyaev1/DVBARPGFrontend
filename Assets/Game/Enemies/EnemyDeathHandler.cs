using DVBARPG.Core;
using DVBARPG.Core.Services;
using DVBARPG.Net.Events;
using UnityEngine;
using UnityEngine.AI;

namespace DVBARPG.Game.Enemies
{
    public sealed class EnemyDeathHandler : MonoBehaviour
    {
        private ICombatService _combat;
        private NavMeshAgent _agent;
        private Collider _collider;
        private Rigidbody _rb;
        private string _entityId;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _collider = GetComponent<Collider>();
            _rb = GetComponent<Rigidbody>();
            var ce = GetComponent<DVBARPG.Core.Combat.ICombatEntity>();
            _entityId = ce != null ? ce.EntityId : "";
        }

        private void OnEnable()
        {
            _combat = GameRoot.Instance.Services.Get<ICombatService>();
            _combat.Death += OnDeath;
        }

        private void OnDisable()
        {
            if (_combat != null) _combat.Death -= OnDeath;
        }

        private void OnDeath(EvtDeath evt)
        {
            if (evt.EntityId != _entityId) return;

            if (_agent != null) _agent.enabled = false;
            if (_collider != null) _collider.enabled = false;
            if (_rb != null) _rb.isKinematic = true;

            gameObject.SetActive(false);
        }
    }
}
