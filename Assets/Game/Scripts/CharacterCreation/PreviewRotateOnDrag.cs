using UnityEngine;
using UnityEngine.InputSystem;

namespace DVBARPG.Game.CharacterCreation
{
    /// <summary>
    /// Вращение объекта по зажатой ПКМ (удерживать и тянуть). Вешать на камеру превью или на пивот, вокруг которого стоит персонаж.
    /// Для камеры: вращаем этот transform (камера крутится вокруг цели). Для пивота с персонажем: вращаем пивот (персонаж крутится на месте).
    /// Требует Input System package (новый ввод).
    /// </summary>
    public sealed class PreviewRotateOnDrag : MonoBehaviour
    {
        [Tooltip("Скорость вращения по горизонтали (градусы на пиксель).")]
        [SerializeField] private float sensitivityY = 0.3f;
        [Tooltip("Вращать по вертикали (например для камеры).")]
        [SerializeField] private bool rotateVertical;
        [Tooltip("Скорость по вертикали.")]
        [SerializeField] private float sensitivityX = 0.2f;

        private float _yaw;
        private float _pitch;

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.rightButton.isPressed)
                return;

            var delta = mouse.delta.ReadValue();
            float dx = delta.x;
            float dy = delta.y;
            _yaw += dx * sensitivityY;
            if (rotateVertical)
                _pitch = Mathf.Clamp(_pitch - dy * sensitivityX, -85f, 85f);
            transform.rotation = Quaternion.Euler(rotateVertical ? _pitch : 0f, _yaw, 0f);
        }
    }
}
