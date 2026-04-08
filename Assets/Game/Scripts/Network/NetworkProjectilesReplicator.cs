using System;
using System.Collections.Generic;
using DVBARPG.Net.Network;
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
        [Tooltip("Каталог префабов снарядов по monsterId/type.")]
        [SerializeField] private EnemyProjectileCatalog enemyProjectileCatalog;
        [Tooltip("Задержка интерполяции (мс).")]
        [SerializeField] private float interpolationDelayMs = 80f;
        [Tooltip("Макс. время экстраполяции (мс) при потере снапшотов.")]
        [SerializeField] private float maxExtrapolationMs = 120f;

        [Header("Визуал")]
        [Tooltip("Умножать размер снаряда на радиус из снапшота.")]
        [SerializeField] private bool scaleByRadius = true;
        [Tooltip("Базовый диаметр префаба в мировых единицах. Если 0 — вычислим автоматически по Renderer.bounds.")]
        [SerializeField] private float prefabBaseDiameter = 0f;
        [Tooltip("Прижимать снаряды к земле локальным рейкастом (высота сервера в снапшоте — поле z).")]
        [SerializeField] private bool followGround = true;
        [Tooltip("Слои, по которым ищем землю для снарядов.")]
        [SerializeField] private LayerMask groundMask = ~0;
        [Tooltip("Доп. смещение по высоте для снарядов.")]
        [SerializeField] private float heightOffset = 0.2f;

        private NetworkSessionRunner _net;
        private readonly Dictionary<Guid, Transform> _projectiles = new();
        private readonly Dictionary<Guid, float> _projectileBaseDiameters = new();
        private readonly Dictionary<Guid, int> _projectilePrefabKeyById = new();
        private readonly Dictionary<Guid, Transform> _despawnVfxByProjectileId = new();
        private readonly HashSet<Guid> _seen = new();
        private readonly List<Guid> _toDisable = new();
        private readonly Dictionary<int, Stack<Transform>> _poolByPrefabKey = new();
        private const string DefaultProjectileCatalogResourcesPath = "EnemyProjectileCatalog";

        private void OnEnable()
        {
            AutoBindProjectileCatalogIfNeeded();
            var root = DVBARPG.Core.GameRoot.Instance;
            if (root == null || root.Services == null) return;
            if (!root.Services.TryGet<DVBARPG.Core.Services.ISessionService>(out var session)) return;
            _net = session as NetworkSessionRunner;
        }

        private void AutoBindProjectileCatalogIfNeeded()
        {
            if (enemyProjectileCatalog != null) return;

            enemyProjectileCatalog = Resources.Load<EnemyProjectileCatalog>(DefaultProjectileCatalogResourcesPath);
            if (enemyProjectileCatalog != null) return;

            var all = Resources.LoadAll<EnemyProjectileCatalog>(string.Empty);
            if (all != null && all.Length > 0)
                enemyProjectileCatalog = all[0];
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
            var ownerMonsterIdById = new Dictionary<Guid, string>();
            var ownerMonsterTypeById = new Dictionary<Guid, string>();
            for (int i = 0; i < to.Monsters.Length; i++)
            {
                var m = to.Monsters[i];
                ownerMonsterIdById[m.Id] = m.MonsterId ?? "";
                ownerMonsterTypeById[m.Id] = m.Type ?? "";
            }
            foreach (var p in to.Projectiles)
            {
                _seen.Add(p.Id);
                if (!_projectiles.TryGetValue(p.Id, out var tr) || tr == null)
                {
                    ownerMonsterIdById.TryGetValue(p.OwnerId, out var ownerMonsterId);
                    ownerMonsterTypeById.TryGetValue(p.OwnerId, out var ownerMonsterType);
                    var prefab = ResolveProjectilePrefab(ownerMonsterId, ownerMonsterType);
                    var despawnVfx = ResolveProjectileDespawnVfx(ownerMonsterId, ownerMonsterType);
                    var prefabKey = prefab != null ? prefab.GetInstanceID() : 0;
                    tr = AcquireProjectile(prefab, prefabKey);
                    if (tr.parent != transform) tr.SetParent(transform, worldPositionStays: false);
                    tr.name = $"Projectile-{p.Id.ToString().Substring(0, 8)}";
                    _projectiles[p.Id] = tr;
                    _projectilePrefabKeyById[p.Id] = prefabKey;
                    _despawnVfxByProjectileId[p.Id] = despawnVfx;
                    _projectileBaseDiameters[p.Id] = ComputePrefabBaseDiameter(tr);
                }
                var hasFrom = TryGetProjectilePos(from, p.Id, out var fromPos);
                var toPy = p.Z ?? 0f;
                var toPos = new Vector3(p.X, toPy, p.Y);

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

                // После ввода третьей оси сервер присылает высоту в Z.
                // При наличии Z всегда доверяем серверной высоте.
                if (p.Z.HasValue)
                    pos.y = p.Z.Value + heightOffset;
                else if (followGround)
                    pos.y = SampleProjectileHeight(pos, tr);
                else
                    pos.y = heightOffset;
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
                        if (_despawnVfxByProjectileId.TryGetValue(id, out var despawnVfx) && despawnVfx != null)
                            SpawnDespawnVfx(despawnVfx, tr.position);
                        tr.gameObject.SetActive(false);
                        _projectilePrefabKeyById.TryGetValue(id, out var prefabKey);
                        GetPool(prefabKey).Push(tr);
                    }
                    _projectiles.Remove(id);
                    _projectileBaseDiameters.Remove(id);
                    _projectilePrefabKeyById.Remove(id);
                    _despawnVfxByProjectileId.Remove(id);
                }
            }
        }

        private Transform ResolveProjectilePrefab(string monsterId, string monsterType)
        {
            if (enemyProjectileCatalog != null)
            {
                if (enemyProjectileCatalog.TryGetByMonsterId(monsterId, out var byId) &&
                    byId != null && byId.projectilePrefab != null)
                    return byId.projectilePrefab;

                if (enemyProjectileCatalog.TryGetDefaultForType(monsterType, out var byType) &&
                    byType != null && byType.projectilePrefab != null)
                    return byType.projectilePrefab;
            }

            return projectilePrefab;
        }

        private Transform ResolveProjectileDespawnVfx(string monsterId, string monsterType)
        {
            if (enemyProjectileCatalog != null)
            {
                if (enemyProjectileCatalog.TryGetByMonsterId(monsterId, out var byId) &&
                    byId != null && byId.despawnVfxPrefab != null)
                    return byId.despawnVfxPrefab;

                if (enemyProjectileCatalog.TryGetDefaultForType(monsterType, out var byType) &&
                    byType != null && byType.despawnVfxPrefab != null)
                    return byType.despawnVfxPrefab;
            }
            return null;
        }

        private Transform AcquireProjectile(Transform prefab, int prefabKey)
        {
            var pool = GetPool(prefabKey);
            if (pool.Count > 0) return pool.Pop();
            return Instantiate(prefab, transform);
        }

        private Stack<Transform> GetPool(int prefabKey)
        {
            if (!_poolByPrefabKey.TryGetValue(prefabKey, out var pool))
            {
                pool = new Stack<Transform>();
                _poolByPrefabKey[prefabKey] = pool;
            }
            return pool;
        }

        private void SpawnDespawnVfx(Transform vfxPrefab, Vector3 position)
        {
            var vfx = Instantiate(vfxPrefab, position, Quaternion.identity, transform);
            Destroy(vfx.gameObject, 2.5f);
        }

        private static bool TryGetProjectilePos(SnapshotEnvelope snap, Guid id, out Vector3 pos)
        {
            for (int i = 0; i < snap.Projectiles.Length; i++)
            {
                if (snap.Projectiles[i].Id == id)
                {
                    var pr = snap.Projectiles[i];
                    var py = pr.Z ?? 0f;
                    pos = new Vector3(pr.X, py, pr.Y);
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
