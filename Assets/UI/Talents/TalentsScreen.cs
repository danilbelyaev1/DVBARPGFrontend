using System;
using DVBARPG.Core;
using DVBARPG.Core.Services;
using UnityEngine;
using UnityEngine.UI;

namespace DVBARPG.UI.Talents
{
    /// <summary>
    /// Минимальный экран талантов: отображение статуса и кнопка выделения таланта (для теста — один код).
    /// </summary>
    public sealed class TalentsScreen : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Text statusText;
        [SerializeField] private Button allocateButton;
        [Tooltip("Код таланта для кнопки (например brutal_strike).")]
        [SerializeField] private string talentCode = "brutal_strike";
        [SerializeField] private Button closeButton;

        private void Awake()
        {
            if (allocateButton != null) allocateButton.onClick.AddListener(OnAllocate);
            if (closeButton != null) closeButton.onClick.AddListener(() => UnityEngine.SceneManagement.SceneManager.LoadScene("CharacterSelect"));
        }

        private void OnEnable()
        {
            SetStatus("Выберите талант и нажмите «Выделить».");
        }

        private void OnAllocate()
        {
            var meta = GameRoot.Instance?.Services?.Get<IRuntimeMetaService>();
            var profile = GameRoot.Instance?.Services?.Get<IProfileService>();
            if (meta == null || profile == null)
            {
                SetStatus("Сервисы недоступны.");
                return;
            }

            var characterId = profile.SelectedCharacterId;
            var seasonId = profile.CurrentSeasonId;
            var auth = profile.CurrentAuth;
            if (string.IsNullOrWhiteSpace(characterId) || string.IsNullOrWhiteSpace(seasonId) || auth == null)
            {
                SetStatus("Выберите персонажа и сезон.");
                return;
            }

            if (string.IsNullOrWhiteSpace(talentCode))
            {
                SetStatus("Укажите код таланта.");
                return;
            }

            SetStatus("Отправка...");
            var requestId = $"talent-{Guid.NewGuid():N}";
            meta.AllocateTalent(auth, characterId, seasonId, talentCode, requestId, result =>
            {
                if (result != null && result.Ok)
                    SetStatus("Талант выделен.");
                else
                    SetStatus(result?.Error ?? "Ошибка.");
            });
        }

        private void SetStatus(string msg)
        {
            if (statusText != null) statusText.text = msg;
        }
    }
}
