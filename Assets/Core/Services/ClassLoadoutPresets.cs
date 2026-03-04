using System;

namespace DVBARPG.Core.Services
{
    /// <summary>
    /// Пресеты лоадута по классу (vanguard/hunter/mystic), совпадают со starter skills на бэке.
    /// </summary>
    public static class ClassLoadoutPresets
    {
        public const string Vanguard = "vanguard";
        public const string Hunter = "hunter";
        public const string Mystic = "mystic";

        public static RuntimeLoadoutPayload GetLoadoutForClass(string classCode)
        {
            if (string.IsNullOrWhiteSpace(classCode)) return Melee();
            var c = classCode.Trim().ToLowerInvariant();
            if (c == Hunter) return Ranged();
            if (c == Mystic) return Mage();
            return Melee();
        }

        public static RuntimeLoadoutPayload Melee()
        {
            return new RuntimeLoadoutPayload
            {
                AttackSkillId = "slash",
                SupportASkillId = "stone_skin_aura",
                SupportBSkillId = "dash",
                MovementSlot = "supportB"
            };
        }

        public static RuntimeLoadoutPayload Ranged()
        {
            return new RuntimeLoadoutPayload
            {
                AttackSkillId = "quick_shot",
                SupportASkillId = "battle_hymn",
                SupportBSkillId = "combat_roll",
                MovementSlot = "supportB"
            };
        }

        public static RuntimeLoadoutPayload Mage()
        {
            return new RuntimeLoadoutPayload
            {
                AttackSkillId = "arc_bolt",
                SupportASkillId = "ghost_shroud_aura",
                SupportBSkillId = "rift_step",
                MovementSlot = "supportB"
            };
        }
    }
}
