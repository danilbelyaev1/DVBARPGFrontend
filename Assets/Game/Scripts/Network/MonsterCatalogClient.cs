using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace DVBARPG.Game.Network
{
    public sealed class MonsterCatalogClient : MonoBehaviour
    {
        [Header("Поведение")]
        [Tooltip("Логировать успешную загрузку.")]
        [SerializeField] private bool logOnSuccess = true;

        public static MonsterCatalogStore Store { get; } = new MonsterCatalogStore();

        private void Start()
        {
            StartCoroutine(Fetch());
        }

        public System.Collections.IEnumerator Fetch()
        {
            var url = BuildRuntimeUrl();
            if (string.IsNullOrWhiteSpace(url))
            {
                yield break;
            }

            using var req = UnityWebRequest.Get(url);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                DebugLogWarning($"MonsterCatalogClient: request failed {req.error}");
                yield break;
            }

            var json = req.downloadHandler.text;
            var response = JsonUtility.FromJson<MonsterPoolResponse>(json);
            if (response == null || response.monsters == null)
            {
                DebugLogWarning("MonsterCatalogClient: empty response.");
                yield break;
            }

            Store.Clear();
            foreach (var m in response.monsters)
            {
                if (string.IsNullOrWhiteSpace(m.type)) continue;
                if (Store.Contains(m.type)) continue;

                Store.Upsert(m);
            }

            if (logOnSuccess)
            {
                DebugLog($"MonsterCatalogClient: loaded {Store.Count} monster types.");
                DebugLog($"MonsterCatalogClient: response json: {json}");
                foreach (var m in Store.All)
                {
                    DebugLog($"Monster[{m.type}] ms={m.moveSpeed:0.00} atkDmg={m.attackDamage} atkRange={m.attackRange:0.0} atkCd={m.attackCooldownSec:0.00} projCd={m.projectileCooldownSec:0.00}");
                }
            }
        }

        private string BuildRuntimeUrl()
        {
            var mapId = FindMapId();
            if (string.IsNullOrWhiteSpace(mapId)) return null;

            var baseUrl = ResolveRuntimeBaseUrl();
            if (string.IsNullOrWhiteSpace(baseUrl)) return null;

            return $"{baseUrl.TrimEnd('/')}/maps/{mapId}/monsters";
        }

        private static string ResolveRuntimeBaseUrl()
        {
            var connector = FindFirstObjectByType<DVBARPG.Game.Network.NetworkRunConnector>();
            if (connector == null)
            {
                return "http://127.0.0.1:8080";
            }

            var serverUrlField = typeof(DVBARPG.Game.Network.NetworkRunConnector).GetField("serverUrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var raw = serverUrlField?.GetValue(connector) as string;
            if (string.IsNullOrWhiteSpace(raw)) return "http://127.0.0.1:8080";

            if (raw.StartsWith("udp://", StringComparison.OrdinalIgnoreCase))
            {
                raw = "http://" + raw.Substring("udp://".Length);
            }
            raw = raw.TrimEnd('/');

            // Если указан только хост:порт UDP, переводим на HTTP 8080.
            if (raw.EndsWith(":8081", StringComparison.OrdinalIgnoreCase))
            {
                raw = raw.Substring(0, raw.Length - 5) + ":8080";
            }

            return raw;
        }

        private static string FindMapId()
        {
            var connector = FindFirstObjectByType<DVBARPG.Game.Network.NetworkRunConnector>();
            if (connector == null)
            {
                return "default";
            }

            var mapIdField = typeof(DVBARPG.Game.Network.NetworkRunConnector).GetField("mapId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var raw = mapIdField?.GetValue(connector) as string;
            return string.IsNullOrWhiteSpace(raw) ? "default" : raw;
        }

        public static bool TryGetByType(string type, out MonsterStats stats) => Store.TryGetStats(type, out stats);
        public static bool TryGetMonster(string type, out MonsterRow monster) => Store.TryGetMonster(type, out monster);

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void DebugLog(string message)
        {
            Debug.Log(message);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void DebugLogWarning(string message)
        {
            Debug.LogWarning(message);
        }

        [Serializable]
        private sealed class MonsterPoolResponse
        {
            public bool ok;
            public string mapId;
            public string[] tags;
            public string[] kinds;
            public MonsterRow[] monsters;
        }

        [Serializable]
        public sealed class MonsterRow
        {
            public string id;
            public string name;
            public string tag;
            public string kind;
            public string type;
            public float spawnWeight;
            public int maxHp;
            public float moveSpeed;
            public int attackDamage;
            public float attackRange;
            public float aggroRange;
            public float projectileSpeed;
            public float projectileRadius;
            public int projectileDamage;
            public float projectileCooldownSec;
            public float resistPhys;
            public float resistElemental;
            public float attackCooldownSec;
            public bool isRanged;
        }

        public readonly struct MonsterStats
        {
            public readonly string Type;
            public readonly float MoveSpeed;
            public readonly int AttackDamage;
            public readonly float AttackRange;
            public readonly float AttackCooldownSec;
            public readonly float ProjectileSpeed;
            public readonly float ProjectileRadius;
            public readonly float ProjectileCooldownSec;

            public MonsterStats(
                string type,
                float moveSpeed,
                int attackDamage,
                float attackRange,
                float attackCooldownSec,
                float projectileSpeed,
                float projectileRadius,
                float projectileCooldownSec)
            {
                Type = type;
                MoveSpeed = moveSpeed;
                AttackDamage = attackDamage;
                AttackRange = attackRange;
                AttackCooldownSec = attackCooldownSec;
                ProjectileSpeed = projectileSpeed;
                ProjectileRadius = projectileRadius;
                ProjectileCooldownSec = projectileCooldownSec;
            }
        }

        public sealed class MonsterCatalogStore
        {
            private readonly Dictionary<string, MonsterRow> _byType = new(StringComparer.OrdinalIgnoreCase);

            public int Count => _byType.Count;
            public IEnumerable<MonsterRow> All => _byType.Values;

            public bool TryGetMonster(string type, out MonsterRow monster)
            {
                if (string.IsNullOrWhiteSpace(type))
                {
                    monster = default;
                    return false;
                }
                return _byType.TryGetValue(type, out monster);
            }

            public bool Contains(string type) => _byType.ContainsKey(type);

            public void Upsert(MonsterRow monster)
            {
                if (string.IsNullOrWhiteSpace(monster.type)) return;
                _byType[monster.type] = monster;
            }

            public void Clear() => _byType.Clear();

            public bool TryGetStats(string type, out MonsterStats stats)
            {
                if (TryGetMonster(type, out var monster))
                {
                    stats = new MonsterStats(
                        monster.type,
                        monster.moveSpeed,
                        monster.attackDamage,
                        monster.attackRange,
                        monster.attackCooldownSec,
                        monster.projectileSpeed,
                        monster.projectileRadius,
                        monster.projectileCooldownSec
                    );
                    return true;
                }
                stats = default;
                return false;
            }
        }
    }
}
