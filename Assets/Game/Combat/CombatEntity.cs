using DVBARPG.Core;
using DVBARPG.Core.Combat;
using DVBARPG.Core.Services;
using UnityEngine;

namespace DVBARPG.Game.Combat
{
    public sealed class CombatEntity : MonoBehaviour, ICombatEntity
    {
        private static readonly System.Collections.Generic.Dictionary<string, Transform> Registry = new();

        [SerializeField] private string entityId = "";
        [SerializeField] private EntityStats stats = new EntityStats
        {
            MaxHp = 100,
            Damage = 10,
            AttackSpeed = 1.2f,
            CritChance = 0.05f,
            CritMulti = 1.5f,
            Armor = 0
        };

        private int _currentHp;
        private ICombatService _combat;

        public string EntityId => entityId;
        public EntityStats Stats => stats;
        public int CurrentHp => _currentHp;

        private void Awake()
        {
            _currentHp = Mathf.Max(1, stats.MaxHp);
        }

        private void Start()
        {
            if (string.IsNullOrEmpty(entityId))
            {
                var profile = GameRoot.Instance.Services.Get<IProfileService>();
                if (profile.CurrentAuth != null && !string.IsNullOrEmpty(profile.CurrentAuth.PlayerId))
                {
                    entityId = profile.CurrentAuth.PlayerId;
                }
            }

            _combat = GameRoot.Instance.Services.Get<ICombatService>();
            _combat.RegisterEntity(this);

            if (!string.IsNullOrEmpty(entityId))
            {
                Registry[entityId] = transform;
            }
        }

        private void OnDestroy()
        {
            if (_combat != null && !string.IsNullOrEmpty(entityId))
            {
                _combat.UnregisterEntity(entityId);
            }

            if (!string.IsNullOrEmpty(entityId))
            {
                Registry.Remove(entityId);
            }
        }

        public void ApplyDamage(DamageResult result)
        {
            _currentHp = result.TargetRemainingHp;
        }

        public static bool TryGetTransform(string id, out Transform tr)
        {
            return Registry.TryGetValue(id, out tr);
        }
    }
}
