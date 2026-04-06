using System.Collections.Generic;
using UnityEngine;

namespace DVBARPG.Game.World
{
    /// <summary>
    /// Рейкаст к коллайдерам/террейну для офлайн-инструментов и сцен без серверной высоты (например порталы).
    /// </summary>
    public sealed class UnifiedHeightSampler : MonoBehaviour
    {
        public static UnifiedHeightSampler Current { get; private set; }

        [Header("Высота")]
        [Tooltip("Terrain, от которого берём высоту (fallback). Если не задан — найдём автоматически.")]
        [SerializeField] private Terrain terrain;
        [Tooltip("Точка старта луча над позицией (м). Сначала луч идёт от pos + это значение вниз — так находим поверхность прямо под ногами.")]
        [SerializeField] private float rayStartAbove = 2f;
        [Tooltip("Максимальная дистанция луча вниз (м).")]
        [SerializeField] private float rayDistance = 20f;
        [Tooltip("Если луч от pos+rayStartAbove ничего не попал, пробуем луч с большой высоты (для первого кадра или лестниц).")]
        [SerializeField] private float fallbackRayStartAbove = 50f;
        [Tooltip("Слои, по которым ищем землю (коллайдеры). По умолчанию всё; сузьте до слоёв пола/террейна, чтобы не цеплять лишнее.")]
        [SerializeField] private LayerMask groundMask = ~0;
        [Tooltip("Смещение по высоте (например, половина высоты персонажа, если pivot в центре).")]
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
            // Луч от точки чуть выше позиции вниз — первое попадание = поверхность под ногами (коллайдер).
            var origin = worldPosition + Vector3.up * rayStartAbove;
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
                    return bestPoint.y + heightOffset;
            }

            // Запасной вариант: луч с большой высоты — берём самую верхнюю поверхность под лучом (пол, а не потолок).
            var fallbackOrigin = worldPosition + Vector3.up * fallbackRayStartAbove;
            var fallbackDist = fallbackRayStartAbove + rayDistance;
            count = Physics.RaycastNonAlloc(fallbackOrigin, Vector3.down, RayHits, fallbackDist, groundMask, QueryTriggerInteraction.Ignore);
            if (count > 0)
            {
                float topY = float.MinValue;
                bool found = false;

                for (int i = 0; i < count; i++)
                {
                    var hit = RayHits[i];
                    if (hit.collider == null) continue;
                    if (IsIgnored(hit.collider)) continue;
                    if (hit.point.y > topY)
                    {
                        topY = hit.point.y;
                        found = true;
                    }
                }

                if (found)
                    return topY + heightOffset;
            }

            if (terrain != null)
                return terrain.SampleHeight(worldPosition) + terrain.GetPosition().y + heightOffset;

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
