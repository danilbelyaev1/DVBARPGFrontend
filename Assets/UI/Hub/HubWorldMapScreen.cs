using System.Collections;
using System.Collections.Generic;
using System;
using DVBARPG.Core;
using DVBARPG.Core.Services;
using DVBARPG.UI.Common;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DVBARPG.UI.Hub
{
    public sealed class HubWorldMapScreen : MonoBehaviour
    {
        private const string LeftWindowId = "hub_teleport";

        [Tooltip("Модалка с затемнением и закрытием по клику вне контента. Если задана — показывается вместо простого SetActive(locationsPanel).")]
        [SerializeField] private UiModalLayer locationPickerModal;
        [SerializeField] private GameObject locationsPanel;
        [SerializeField] private Transform locationsRoot;
        [SerializeField] private GameObject locationButtonPrefab;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private ErrorToast errorToast;
        [Tooltip("Если задано — использовать этот код хаба вместо вывода из SessionState (отладка).")]
        [SerializeField] private string hubMapOverride = "";

        [Header("Карточка локации (сведения → переход)")]
        [SerializeField] private GameObject locationDetailPanel;
        [SerializeField] private TextMeshProUGUI locationDetailBodyText;
        [SerializeField] private Button locationDetailConfirmButton;
        [SerializeField] private Button locationDetailBackButton;

        private string _fromMapCode = "act1_hub";
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private CampaignTravelOption _detailOption;
        private bool _isLocationsVisible;

        private void Awake()
        {
            if (locationPickerModal != null)
            {
                if (locationsPanel != null)
                {
                    locationPickerModal.Configure(locationsPanel.transform as RectTransform);
                }
                locationPickerModal.DismissRequested += OnLocationPickerModalDismissed;
            }

            HudWindowCoordinator.LeftWindowOpened += OnOtherLeftWindowOpened;
        }

        private void OnDestroy()
        {
            UnbindLocationDetailClickHandlers();

            if (locationPickerModal != null)
            {
                locationPickerModal.DismissRequested -= OnLocationPickerModalDismissed;
            }

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

        private void OnLocationPickerModalDismissed()
        {
            var sessionState = GameRoot.Instance?.Services?.Get<SessionState>();
            if (sessionState != null)
            {
                sessionState.HubTeleportMenuOpen = false;
            }

            HideLocationDetail();
            SetLocationsVisible(false);
        }

        private void Start()
        {
            var sessionState = GameRoot.Instance?.Services?.Get<SessionState>();
            if (sessionState != null)
            {
                sessionState.HubTeleportMenuOpen = false;
            }

            if (!ValidateLocationDetailUi())
            {
                Debug.LogError(
                    "[HubWorldMapScreen] Заполните в инспекторе: Location Detail Panel, Body Text, Confirm Button, Back Button.",
                    this);
            }

            BindLocationDetailClickHandlers();

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
            SetStatus("Campaign loaded.");
        }

        private void BuildButtons(CampaignState state)
        {
            foreach (var go in _spawned) if (go != null) Destroy(go);
            _spawned.Clear();

            if (locationsRoot == null || locationButtonPrefab == null) return;
            if (!state.TravelOptionsByMap.TryGetValue(_fromMapCode, out var options) || options == null) return;
            Debug.Log($"[HubWorldMapScreen] BuildButtons: fromMap={_fromMapCode}, optionsCount={options.Length}");

            foreach (var option in options)
            {
                var canClick = option.CanFirstVisit || option.Teleportable;
                if (!canClick)
                {
                    continue;
                }

                var row = Instantiate(locationButtonPrefab, locationsRoot);
                _spawned.Add(row);
                // MapButton.prefab: корневой CanvasGroup с blocksRaycasts=0 пропускает все лучи — onClick никогда не срабатывает.
                foreach (var cg in row.GetComponentsInChildren<CanvasGroup>(true))
                {
                    cg.blocksRaycasts = true;
                }

                var btn = row.GetComponent<Button>() ?? row.GetComponentInChildren<Button>();
                foreach (var tmp in row.GetComponentsInChildren<TMP_Text>(true))
                {
                    tmp.raycastTarget = false;
                }

                var labelTmp = row.GetComponentInChildren<TMP_Text>();
                var cta = option.Teleportable ? "Teleport" : "Portal";
                var labelText = $"{option.ToMapCode} [{cta}]";
                if (labelTmp != null)
                {
                    labelTmp.text = labelText;
                }
                if (btn != null)
                {
                    btn.interactable = canClick;
                    var g = btn.targetGraphic;
                    if (g == null)
                    {
                        g = btn.GetComponent<Graphic>() ?? btn.GetComponentInChildren<Graphic>(true);
                        if (g != null)
                        {
                            btn.targetGraphic = g;
                        }
                    }

                    if (g != null)
                    {
                        g.raycastTarget = true;
                    }
                    else
                    {
                        Debug.LogError($"[HubWorldMapScreen] У кнопки в префабе строки нет Graphic для raycast: '{btn.name}'.");
                    }

                    var selected = option;
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() =>
                    {
                        Debug.Log($"[HubWorldMapScreen] Location button clicked: to={selected.ToMapCode}, teleportable={selected.Teleportable}, canFirstVisit={selected.CanFirstVisit}, travelType={selected.TravelType ?? "<null>"}, button={btn.name}, row={row.name}");
                        OpenLocationDetail(selected);
                    });
                }
                else
                {
                    Debug.LogWarning($"[HubWorldMapScreen] Location row has no Button: to={option.ToMapCode}, row={row.name}");
                }
            }
            Debug.Log($"[HubWorldMapScreen] BuildButtons done: spawned={_spawned.Count}");
        }

        private bool ValidateLocationDetailUi()
        {
            return locationDetailPanel != null
                   && locationDetailBodyText != null
                   && locationDetailConfirmButton != null
                   && locationDetailBackButton != null;
        }

        private static void PrepareDetailButtonForClicks(Button btn)
        {
            if (btn == null) return;
            btn.interactable = true;
            foreach (var tmp in btn.GetComponentsInChildren<TMP_Text>(true))
            {
                tmp.raycastTarget = false;
            }

            var g = btn.targetGraphic;
            if (g == null)
            {
                g = btn.GetComponent<Graphic>() ?? btn.GetComponentInChildren<Graphic>(true);
            }

            if (g != null)
            {
                g.raycastTarget = true;
                btn.targetGraphic = g;
            }
        }

        private void BindLocationDetailClickHandlers()
        {
            if (!ValidateLocationDetailUi())
            {
                return;
            }

            UnbindLocationDetailClickHandlers();

            foreach (var cg in locationDetailPanel.GetComponentsInChildren<CanvasGroup>(true))
            {
                cg.blocksRaycasts = true;
            }

            PrepareDetailButtonForClicks(locationDetailConfirmButton);
            PrepareDetailButtonForClicks(locationDetailBackButton);
            locationDetailConfirmButton.onClick.AddListener(OnLocationDetailConfirmClicked);
            locationDetailBackButton.onClick.AddListener(OnHideLocationDetailClicked);
        }

        private void UnbindLocationDetailClickHandlers()
        {
            if (locationDetailConfirmButton != null)
            {
                locationDetailConfirmButton.onClick.RemoveListener(OnLocationDetailConfirmClicked);
            }

            if (locationDetailBackButton != null)
            {
                locationDetailBackButton.onClick.RemoveListener(OnHideLocationDetailClicked);
            }
        }

        private void OnHideLocationDetailClicked()
        {
            HideLocationDetail();
        }

        private void OpenLocationDetail(CampaignTravelOption option)
        {
            if (option == null) return;

            if (!ValidateLocationDetailUi())
            {
                Debug.LogError("[HubWorldMapScreen] Карточка локации: не все ссылки заданы в инспекторе.", this);
                return;
            }

            _detailOption = option;
            locationDetailBodyText.text = FormatLocationDetail(option);

            locationDetailPanel.SetActive(true);
            if (locationsRoot != null)
            {
                locationsRoot.gameObject.SetActive(false);
            }

            SetStatus("Проверьте сведения о локации и нажмите «Перейти».");
        }

        private void HideLocationDetail()
        {
            _detailOption = null;
            if (locationDetailPanel != null)
            {
                locationDetailPanel.SetActive(false);
            }

            if (locationsRoot != null)
            {
                locationsRoot.gameObject.SetActive(true);
            }
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

            if (locationPickerModal != null)
            {
                if (isVisible)
                {
                    locationPickerModal.Show();
                }
                else
                {
                    locationPickerModal.Hide();
                }

                return;
            }

            if (locationsPanel != null)
            {
                locationsPanel.SetActive(isVisible);
                return;
            }

            if (locationsRoot != null)
            {
                locationsRoot.gameObject.SetActive(isVisible);
            }
        }

        private void Update()
        {
            var sessionState = GameRoot.Instance?.Services?.Get<SessionState>();
            if (sessionState == null) return;
            SetLocationsVisible(sessionState.HubTeleportMenuOpen);
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
    }
}
