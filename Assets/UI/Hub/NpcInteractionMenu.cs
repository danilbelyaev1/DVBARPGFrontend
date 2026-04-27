using System;
using System.Collections;
using DVBARPG.Core;
using DVBARPG.Core.Services;
using DVBARPG.UI.Common;
using DVBARPG.UI.Shop;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

namespace DVBARPG.UI.Hub
{
    /// <summary>
    /// Меню действий при клике по NPC в хабе: торговля (если уместно), квестовый interact с бэка.
    /// </summary>
    public sealed class NpcInteractionMenu : MonoBehaviour
    {
        private const string LeftWindowId = "hub_npc_menu";

        [SerializeField] private NpcShopScreen shopScreen;
        [SerializeField] private GameObject shopPanelRoot;
        [SerializeField] private ErrorToast errorToast;
        [Header("UI Toolkit")]
        [SerializeField] private UIDocument uiDocument;

        private NpcInfo _current;
        private SessionState _session;
        private VisualElement _uiPanel;
        private Label _uiTitle;
        private VisualElement _uiActionsRoot;

        private void Awake()
        {
            if (!TryInitUiToolkit())
            {
                Debug.LogError("[NpcInteractionMenu] UIDocument/UXML is required. Canvas fallback removed.", this);
                enabled = false;
                return;
            }

            HudWindowCoordinator.LeftWindowOpened += OnOtherLeftWindowOpened;
        }

        private bool TryInitUiToolkit()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            if (uiDocument == null)
            {
                return false;
            }

            var root = uiDocument.rootVisualElement;
            if (root == null)
            {
                return false;
            }

            _uiPanel = root.Q<VisualElement>("NpcMenuRoot");
            var uiContentPanel = root.Q<VisualElement>("NpcMenuPanel");
            _uiTitle = root.Q<Label>("NpcMenuTitleLabel");
            _uiActionsRoot = root.Q<VisualElement>("NpcActionsList");
            var close = root.Q<UnityEngine.UIElements.Button>("NpcCloseButton");
            if (close != null)
            {
                close.clicked += Hide;
            }
            if (_uiPanel != null)
            {
                _uiPanel.pickingMode = PickingMode.Ignore;
            }
            if (uiContentPanel != null)
            {
                uiContentPanel.pickingMode = PickingMode.Position;
            }
            SetUiVisible(false);
            return _uiPanel != null && _uiActionsRoot != null && _uiTitle != null;
        }

        private void OnDestroy()
        {
            HudWindowCoordinator.LeftWindowOpened -= OnOtherLeftWindowOpened;
        }

        public void Open(NpcInfo npc)
        {
            if (npc == null)
            {
                return;
            }

            _current = npc;
            _session = GameRoot.Instance?.Services?.Get<SessionState>();
            if (_session != null)
            {
                _session.HubNpcDialogOpen = true;
            }

            _uiTitle.text = string.IsNullOrWhiteSpace(npc.Name) ? npc.Code : npc.Name;

            ClearButtons();
            BuildActions(npc);
            HudWindowCoordinator.NotifyLeftWindowOpened(LeftWindowId);
            SetUiVisible(true);
        }

        public void Hide()
        {
            SetUiVisible(false);

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

            var isVisible = _uiPanel != null && _uiPanel.style.display != DisplayStyle.None;
            if (isVisible)
            {
                Hide();
            }
        }

        private void Update()
        {
            if (Keyboard.current == null) return;
            if (!Keyboard.current.escapeKey.wasPressedThisFrame) return;
            if (_uiPanel == null || _uiPanel.style.display == DisplayStyle.None) return;
            Hide();
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

            if (_uiActionsRoot == null || _uiActionsRoot.childCount == 0)
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
            _uiActionsRoot?.Clear();
        }

        private void AddButton(string label, UnityEngine.Events.UnityAction onClick)
        {
            var btn = new UnityEngine.UIElements.Button(() => onClick?.Invoke())
            {
                text = label
            };
            btn.AddToClassList("hud-button");
            _uiActionsRoot.Add(btn);
        }

        private void SetUiVisible(bool isVisible)
        {
            if (_uiPanel == null)
            {
                return;
            }

            _uiPanel.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
