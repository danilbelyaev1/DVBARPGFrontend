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
        [Tooltip("Сглаживание параметров направления (сек).")]
        [SerializeField] private float directionSmoothingTime = 0.08f;
        [Tooltip("Базовая скорость движения для нормализации параметра Speed.")]
        [SerializeField] private float baseMoveSpeed = 4.0f;
        [Tooltip("Значение параметра Speed при базовой скорости.")]
        [SerializeField] private float speedAtBase = 0.1f;
        [Tooltip("Максимальное значение параметра Speed.")]
        [SerializeField] private float maxSpeedParam = 2.0f;
        [Tooltip("Мгновенно выключать IsMoving при нулевом смещении.")]
        [SerializeField] private bool instantStop = true;
        [Header("Поворот")]
        [Tooltip("Поворачивать объект по направлению движения.")]
        [SerializeField] private bool rotateToMovement = false;
        [Tooltip("Скорость сглаживания поворота.")]
        [SerializeField] private float rotationLerp = 10f;

        private Vector3 _lastPos;
        private float _smoothedSpeed;
        private Vector2 _smoothedDir;
        private bool _isMoving;
        private bool _rotationLocked;

        // Кэш наличия параметров Animator, чтобы не сканировать их каждый кадр.
        private bool _paramsScanned;
        private bool _hasIsMovingBool;
        private bool _hasSpeedFloat;
        private bool _hasMoveXFloat;
        private bool _hasMoveYFloat;

        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveYHash = Animator.StringToHash("MoveY");
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

        private bool HasParam(int nameHash, AnimatorControllerParameterType type)
        {
            if (animator == null) return false;
            for (int i = 0; i < animator.parameterCount; i++)
            {
                var p = animator.GetParameter(i);
                if (p.nameHash == nameHash && p.type == type) return true;
            }
            return false;
        }

        private bool HasFloatParam(int nameHash) => HasParam(nameHash, AnimatorControllerParameterType.Float);
        private bool HasBoolParam(int nameHash) => HasParam(nameHash, AnimatorControllerParameterType.Bool);

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null)
                animator.applyRootMotion = false; // поворот и сдвиг корня только из кода (PlayerTargetFacing, движение)
            _lastPos = transform.position;

            // Один раз подтягиваем базовую скорость из профиля, если она не задана.
            if (baseMoveSpeed <= 0.001f)
            {
                var root = DVBARPG.Core.GameRoot.Instance;
                var services = root != null ? root.Services : null;
                var profile = services != null ? services.Get<DVBARPG.Core.Services.IProfileService>() : null;
                if (profile != null && profile.BaseMoveSpeed > 0.001f)
                {
                    baseMoveSpeed = profile.BaseMoveSpeed;
                }
            }

            CacheAnimatorParams();
        }

        private void CacheAnimatorParams()
        {
            if (animator == null || _paramsScanned) return;
            _paramsScanned = true;
            for (int i = 0; i < animator.parameterCount; i++)
            {
                var p = animator.GetParameter(i);
                if (p.nameHash == IsMovingHash && p.type == AnimatorControllerParameterType.Bool) _hasIsMovingBool = true;
                if (p.nameHash == SpeedHash && p.type == AnimatorControllerParameterType.Float) _hasSpeedFloat = true;
                if (p.nameHash == MoveXHash && p.type == AnimatorControllerParameterType.Float) _hasMoveXFloat = true;
                if (p.nameHash == MoveYHash && p.type == AnimatorControllerParameterType.Float) _hasMoveYFloat = true;
            }
        }

        private void Update()
        {
            using (RuntimeProfiler.Sample("MovementAnimator.Update"))
            {
                var pos = transform.position;
                var delta = pos - _lastPos;
                _lastPos = pos;

                var dt = Mathf.Max(Time.deltaTime, 0.0001f);
                var localVel = transform.InverseTransformDirection(new Vector3(delta.x, 0f, delta.z)) / dt;
                var targetDir = new Vector2(localVel.x, localVel.z);
                var dirAlpha = 1f - Mathf.Exp(-dt / Mathf.Max(directionSmoothingTime, 0.001f));
                _smoothedDir = Vector2.Lerp(_smoothedDir, targetDir, dirAlpha);

                if (instantStop && delta.sqrMagnitude < 0.0000001f)
                {
                    _smoothedSpeed = 0f;
                    _isMoving = false;
                    if (_hasIsMovingBool) animator.SetBool(IsMovingHash, _isMoving);
                }
                else
                {
                    var speedNow = delta.magnitude / dt;
                    var alpha = 1f - Mathf.Exp(-dt / Mathf.Max(speedSmoothingTime, 0.001f));
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

                    if (_hasIsMovingBool) animator.SetBool(IsMovingHash, _isMoving);
                }

                // Параметры направления для Blend Tree (только если есть в Controller).
                if (_hasSpeedFloat) animator.SetFloat(SpeedHash, Mathf.Clamp(baseMoveSpeed > 0.001f ? speedAtBase * (_smoothedSpeed / baseMoveSpeed) : speedAtBase, speedAtBase, maxSpeedParam));
                if (_hasMoveXFloat) animator.SetFloat(MoveXHash, _smoothedDir.x);
                if (_hasMoveYFloat) animator.SetFloat(MoveYHash, _smoothedDir.y);

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
