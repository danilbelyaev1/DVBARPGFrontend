using System;
using System.Collections.Generic;
using DVBARPG.Net.Network;
using DVBARPG.Game.World;
using DVBARPG.Game.Animation;
using UnityEngine;
using DVBARPG.Tools;

namespace DVBARPG.Game.Network
{
    public sealed class NetworkProjectilesReplicator : MonoBehaviour
    {
        [Header("Сеть")]
        [Tooltip("Префаб снаряда для отображения.")]
        [SerializeField] private Transform projectilePrefab;
        [Tooltip("Задержка интерполяции (мс).")]
        [SerializeField] private float interpolationDelayMs = 80f;
        [Tooltip("Макс. время экстраполяции (мс) при потере снапшотов.")]
        [SerializeField] private float maxExtrapolationMs = 120f;

        [Header("Визуал")]
        [Tooltip("Умножать размер снаряда на радиус из снапшота.")]
        [SerializeField] private bool scaleByRadius = true;
        [Tooltip("Базовый диаметр префаба в мировых единицах. Если 0 — вычислим автоматически по Renderer.bounds.")]
        [SerializeField] private float prefabBaseDiameter = 0f;
        [Tooltip("Прижимать снаряды к земле через HeightSampler.")]
        [SerializeField] private bool followGround = false;
        [Tooltip("Слои, по которым ищем землю для снарядов.")]
        [SerializeField] private LayerMask groundMask = ~0;
        [Tooltip("Доп. смещение по высоте для снарядов.")]
        [SerializeField] private float heightOffset = 0.2f;

        private NetworkSessionRunner _net;
        private readonly Dictionary<Guid, Transform> _projectiles = new();
        private readonly Dictionary<Guid, float> _projectileBaseDiameters = new();
        private readonly HashSet<Guid> _seen = new();
        private readonly List<Guid> _toDisable = new();

        private void OnEnable()
        {
            var session = DVBARPG.Core.GameRoot.Instance.Services.Get<DVBARPG.Core.Services.ISessionService>();
            _net = session as NetworkSessionRunner;
        }

        private void Update()
        {
            using (RuntimeProfiler.Sample("NetworkProjectilesReplicator.Update"))
            {
                if (projectilePrefab == null || _net == null) return;

                if (!_net.TryGetSnapshotsForRender(interpolationDelayMs, out var from, out var to, out var renderTime))
                {
                    return;
                }

            _seen.Clear();
            foreach (var p in to.Projectiles)
            {
                _seen.Add(p.Id);
                if (!_projectiles.TryGetValue(p.Id, out var tr) || tr == null)
                {
                    tr = Instantiate(projectilePrefab, transform);
                    tr.name = $"Projectile-{p.Id.ToString().Substring(0, 8)}";
                    _projectiles[p.Id] = tr;
                    _projectileBaseDiameters[p.Id] = ComputePrefabBaseDiameter(tr);
                }
                var hasFrom = TryGetProjectilePos(from, p.Id, out var fromPos);
                var toPos = new Vector3(p.X, 0f, p.Y);

                Vector3 pos;
                if (renderTime <= to.ServerTimeMs)
                {
                    float t = 0f;
                    var dt = to.ServerTimeMs - from.ServerTimeMs;
                    if (dt > 0)
                    {
                        t = Mathf.Clamp01((float)((renderTime - from.ServerTimeMs) / dt));
                    }
                    // Если снаряд появился только в новом снапшоте — не интерполируем от (0,0,0).
                    pos = hasFrom ? Vector3.Lerp(fromPos, toPos, t) : toPos;
                }
                else
                {
                    var extraMs = Mathf.Min((float)(renderTime - to.ServerTimeMs), maxExtrapolationMs);
                    var vel = EstimateProjectileVelocity(p.Id);
                    pos = vel.sqrMagnitude > 0.0001f ? toPos + vel * (extraMs / 1000f) : toPos;
                }

                pos.y = followGround ? SampleProjectileHeight(pos, tr) : heightOffset;
                tr.position = pos;

                if (scaleByRadius)
                {
                    var size = Mathf.Max(0.05f, p.Radius * 2f);
                    var baseDiameter = GetCachedBaseDiameter(p.Id, tr);
                    var scale = size / baseDiameter;
                    tr.localScale = new Vector3(scale, scale, scale);
                }

                if (!tr.gameObject.activeSelf) tr.gameObject.SetActive(true);
            }

            _toDisable.Clear();
            foreach (var kv in _projectiles)
            {
                if (!_seen.Contains(kv.Key))
                {
                    if (kv.Value != null) kv.Value.gameObject.SetActive(false);
                    _toDisable.Add(kv.Key);
                }
            }

                foreach (var id in _toDisable)
                {
                    if (_projectiles.TryGetValue(id, out var tr) && tr != null)
                    {
                        Destroy(tr.gameObject);
                    }
                    _projectiles.Remove(id);
                    _projectileBaseDiameters.Remove(id);
                }
            }
        }

