using System.Collections;
using System.Collections.Generic;
using System;
using DVBARPG.Core;
using DVBARPG.Core.Services;
using DVBARPG.UI.Common;
using UnityEngine;
using TMPro;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using UiButton = UnityEngine.UIElements.Button;

namespace DVBARPG.UI.Hub
{
    public sealed class HubWorldMapScreen : MonoBehaviour
    {
        private const string LeftWindowId = "hub_teleport";

        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private ErrorToast errorToast;
        [Tooltip("Если задано — использовать этот код хаба вместо вывода из SessionState (отладка).")]
        [SerializeField] private string hubMapOverride = "";
        [Header("UI Toolkit")]
        [SerializeField] private UIDocument uiDocument;

        private string _fromMapCode = "act1_hub";
        private CampaignTravelOption _detailOption;
        private bool _isLocationsVisible;
        private ScrollView _uiLocationsList;
        private ScrollView _uiAllQuestsList;
        private ScrollView _uiCurrentQuestsList;
        private VisualElement _uiPanel;
        private VisualElement _uiDetailPanel;
        private Label _uiDetailBody;
        private Label _uiStatus;
        private Label _uiCurrentQuestsStatus;
        private UiButton _uiConfirmButton;
        private int _lastQuestFingerprint = int.MinValue;

