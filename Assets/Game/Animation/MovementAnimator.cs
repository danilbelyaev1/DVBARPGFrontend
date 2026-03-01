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
        [Tooltip("Порог включения IsMoving.")]
        [SerializeField] private float movingOnThreshold = 0.15f;
        [Tooltip("Порог выключения IsMoving (должен быть меньше порога включения).")]
        [SerializeField] private float movingOffThreshold = 0.05f;
        [Tooltip("Сглаживание скорости (сек).")]
        [SerializeField] private float speedSmoothingTime = 0.1f;
        [Tooltip("Мгновенно выключать IsMoving при нулевом смещении.")]
        [SerializeField] private bool instantStop = true;
        [Header("Поворот")]
        [Tooltip("Поворачивать объект по направлению движения.")]
        [SerializeField] private bool rotateToMovement = false;
        [Tooltip("Скорость сглаживания поворота.")]
        [SerializeField] private float rotationLerp = 10f;

        private Vector3 _lastPos;
        private float _smoothedSpeed;
        private bool _isMoving;
        private bool _rotationLocked;

        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int AttackRangedHash = Animator.StringToHash("AttackRanged");

        public void TriggerAttack(bool ranged)
        {
            if (animator == null) return;
            animator.SetTrigger(ranged ? AttackRangedHash : AttackHash);
        }

        public void SetRotationLocked(bool locked)
        {
            _rotationLocked = locked;
        }

        public void SetRotateToMovement(bool enabled)
        {
            rotateToMovement = enabled;
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            _lastPos = transform.position;
        }

        private void Update()
        {
            using (RuntimeProfiler.Sample("MovementAnimator.Update"))
            {
                var pos = transform.position;
                var delta = pos - _lastPos;
                _lastPos = pos;

            if (instantStop && delta.sqrMagnitude < 0.0000001f)
            {
                _smoothedSpeed = 0f;
                _isMoving = false;
                animator.SetBool(IsMovingHash, _isMoving);
            }
            else
            {
            var speedNow = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            var alpha = 1f - Mathf.Exp(-Mathf.Max(Time.deltaTime, 0.0001f) / Mathf.Max(speedSmoothingTime, 0.001f));
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, speedNow, alpha);

            if (_isMoving)
            {
                if (_smoothedSpeed <= movingOffThreshold)
                {
                    _isMoving = false;
                }
            }
            else
            {
                if (_smoothedSpeed >= movingOnThreshold)
                {
                    _isMoving = true;
                }
            }

            // Только флаг движения, без параметра скорости.
            animator.SetBool(IsMovingHash, _isMoving);
            }

                if (rotateToMovement && !_rotationLocked && delta.sqrMagnitude > 0.0001f)
                {
                    // Поворачиваем объект в сторону фактического движения.
                    var dir = new Vector3(delta.x, 0f, delta.z);
                    if (dir.sqrMagnitude > 0.000001f)
                    {
                        dir.Normalize();
                        var targetRot = Quaternion.LookRotation(dir, Vector3.up);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationLerp * Time.deltaTime);
                    }
                }
            }
        }
    }
}
