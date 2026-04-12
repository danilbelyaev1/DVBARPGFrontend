using System.Collections;
using System.Collections.Generic;
using DVBARPG.Core;
using DVBARPG.Core.Services;
using DVBARPG.UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace DVBARPG.UI.Shop
{
    public sealed class NpcShopScreen : MonoBehaviour
    {
        private const string LeftWindowId = "hub_shop";

        [Tooltip("Пусто = брать хаб текущего акта из SessionState (actN_hub).")]
        [SerializeField] private string mapId = "";
        [SerializeField] private Transform offersRoot;
        [SerializeField] private GameObject offerRowPrefab;
        [SerializeField] private Text npcTitleText;
        [SerializeField] private Text statusText;
        [SerializeField] private ErrorToast errorToast;
        [SerializeField] private UiModalLayer modalLayer;
        [SerializeField] private GameObject panelRoot;

        private readonly List<GameObject> _rows = new List<GameObject>();

        private void Awake()
        {
            if (modalLayer == null)
            {
                modalLayer = GetComponent<UiModalLayer>();
            }

            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            HudWindowCoordinator.LeftWindowOpened += OnOtherLeftWindowOpened;
        }

        private void OnDestroy()
        {
            HudWindowCoordinator.LeftWindowOpened -= OnOtherLeftWindowOpened;
        }

        /// <summary>Открыть магазин выбранного NPC (вызывается из UI хаба, не из Start).</summary>
        public void OpenShop(string npcCode)
        {
            if (string.IsNullOrWhiteSpace(npcCode))
            {
                SetStatus("No NPC.");
                return;
            }

            var normalizedNpcCode = npcCode.Trim();
            SetStatus("Loading shop...");
            HudWindowCoordinator.NotifyLeftWindowOpened(LeftWindowId);
            if (modalLayer != null)
            {
                modalLayer.Show();
            }
            else if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }

            if (gameObject.activeInHierarchy)
            {
                StopAllCoroutines();
                StartCoroutine(CoOpenShop(normalizedNpcCode));
                return;
            }

            // Fallback for inactive UI object: run via active root runner.
            var root = GameRoot.Instance;
            if (root != null)
            {
                root.StartCoroutine(CoOpenShop(normalizedNpcCode));
                return;
            }

            SetStatus("Shop init failed.");
        }

        public void HideShop()
        {
            if (modalLayer != null)
            {
                modalLayer.Hide();
                return;
            }

            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        private void OnOtherLeftWindowOpened(string sourceId)
        {
            if (string.Equals(sourceId, LeftWindowId, System.StringComparison.Ordinal))
            {
                return;
            }

            var isVisible = modalLayer != null ? modalLayer.IsVisible : (panelRoot != null && panelRoot.activeSelf);
            if (isVisible)
            {
                HideShop();
            }
        }

        private IEnumerator CoOpenShop(string npcCode)
        {
            var services = GameRoot.Instance?.Services;
            var profile = services?.Get<IProfileService>();
            var shopState = services?.Get<ShopState>();
            if (profile == null || shopState == null)
            {
                SetStatus("Shop init failed.");
                yield break;
            }

            shopState.ActiveNpcCode = npcCode;
            yield return LoadShop(npcCode);
        }

        private IEnumerator LoadShop(string npcCode)
        {
            var services = GameRoot.Instance.Services;
            var profile = services.Get<IProfileService>();
            var session = profile.CurrentAuth;
            var mvp = services.Get<IRuntimeMvpService>();
            var shopState = services.Get<ShopState>();

            bool done = false;
            RuntimeShopSnapshot snapshot = null;
            mvp.FetchShop(session, profile.SelectedCharacterId, npcCode, profile.CurrentSeasonId, result =>
            {
                snapshot = result;
                done = true;
            });
            while (!done) yield return null;

            if (snapshot == null || !snapshot.Ok)
            {
                var error = snapshot?.Error ?? "shop_load_failed";
                errorToast?.ShowErrorCode(error);
                SetStatus(error);
                yield break;
            }

            shopState.Offers = snapshot.Offers;
            if (npcTitleText != null)
            {
                npcTitleText.text = snapshot.Npc != null ? snapshot.Npc.Name : npcCode;
            }

            BuildOffers(snapshot.Offers);
            SetStatus("Shop loaded.");
            // Do not block shop UI on quest meta-sync request.
            StartCoroutine(CoApplyHubMerchantInteractQuest(npcCode));
            yield break;
        }

        /// <summary>
        /// Квест campaign_intro (interact npc_hub_merchant): мета-батч без UDP-инстанса.
        /// </summary>
        private static IEnumerator CoApplyHubMerchantInteractQuest(string npcCode)
        {
            if (!string.Equals(npcCode, "npc_hub_merchant", System.StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }

            var services = GameRoot.Instance?.Services;
            var profile = services?.Get<IProfileService>();
            var session = profile?.CurrentAuth;
            var mvp = services?.Get<IRuntimeMvpService>();
            var campaignState = services?.Get<CampaignState>();
            var sessionState = services?.Get<SessionState>();
            if (profile == null || session == null || mvp == null || campaignState == null || sessionState == null)
            {
                yield break;
            }

            var mapId = ActHubResolver.ResolveHubMapCode(sessionState, campaignState);
            var requestId = $"hub-metagame-{profile.SelectedCharacterId}-npc_hub_merchant";
            var done = false;
            RuntimeCampaignSnapshot snap = null;
            mvp.PostCampaignQuestEventsBatch(
                session,
                profile.SelectedCharacterId,
                profile.CurrentSeasonId,
                mapId,
                requestId,
                new object[] { new { type = "interact", interactId = "npc_hub_merchant" } },
                r =>
                {
                    snap = r;
                    done = true;
                });
            const float timeoutSeconds = 12f;
            var startedAt = Time.realtimeSinceStartup;
            while (!done && Time.realtimeSinceStartup - startedAt < timeoutSeconds)
            {
                yield return null;
            }

            if (!done)
            {
                Debug.LogWarning("[NpcShopScreen] Campaign quest batch timeout in shop meta-sync.");
                yield break;
            }

            if (snap == null || !snap.Ok) yield break;

            campaignState.UnlockedMapCodes = snap.UnlockedMapCodes;
            campaignState.VisitedMapCodes = snap.VisitedMapCodes;
            campaignState.Quests = snap.Quests;
            campaignState.TravelOptionsByMap.Clear();
            foreach (var mapOptions in snap.TravelOptionsByMap)
            {
                campaignState.TravelOptionsByMap[mapOptions.MapCode] = mapOptions.Options;
            }
        }

        private void BuildOffers(ShopOfferInfo[] offers)
        {
            foreach (var row in _rows) if (row != null) Destroy(row);
            _rows.Clear();
            if (offersRoot == null || offerRowPrefab == null || offers == null) return;

            foreach (var offer in offers)
            {
                var row = Instantiate(offerRowPrefab, offersRoot);
                _rows.Add(row);
                var button = row.GetComponentInChildren<Button>();
                var label = row.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = $"{offer.ItemName} ({offer.Price} {offer.CurrencyCode})";
                }
                if (button != null)
                {
                    var selected = offer;
                    button.onClick.AddListener(() => StartCoroutine(Buy(selected)));
                }
            }
        }

        private IEnumerator Buy(ShopOfferInfo offer)
        {
            var services = GameRoot.Instance.Services;
            var shopState = services.Get<ShopState>();
            var profile = services.Get<IProfileService>();
            var session = profile.CurrentAuth;
            var mvp = services.Get<IRuntimeMvpService>();
            var state = services.Get<SessionState>();
            if (shopState.PendingBuy) yield break;

            shopState.PendingBuy = true;
            bool done = false;
            RuntimeShopBuyResult buyResult = null;
            mvp.BuyShopOffer(session, profile.SelectedCharacterId, shopState.ActiveNpcCode, profile.CurrentSeasonId, offer.Id, 1, result =>
            {
                buyResult = result;
                done = true;
            });
            while (!done) yield return null;
            shopState.PendingBuy = false;

            if (buyResult == null || !buyResult.Ok)
            {
                var error = buyResult?.Error ?? "buy_failed";
                state.LastApiError = error;
                errorToast?.ShowErrorCode(error);
                SetStatus(error);
                yield break;
            }

            state.LastApiError = null;
            SetStatus($"Purchase ok. New balance: {buyResult.NewBalance}");
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }
    }
}