        private void Awake()
        {
            if (!TryInitUiToolkit())
            {
                Debug.LogError("[HubWorldMapScreen] UIDocument/UXML is required. Canvas fallback removed.", this);
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

            _uiPanel = root.Q<VisualElement>("TeleportRoot");
            var uiContentPanel = root.Q<VisualElement>("TeleportPanel");
            _uiLocationsList = root.Q<ScrollView>("LocationsList");
            _uiAllQuestsList = root.Q<ScrollView>("AllQuestsList");
            _uiCurrentQuestsList = root.Q<ScrollView>("CurrentQuestsList");
            _uiDetailPanel = root.Q<VisualElement>("LocationDetailPanel");
            _uiDetailBody = root.Q<Label>("LocationDetailBody");
            _uiStatus = root.Q<Label>("TeleportStatusLabel");
            _uiCurrentQuestsStatus = root.Q<Label>("CurrentQuestsStatusLabel");
            _uiConfirmButton = root.Q<UiButton>("LocationConfirmButton");
            var uiBackButton = root.Q<UiButton>("LocationBackButton");
            var closeButton = root.Q<UiButton>("TeleportCloseButton");
            _uiConfirmButton?.RegisterCallback<ClickEvent>(_ => OnLocationDetailConfirmClicked());
            uiBackButton?.RegisterCallback<ClickEvent>(_ => OnHideLocationDetailClicked());
            closeButton?.RegisterCallback<ClickEvent>(_ => CloseTeleportModal());
            if (_uiPanel != null)
            {
                _uiPanel.pickingMode = PickingMode.Ignore;
            }
            if (uiContentPanel != null)
            {
                uiContentPanel.pickingMode = PickingMode.Position;
            }
            SetUiVisible(false);
            HideLocationDetail();
            return _uiPanel != null && _uiLocationsList != null && _uiAllQuestsList != null && _uiCurrentQuestsList != null;
        }

        private void OnDestroy()
        {
            HudWindowCoordinator.LeftWindowOpened -= OnOtherLeftWindowOpened;
        }

        private void OnOtherLeftWindowOpened(string sourceId)
        {
            if (string.Equals(sourceId, LeftWindowId, StringComparison.Ordinal))
            {
                return;
            }

            if (_isLocationsVisible)
            {
                HideLocationDetail();
                SetLocationsVisible(false);
            }
        }

        private void Start()
        {
            var sessionState = GameRoot.Instance?.Services?.Get<SessionState>();
            if (sessionState != null)
            {
                sessionState.HubTeleportMenuOpen = false;
            }

            HideLocationDetail();
            SetLocationsVisible(false);
            StartCoroutine(LoadCampaign());
        }

        private IEnumerator LoadCampaign()
        {
            var services = GameRoot.Instance?.Services;
            var profile = services?.Get<IProfileService>();
            var session = profile?.CurrentAuth;
            var mvp = services?.Get<IRuntimeMvpService>();
            var campaignState = services?.Get<CampaignState>();
            var sessionState = services?.Get<SessionState>();
            if (profile == null || session == null || mvp == null || campaignState == null || sessionState == null)
            {
                SetStatus("Hub init failed.");
                yield break;
            }

            _fromMapCode = string.IsNullOrWhiteSpace(hubMapOverride)
                ? (!string.IsNullOrWhiteSpace(sessionState.MapId)
                    ? sessionState.MapId
                    : ActHubResolver.ResolveHubMapCode(sessionState, campaignState))
                : hubMapOverride.Trim();
            var idsReady = TryResolveCharacterContext(profile, sessionState, out var characterId, out var seasonId);
            Debug.Log($"[HubWorldMapScreen] LoadCampaign start: fromMap={_fromMapCode}, characterId={characterId}, seasonId={seasonId}");
            sessionState.MapId = _fromMapCode;
            if (ActHubResolver.TryParseActFromMapCode(_fromMapCode, out var hubAct))
                sessionState.ActiveActNumber = hubAct;

            if (!idsReady)
            {
                const string error = "character_or_season_missing";
                Debug.LogWarning("[HubWorldMapScreen] Character context missing. Open CharacterSelect first, or provide SessionState/Auth IDs for direct Hub launch.");
                sessionState.LastApiError = error;
                errorToast?.ShowErrorCode(error);
                SetStatus(error);
                yield break;
            }

            SetStatus("Loading campaign...");
            bool done = false;
            RuntimeCampaignSnapshot snapshot = null;
            mvp.FetchCampaign(session, characterId, seasonId, result =>
            {
                snapshot = result;
                done = true;
            });

            while (!done) yield return null;

            if (snapshot == null || !snapshot.Ok)
            {
                var error = snapshot?.Error ?? "campaign_error";
                Debug.LogWarning($"[HubWorldMapScreen] FetchCampaign failed: error={error}");
                errorToast?.ShowErrorCode(error);
                SetStatus(error);
                yield break;
            }

            campaignState.UnlockedMapCodes = snapshot.UnlockedMapCodes;
            campaignState.VisitedMapCodes = snapshot.VisitedMapCodes;
            campaignState.Quests = snapshot.Quests;
            campaignState.CurrentMapCode = _fromMapCode;
            campaignState.TravelOptionsByMap.Clear();
            foreach (var mapOptions in snapshot.TravelOptionsByMap)
            {
                campaignState.TravelOptionsByMap[mapOptions.MapCode] = mapOptions.Options;
            }
            Debug.Log($"[HubWorldMapScreen] Campaign loaded: unlocked={campaignState.UnlockedMapCodes?.Length ?? 0}, mapsWithOptions={campaignState.TravelOptionsByMap.Count}");

            BuildButtons(campaignState);
            RenderQuestViews(campaignState.Quests);
            SetStatus("Campaign loaded.");
        }

        private void BuildButtons(CampaignState state)
        {
            _uiLocationsList?.Clear();
            if (_uiLocationsList == null)
            {
                return;
            }

            if (!state.TravelOptionsByMap.TryGetValue(_fromMapCode, out var options) || options == null) return;
            Debug.Log($"[HubWorldMapScreen] BuildButtons: fromMap={_fromMapCode}, optionsCount={options.Length}");

            foreach (var option in options)
            {
                var canClick = option.CanFirstVisit || option.Teleportable;
                if (!canClick)
                {
                    continue;
                }

                var cta = option.Teleportable ? "Teleport" : "Portal";
                var button = new UiButton(() => OpenLocationDetail(option))
                {
                    text = $"{option.ToMapCode} [{cta}]"
                };
                button.AddToClassList("hud-button");
                _uiLocationsList.Add(button);
            }
        }

        private void OnHideLocationDetailClicked()
        {
            HideLocationDetail();
        }

        private void OpenLocationDetail(CampaignTravelOption option)
        {
            if (option == null) return;

            _detailOption = option;
            if (_uiDetailBody != null) _uiDetailBody.text = FormatLocationDetail(option);
            if (_uiDetailPanel != null) _uiDetailPanel.style.display = DisplayStyle.Flex;
            if (_uiLocationsList != null) _uiLocationsList.style.display = DisplayStyle.None;

            SetStatus("Проверьте сведения о локации и нажмите «Перейти».");
        }

        private void HideLocationDetail()
        {
            _detailOption = null;
            if (_uiDetailPanel != null) _uiDetailPanel.style.display = DisplayStyle.None;
            if (_uiLocationsList != null) _uiLocationsList.style.display = DisplayStyle.Flex;
        }

        private void OnLocationDetailConfirmClicked()
        {
            if (_detailOption == null) return;
            StartCoroutine(TravelAndEnterRun(_detailOption));
        }

        private static string FormatLocationDetail(CampaignTravelOption o)
        {
            if (o == null) return "";
            var travel = o.Teleportable
                ? "телепорт"
                : (string.IsNullOrWhiteSpace(o.TravelType) ? "портал (первый визит)" : o.TravelType);
            var reqQuest = string.IsNullOrWhiteSpace(o.RequiredQuestCode) ? "—" : o.RequiredQuestCode;
            var reqLvl = o.RequiredLevel.HasValue ? o.RequiredLevel.Value.ToString() : "—";
            return
                $"Код карты: {o.ToMapCode}\n" +
                $"Способ перехода: {travel}\n" +
                $"Первый визит разрешён: {(o.CanFirstVisit ? "да" : "нет")}\n" +
                $"Уже посещали: {(o.Visited ? "да" : "нет")}\n" +
                $"Телепорт доступен: {(o.Teleportable ? "да" : "нет")}\n" +
                $"Требуется квест: {reqQuest}\n" +
                $"Требуемый уровень: {reqLvl}";
        }

        private IEnumerator TravelAndEnterRun(CampaignTravelOption option)
        {
            Debug.Log($"[HubWorldMapScreen] Travel started: from={_fromMapCode}, to={option?.ToMapCode ?? "<null>"}");
            if (option == null) yield break;

            SetStatus("Validating travel...");
            var travelType = option.Teleportable
                ? "teleport"
                : (string.IsNullOrWhiteSpace(option.TravelType) ? "portal" : option.TravelType);
            Debug.Log($"[HubWorldMapScreen] Travel validating: from={_fromMapCode}, to={option.ToMapCode}, travelType={travelType}, canFirstVisit={option.CanFirstVisit}, teleportable={option.Teleportable}");

            yield return WorldTravelFlow.CoTravelToMap(
                _fromMapCode,
                option.ToMapCode,
                travelType,
                onError: code =>
                {
                    var sessionState = GameRoot.Instance?.Services?.Get<SessionState>();
                    if (sessionState != null) sessionState.LastApiError = code;
                    errorToast?.ShowErrorCode(code);
                    SetStatus(code);
                },
                onSuccessBeforeRouter: () =>
                {
                    SetLocationsVisible(false);
                    HideLocationDetail();
                    Debug.Log($"[HubWorldMapScreen] ValidateTravel OK: destination={option.ToMapCode}. Loading run.");
                    SetStatus("Загрузка…");
                },
                clearHubTravelUiState: true);
        }

        private void SetStatus(string text)
        {
            if (_uiStatus != null)
            {
                _uiStatus.text = text;
                return;
            }
            if (statusText != null) statusText.text = text;
        }

        private void SetLocationsVisible(bool isVisible)
        {
            if (_isLocationsVisible == isVisible)
            {
                return;
            }

            _isLocationsVisible = isVisible;
            if (isVisible)
            {
                HudWindowCoordinator.NotifyLeftWindowOpened(LeftWindowId);
            }

            SetUiVisible(isVisible);
        }

        private void Update()
        {
            var sessionState = GameRoot.Instance?.Services?.Get<SessionState>();
            if (sessionState == null) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && _isLocationsVisible)
            {
                sessionState.HubTeleportMenuOpen = false;
            }
            SetLocationsVisible(sessionState.HubTeleportMenuOpen);

            var campaignState = GameRoot.Instance?.Services?.Get<CampaignState>();
            var quests = campaignState?.Quests;
            var fingerprint = ComputeQuestFingerprint(quests);
            if (fingerprint != _lastQuestFingerprint)
            {
                RenderQuestViews(quests);
            }
        }

