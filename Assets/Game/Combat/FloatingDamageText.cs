using UnityEngine;

namespace DVBARPG.Game.Combat
{
    public sealed class FloatingDamageText : MonoBehaviour
    {
        private float _speed;
        private float _timeLeft;

        public void Init(float speed, float lifetime)
        {
            _speed = speed;
            _timeLeft = lifetime;
        }

        private void Update()
        {
            transform.position += Vector3.up * (_speed * Time.deltaTime);
            _timeLeft -= Time.deltaTime;
            if (_timeLeft <= 0f)
            {
                Destroy(gameObject);
            }
        }
    }
}
