using UnityEngine;
using System.Collections.Generic;

namespace DVBARPG.Game.Network
{
    public sealed class EnemyVisualHost : MonoBehaviour
    {
        [Header("Визуал")]
        [Tooltip("Родитель для инстанса визуала. Если пусто — текущий transform.")]
        [SerializeField] private Transform visualRoot;

        private GameObject _currentVisual;
        private readonly List<GameObject> _spawnedVisualRoots = new();
        private readonly List<GameObject> _baseVisualRoots = new();
        private bool _baseRootsCaptured;

        public GameObject CurrentVisual => _currentVisual;

        public void SetVisual(GameObject visualPrefab, bool useFallbackVisual)
        {
            if (visualPrefab == null) return;
            ClearVisual();
            var root = visualRoot != null ? visualRoot : transform;

            CaptureBaseRootsIfNeeded(root);
            SetBaseRootsActive(useFallbackVisual);
            if (useFallbackVisual)
            {
                _currentVisual = root.gameObject;
                return;
            }

            var instanceRoot = Instantiate(visualPrefab, root, false);
            instanceRoot.name = visualPrefab.name;

            // Unpack prefab contents into monster root.
            var childrenToMove = new List<Transform>();
            for (int i = 0; i < instanceRoot.transform.childCount; i++)
                childrenToMove.Add(instanceRoot.transform.GetChild(i));

            foreach (var child in childrenToMove)
            {
                child.SetParent(root, false);
                _spawnedVisualRoots.Add(child.gameObject);
            }

            Destroy(instanceRoot);
            _currentVisual = root.gameObject;
        }

        public void ClearVisual()
        {
            for (int i = 0; i < _spawnedVisualRoots.Count; i++)
            {
                var go = _spawnedVisualRoots[i];
                if (go != null) Destroy(go);
            }
            _spawnedVisualRoots.Clear();
            SetBaseRootsActive(true);

            _currentVisual = null;
        }

        private void CaptureBaseRootsIfNeeded(Transform root)
        {
            if (_baseRootsCaptured) return;
            _baseRootsCaptured = true;
            _baseVisualRoots.Clear();

            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child != null)
                    _baseVisualRoots.Add(child.gameObject);
            }
        }

        private void SetBaseRootsActive(bool active)
        {
            for (int i = 0; i < _baseVisualRoots.Count; i++)
            {
                var go = _baseVisualRoots[i];
                if (go != null) go.SetActive(active);
            }
        }
    }
}
