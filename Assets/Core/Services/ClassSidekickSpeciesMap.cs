using System.Collections.Generic;

namespace DVBARPG.Core.Services
{
    /// <summary>
    /// Маппинг класс → вид Sidekick (Species). Для каждого класса свой набор ассетов (пак).
    /// Сейчас: hunter = эльфы; vanguard/mystic — задать при добавлении паков.
    /// </summary>
    public static class ClassSidekickSpeciesMap
    {
        /// <summary>Имя вида в БД Sidekick (sk_species.name).</summary>
        private static readonly Dictionary<string, string> ClassToSpeciesName = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "hunter", "Elf" },
            { "vanguard", "Human" },
            { "mystic", "Human" }
        };

        /// <summary>Возвращает имя вида Sidekick для класса, или null.</summary>
        public static string GetSpeciesNameForClass(string classId)
        {
            if (string.IsNullOrWhiteSpace(classId)) return null;
            return ClassToSpeciesName.TryGetValue(classId.Trim(), out var name) ? name : null;
        }
    }
}
