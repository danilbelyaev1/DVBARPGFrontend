using System.Collections.Generic;
using UnityEngine;

namespace DVBARPG.Game.Skills.Presentation
{
    [CreateAssetMenu(menuName = "DVBARPG/Skills/Skill Presentation Catalog", fileName = "SkillPresentationCatalog")]
    public sealed class SkillPresentationCatalog : ScriptableObject
    {
        [Header("Каталог")]
        [Tooltip("Список презентаций по SkillId.")]
        [SerializeField] private List<SkillPresentation> items = new();

        private Dictionary<string, SkillPresentation> _map;

        public IReadOnlyList<SkillPresentation> Items => items;

        public bool TryGet(string skillId, out SkillPresentation presentation)
        {
            presentation = null;
            if (string.IsNullOrWhiteSpace(skillId)) return false;

            EnsureMap();
            return _map.TryGetValue(skillId, out presentation) && presentation != null;
        }

        private void EnsureMap()
        {
            if (_map != null) return;

            _map = new Dictionary<string, SkillPresentation>(System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null) continue;
                if (string.IsNullOrWhiteSpace(item.SkillId)) continue;
                if (_map.ContainsKey(item.SkillId)) continue;
                _map[item.SkillId] = item;
            }
        }

        private void OnEnable()
        {
            _map = null;
        }

        public void SetItems(List<SkillPresentation> newItems)
        {
            items = newItems ?? new List<SkillPresentation>();
            _map = null;
        }
    }
}
