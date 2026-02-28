using System;
using System.Collections.Generic;
using DVBARPG.Net.Network;
using UnityEngine;
using DVBARPG.Game.World;
using DVBARPG.Game.Animation;
using DVBARPG.Tools;
namespace DVBARPG.Game.Network
{
    public sealed class NetworkMonstersReplicator : MonoBehaviour
    {
        private static readonly Dictionary<Guid, Transform> Registry = new();

        [Header("Сеть")]
        [Tooltip("Префаб монстра для отображения.")]
        [SerializeField] private Transform monsterPrefab;
        [Tooltip("Задержка интерполяции (мс).")]
        [SerializeField] private float interpolationDelayMs = 180f;
        [Tooltip("Макс. время экстраполяции (мс) при потере снапшотов.")]
        [SerializeField] private float maxExtrapolationMs = 120f;
        [Tooltip("Сглаживание позиции (0 = выключено).")]
        [SerializeField] private float positionSmoothing = 12f;

        private NetworkSessionRunner _net;
        private readonly Dictionary<Guid, Transform> _monsters = new();
        private readonly Dictionary<Guid, string> _monsterType = new();
        private readonly Dictionary<Guid, string> _monsterState = new();
        private readonly HashSet<Guid> _seen = new();
        private readonly List<Guid> _toDisable = new();

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
            if (monsterPrefab == null) return;
        }

        private void Update()
        {
            using (RuntimeProfiler.Sample("NetworkMonstersReplicator.Update"))
            {
                if (monsterPrefab == null || _net == null) return;

                if (!_net.TryGetSnapshotsForRender(interpolationDelayMs, out var from, out var to, out var renderTime))
                {
                    return;
                }

                _seen.Clear();
                foreach (var m in to.Monsters)
                {
                    _seen.Add(m.Id);
                    if (!_monsters.TryGetValue(m.Id, out var tr) || tr == null)
                    {
                        tr = Instantiate(monsterPrefab, transform);
                        tr.name = $"Monster-{m.Id.ToString().Substring(0, 8)}";
                        _monsters[m.Id] = tr;
                        Registry[m.Id] = tr;
                    }

                    var hasFrom = TryGetMonsterPos(from, m.Id, out var fromPos);
                    var toPos = new Vector3(m.X, 0f, m.Y);

                    if (renderTime <= to.ServerTimeMs)
                    {
                        float t = 0f;
                        var dt = to.ServerTimeMs - from.ServerTimeMs;
                        if (dt > 0)
                        {
                            t = Mathf.Clamp01((float)((renderTime - from.ServerTimeMs) / dt));
                        }

                        var pos = hasFrom ? Vector3.Lerp(fromPos, toPos, t) : toPos;
                        pos.y = SampleHeight(pos);
                        tr.position = ApplySmoothing(tr.position, pos);
                    }
                    else
                    {
                        var extraMs = Mathf.Min((float)(renderTime - to.ServerTimeMs), maxExtrapolationMs);
                        var vel = EstimateMonsterVelocity(m.Id);
                        var pos = vel.sqrMagnitude > 0.0001f ? toPos + vel * (extraMs / 1000f) : toPos;
                        pos.y = SampleHeight(pos);
                        tr.position = ApplySmoothing(tr.position, pos);
                    }
                    if (!tr.gameObject.activeSelf) tr.gameObject.SetActive(true);

                    _monsterType[m.Id] = m.Type;
                    _monsterState[m.Id] = m.State;

                    var ability = tr.GetComponent<AbilityAnimationDriver>();
                    if (ability != null)
                    {
                        ability.ApplyNetworkState(m.State, m.Type);
                    }
                }

                _toDisable.Clear();
                foreach (var kv in _monsters)
                {
                    if (!_seen.Contains(kv.Key))
                    {
                        if (kv.Value != null) kv.Value.gameObject.SetActive(false);
                        _toDisable.Add(kv.Key);
                    }
                }

                foreach (var id in _toDisable)
                {
                    Registry.Remove(id);
                    _monsters.Remove(id);
                    _monsterType.Remove(id);
                    _monsterState.Remove(id);
                }
            }
        }

        private static bool TryGetMonsterPos(SnapshotEnvelope snap, Guid id, out Vector3 pos)
        {
            for (int i = 0; i < snap.Monsters.Length; i++)
            {
                if (snap.Monsters[i].Id == id)
                {
                    pos = new Vector3(snap.Monsters[i].X, 0f, snap.Monsters[i].Y);
                    return true;
                }
            }
            pos = Vector3.zero;
            return false;
        }

        private Vector3 EstimateMonsterVelocity(Guid id)
        {
            if (_net == null) return Vector3.zero;
            if (!_net.TryGetLastTwoSnapshots(out var prev, out var last)) return Vector3.zero;

            if (!TryGetMonsterPos(last, id, out var lastPos)) return Vector3.zero;
            if (!TryGetMonsterPos(prev, id, out var prevPos)) return Vector3.zero;
            var dtMs = last.ServerTimeMs - prev.ServerTimeMs;
            if (dtMs <= 0) return Vector3.zero;

            return (lastPos - prevPos) / (dtMs / 1000f);
        }

        private float SampleHeight(Vector3 worldPos)
        {
            return UnifiedHeightSampler.SampleHeight(worldPos);
        }

        private Vector3 ApplySmoothing(Vector3 current, Vector3 target)
        {
            if (positionSmoothing <= 0f) return target;
            var alpha = 1f - Mathf.Exp(-positionSmoothing * Time.deltaTime);
            return Vector3.Lerp(current, target, alpha);
        }

        public static bool TryGetTransform(Guid id, out Transform tr)
        {
            return Registry.TryGetValue(id, out tr);
        }
    }
}
