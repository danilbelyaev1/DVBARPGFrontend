using DVBARPG.Core;
using DVBARPG.Core.Services;
using UnityEngine;
using UnityEngine.UI;

namespace DVBARPG.UI.Currency
{
    /// <summary>
    /// Отображает баланс валюты (например золото). Обновляется при включении и по кнопке.
    /// </summary>
    public sealed class CurrencyLabel : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("Текст для вывода баланса.")]
        [SerializeField] private Text targetText;
        [Tooltip("Код валюты (например gold).")]
        [SerializeField] private string currencyCode = "gold";

        [Header("Формат")]
        [Tooltip("Формат строки: {0} — баланс.")]
        [SerializeField] private string format = "Золото: {0}";

        private void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            var currency = GameRoot.Instance?.Services?.Get<ICurrencyService>();
            var profile = GameRoot.Instance?.Services?.Get<IProfileService>();
            if (currency == null || profile == null || targetText == null) return;

            var characterId = profile.SelectedCharacterId;
            var seasonId = profile.CurrentSeasonId;
            if (string.IsNullOrWhiteSpace(characterId) || string.IsNullOrWhiteSpace(seasonId))
            {
                targetText.text = string.Format(format, "—");
                return;
            }

            currency.GetBalance(characterId, seasonId, currencyCode, result =>
            {
                if (targetText == null) return;
                if (result != null && result.Ok)
                    targetText.text = string.Format(format, result.Balance);
                else
                    targetText.text = string.Format(format, "—");
            });
        }
    }
}
