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

        [Header("Оптимизация высоты")]
        [Tooltip("Как часто обновлять высоту по коллайдерам для монстров (сек). 0 = каждый кадр.")]
        [SerializeField] private float heightSampleIntervalSec = 0.1f;
        [Tooltip("Минимальное смещение по XZ для пересчёта высоты, даже если интервал ещё не прошёл.")]
        [SerializeField] private float heightResampleDistance = 0.15f;

        private NetworkSessionRunner _net;
        private readonly Dictionary<Guid, Transform> _monsters = new();
        private readonly Dictionary<Guid, MonsterAnimationDriver> _animCache = new();
        private readonly Dictionary<Guid, float> _lastHeightSampleTime = new();
        private readonly Dictionary<Guid, Vector3> _lastHeightSamplePos = new();
        private readonly HashSet<Guid> _seen = new();
        private readonly List<Guid> _toDisable = new();

        private void OnEnable()
        {
            var root = DVBARPG.Core.GameRoot.Instance;
            if (root == null || root.Services == null) return;
            if (!root.Services.TryGet<DVBARPG.Core.Services.ISessionService>(out var session)) return;
            _net = session as NetworkSessionRunner;
        }

        private void OnDisable()
        {
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
                        pos.y = SampleHeightThrottled(m.Id, pos);
                        tr.position = ApplySmoothing(tr.position, pos);
                    }
                    else
                    {
                        var extraMs = Mathf.Min((float)(renderTime - to.ServerTimeMs), maxExtrapolationMs);
                        var vel = EstimateMonsterVelocity(m.Id);
                        var pos = vel.sqrMagnitude > 0.0001f ? toPos + vel * (extraMs / 1000f) : toPos;
                        pos.y = SampleHeightThrottled(m.Id, pos);
                        tr.position = ApplySmoothing(tr.position, pos);
                    }
                    if (!tr.gameObject.activeSelf) tr.gameObject.SetActive(true);

                    if (!_animCache.TryGetValue(m.Id, out var monsterAnim) || monsterAnim == null)
                    {
                        monsterAnim = tr.GetComponent<MonsterAnimationDriver>();
                        _animCache[m.Id] = monsterAnim;
                    }
                    if (monsterAnim != null)
                    {
                        monsterAnim.ApplyNetworkState(m.State, m.Type, to.ServerTimeMs);
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
                    _animCache.Remove(id);
                    _lastHeightSampleTime.Remove(id);
                    _lastHeightSamplePos.Remove(id);
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

        private float SampleHeightThrottled(Guid id, Vector3 worldPos)
        {
            if (heightSampleIntervalSec <= 0f)
                return UnifiedHeightSampler.SampleHeight(worldPos);

            var now = Time.unscaledTime;
            if (_lastHeightSampleTime.TryGetValue(id, out var lastT) &&
                _lastHeightSamplePos.TryGetValue(id, out var lastPos))
            {
                var dt = now - lastT;
                var dx = worldPos.x - lastPos.x;
                var dz = worldPos.z - lastPos.z;
                var movedSq = dx * dx + dz * dz;
                if (dt < heightSampleIntervalSec && movedSq < heightResampleDistance * heightResampleDistance)
                {
                    return lastPos.y;
                }
            }

            var y = UnifiedHeightSampler.SampleHeight(worldPos);
            _lastHeightSampleTime[id] = now;
            _lastHeightSamplePos[id] = new Vector3(worldPos.x, y, worldPos.z);
            return y;
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

        public static IReadOnlyCollection<Transform> AllTransforms => Registry.Values;
    }
}
