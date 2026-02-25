using System;
using System.Collections.Generic;
using DVBARPG.Net.Network;
using UnityEngine;
using DVBARPG.Game.Player;

namespace DVBARPG.Game.Network
{
    public sealed class NetworkDamageFromSnapshots : MonoBehaviour
    {
        [Header("Текст урона")]
        [Tooltip("Префаб TextMesh для урона.")]
        [SerializeField] private TextMesh textPrefab;
        [Tooltip("Смещение текста над целью.")]
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2f, 0f);
        [Tooltip("Скорость всплытия текста.")]
        [SerializeField] private float floatSpeed = 1.5f;
        [Tooltip("Время жизни текста (сек).")]
        [SerializeField] private float lifetime = 0.8f;
        [Tooltip("Цвет урона по игроку.")]
        [SerializeField] private Color playerDamageColor = new Color(1f, 0.4f, 0.4f);
        [Tooltip("Цвет урона по монстрам.")]
        [SerializeField] private Color monsterDamageColor = Color.white;

        private NetworkSessionRunner _net;
        private int _lastPlayerHp = -1;
        private readonly Dictionary<Guid, int> _lastMonsterHp = new();

        private void OnEnable()
        {
            var session = DVBARPG.Core.GameRoot.Instance.Services.Get<DVBARPG.Core.Services.ISessionService>();
            _net = session as NetworkSessionRunner;
            if (_net != null)
            {
                _net.Snapshot += OnSnapshot;
            }
        }

        private void OnDisable()
        {
            if (_net != null)
            {
                _net.Snapshot -= OnSnapshot;
            }
        }

        private void OnSnapshot(SnapshotEnvelope snap)
        {
            if (textPrefab == null) return;

            // Player damage
            if (_lastPlayerHp >= 0 && snap.Player.Hp < _lastPlayerHp)
            {
                // Считаем урон по разнице HP между снапшотами.
                var amount = _lastPlayerHp - snap.Player.Hp;
                if (NetworkPlayerReplicator.PlayerTransform != null)
                {
                    SpawnText(NetworkPlayerReplicator.PlayerTransform.position, amount, playerDamageColor);
                }
            }
            _lastPlayerHp = snap.Player.Hp;

            // Monster damage
            foreach (var m in snap.Monsters)
            {
                if (_lastMonsterHp.TryGetValue(m.Id, out var lastHp))
                {
                    if (m.Hp < lastHp)
                    {
                        // Урон по монстру = падение HP между снапшотами.
                        var amount = lastHp - m.Hp;
                        if (NetworkMonstersReplicator.TryGetTransform(m.Id, out var tr))
                        {
                            SpawnText(tr.position, amount, monsterDamageColor);
                        }
                    }
                    _lastMonsterHp[m.Id] = m.Hp;
                }
                else
                {
                    _lastMonsterHp[m.Id] = m.Hp;
                }
            }
        }

        private void SpawnText(Vector3 position, int amount, Color color)
        {
            var tm = Instantiate(textPrefab, position + worldOffset, Quaternion.identity);
            tm.text = amount.ToString();
            tm.color = color;

            var mover = tm.GetComponent<FloatingDamageText>();
            if (mover == null) mover = tm.gameObject.AddComponent<FloatingDamageText>();
            mover.Init(floatSpeed, lifetime);
        }
    }
}
