using System;
using System.Collections.Generic;

namespace DVBARPG.Game.Skills
{
    public static class SkillCastModeCatalog
    {
        private static readonly Dictionary<string, int> CastModes = new(StringComparer.OrdinalIgnoreCase);

        public static void Clear()
        {
            CastModes.Clear();
        }

        public static void SetCastMode(string skillId, int castMode)
        {
            if (string.IsNullOrWhiteSpace(skillId)) return;
            CastModes[skillId] = castMode;
        }

        public static bool TryGetCastMode(string skillId, out int castMode)
        {
            if (string.IsNullOrWhiteSpace(skillId))
            {
                castMode = 2;
                return false;
            }
            if (CastModes.TryGetValue(skillId, out castMode))
            {
                return true;
            }
            castMode = 2;
            return false;
        }
    }
}
