using DVBARPG.Core;
using DVBARPG.Core.Services;
using DVBARPG.Net.Commands;
using UnityEngine;
using DVBARPG.Tools;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DVBARPG.Game.Player
{
    public enum InputMode
    {
        Wasd,
        MouseFollow,
        TouchJoystick
    }

    public sealed class PlayerInputController : MonoBehaviour
    {
        [Header("Управление")]
        [Tooltip("Режим ввода: клавиатура, мышь или тач-джойстик.")]
        [SerializeField] private InputMode inputMode = InputMode.Wasd;
        [Tooltip("Скорость движения (используется для отправки в сеть).")]
        [SerializeField] private float moveSpeed = 4.5f;
        [Tooltip("Камера для режима MouseFollow.")]
        [SerializeField] private UnityEngine.Camera viewCamera;
        [Tooltip("Ссылка на компонент тач-джойстика.")]
        [SerializeField] private JoystickInput joystick;

        private string _entityId;
        private ISessionService _session;
        private Transform _self;
        private bool _wasMoving;

        private void Awake()
        {
            _self = transform;
            _session = GameRoot.Instance.Services.Get<ISessionService>();

            var profile = GameRoot.Instance.Services.Get<IProfileService>();
            _entityId = profile.CurrentAuth != null ? profile.CurrentAuth.PlayerId : "local-player";
        }

        private void Update()
        {
            using (RuntimeProfiler.Sample("PlayerInputController.Update"))
            {
            // Если нет сессии — не отправляем ввод.
            if (_session == null) return;
            if (!CanMove()) return;

            var dir = ReadMoveDirection();
            if (dir.sqrMagnitude <= 0.0001f)
            {
                if (_wasMoving)
                {
                    // Сообщаем серверу, что ввод остановлен.
                    _session.Send(new DVBARPG.Net.Commands.CmdStop());
                    _wasMoving = false;
                }
                return;
            }

            if (dir.sqrMagnitude > 1f)
            {
                // Нормализуем ввод, чтобы сервер не отвергал диагональ.
                dir.Normalize();
            }

            // Отправляем намерение движения на сервер.
            _session.Send(new CmdMove
            {
                EntityId = _entityId,
                Direction = dir,
                Speed = moveSpeed,
                DeltaTime = Time.deltaTime
            });
            _wasMoving = true;
            }
        }

        private bool CanMove()
        {
            if (_session is DVBARPG.Net.Network.NetworkSessionRunner)
            {
                if (NetworkPlayerReplicator.CurrentHp == 0)
                {
                    if (_wasMoving)
                    {
                        // Если игрок мёртв — прекращаем движение.
                        _session.Send(new DVBARPG.Net.Commands.CmdStop());
                        _wasMoving = false;
                    }
                    return false;
                }
            }

            return true;
        }

        private Vector3 ReadMoveDirection()
        {
            switch (inputMode)
            {
                case InputMode.Wasd:
                    return ReadWasd();
                case InputMode.MouseFollow:
                    return ReadMouseFollow();
                case InputMode.TouchJoystick:
                    return ReadTouchJoystick();
                default:
                    return Vector3.zero;
            }
        }

        private static Vector3 ReadWasd()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return Vector3.zero;

            var x = 0f;
            if (k.aKey.isPressed || k.leftArrowKey.isPressed) x -= 1f;
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) x += 1f;

            var z = 0f;
            if (k.wKey.isPressed || k.upArrowKey.isPressed) z += 1f;
            if (k.sKey.isPressed || k.downArrowKey.isPressed) z -= 1f;

            return new Vector3(x, 0f, z);
#elif ENABLE_LEGACY_INPUT_MANAGER
            var x = Input.GetAxisRaw("Horizontal");
            var z = Input.GetAxisRaw("Vertical");
            return new Vector3(x, 0f, z);
#else
            return Vector3.zero;
#endif
        }

        private Vector3 ReadMouseFollow()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.isPressed) return Vector3.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (!Input.GetMouseButton(0)) return Vector3.zero;
#else
            return Vector3.zero;
#endif

            var cam = viewCamera != null ? viewCamera : UnityEngine.Camera.main;
            if (cam == null) return Vector3.zero;

#if ENABLE_INPUT_SYSTEM
            var screenPos = mouse.position.ReadValue();
#else
            var screenPos = (Vector2)Input.mousePosition;
#endif

            var ray = cam.ScreenPointToRay(screenPos);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out var enter)) return Vector3.zero;

            var hit = ray.GetPoint(enter);
            var dir = hit - _self.position;
            dir.y = 0f;
            return dir.normalized;
        }

        private Vector3 ReadTouchJoystick()
        {
            if (joystick == null) return Vector3.zero;
            var dir = joystick.Direction;
            return new Vector3(dir.x, 0f, dir.y);
        }
    }
}
