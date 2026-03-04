using System.Collections.Generic;
using DVBARPG.Core;
using DVBARPG.Core.Services;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace DVBARPG.UI.CharacterSelect
{
    /// <summary>
    /// Окно выбора персонажа: список (клик по строке — выделение), превью слева, одна кнопка «Играть» при выделении, «Создать персонажа».
    /// </summary>
    public sealed class CharacterSelectScreen : MonoBehaviour
    {
        [Header("Список персонажей")]
        [Tooltip("Родитель для строк списка (вертикальный layout).")]
        [SerializeField] private Transform characterListContent;
        [Tooltip("Префаб одной строки: имя; вся строка кликабельна (вешать Button на корень или дочерний элемент).")]
        [SerializeField] private GameObject characterRowPrefab;

        [Header("Превью")]
        [Tooltip("Корень панели предпросмотра (левее списка). Заполняется при выделении персонажа.")]
        [SerializeField] private GameObject previewPanel;
        [Tooltip("Текст в превью (имя и/или описание выделенного персонажа).")]
        [SerializeField] private Text previewText;

        [Header("Кнопки")]
        [Tooltip("Единая кнопка «Играть» — видна только когда выделен персонаж.")]
        [SerializeField] private Button playButton;
        [Tooltip("Кнопка «Удалить» — видна только когда выделен персонаж.")]
        [SerializeField] private Button deleteButton;
        [Tooltip("Кнопка перехода к созданию персонажа.")]
        [SerializeField] private Button createCharacterButton;

        [Header("Визуал выделения")]
        [Tooltip("Цвет фона строки при выделении (если у префаба есть Image на корне).")]
        [SerializeField] private Color selectedRowColor = new Color(0.4f, 0.6f, 1f, 0.5f);
        [Tooltip("Цвет фона строки без выделения.")]
        [SerializeField] private Color normalRowColor = new Color(1f, 1f, 1f, 0.2f);

        [Header("Текст")]
        [Tooltip("Статус загрузки / ошибка.")]
        [SerializeField] private Text statusText;

        [Header("Runtime")]
        [Tooltip("Автоматически загружать список персонажей и текущий сезон.")]
        [SerializeField] private bool autoFetchMeta = true;

        private IProfileService _profile;
        private readonly List<GameObject> _spawnedRows = new List<GameObject>();
        private RuntimeCharacterSummary[] _lastCharacters;
        private string _selectedCharacterId;

        private void Awake()
        {
            if (createCharacterButton != null)
                createCharacterButton.onClick.AddListener(LoadCharacterCreateScene);
            if (playButton != null)
            {
                playButton.onClick.AddListener(OnPlaySelected);
                playButton.gameObject.SetActive(false);
            }
            if (deleteButton != null)
            {
                deleteButton.onClick.AddListener(OnDeleteSelected);
                deleteButton.gameObject.SetActive(false);
            }
            if (previewPanel != null) previewPanel.SetActive(false);
        }

        private void Start()
        {
            if (GameRoot.Instance == null)
            {
                Debug.LogWarning("CharacterSelectScreen: GameRoot не найден. Запускайте игру со сцены Login или Bootstrap.");
                SetStatus("GameRoot не найден.");
                return;
            }
            _profile = GameRoot.Instance.Services.Get<IProfileService>();
            if (_profile == null)
            {
                Debug.LogWarning("CharacterSelectScreen: IProfileService не найден.");
                SetStatus("Профиль недоступен.");
                return;
            }
            if (!autoFetchMeta)
            {
                Debug.Log("CharacterSelectScreen: autoFetchMeta выключен, загрузка не выполняется.");
                return;
            }
            var auth = GameRoot.Instance.Services.Get<IAuthService>();
            if (_profile.CurrentAuth == null && auth != null)
                _profile.SetAuth(auth.Login());

            FetchCharactersAndSeason();
        }

        private void OnDestroy()
        {
            if (createCharacterButton != null) createCharacterButton.onClick.RemoveAllListeners();
            if (playButton != null) playButton.onClick.RemoveAllListeners();
            if (deleteButton != null) deleteButton.onClick.RemoveAllListeners();
            ClearSpawnedRows();
        }

        private void FetchCharactersAndSeason()
        {
            if (_profile == null) { SetStatus("Профиль недоступен."); return; }

            var meta = GameRoot.Instance?.Services?.Get<IRuntimeMetaService>();
            if (meta == null)
            {
                Debug.LogWarning("CharacterSelectScreen: IRuntimeMetaService не найден.");
                SetStatus("Сервис недоступен.");
                return;
            }
            if (_profile.CurrentAuth == null)
            {
                Debug.LogWarning("CharacterSelectScreen: CurrentAuth == null, запрос к бэку может не пройти.");
            }

            SetStatus("Загрузка...");
            meta.FetchCurrentSeason(_profile.CurrentAuth, season =>
            {
                if (season == null || !season.Ok)
                {
                    var err = season?.Error ?? "Ошибка сезона.";
                    Debug.LogWarning($"CharacterSelectScreen: сезон — {err}");
                    SetStatus(err);
                    return;
                }
                _profile.SetCurrentSeason(season.SeasonId);
                meta.FetchCharacters(_profile.CurrentAuth, characters =>
                {
                    if (characters == null || !characters.Ok)
                    {
                        var err = characters?.Error ?? "Ошибка загрузки персонажей.";
                        Debug.LogWarning($"CharacterSelectScreen: персонажи — {err}");
                        SetStatus(err);
                        return;
                    }
                    _profile.SetCharacters(characters.Characters);
                    BuildCharacterList(characters.Characters);
                    if (string.IsNullOrWhiteSpace(_selectedCharacterId) && characters.Characters.Length > 0)
                        SelectCharacter(characters.Characters[0].Id);
                    SetStatus(characters.Characters.Length > 0 ? $"Персонажей: {characters.Characters.Length}" : "Нет персонажей. Создайте первого.");
                });
            });
        }

        private void BuildCharacterList(RuntimeCharacterSummary[] characters)
        {
            ClearSpawnedRows();
            _lastCharacters = characters;
            if (characterListContent == null || characterRowPrefab == null || characters == null)
            {
                if (characterListContent == null) Debug.LogWarning("CharacterSelectScreen: Character List Content не назначен.");
                if (characterRowPrefab == null) Debug.LogWarning("CharacterSelectScreen: Character Row Prefab не назначен.");
                return;
            }

            for (int i = 0; i < characters.Length; i++)
            {
                var summary = characters[i];
                var row = Object.Instantiate(characterRowPrefab, characterListContent);
                _spawnedRows.Add(row);

                var displayName = string.IsNullOrEmpty(summary.Name) ? summary.Id : summary.Name;
                var label = row.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = displayName;
                var tmpLabel = row.GetComponentInChildren<TextMeshProUGUI>();
                if (tmpLabel != null)
                    tmpLabel.text = displayName;

                var rowButton = row.GetComponent<Button>();
                if (rowButton == null) rowButton = row.GetComponentInChildren<Button>();
                if (rowButton != null)
                {
                    var characterId = summary.Id;
                    rowButton.onClick.RemoveAllListeners();
                    rowButton.onClick.AddListener(() => SelectCharacter(characterId));
                }
            }

            RefreshRowHighlights();
            UpdatePreview();
            UpdatePlayButtonVisibility();
        }

        private void SelectCharacter(string characterId)
        {
            _selectedCharacterId = characterId;
            if (_profile != null) _profile.SetSelectedCharacter(characterId);
            RefreshRowHighlights();
            UpdatePreview();
            UpdatePlayButtonVisibility();
        }

        private void RefreshRowHighlights()
        {
            if (_lastCharacters == null) return;
            for (int i = 0; i < _spawnedRows.Count && i < _lastCharacters.Length; i++)
            {
                var row = _spawnedRows[i];
                var img = row != null ? row.GetComponent<Image>() : null;
                if (img != null)
                    img.color = string.Equals(_lastCharacters[i].Id, _selectedCharacterId, System.StringComparison.OrdinalIgnoreCase) ? selectedRowColor : normalRowColor;
            }
        }

        private void UpdatePreview()
        {
            if (previewPanel != null) previewPanel.SetActive(!string.IsNullOrEmpty(_selectedCharacterId));
            if (previewText == null || _lastCharacters == null) return;
            var summary = FindCharacter(_selectedCharacterId);
            if (summary == null) { previewText.text = ""; return; }
            var name = string.IsNullOrEmpty(summary.Name) ? summary.Id : summary.Name;
            var genderLabel = string.IsNullOrEmpty(summary.Gender) ? "" : (summary.Gender == "female" ? " (Ж)" : " (М)");
            previewText.text = name + genderLabel;
        }

        private void UpdatePlayButtonVisibility()
        {
            var visible = !string.IsNullOrEmpty(_selectedCharacterId);
            if (playButton != null) playButton.gameObject.SetActive(visible);
            if (deleteButton != null) deleteButton.gameObject.SetActive(visible);
        }

        private RuntimeCharacterSummary FindCharacter(string id)
        {
            if (_lastCharacters == null || string.IsNullOrEmpty(id)) return null;
            foreach (var c in _lastCharacters)
                if (string.Equals(c?.Id, id, System.StringComparison.OrdinalIgnoreCase)) return c;
            return null;
        }

        private void OnPlaySelected()
        {
            if (_profile == null || string.IsNullOrWhiteSpace(_selectedCharacterId)) return;
            if (string.IsNullOrWhiteSpace(_profile.CurrentSeasonId)) return;

            _profile.SetSelectedCharacter(_selectedCharacterId);
            UpdateAuthWithCharacter();
            SceneManager.LoadScene("Run");
        }

        private void UpdateAuthWithCharacter()
        {
            var current = _profile?.CurrentAuth;
            if (current == null || string.IsNullOrWhiteSpace(_profile.SelectedCharacterId) || string.IsNullOrWhiteSpace(_profile.CurrentSeasonId)) return;

            _profile.SetAuth(new AuthSession
            {
                PlayerId = current.PlayerId,
                Token = current.Token,
                CharacterId = System.Guid.Parse(_profile.SelectedCharacterId),
                SeasonId = System.Guid.Parse(_profile.CurrentSeasonId)
            });
        }

        private void LoadCharacterCreateScene()
        {
            SceneManager.LoadScene("CharacterCreate");
        }

        private void OnDeleteSelected()
        {
            if (_profile == null || string.IsNullOrWhiteSpace(_selectedCharacterId)) return;

            var meta = GameRoot.Instance?.Services?.Get<IRuntimeMetaService>();
            if (meta == null)
            {
                SetStatus("Сервис недоступен.");
                return;
            }
            if (_profile.CurrentAuth == null)
            {
                SetStatus("Нужна авторизация.");
                return;
            }

            SetStatus("Удаление...");
            meta.DeleteCharacter(_profile.CurrentAuth, _selectedCharacterId, result =>
            {
                if (result == null || !result.Ok)
                {
                    var err = result?.Error ?? "Ошибка удаления.";
                    SetStatus(err);
                    return;
                }
                _selectedCharacterId = null;
                _profile.SetSelectedCharacter(null);
                FetchCharactersAndSeason();
            });
        }

        private void ClearSpawnedRows()
        {
            foreach (var go in _spawnedRows)
            {
                if (go != null) Object.Destroy(go);
            }
            _spawnedRows.Clear();
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }
    }
}
