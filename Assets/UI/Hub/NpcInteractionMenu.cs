using System;
using System.Collections;
using DVBARPG.Core;
using DVBARPG.Core.Services;
using DVBARPG.UI.Common;
using DVBARPG.UI.Shop;
using UnityEngine;
using UnityEngine.UI;

namespace DVBARPG.UI.Hub
{
    /// <summary>
    /// Меню действий при клике по NPC в хабе: торговля (если уместно), квестовый interact с бэка.
    /// </summary>
    public sealed class NpcInteractionMenu : MonoBehaviour
    {
        private const string LeftWindowId = "hub_npc_menu";

        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text titleText;
        [SerializeField] private Transform buttonsParent;
        [SerializeField] private Button closeButton;
        [SerializeField] private NpcShopScreen shopScreen;
        [SerializeField] private GameObject shopPanelRoot;
        [SerializeField] private ErrorToast errorToast;

        private NpcInfo _current;
        private Font _font;
        private SessionState _session;

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Hide);
            }

            if (buttonsParent != null && buttonsParent.GetComponent<VerticalLayoutGroup>() == null)
            {
                var v = buttonsParent.gameObject.AddComponent<VerticalLayoutGroup>();
                v.childAlignment = TextAnchor.UpperCenter;
                v.spacing = 10f;
                v.padding = new RectOffset(12, 12, 12, 12);
                v.childControlHeight = true;
                v.childControlWidth = true;
                v.childForceExpandWidth = true;
            }

            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            HudWindowCoordinator.LeftWindowOpened += OnOtherLeftWindowOpened;
        }

        private void OnDestroy()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Hide);
            }

            HudWindowCoordinator.LeftWindowOpened -= OnOtherLeftWindowOpened;
        }

        public void Open(NpcInfo npc)
        {
            if (npc == null || panelRoot == null || buttonsParent == null)
            {
                return;
            }

            _current = npc;
            _session = GameRoot.Instance?.Services?.Get<SessionState>();
            if (_session != null)
            {
                _session.HubNpcDialogOpen = true;
            }

            if (titleText != null)
            {
                titleText.text = string.IsNullOrWhiteSpace(npc.Name) ? npc.Code : npc.Name;
            }

            ClearButtons();
            BuildActions(npc);
            HudWindowCoordinator.NotifyLeftWindowOpened(LeftWindowId);
            panelRoot.SetActive(true);
        }

        public void Hide()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            if (_session != null)
            {
                _session.HubNpcDialogOpen = false;
            }

            _current = null;
        }

        private void OnOtherLeftWindowOpened(string sourceId)
        {
            if (string.Equals(sourceId, LeftWindowId, StringComparison.Ordinal))
            {
                return;
            }

            if (panelRoot != null && panelRoot.activeSelf)
            {
                Hide();
            }
        }

        private void BuildActions(NpcInfo npc)
        {
            if (npc.LikelyHasShop)
            {
                AddButton("Торговля", () => OpenTrade(npc));
            }

            var quests = GameRoot.Instance?.Services?.Get<CampaignState>()?.Quests;
            if (quests != null)
            {
                foreach (var q in quests)
                {
                    if (q == null)
                    {
                        continue;
                    }

                    var interactId = q.TryGetInteractObjectiveId();
                    if (string.IsNullOrWhiteSpace(interactId))
                    {
                        continue;
                    }

                    if (!string.Equals(interactId.Trim(), npc.Code.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var title = string.IsNullOrWhiteSpace(q.Title) ? q.QuestCode : q.Title;
                    AddButton($"Квест: {title}", () => StartCoroutine(CoSendInteract(interactId.Trim())));
                }
            }

            if (buttonsParent.childCount == 0)
            {
                AddButton("Закрыть", Hide);
            }
        }

        private void OpenTrade(NpcInfo npc)
        {
            Hide();
            if (shopScreen == null)
            {
                errorToast?.ShowErrorCode("shop_ui_not_configured");
                return;
            }

            var targetRoot = shopPanelRoot != null ? shopPanelRoot : shopScreen.gameObject;
            EnsureActiveHierarchy(targetRoot);
            EnsureActiveHierarchy(shopScreen.gameObject);
            shopScreen.OpenShop(npc.Code);
        }

        private static void EnsureActiveHierarchy(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            var current = target.transform;
            while (current != null)
            {
                if (!current.gameObject.activeSelf)
                {
                    current.gameObject.SetActive(true);
                }

                current = current.parent;
            }
        }

        private IEnumerator CoSendInteract(string interactId)
        {
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
            var requestId = $"hub-interact-{profile.SelectedCharacterId}-{interactId}-{Guid.NewGuid():N}";
            var done = false;
            RuntimeCampaignSnapshot snap = null;
            mvp.PostCampaignQuestEventsBatch(
                session,
                profile.SelectedCharacterId,
                profile.CurrentSeasonId,
                mapId,
                requestId,
                new object[] { new { type = "interact", interactId } },
                r =>
                {
                    snap = r;
                    done = true;
                });
            while (!done)
            {
                yield return null;
            }

            if (snap == null || !snap.Ok)
            {
                errorToast?.ShowErrorCode(snap?.Error ?? "campaign_quest_batch_failed");
                yield break;
            }

            ApplyCampaignSnapshot(campaignState, snap);
            Hide();
        }

        private static void ApplyCampaignSnapshot(CampaignState campaignState, RuntimeCampaignSnapshot snap)
        {
            campaignState.UnlockedMapCodes = snap.UnlockedMapCodes;
            campaignState.VisitedMapCodes = snap.VisitedMapCodes;
            campaignState.Quests = snap.Quests;
            campaignState.TravelOptionsByMap.Clear();
            foreach (var mapOptions in snap.TravelOptionsByMap)
            {
                campaignState.TravelOptionsByMap[mapOptions.MapCode] = mapOptions.Options;
            }
        }

        private void ClearButtons()
        {
            for (var i = buttonsParent.childCount - 1; i >= 0; i--)
            {
                Destroy(buttonsParent.GetChild(i).gameObject);
            }
        }

        private void AddButton(string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(buttonsParent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(360f, 48f);

            var img = go.GetComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.2f, 0.95f);

            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(onClick);

            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 48f;
            le.preferredHeight = 48f;

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(8f, 4f);
            trt.offsetMax = new Vector2(-8f, -4f);
            var tx = textGo.GetComponent<Text>();
            tx.font = _font;
            tx.fontSize = 22;
            tx.alignment = TextAnchor.MiddleCenter;
            tx.color = Color.white;
            tx.text = label;
        }
    }
}
