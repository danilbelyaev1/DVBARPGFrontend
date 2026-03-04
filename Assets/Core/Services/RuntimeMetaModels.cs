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
