using System;
using System.Collections.Generic;
using UnityEngine;

namespace DVBARPG.Game.Network
{
    [CreateAssetMenu(fileName = "EnemySkinCatalog", menuName = "DVBARPG/Enemies/Skin Catalog")]
    public sealed class EnemySkinCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Header("Идентификаторы")]
            [Tooltip("Уникальный ID скина.")]
            public string skinId;
            [Tooltip("Тип монстра из снапшота (например melee/ranged). Для дефолтного выбора.")]
            public string monsterType;

            [Header("Визуал")]
            [Tooltip("Ключ Addressables (подготовка под адресные ассеты).")]
            public string addressableKey;
            [Tooltip("Fallback-префаб визуала, если Addressables не подключены или ключ не найден.")]
            public GameObject fallbackPrefab;

            [Header("Анимации")]
            [Tooltip("ID набора анимаций, который нужно применить к этому скину.")]
            public string animationSetId;

            [Header("Поведение")]
            [Tooltip("Использовать этот скин по умолчанию для указанного monsterType.")]
            public bool isDefaultForType;
        }

        [Header("Скины")]
        [Tooltip("Список скинов монстров.")]
        [SerializeField] private List<Entry> entries = new();

        public bool TryGetBySkinId(string skinId, out Entry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(skinId)) return false;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null || string.IsNullOrWhiteSpace(e.skinId)) continue;
                if (!string.Equals(e.skinId, skinId, StringComparison.OrdinalIgnoreCase)) continue;
                entry = e;
                return true;
            }
            return false;
        }

        public bool TryGetDefaultForType(string monsterType, out Entry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(monsterType)) return false;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null || !e.isDefaultForType) continue;
                if (string.IsNullOrWhiteSpace(e.monsterType)) continue;
                if (!string.Equals(e.monsterType, monsterType, StringComparison.OrdinalIgnoreCase)) continue;
                entry = e;
                return true;
            }
            return false;
        }
    }
}
