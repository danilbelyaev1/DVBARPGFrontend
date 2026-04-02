using System;
using System.Collections.Generic;
using UnityEngine;

namespace DVBARPG.Game.Network
{
    [CreateAssetMenu(fileName = "EnemyAnimationSetCatalog", menuName = "DVBARPG/Enemies/Animation Set Catalog")]
    public sealed class EnemyAnimationSetCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Header("Идентификатор")]
            [Tooltip("Уникальный ID набора анимаций.")]
            public string animationSetId;

            [Header("Аниматор")]
            [Tooltip("Базовый контроллер для набора.")]
            public RuntimeAnimatorController baseController;
            [Tooltip("Override-контроллер (опционально). Если задан, используется вместо baseController.")]
            public AnimatorOverrideController overrideController;
            [Tooltip("Avatar для отличающегося рига (опционально).")]
            public Avatar avatar;
        }

        [Header("Наборы анимаций")]
        [Tooltip("Список наборов анимаций по animationSetId.")]
        [SerializeField] private List<Entry> entries = new();

        public bool TryGet(string animationSetId, out Entry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(animationSetId)) return false;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null || string.IsNullOrWhiteSpace(e.animationSetId)) continue;
                if (!string.Equals(e.animationSetId, animationSetId, StringComparison.OrdinalIgnoreCase)) continue;
                entry = e;
                return true;
            }
            return false;
        }
    }
}
