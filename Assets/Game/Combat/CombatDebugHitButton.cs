using DVBARPG.Core;
using DVBARPG.Core.Services;
using UnityEngine;

namespace DVBARPG.Game.Combat
{
    public sealed class CombatDebugHitButton : MonoBehaviour
    {
        [SerializeField] private string attackerId;
        [SerializeField] private string targetId;
        [SerializeField] private string skillId = "basic";

        public void Hit()
        {
            Debug.Log("CombatDebugHitButton: Hit()");
            var combat = GameRoot.Instance.Services.Get<ICombatService>();
            var attacker = attackerId;

            if (string.IsNullOrEmpty(attacker))
            {
                var profile = GameRoot.Instance.Services.Get<IProfileService>();
                attacker = profile.CurrentAuth != null ? profile.CurrentAuth.PlayerId : null;
            }

            if (string.IsNullOrEmpty(attacker))
            {
                Debug.LogWarning("CombatDebugHitButton: AttackerId is empty.");
                return;
            }
            if (string.IsNullOrEmpty(targetId))
            {
                Debug.LogWarning("CombatDebugHitButton: TargetId is empty.");
                return;
            }

            combat.RequestHit(attacker, targetId, skillId);
        }
    }
}
