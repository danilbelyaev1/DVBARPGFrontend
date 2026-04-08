using UnityEngine;

namespace DVBARPG.Game.Network
{
    public sealed class EnemySkinResolver : MonoBehaviour
    {
        [Header("Каталоги")]
        [Tooltip("Каталог skinId -> визуал/animationSetId.")]
        [SerializeField] private EnemySkinCatalog skinCatalog;
        [Tooltip("Каталог animationSetId -> controller/override/avatar.")]
        [SerializeField] private EnemyAnimationSetCatalog animationCatalog;

        [Header("Fallback")]
        [Tooltip("Базовый визуал, если skinId не найден.")]
        [SerializeField] private GameObject fallbackVisualPrefab;
        [Tooltip("Fallback контроллер для анимаций.")]
        [SerializeField] private RuntimeAnimatorController fallbackController;

        private EnemyVisualHost _visualHost;
        private EnemyAnimationBinder _animationBinder;

        public void Initialize(
            EnemySkinCatalog skins,
            EnemyAnimationSetCatalog animations,
            GameObject fallbackVisual,
            RuntimeAnimatorController fallbackAnimController)
        {
            skinCatalog = skins;
            animationCatalog = animations;
            if (fallbackVisual != null) fallbackVisualPrefab = fallbackVisual;
            if (fallbackAnimController != null) fallbackController = fallbackAnimController;
        }

        public bool ApplySkin(string monsterType, string requestedSkinId)
        {
            EnsureDependencies();

            EnemySkinCatalog.Entry skin = null;
            if (!string.IsNullOrWhiteSpace(requestedSkinId) && skinCatalog != null)
                skinCatalog.TryGetBySkinId(requestedSkinId, out skin);
            if (skin == null && skinCatalog != null)
                skinCatalog.TryGetDefaultForType(monsterType, out skin);

            var prefab = skin?.fallbackPrefab != null ? skin.fallbackPrefab : fallbackVisualPrefab;
            if (prefab == null)
            {
                Debug.LogWarning($"[EnemySkin] No visual prefab found. type={monsterType}, skinId={requestedSkinId}");
                return false;
            }

            var useFallbackVisual = skin?.fallbackPrefab == null;
            _visualHost.SetVisual(prefab, useFallbackVisual);
            _animationBinder.Initialize(animationCatalog, fallbackController);
            _animationBinder.Apply(skin?.animationSetId, _visualHost.CurrentVisual);
            return true;
        }

        private void EnsureDependencies()
        {
            if (_visualHost == null)
                _visualHost = gameObject.GetComponent<EnemyVisualHost>() ?? gameObject.AddComponent<EnemyVisualHost>();
            if (_animationBinder == null)
                _animationBinder = gameObject.GetComponent<EnemyAnimationBinder>() ?? gameObject.AddComponent<EnemyAnimationBinder>();
        }
    }
}
