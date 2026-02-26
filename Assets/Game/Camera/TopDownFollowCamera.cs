using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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

        private float _distance;
        private Vector3 _offsetDir;

        private void Awake()
        {
            _distance = offset.magnitude;
            _offsetDir = _distance > 0.001f ? offset.normalized : Vector3.back;
            // При старте сцены сразу ставим камеру на максимальную дистанцию.
            _distance = maxDistance;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // Следуем за целью и смотрим на неё.
            ApplyZoomInput();
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
        }
    }
}
