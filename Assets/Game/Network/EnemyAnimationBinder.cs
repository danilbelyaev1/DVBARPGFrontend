using DVBARPG.Game.Animation;
using UnityEngine;

namespace DVBARPG.Game.Network
{
    public sealed class EnemyAnimationBinder : MonoBehaviour
    {
        [Header("Источники")]
        [Tooltip("Каталог наборов анимаций по animationSetId.")]
        [SerializeField] private EnemyAnimationSetCatalog animationCatalog;

        [Header("Fallback")]
        [Tooltip("Fallback-контроллер, если набор не найден.")]
        [SerializeField] private RuntimeAnimatorController fallbackController;

        public void Initialize(EnemyAnimationSetCatalog catalog, RuntimeAnimatorController fallback)
        {
            animationCatalog = catalog;
            if (fallback != null) fallbackController = fallback;
        }

        public void Apply(string animationSetId, GameObject visualInstance)
        {
            if (visualInstance == null) return;
            var animator = visualInstance.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                Debug.LogWarning("[EnemySkin] Animator not found on visual instance.");
                return;
            }

            RuntimeAnimatorController controller = fallbackController;
            Avatar avatar = null;

            if (animationCatalog != null && animationCatalog.TryGet(animationSetId, out var entry))
            {
                if (entry.overrideController != null) controller = entry.overrideController;
                else if (entry.baseController != null) controller = entry.baseController;
                if (entry.avatar != null) avatar = entry.avatar;
            }

            if (controller != null)
                animator.runtimeAnimatorController = controller;
            if (avatar != null)
                animator.avatar = avatar;

            var driver = visualInstance.GetComponentInChildren<MonsterAnimationDriver>(true);
            if (driver == null)
                Debug.LogWarning("[EnemySkin] MonsterAnimationDriver not found on visual instance.");
        }
    }
}
