using System;
using System.Collections.Generic;
using UnityEngine;

namespace DVBARPG.Game.Skills.Presentation
{
    [Serializable]
    public sealed class SkillSocket
    {
        [Header("Сокет")]
        [Tooltip("Имя сокета.")]
        [SerializeField] private string name = "";
        [Tooltip("Transform сокета.")]
        [SerializeField] private Transform transform;

        public string Name => name;
        public Transform Transform => transform;
    }

    public sealed class SkillSocketLocator : MonoBehaviour
    {
        [Header("Сокеты")]
        [Tooltip("Список доступных сокетов.")]
        [SerializeField] private List<SkillSocket> sockets = new();

        [Header("Фолбэк")]
        [Tooltip("Трансформ по умолчанию, если сокет не найден.")]
        [SerializeField] private Transform fallback;

        private readonly Dictionary<string, Transform> _map = new(System.StringComparer.OrdinalIgnoreCase);
        private bool _ready;

        public Transform GetSocket(string socketName)
        {
            EnsureMap();
            if (!string.IsNullOrWhiteSpace(socketName) && _map.TryGetValue(socketName, out var tr) && tr != null)
            {
                return tr;
            }

            return fallback != null ? fallback : transform;
        }

        private void EnsureMap()
        {
            if (_ready) return;
            _map.Clear();
            for (int i = 0; i < sockets.Count; i++)
            {
                var s = sockets[i];
                if (s == null) continue;
                if (string.IsNullOrWhiteSpace(s.Name) || s.Transform == null) continue;
                if (_map.ContainsKey(s.Name)) continue;
                _map[s.Name] = s.Transform;
            }
            _ready = true;
        }

        private void OnEnable()
        {
            _ready = false;
        }
    }
}
