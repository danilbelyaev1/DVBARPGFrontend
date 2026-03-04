using DVBARPG.Core;
using DVBARPG.Core.Services;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace DVBARPG.UI.CharacterCreate
{
    /// <summary>
    /// Сцена создания персонажа: выбор класса, пола, ввод имени. Задел под кастомизацию внешности.
    /// </summary>
    public sealed class CharacterCreateScreen : MonoBehaviour
    {
        [Header("Класс")]
        [Tooltip("Кнопка класса Vanguard (Melee).")]
        [SerializeField] private Button classVanguardButton;
        [Tooltip("Кнопка класса Hunter (Ranged).")]
        [SerializeField] private Button classHunterButton;
        [Tooltip("Кнопка класса Mystic (Mage).")]
        [SerializeField] private Button classMysticButton;

        [Header("Пол")]
        [Tooltip("Кнопка «Мужской».")]
        [SerializeField] private Button genderMaleButton;
        [Tooltip("Кнопка «Женский».")]
        [SerializeField] private Button genderFemaleButton;

        [Header("Имя")]
        [Tooltip("Поле ввода имени (legacy UI).")]
        [SerializeField] private InputField nameInputField;
        [Tooltip("Поле ввода имени (TextMeshPro). Заполни одно из двух: nameInputField или nameTmp.")]
        [SerializeField] private TMP_InputField nameTmp;

        [Header("Внешность (на будущее)")]
        [Tooltip("Корень блока кастомизации внешности — пока заглушка.")]
        [SerializeField] private GameObject appearancePanel;

        [Header("Кнопки")]
        [Tooltip("Кнопка «Создать».")]
        [SerializeField] private Button createButton;
        [Tooltip("Кнопка «Назад».")]
        [SerializeField] private Button backButton;

        [Header("Текст")]
        [Tooltip("Сообщение об ошибке / статус (legacy Text).")]
        [SerializeField] private Text statusText;
        [Tooltip("Сообщение об ошибке / статус (TextMeshPro). Заполни одно из двух.")]
        [SerializeField] private TextMeshProUGUI statusTmp;

        private string _selectedClassId = ClassLoadoutPresets.Vanguard;
        private string _selectedGender = "male";

        private void Awake()
        {
            if (classVanguardButton != null) classVanguardButton.onClick.AddListener(() => SelectClass(ClassLoadoutPresets.Vanguard));
            if (classHunterButton != null) classHunterButton.onClick.AddListener(() => SelectClass(ClassLoadoutPresets.Hunter));
            if (classMysticButton != null) classMysticButton.onClick.AddListener(() => SelectClass(ClassLoadoutPresets.Mystic));
            if (genderMaleButton != null) genderMaleButton.onClick.AddListener(() => SelectGender("male"));
            if (genderFemaleButton != null) genderFemaleButton.onClick.AddListener(() => SelectGender("female"));
            if (createButton != null) createButton.onClick.AddListener(OnCreate);
            if (backButton != null) backButton.onClick.AddListener(() => SceneManager.LoadScene("CharacterSelect"));
            if (appearancePanel != null) appearancePanel.SetActive(true);
        }

        private void OnDestroy()
        {
            if (classVanguardButton != null) classVanguardButton.onClick.RemoveAllListeners();
            if (classHunterButton != null) classHunterButton.onClick.RemoveAllListeners();
            if (classMysticButton != null) classMysticButton.onClick.RemoveAllListeners();
            if (genderMaleButton != null) genderMaleButton.onClick.RemoveAllListeners();
            if (genderFemaleButton != null) genderFemaleButton.onClick.RemoveAllListeners();
            if (createButton != null) createButton.onClick.RemoveAllListeners();
            if (backButton != null) backButton.onClick.RemoveAllListeners();
        }

        private void SelectClass(string classId)
        {
            _selectedClassId = classId ?? ClassLoadoutPresets.Vanguard;
        }

        private void SelectGender(string gender)
        {
            _selectedGender = gender ?? "male";
        }

        private string GetNameFromField()
        {
            if (nameTmp != null && !string.IsNullOrWhiteSpace(nameTmp.text))
                return nameTmp.text.Trim();
            if (nameInputField != null && !string.IsNullOrWhiteSpace(nameInputField.text))
                return nameInputField.text.Trim();
            return "";
        }

        private void OnCreate()
        {
            var name = GetNameFromField();
            if (string.IsNullOrEmpty(name))
            {
                SetStatus("Введите имя персонажа.");
                return;
            }

            var profile = GameRoot.Instance?.Services?.Get<IProfileService>();
            var auth = profile?.CurrentAuth;
            if (auth == null)
            {
                SetStatus("Нет авторизации.");
                return;
            }

            var meta = GameRoot.Instance?.Services?.Get<IRuntimeMetaService>();
            if (meta == null)
            {
                SetStatus("Сервис недоступен.");
                return;
            }

            SetStatus("Создание...");
            createButton.interactable = false;
            var classId = _selectedClassId;
            var seasonId = profile.CurrentSeasonId;
            meta.CreateCharacter(auth, name, classId, _selectedGender, result =>
            {
                if (result == null || !result.Ok)
                {
                    createButton.interactable = true;
                    SetStatus(result?.Error ?? "Ошибка создания.");
                    return;
                }
                if (string.IsNullOrWhiteSpace(result.CharacterId) || string.IsNullOrWhiteSpace(seasonId))
                {
                    createButton.interactable = true;
                    SceneManager.LoadScene("CharacterSelect");
                    return;
                }
                var loadout = ClassLoadoutPresets.GetLoadoutForClass(classId);
                meta.SetLoadout(auth, result.CharacterId, seasonId, loadout, setLoadoutResult =>
                {
                    createButton.interactable = true;
                    if (setLoadoutResult != null && !setLoadoutResult.Ok)
                        SetStatus(setLoadoutResult.Error ?? "Персонаж создан, лоадут не установлен.");
                    profile.SetSelectedCharacter(result.CharacterId);
                    profile.SetCurrentSeason(seasonId);
                    SceneManager.LoadScene("CharacterSelect");
                });
            });
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
            if (statusTmp != null) statusTmp.text = message;
        }
    }
}
