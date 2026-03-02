using System;
using DVBARPG.Core.Services;
using UnityEngine;

namespace DVBARPG.Game.Dev
{
    public sealed class DevRunBootstrap : MonoBehaviour
    {
        [Header("Dev-старт")]
        [Tooltip("Автоматически логинит и выбирает класс при запуске сцены Run.")]
        [SerializeField] private bool autoLoginInRun = true;
        [Tooltip("Если включено — работает только в редакторе.")]
        [SerializeField] private bool onlyInEditor = true;
        [Tooltip("Класс по умолчанию при автологине.")]
        [SerializeField] private string defaultClassId = "Melee";
        [Tooltip("Предпочитаемый CharacterId (если пусто — берём первого из списка).")]
        [SerializeField] private string preferredCharacterId = "";

        private RuntimeCharacterSummary[] _characters = Array.Empty<RuntimeCharacterSummary>();

        private void Awake()
        {
            if (!autoLoginInRun) return;
            if (onlyInEditor && !Application.isEditor) return;

            var profile = DVBARPG.Core.GameRoot.Instance.Services.Get<IProfileService>();
            if (profile.CurrentAuth == null)
            {
                var auth = DVBARPG.Core.GameRoot.Instance.Services.Get<IAuthService>();
                profile.SetAuth(auth.Login());
            }

            if (string.IsNullOrEmpty(profile.SelectedClassId))
            {
                profile.SetSelectedClass(defaultClassId);
            }

            var meta = DVBARPG.Core.GameRoot.Instance.Services.Get<IRuntimeMetaService>();
            meta.FetchCurrentSeason(profile.CurrentAuth, season =>
            {
                if (season == null || !season.Ok)
                {
                    return;
                }

                profile.SetCurrentSeason(season.SeasonId);
                meta.FetchCharacters(profile.CurrentAuth, characters =>
                {
                    if (characters == null || !characters.Ok)
                    {
                        return;
                    }

                    profile.SetCharacters(characters.Characters);
                    _characters = characters.Characters ?? Array.Empty<RuntimeCharacterSummary>();
                    ApplySelection(profile);
                });
            });
        }

        private void ApplySelection(IProfileService profile)
        {
            if (_characters.Length == 0) return;
            var selected = _characters[0].Id;
            if (!string.IsNullOrWhiteSpace(preferredCharacterId))
            {
                for (int i = 0; i < _characters.Length; i++)
                {
                    if (string.Equals(_characters[i].Id, preferredCharacterId, StringComparison.OrdinalIgnoreCase))
                    {
                        selected = _characters[i].Id;
                        break;
                    }
                }
            }
            profile.SetSelectedCharacter(selected);

            ApplySelectionToAuth(profile);
        }

        private static void ApplySelectionToAuth(IProfileService profile)
        {
            
            if (profile.CurrentAuth == null) return;
            if (string.IsNullOrWhiteSpace(profile.SelectedCharacterId)) return;
            if (string.IsNullOrWhiteSpace(profile.CurrentSeasonId)) return;

            profile.SetAuth(new AuthSession
            {
                PlayerId = profile.CurrentAuth.PlayerId,
                Token = profile.CurrentAuth.Token,
                CharacterId = Guid.Parse(profile.SelectedCharacterId),
                SeasonId = Guid.Parse(profile.CurrentSeasonId)
            });
        }

    }
}