        private void CloseTeleportModal()
        {
            var sessionState = GameRoot.Instance?.Services?.Get<SessionState>();
            if (sessionState != null)
            {
                sessionState.HubTeleportMenuOpen = false;
            }
            HideLocationDetail();
            SetLocationsVisible(false);
        }

        private static bool TryResolveCharacterContext(IProfileService profile, SessionState sessionState, out string characterId, out string seasonId)
        {
            characterId = profile?.SelectedCharacterId;
            seasonId = profile?.CurrentSeasonId;
            if (!string.IsNullOrWhiteSpace(characterId) && !string.IsNullOrWhiteSpace(seasonId))
            {
                return true;
            }

            if (sessionState != null)
            {
                if (string.IsNullOrWhiteSpace(characterId))
                {
                    characterId = sessionState.CharacterId;
                }

                if (string.IsNullOrWhiteSpace(seasonId))
                {
                    seasonId = sessionState.SeasonId;
                }
            }

            var auth = profile?.CurrentAuth;
            if (auth != null)
            {
                if (string.IsNullOrWhiteSpace(characterId) && auth.CharacterId != Guid.Empty)
                {
                    characterId = auth.CharacterId.ToString();
                }

                if (string.IsNullOrWhiteSpace(seasonId) && auth.SeasonId != Guid.Empty)
                {
                    seasonId = auth.SeasonId.ToString();
                }
            }

            return !string.IsNullOrWhiteSpace(characterId) && !string.IsNullOrWhiteSpace(seasonId);
        }

