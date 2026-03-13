using UnityEngine;

namespace DVBARPG.Game.Progression
{
    /// <summary>
    /// Простая кривая опыта по уровням.
    /// Важно: progression.xpTotal трактуем как ГЛОБАЛЬНЫЙ суммарный XP (как на Laravel),
    /// а runXpTotal из снапшотов только временно добавляет визуальный прогресс поверх этого числа.
    /// Сейчас используется константный XP на уровень для удобства (визуальная кривая клиента).
    /// </summary>
    public static class XpCurve
    {
        // TODO: при необходимости заменить на таблицу из ScriptableObject или данных с сервера.
        // Для визуала считаем, что каждый уровень требует одинаковое количество XP.
        // Это значение должно быть согласовано с серверной логикой, когда она станет доступна.
        private const int XpPerLevel = 999;

        /// <summary>Суммарный XP на старте указанного уровня (Level >= 1).</summary>
        public static int GetXpAtLevelStart(int level)
        {
            if (level < 1) level = 1;
            // Предполагаем линейную кривую: каждый уровень требует XpPerLevel глобального XP.
            return (level - 1) * XpPerLevel;
        }

        /// <summary>Сколько XP нужно внутри уровня, чтобы перейти на следующий.</summary>
        public static int GetRequiredInsideLevel(int level)
        {
            if (level < 1) level = 1;
            return XpPerLevel;
        }

        /// <summary>
        /// Считает прогресс внутри уровня по суммарному XP.
        /// currentInsideLevel — XP в рамках текущего уровня;
        /// requiredInsideLevel — полный XP для апа уровня.
        /// </summary>
        public static void GetLevelProgress(int level, int totalXp, out int currentInsideLevel, out int requiredInsideLevel)
        {
            if (level < 1) level = 1;
            var start = GetXpAtLevelStart(level);
            requiredInsideLevel = GetRequiredInsideLevel(level);
            var inside = totalXp - start;
            inside = Mathf.Clamp(inside, 0, requiredInsideLevel);
            currentInsideLevel = inside;
        }
    }
}

