using System;
using System.Collections.Generic;
using DVBARPG.Core;
using DVBARPG.Core.Services;
using UnityEngine;
using UnityEngine.UI;

namespace DVBARPG.UI.Market
{
    /// <summary>
    /// Экран маркета: список лотов, кнопка купить. Опционально — выставить предмет (нужен выбор instanceId и цены).
    /// </summary>
    public sealed class MarketScreen : MonoBehaviour
    {
        [Header("Контейнеры")]
        [SerializeField] private Transform listingsContentRoot;
        [SerializeField] private GameObject listingRowPrefab;

        [Header("Кнопки")]
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button closeButton;

        [Header("Текст")]
        [SerializeField] private Text statusText;

        private IMarketService _market;
        private IProfileService _profile;

        private void Awake()
        {
            if (refreshButton != null) refreshButton.onClick.AddListener(Refresh);
            if (closeButton != null) closeButton.onClick.AddListener(() => UnityEngine.SceneManagement.SceneManager.LoadScene("CharacterSelect"));
        }

        private void OnEnable()
        {
            _market = GameRoot.Instance?.Services?.Get<IMarketService>();
            _profile = GameRoot.Instance?.Services?.Get<IProfileService>();
            Refresh();
        }

        private void Refresh()
        {
            if (_market == null || _profile == null) { SetStatus("Сервисы недоступны."); return; }
            var seasonId = _profile.CurrentSeasonId;
            if (string.IsNullOrWhiteSpace(seasonId)) { SetStatus("Выберите сезон."); return; }

            SetStatus("Загрузка...");
            _market.GetListings(seasonId, 50, 0, OnListingsLoaded);
        }

        private void OnListingsLoaded(GetListingsResult result)
        {
            if (result == null || !result.Ok)
            {
                SetStatus(result?.Error ?? "Ошибка загрузки.");
                return;
            }

            SetStatus(result.Listings != null ? $"Лотов: {result.Listings.Length}" : "Нет лотов.");

            if (listingsContentRoot == null || listingRowPrefab == null) return;

            foreach (Transform c in listingsContentRoot)
                Destroy(c.gameObject);

            var listings = result.Listings ?? Array.Empty<MarketListingDto>();
            foreach (var listing in listings)
            {
                if (listing == null) continue;
                var row = Instantiate(listingRowPrefab, listingsContentRoot);
                var label = row.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = listing.Item?.Definition != null
                        ? $"{listing.Item.Definition.Name ?? listing.Item.Definition.Code} — {listing.Price} {listing.CurrencyCode}"
                        : $"{listing.ListingId} — {listing.Price}";
                var buyBtn = row.GetComponentInChildren<Button>();
                if (buyBtn != null)
                {
                    var listingId = listing.ListingId;
                    buyBtn.onClick.AddListener(() => Buy(listingId));
                }
            }
        }

        private void Buy(string listingId)
        {
            if (_market == null || _profile == null) return;
            var characterId = _profile.SelectedCharacterId;
            var seasonId = _profile.CurrentSeasonId;
            if (string.IsNullOrWhiteSpace(characterId) || string.IsNullOrWhiteSpace(seasonId)) return;
            var requestId = $"buy-{Guid.NewGuid():N}";
            _market.BuyListing(characterId, seasonId, listingId, requestId, r =>
            {
                if (r != null && r.Ok) Refresh();
                else SetStatus(r?.Error ?? "Ошибка покупки.");
            });
        }

        private void SetStatus(string msg) { if (statusText != null) statusText.text = msg; }
    }
}