        private void SetUiVisible(bool isVisible)
        {
            if (_uiPanel == null)
            {
                return;
            }

            _uiPanel.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RenderQuestViews(CampaignQuestInfo[] quests)
        {
            _lastQuestFingerprint = ComputeQuestFingerprint(quests);
            RenderAllQuests(quests);
            RenderCurrentQuests(quests);
        }

        private void RenderAllQuests(CampaignQuestInfo[] quests)
        {
            _uiAllQuestsList?.Clear();
            if (_uiAllQuestsList == null)
            {
                return;
            }

            if (quests == null || quests.Length == 0)
            {
                _uiAllQuestsList.Add(BuildQuestLabel("No quests available."));
                return;
            }

            foreach (var quest in quests)
            {
                if (quest == null)
                {
                    continue;
                }

                _uiAllQuestsList.Add(BuildQuestCard(quest, includeStatus: true));
            }
        }

        private void RenderCurrentQuests(CampaignQuestInfo[] quests)
        {
            _uiCurrentQuestsList?.Clear();
            if (_uiCurrentQuestsList == null)
            {
                return;
            }

            var activeCount = 0;
            if (quests != null)
            {
                foreach (var quest in quests)
                {
                    if (!IsCurrentQuest(quest))
                    {
                        continue;
                    }

                    activeCount++;
                    _uiCurrentQuestsList.Add(BuildQuestCard(quest, includeStatus: false));
                }
            }

            if (activeCount == 0)
            {
                _uiCurrentQuestsList.Add(BuildQuestLabel("No active quests."));
            }

            if (_uiCurrentQuestsStatus != null)
            {
                _uiCurrentQuestsStatus.text = activeCount > 0
                    ? $"Active quests: {activeCount}"
                    : "No active quests.";
            }
        }

        private static VisualElement BuildQuestCard(CampaignQuestInfo quest, bool includeStatus)
        {
            var card = new VisualElement();
            card.AddToClassList("hud-quest-item");

            var title = string.IsNullOrWhiteSpace(quest.Title) ? quest.QuestCode : quest.Title;
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("hud-quest-title");
            card.Add(titleLabel);

            var objective = string.IsNullOrWhiteSpace(quest.ShortObjective) ? "No objective." : quest.ShortObjective;
            if (includeStatus)
            {
                var status = string.IsNullOrWhiteSpace(quest.Status) ? "unknown" : quest.Status;
                var category = string.IsNullOrWhiteSpace(quest.Category) ? "main" : quest.Category;
                card.Add(BuildQuestLabel($"[{status}] ({category}) {objective}"));
            }
            else
            {
                card.Add(BuildQuestLabel(objective));
            }

            return card;
        }

        private static Label BuildQuestLabel(string text)
        {
            var label = new Label(text);
            label.AddToClassList("hud-quest-meta");
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        private static bool IsCurrentQuest(CampaignQuestInfo quest)
        {
            if (quest == null)
            {
                return false;
            }

            var status = quest.Status?.Trim();
            if (string.IsNullOrEmpty(status))
            {
                return true;
            }

            return !string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(status, "done", StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(status, "rewarded", StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase);
        }

        private static int ComputeQuestFingerprint(CampaignQuestInfo[] quests)
        {
            unchecked
            {
                var hash = 17;
                if (quests == null)
                {
                    return hash;
                }

                hash = hash * 31 + quests.Length;
                foreach (var quest in quests)
                {
                    if (quest == null)
                    {
                        hash = hash * 31;
                        continue;
                    }

                    hash = hash * 31 + (quest.QuestCode?.GetHashCode() ?? 0);
                    hash = hash * 31 + (quest.Title?.GetHashCode() ?? 0);
                    hash = hash * 31 + (quest.Status?.GetHashCode() ?? 0);
                    hash = hash * 31 + (quest.ShortObjective?.GetHashCode() ?? 0);
                }

                return hash;
            }
        }
    }
}