        private static bool TryGetProjectilePos(SnapshotEnvelope snap, Guid id, out Vector3 pos)
        {
            for (int i = 0; i < snap.Projectiles.Length; i++)
            {
                if (snap.Projectiles[i].Id == id)
                {
                    pos = new Vector3(snap.Projectiles[i].X, 0f, snap.Projectiles[i].Y);
                    return true;
                }
            }
            pos = Vector3.zero;
            return false;
        }

        private Vector3 EstimateProjectileVelocity(Guid id)
        {
            if (_net == null) return Vector3.zero;
            if (!_net.TryGetLastTwoSnapshots(out var prev, out var last)) return Vector3.zero;

            if (!TryGetProjectilePos(last, id, out var lastPos)) return Vector3.zero;
            if (!TryGetProjectilePos(prev, id, out var prevPos)) return Vector3.zero;
            var dtMs = last.ServerTimeMs - prev.ServerTimeMs;
            if (dtMs <= 0) return Vector3.zero;

            return (lastPos - prevPos) / (dtMs / 1000f);
        }

        private float GetCachedBaseDiameter(Guid id, Transform instance)
        {
            if (prefabBaseDiameter > 0.001f) return prefabBaseDiameter;
            if (_projectileBaseDiameters.TryGetValue(id, out var cached) && cached > 0.001f)
            {
                return cached;
            }

            var computed = ComputePrefabBaseDiameter(instance);
            _projectileBaseDiameters[id] = computed;
            return computed;
        }

        private float ComputePrefabBaseDiameter(Transform instance)
        {
            // Автоматически вычисляем базовый диаметр по визуалу префаба.
            var renderer = instance.GetComponentInChildren<Renderer>();
            if (renderer == null)
            {
                return 1f;
            }

            // Берём размер в локальных единицах и убираем влияние текущего масштаба корня.
            var local = renderer.localBounds.size;
            var childScale = renderer.transform.lossyScale;
            var rootScale = instance.lossyScale;
            var sizeX = local.x * Mathf.Abs(childScale.x);
            var sizeZ = local.z * Mathf.Abs(childScale.z);
            var size = Mathf.Max(sizeX, sizeZ);
            var root = Mathf.Max(Mathf.Abs(rootScale.x), Mathf.Abs(rootScale.z));
            if (root > 0.0001f) size /= root;
            return Mathf.Max(0.001f, size);
        }

        private float SampleProjectileHeight(Vector3 worldPos, Transform instance)
        {
            // Не даём снаряду попадать в свой же коллайдер.
            var layerMask = groundMask & ~(1 << instance.gameObject.layer);
            var origin = worldPos + Vector3.up * 50f;
            if (Physics.Raycast(origin, Vector3.down, out var hit, 200f, layerMask, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y + heightOffset;
            }

            var terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                return terrain.SampleHeight(worldPos) + terrain.GetPosition().y + heightOffset;
            }

            return heightOffset;
        }

    }
}
