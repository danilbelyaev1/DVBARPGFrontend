using DVBARPG.Core;
using DVBARPG.Core.Services;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DVBARPG.UI.CharacterSelect
{
    public sealed class CharacterSelectScreen : MonoBehaviour
    {
        [Header("Кнопки")]
        [Tooltip("Кнопка выбора класса Melee.")]
        [SerializeField] private Button meleeButton;
        [Tooltip("Кнопка выбора класса Ranged.")]
        [SerializeField] private Button rangedButton;
        [Tooltip("Кнопка выбора класса Mage.")]
        [SerializeField] private Button mageButton;

        [Header("Runtime")]
        [Tooltip("Автоматически загружать список персонажей и текущий сезон.")]
        [SerializeField] private bool autoFetchMeta = true;

        private void Awake()
        {
            if (meleeButton != null) meleeButton.onClick.AddListener(OnSelect);
            if (rangedButton != null) rangedButton.onClick.AddListener(OnSelect);
            if (mageButton != null) mageButton.onClick.AddListener(OnSelect);
        }

        private void Start()
        {
            if (!autoFetchMeta) return;

            var profile = GameRoot.Instance.Services.Get<IProfileService>();
            var auth = GameRoot.Instance.Services.Get<IAuthService>();
            if (profile.CurrentAuth == null)
            {
                profile.SetAuth(auth.Login());
            }

            var meta = GameRoot.Instance.Services.Get<IRuntimeMetaService>();
            meta.FetchCurrentSeason(profile.CurrentAuth, season =>
            {
                if (season == null || !season.Ok)
                {
                    Debug.LogWarning($"CharacterSelectScreen: season fetch failed. error={season?.Error}");
                    return;
                }

                profile.SetCurrentSeason(season.SeasonId);
                meta.FetchCharacters(profile.CurrentAuth, characters =>
                {
                    if (characters == null || !characters.Ok)
                    {
                        Debug.LogWarning($"CharacterSelectScreen: characters fetch failed. error={characters?.Error}");
                        return;
                    }

                    profile.SetCharacters(characters.Characters);
                    if (string.IsNullOrWhiteSpace(profile.SelectedCharacterId) && characters.Characters.Length > 0)
                    {
                        profile.SetSelectedCharacter(characters.Characters[0].Id);
                    }
                });
            });
        }

        private void OnDestroy()
        {
            if (meleeButton != null) meleeButton.onClick.RemoveAllListeners();
            if (rangedButton != null) rangedButton.onClick.RemoveAllListeners();
            if (mageButton != null) mageButton.onClick.RemoveAllListeners();
        }

        private void OnSelect()
        {
            var profile = GameRoot.Instance.Services.Get<IProfileService>();
            if (!EnsureCharacterSelection(profile))
            {
                Debug.LogWarning("CharacterSelectScreen: character list not ready.");
                return;
            }

            SceneManager.LoadScene("Run");
        }

        private static bool EnsureCharacterSelection(IProfileService profile)
        {
            if (!string.IsNullOrWhiteSpace(profile.SelectedCharacterId) && !string.IsNullOrWhiteSpace(profile.CurrentSeasonId))
            {
                var current = profile.CurrentAuth;
                if (current != null)
                {
                    profile.SetAuth(new AuthSession
                    {
                        PlayerId = current.PlayerId,
                        Token = current.Token,
                        CharacterId = System.Guid.Parse(profile.SelectedCharacterId),
                        SeasonId = System.Guid.Parse(profile.CurrentSeasonId)
                    });
                }
                return true;
            }

            if (profile.Characters != null && profile.Characters.Length > 0)
            {
                profile.SetSelectedCharacter(profile.Characters[0].Id);
            }

            return !string.IsNullOrWhiteSpace(profile.SelectedCharacterId) && !string.IsNullOrWhiteSpace(profile.CurrentSeasonId);
        }

    }
}
