using System;
using System.Collections.Generic;
using UnityEngine;

namespace DVBARPG.Game.Network
{
    [CreateAssetMenu(
        fileName = "EnemyProjectileCatalog",
        menuName = "DVBARPG/Network/Enemy Projectile Catalog",
        order = 2101)]
    public sealed class EnemyProjectileCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Header("Идентификаторы")]
            [Tooltip("Точное соответствие monsterId (например ruin_cultist).")]
            public string monsterId = "";
            [Tooltip("Fallback по типу (melee/ranged/dummy).")]
            public string monsterType = "";
            [Tooltip("Запись по умолчанию для monsterType.")]
            public bool isDefaultForType;

            [Header("Визуал снаряда")]
            [Tooltip("Префаб снаряда для этого врага.")]
            public Transform projectilePrefab;
            [Tooltip("VFX при исчезновении/ударе снаряда.")]
            public Transform despawnVfxPrefab;
        }

        [Header("Записи")]
        [Tooltip("Сначала ищем по monsterId, затем по monsterType, затем fallback.")]
        [SerializeField] private List<Entry> entries = new();

        public bool TryGetByMonsterId(string monsterId, out Entry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(monsterId) || entries == null) return false;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null) continue;
                if (!string.IsNullOrWhiteSpace(e.monsterId) &&
                    string.Equals(e.monsterId, monsterId, StringComparison.OrdinalIgnoreCase))
                {
                    entry = e;
                    return true;
                }
            }
            return false;
        }

        public bool TryGetDefaultForType(string monsterType, out Entry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(monsterType) || entries == null) return false;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null || !e.isDefaultForType) continue;
                if (!string.IsNullOrWhiteSpace(e.monsterType) &&
                    string.Equals(e.monsterType, monsterType, StringComparison.OrdinalIgnoreCase))
                {
                    entry = e;
                    return true;
                }
            }
            return false;
        }
    }
}
