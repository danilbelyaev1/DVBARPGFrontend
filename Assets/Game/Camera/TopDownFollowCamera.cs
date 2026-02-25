using UnityEngine;

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

        private void LateUpdate()
        {
            if (target == null) return;

            // Плавно следуем за целью и смотрим на неё.
            var desired = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desired, followSpeed * Time.deltaTime);
            transform.LookAt(target.position, Vector3.up);
        }
    }
}
