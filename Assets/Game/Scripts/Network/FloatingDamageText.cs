using UnityEngine;

namespace DVBARPG.Game.Network
{
    public sealed class FloatingDamageText : MonoBehaviour
    {
        private float _speed;
        private float _timeLeft;

        public void Init(float speed, float lifetime)
        {
            // Параметры анимации всплытия.
            _speed = speed;
            _timeLeft = lifetime;
        }

        private void Update()
        {
            // Плавно поднимаем текст вверх и уничтожаем по таймеру.
            transform.position += Vector3.up * (_speed * Time.deltaTime);
            _timeLeft -= Time.deltaTime;
            if (_timeLeft <= 0f)
            {
                Destroy(gameObject);
            }
        }
    }
}
