using UnityEngine;

namespace DVBARPG.Game.Network
{
    public sealed class EnemyVisualHost : MonoBehaviour
    {
        [Header("Визуал")]
        [Tooltip("Родитель для инстанса визуала. Если пусто — текущий transform.")]
        [SerializeField] private Transform visualRoot;

        private GameObject _currentVisual;

        public GameObject CurrentVisual => _currentVisual;

        public void SetVisual(GameObject visualPrefab)
        {
            if (visualPrefab == null) return;
            ClearVisual();
            var root = visualRoot != null ? visualRoot : transform;
            _currentVisual = Instantiate(visualPrefab, root, false);
            _currentVisual.name = visualPrefab.name;
        }

        public void ClearVisual()
        {
            if (_currentVisual == null) return;
            Destroy(_currentVisual);
            _currentVisual = null;
        }
    }
}
