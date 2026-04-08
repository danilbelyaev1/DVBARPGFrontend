using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using DVBARPG.Tools;

namespace DVBARPG.Game.Camera
{
    public sealed class TopDownFollowCamera : MonoBehaviour
    {
        [Header("Камера")]
        [Tooltip("Цель, за которой следует камера.")]
        [SerializeField] private Transform target;
        [Tooltip("Смещение камеры относительно цели.")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 10f, -6f);
        [Tooltip("Скорость сглаживания следования.")]
        [SerializeField] private float followSpeed = 12f;
        [Tooltip("Жёстко фиксировать камеру на цели (без сглаживания).")]
        [SerializeField] private bool lockToTarget = true;
        [Header("Зум")]
        [Tooltip("Скорость зума колёсиком мыши.")]
        [SerializeField] private float zoomSpeed = 5f;
        [Tooltip("Минимальная дистанция до цели.")]
        [SerializeField] private float minDistance = 4f;
        [Tooltip("Максимальная дистанция до цели.")]
        [SerializeField] private float maxDistance = 18f;
        [Header("Анти-окклюзия")]
        [Tooltip("Включить авто-приближение камеры при перекрытии цели объектами.")]
        [SerializeField] private bool enableOcclusionAvoidance = true;
        [Tooltip("Слои, которые считаются препятствиями между камерой и целью.")]
        [SerializeField] private LayerMask occlusionMask = ~0;
        [Tooltip("Радиус SphereCast для проверки перекрытий.")]
        [SerializeField] private float occlusionCastRadius = 0.25f;
        [Tooltip("Небольшой отступ от препятствия, чтобы не клипаться.")]
        [SerializeField] private float occlusionPadding = 0.2f;
        [Tooltip("Скорость приближения/возврата дистанции при окклюзии.")]
        [SerializeField] private float occlusionDistanceLerp = 14f;

        private float _distance;
        private float _userDistance;
        private Vector3 _offsetDir;

        private void Awake()
        {
            _distance = offset.magnitude;
            _offsetDir = _distance > 0.001f ? offset.normalized : Vector3.back;
            // При старте сцены сразу ставим камеру на максимальную дистанцию.
            _distance = maxDistance;
            _userDistance = _distance;
        }

        private void LateUpdate()
        {
            using (RuntimeProfiler.Sample("TopDownFollowCamera.LateUpdate"))
            {
                if (target == null) return;

                // Следуем за целью и смотрим на неё.
                ApplyZoomInput();
                var desiredDistance = ResolveDesiredDistance();
                var lerpT = 1f - Mathf.Exp(-Mathf.Max(0.01f, occlusionDistanceLerp) * Time.deltaTime);
                _distance = Mathf.Lerp(_distance, desiredDistance, lerpT);
                offset = _offsetDir * _distance;
                var desired = target.position + offset;
                if (lockToTarget)
                {
                    transform.position = desired;
                }
                else
                {
                    transform.position = Vector3.Lerp(transform.position, desired, followSpeed * Time.deltaTime);
                }
                transform.LookAt(target.position, Vector3.up);
            }
        }

        private void ApplyZoomInput()
        {
            float scroll = 0f;
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null)
            {
                scroll = mouse.scroll.ReadValue().y * 0.01f;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            scroll = Input.mouseScrollDelta.y;
#endif
            if (Mathf.Abs(scroll) < 0.0001f) return;

            _distance = Mathf.Clamp(_distance - scroll * zoomSpeed, minDistance, maxDistance);
            _userDistance = _distance;
        }

        private float ResolveDesiredDistance()
        {
            var baseDistance = Mathf.Clamp(_userDistance, minDistance, maxDistance);
            if (!enableOcclusionAvoidance || target == null) return baseDistance;

            var origin = target.position;
            var desiredCameraPos = origin + _offsetDir * baseDistance;
            var castDir = (desiredCameraPos - origin).normalized;
            var castLen = baseDistance;
            if (castLen <= 0.001f) return baseDistance;

            var hits = Physics.SphereCastAll(
                origin,
                Mathf.Max(0.01f, occlusionCastRadius),
                castDir,
                castLen,
                occlusionMask,
                QueryTriggerInteraction.Ignore);

            var nearest = float.PositiveInfinity;
            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h.collider == null) continue;
                var hitTr = h.collider.transform;
                if (hitTr == null) continue;
                if (hitTr.IsChildOf(target)) continue;
                if (hitTr.IsChildOf(transform)) continue;
                if (h.distance < nearest) nearest = h.distance;
            }

            if (float.IsPositiveInfinity(nearest)) return baseDistance;
            var safeDistance = Mathf.Clamp(nearest - Mathf.Max(0f, occlusionPadding), minDistance, baseDistance);
            return safeDistance;
        }
    }
}
