using UnityEngine;
using UnityEngine.UI;

namespace DVBARPG.UI.Skills
{
    /// <summary>
    /// Загрузка иконок скиллов из Resources/SkillIcons/{skillId}.
    /// Добавьте спрайты в папку Resources/SkillIcons с именами, совпадающими с skillId (например slash.png).
    /// </summary>
    public static class SkillIconProvider
    {
        private const string ResourcePath = "SkillIcons";

        /// <summary>
        /// Загружает спрайт иконки по skillId. Возвращает null, если не найден.
        /// </summary>
        public static Sprite GetIcon(string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId)) return null;
            var key = skillId.Trim();
            return Resources.Load<Sprite>($"{ResourcePath}/{key}");
        }

        /// <summary>
        /// Устанавливает иконку в Image по skillId. Скрывает Image или показывает placeholder, если иконки нет.
        /// </summary>
        public static void ApplyIcon(Image image, string skillId)
        {
            if (image == null) return;
            var sprite = GetIcon(skillId);
            image.sprite = sprite;
            image.enabled = sprite != null;
        }
    }
}
