using UnityEngine;

namespace DVBARPG.Game.Animation
{
    [RequireComponent(typeof(Animator))]
    public sealed class MovementAnimator : MonoBehaviour
    {
        [Header("Animator")]
        [Tooltip("Ссылка на Animator.")]
        [SerializeField] private Animator animator;

        [Header("Параметры")]
        [Tooltip("Скорость сглаживания параметра Speed.")]
        [SerializeField] private float speedSmooth = 10f;
        [Tooltip("Порог для IsMoving.")]
        [SerializeField] private float movingThreshold = 0.05f;

        private float _speed;
        private Vector3 _lastPos;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            _lastPos = transform.position;
        }

        private void Update()
        {
            var pos = transform.position;
            var delta = pos - _lastPos;
            _lastPos = pos;

            var speedNow = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            _speed = Mathf.Lerp(_speed, speedNow, speedSmooth * Time.deltaTime);

            var isMoving = _speed > movingThreshold;

            animator.SetFloat(SpeedHash, _speed);
            animator.SetBool(IsMovingHash, isMoving);
        }
    }
}
