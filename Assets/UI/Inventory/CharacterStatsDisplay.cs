using DVBARPG.Core;
using DVBARPG.Core.Services;
using UnityEngine;
using UnityEngine.UI;

namespace DVBARPG.UI.Inventory
{
    /// <summary>
    /// Показывает статы персонажа (скорость перемещения и др.) из профиля. Обновляется по Refresh() — вызывай после экипировки/снятия.
    /// </summary>
    public sealed class CharacterStatsDisplay : MonoBehaviour
    {
        [Header("Текст")]
        [Tooltip("Сюда выводится блок статов (скорость и т.д.).")]
        [SerializeField] private Text statsText;

        private IProfileService _profile;

        private void OnEnable()
        {
            _profile = GameRoot.Instance?.Services?.Get<IProfileService>();
            Refresh();
        }

        /// <summary>Перечитать статы из профиля и обновить текст.</summary>
        public void Refresh()
        {
            if (statsText == null) return;
            if (_profile == null)
            {
                statsText.text = "—";
                return;
            }

            var moveSpeed = _profile.BaseMoveSpeed;
            statsText.text = $"Скорость: {moveSpeed:F1}";
        }
    }
}
