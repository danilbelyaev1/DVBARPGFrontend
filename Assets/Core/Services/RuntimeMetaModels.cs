using System;

namespace DVBARPG.Core.Services
{
    public sealed class RuntimeSeasonSnapshot
    {
        public bool Ok;
        public string Error;
        public string SeasonId;
    }

    public sealed class RuntimeCharactersSnapshot
    {
        public bool Ok;
        public string Error;
        public string CurrentSeasonId;
        public RuntimeCharacterSummary[] Characters = Array.Empty<RuntimeCharacterSummary>();
    }

    public sealed class RuntimeCharacterSummary
    {
        public string Id;
        public string Name;
        /// <summary>Пол с бэка: male / female.</summary>
        public string Gender;
        /// <summary>Внешность (Sidekick). JSON-объект или null.</summary>
        public object Appearance;
        public string[] Seasons = Array.Empty<string>();
    }

    public sealed class RuntimeLoadout
    {
        public string AttackSkillId;
        public string SupportASkillId;
        public string SupportBSkillId;
        public string MovementSkillId;
    }

    public sealed class RuntimeAuthSnapshot
    {
        public bool Ok;
        public string Error;
        public RuntimeLoadout Loadout;
        public RuntimeSkillSnapshot[] Skills = Array.Empty<RuntimeSkillSnapshot>();
        public float MoveSpeed;
        public int Level;
        public int XpTotal;
        public int XpToNextLevel;
        public int UnspentTalentPoints;
    }

    /// <summary>Прогресс персонажа из Laravel-профиля.</summary>
    public sealed class RuntimeProgressionSnapshot
    {
        /// <summary>Текущий уровень (серверная правда).</summary>
        public int Level;
        /// <summary>Суммарный XP (как в progression.xpTotal на бэке).</summary>
        public int XpTotal;
        /// <summary>Сколько XP осталось до следующего уровня.</summary>
        public int XpToNextLevel;
        /// <summary>XP, необходимый для входа в текущий уровень (нижний порог).</summary>
        public int XpCurrentLevelBase;
        /// <summary>Глобальный XP, необходимый для следующего уровня (верхний порог).</summary>
        public int XpNextLevelTotal;
    }

    /// <summary>Снимок профиля персонажа (уровень/опыт и т.п.).</summary>
    public sealed class RuntimeProfileSnapshot
    {
        public bool Ok;
        public string Error;
        public RuntimeProgressionSnapshot Progression;
    }

    public sealed class RuntimeSkillSnapshot
    {
        public string SkillId;
        public int Level;
        public string ModifiersJson;
    }

    /// <summary>Пayload для PUT loadout (совпадает с combatLoadout на бэке).</summary>
    public sealed class RuntimeLoadoutPayload
    {
        public string AttackSkillId;
        public string SupportASkillId;
        public string SupportBSkillId;
        public string MovementSlot; // "supportA" или "supportB"
    }

    public sealed class SetLoadoutResult
    {
        public bool Ok;
        public string Error;
    }
}
