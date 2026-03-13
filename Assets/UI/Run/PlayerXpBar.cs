using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DVBARPG.UI.Run
{
    /// <summary>
    /// Привязка UI шкалы опыта к числам уровня и опыта.
    /// Ожидается, что внешняя логика будет вызывать UpdateXp(...) при изменении опыта.
    /// </summary>
    public sealed class PlayerXpBar : MonoBehaviour
    {
        [Header("Ссылки UI")]
        [Tooltip("Слайдер прогресса опыта (0–1).")]
        [SerializeField] private Slider xpSlider;
        [Tooltip("Текст с текущим уровнем (например, \"12\").")]
        [SerializeField] private TMP_Text levelText;
        [Tooltip("Текст опыта в формате \"current / required\" (например, \"123 / 999\").")]
        [SerializeField] private TMP_Text xpText;

        private int _currentLevel;
        private int _currentXp;
        private int _requiredXp;

        private void Awake()
        {
            if (xpSlider != null)
            {
                xpSlider.minValue = 0f;
                xpSlider.maxValue = 1f;
                xpSlider.value = 0f;
            }

            ApplyToUi();
        }

        /// <summary>
        /// Обновить значения уровня и опыта и применить к UI.
        /// </summary>
        public void UpdateXp(int level, int currentXp, int requiredXp)
        {
            _currentLevel = level < 0 ? 0 : level;
            _currentXp = Mathf.Max(0, currentXp);
            _requiredXp = Mathf.Max(1, requiredXp); // защита от деления на ноль

            ApplyToUi();
        }

        private void ApplyToUi()
        {
            float normalized = _requiredXp > 0 ? Mathf.Clamp01(_currentXp / (float)_requiredXp) : 0f;

            if (xpSlider != null)
            {
                xpSlider.value = normalized;
            }

            if (levelText != null)
            {
                levelText.text = _currentLevel.ToString();
            }

            if (xpText != null)
            {
                xpText.text = $"{_currentXp} / {_requiredXp}";
            }
        }
    }
}

