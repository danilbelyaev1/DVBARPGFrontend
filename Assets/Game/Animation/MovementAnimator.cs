using UnityEngine;
using DVBARPG.Tools;

namespace DVBARPG.Game.Animation
{
    [RequireComponent(typeof(Animator))]
    public sealed class MovementAnimator : MonoBehaviour
    {
        [Header("Animator")]
        [Tooltip("Ссылка на Animator.")]
        [SerializeField] private Animator animator;

        [Header("Параметры")]
        [Tooltip("Порог для IsMoving (0 = сразу выключать).")]
        [SerializeField] private float movingThreshold = 0.0f;
        [Header("Поворот")]
        [Tooltip("Поворачивать объект по направлению движения.")]
        [SerializeField] private bool rotateToMovement = false;
        [Tooltip("Скорость сглаживания поворота.")]
        [SerializeField] private float rotationLerp = 10f;

        private Vector3 _lastPos;

        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            _lastPos = transform.position;
        }

        private void Update()
        {
            using (RuntimeProfiler.Sample("MovementAnimator.Update"))
            {
                var pos = transform.position;
                var delta = pos - _lastPos;
                _lastPos = pos;

            var speedNow = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            var isMoving = speedNow > movingThreshold;

            // Только флаг движения, без параметра скорости.
            animator.SetBool(IsMovingHash, isMoving);

                if (rotateToMovement && delta.sqrMagnitude > 0.0001f)
                {
                    // Поворачиваем объект в сторону фактического движения.
                    var dir = new Vector3(delta.x, 0f, delta.z).normalized;
                    var targetRot = Quaternion.LookRotation(dir, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationLerp * Time.deltaTime);
                }
            }
        }
    }
}
