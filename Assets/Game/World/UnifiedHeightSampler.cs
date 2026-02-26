using System.Collections.Generic;
using UnityEngine;

namespace DVBARPG.Game.World
{
    public sealed class UnifiedHeightSampler : MonoBehaviour
    {
        public static UnifiedHeightSampler Current { get; private set; }

        [Header("Высота")]
        [Tooltip("Terrain, от которого берём высоту (fallback). Если не задан — найдём автоматически.")]
        [SerializeField] private Terrain terrain;
        [Tooltip("Максимальная дистанция луча вниз (м).")]
        [SerializeField] private float rayDistance = 200f;
        [Tooltip("Слои, по которым ищем землю.")]
        [SerializeField] private LayerMask groundMask = ~0;
        [Tooltip("Смещение по высоте.")]
        [SerializeField] private float heightOffset = 0f;

        [Header("Исключения")]
        [Tooltip("Слои, которые игнорируем при поиске земли.")]
        [SerializeField] private LayerMask ignoreLayers = 0;
        [Tooltip("Коллайдеры, которые игнорируем при поиске земли.")]
        [SerializeField] private List<Collider> ignoreColliders = new();
        [Tooltip("Корни, чьи дочерние коллайдеры нужно игнорировать.")]
        [SerializeField] private List<Transform> ignoreRoots = new();

        private static readonly RaycastHit[] RayHits = new RaycastHit[32];

        private void Awake()
        {
            Current = this;
            if (terrain == null) terrain = GetComponentInParent<Terrain>();
            if (terrain == null) terrain = Terrain.activeTerrain;
        }

        private void OnDisable()
        {
            if (Current == this) Current = null;
        }

        public static float SampleHeight(Vector3 worldPosition)
        {
            var sampler = Current != null ? Current : FindFirstObjectByType<UnifiedHeightSampler>();
            if (sampler == null) return worldPosition.y;
            return sampler.SampleHeightInternal(worldPosition);
        }

        private float SampleHeightInternal(Vector3 worldPosition)
        {
            // Сначала пытаемся попасть по коллайдерам (меши, лестницы, декор).
            var origin = worldPosition + Vector3.up * (rayDistance * 0.5f);
            var count = Physics.RaycastNonAlloc(origin, Vector3.down, RayHits, rayDistance, groundMask, QueryTriggerInteraction.Ignore);
            if (count > 0)
            {
                float bestDist = float.MaxValue;
                bool found = false;
                Vector3 bestPoint = Vector3.zero;

                for (int i = 0; i < count; i++)
                {
                    var hit = RayHits[i];
                    if (hit.collider == null) continue;
                    if (IsIgnored(hit.collider)) continue;
                    if (hit.distance < bestDist)
                    {
                        bestDist = hit.distance;
                        bestPoint = hit.point;
                        found = true;
                    }
                }

                if (found)
                {
                    return bestPoint.y + heightOffset;
                }
            }

            // Если ничего не нашли — берём высоту Terrain.
            if (terrain != null)
            {
                return terrain.SampleHeight(worldPosition) + terrain.GetPosition().y + heightOffset;
            }

            return worldPosition.y;
        }

        private bool IsIgnored(Collider col)
        {
            if (((1 << col.gameObject.layer) & ignoreLayers) != 0) return true;

            for (int i = 0; i < ignoreColliders.Count; i++)
            {
                if (ignoreColliders[i] == col) return true;
            }

            for (int i = 0; i < ignoreRoots.Count; i++)
            {
                var root = ignoreRoots[i];
                if (root != null && col.transform.IsChildOf(root)) return true;
            }

            return false;
        }
    }
}
