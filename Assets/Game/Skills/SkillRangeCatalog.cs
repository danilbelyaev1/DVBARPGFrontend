using System;
using System.Collections.Generic;

namespace DVBARPG.Game.Skills
{
    public static class SkillRangeCatalog
    {
        private static readonly Dictionary<string, float> Ranges = new(StringComparer.OrdinalIgnoreCase);

        public static void Clear()
        {
            Ranges.Clear();
        }

        public static void SetRange(string skillId, float range)
        {
            if (string.IsNullOrWhiteSpace(skillId)) return;
            Ranges[skillId] = range;
        }

        public static bool TryGetRange(string skillId, out float range)
        {
            if (string.IsNullOrWhiteSpace(skillId))
            {
                range = 0f;
                return false;
            }
            return Ranges.TryGetValue(skillId, out range);
        }
    }
}
