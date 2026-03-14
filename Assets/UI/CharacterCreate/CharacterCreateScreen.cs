using System.Collections;
using System.Collections.Generic;
using DVBARPG.Core;
using DVBARPG.Core.Services;
using DVBARPG.Game.CharacterCreation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace DVBARPG.UI.CharacterCreate
{
    /// <summary>
    /// Сцена создания персонажа: выбор класса, пола, ввод имени, кастомизация внешности (слайдеры тела, превью).
    /// </summary>
    public sealed class CharacterCreateScreen : MonoBehaviour
    {
        [Header("Превью и сборщик")]
        [Tooltip("Сборщик Sidekick. Добавь на этот же объект или на дочерний.")]
        [SerializeField] private SidekickAppearanceBuilder appearanceBuilder;
        [Tooltip("Сюда ставится собранная модель превью (дочерний объект).")]
        [SerializeField] private Transform previewPivot;

        [Header("Тело (слайдеры)")]
        [Tooltip("Вес: минус = худой, плюс = тяжёлый. Влияет на BodySizeValue.")]
        [SerializeField] private Slider weightSlider;
        [Tooltip("Мускулатура. Влияет на MuscleValue.")]
        [SerializeField] private Slider muscleSlider;

        [Header("Волосы")]
        [Tooltip("Выпадающий список вариантов волос для текущего вида (Species). TextMeshPro — TMP_Dropdown.")]
        [SerializeField] private TMP_Dropdown hairDropdown;
        [Header("Борода")]
        [Tooltip("Выпадающий список вариантов бороды (FacialHair) для текущего вида. TextMeshPro — TMP_Dropdown.")]
        [SerializeField] private TMP_Dropdown beardDropdown;

        [Header("Цвета")]
        [Tooltip("Цвет волос. Пресеты группы Species; применяются только строки с Hair в имени свойства.")]
        [SerializeField] private TMP_Dropdown hairColorPresetDropdown;
        [Tooltip("Цвет кожи. Пресеты группы Species; применяются только строки без Hair в имени свойства.")]
        [SerializeField] private TMP_Dropdown skinColorPresetDropdown;

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

        private const int PartTypeHair = 2; // CharacterPartType.Hair
        private const int PartTypeFacialHair = 9; // CharacterPartType.FacialHair

        private string _selectedClassId = ClassLoadoutPresets.Vanguard;
        private string _selectedGender = "male";
        private CharacterAppearanceData _currentAppearance;
        private GameObject _previewInstance;
        private List<string> _hairPartNames = new List<string>();
        private List<string> _facialHairPartNames = new List<string>();
        private List<int> _hairColorPresetIds = new List<int>();
        private List<int> _skinColorPresetIds = new List<int>();
        private Coroutine _refreshPreviewRoutine;

        private static readonly string[] BlendGenderNames = { "masculineFeminine", "Feminine", "Gender" };
        private static readonly string[] BlendSkinnyNames = { "defaultSkinny", "Skinny" };
        private static readonly string[] BlendHeavyNames = { "defaultHeavy", "Heavy" };
        private static readonly string[] BlendMuscleNames = { "defaultBuff", "Buff", "Muscle" };

        private static bool MatchesAny(string blendName, string[] keywords)
        {
            if (string.IsNullOrEmpty(blendName)) return false;
            for (int i = 0; i < keywords.Length; i++)
                if (blendName.IndexOf(keywords[i], System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private void Awake()
        {
            if (appearanceBuilder == null) appearanceBuilder = GetComponent<SidekickAppearanceBuilder>();
            if (previewPivot == null)
            {
                var go = GameObject.Find("PreviewPivot");
                if (go != null) previewPivot = go.transform;
            }
            if (classVanguardButton != null) classVanguardButton.onClick.AddListener(() => SelectClass(ClassLoadoutPresets.Vanguard));
            if (classHunterButton != null) classHunterButton.onClick.AddListener(() => SelectClass(ClassLoadoutPresets.Hunter));
            if (classMysticButton != null) classMysticButton.onClick.AddListener(() => SelectClass(ClassLoadoutPresets.Mystic));
            if (genderMaleButton != null) genderMaleButton.onClick.AddListener(() => SelectGender("male"));
            if (genderFemaleButton != null) genderFemaleButton.onClick.AddListener(() => SelectGender("female"));
            if (createButton != null) createButton.onClick.AddListener(OnCreate);
            if (backButton != null) backButton.onClick.AddListener(() => SceneManager.LoadScene("CharacterSelect"));
            if (appearancePanel != null) appearancePanel.SetActive(true);

            if (weightSlider != null) weightSlider.onValueChanged.AddListener(OnWeightOrMuscleChanged);
            if (muscleSlider != null) muscleSlider.onValueChanged.AddListener(OnWeightOrMuscleChanged);
            if (hairDropdown != null) hairDropdown.onValueChanged.AddListener(OnHairSelectionChanged);
            if (beardDropdown != null) beardDropdown.onValueChanged.AddListener(OnFacialHairSelectionChanged);
            if (hairColorPresetDropdown != null) hairColorPresetDropdown.onValueChanged.AddListener(OnHairColorPresetSelectionChanged);
            if (skinColorPresetDropdown != null) skinColorPresetDropdown.onValueChanged.AddListener(OnSkinColorPresetSelectionChanged);
        }

        private void Start()
        {
            LoadDefaultAppearanceAndRefresh();
        }

        private void OnDestroy()
        {
            if (weightSlider != null) weightSlider.onValueChanged.RemoveListener(OnWeightOrMuscleChanged);
            if (muscleSlider != null) muscleSlider.onValueChanged.RemoveListener(OnWeightOrMuscleChanged);
            if (hairDropdown != null) hairDropdown.onValueChanged.RemoveListener(OnHairSelectionChanged);
            if (beardDropdown != null) beardDropdown.onValueChanged.RemoveListener(OnFacialHairSelectionChanged);
            if (hairColorPresetDropdown != null) hairColorPresetDropdown.onValueChanged.RemoveListener(OnHairColorPresetSelectionChanged);
            if (skinColorPresetDropdown != null) skinColorPresetDropdown.onValueChanged.RemoveListener(OnSkinColorPresetSelectionChanged);
            if (_refreshPreviewRoutine != null) { StopCoroutine(_refreshPreviewRoutine); _refreshPreviewRoutine = null; }
            if (classVanguardButton != null) classVanguardButton.onClick.RemoveAllListeners();
            if (classHunterButton != null) classHunterButton.onClick.RemoveAllListeners();
            if (classMysticButton != null) classMysticButton.onClick.RemoveAllListeners();
            if (genderMaleButton != null) genderMaleButton.onClick.RemoveAllListeners();
            if (genderFemaleButton != null) genderFemaleButton.onClick.RemoveAllListeners();
            if (createButton != null) createButton.onClick.RemoveAllListeners();
            if (backButton != null) backButton.onClick.RemoveAllListeners();
        }

        private void OnWeightOrMuscleChanged(float _)
        {
            if (_currentAppearance?.BlendShapes == null) return;
            if (weightSlider != null) _currentAppearance.BlendShapes.BodySizeValue = weightSlider.value;
            if (muscleSlider != null) _currentAppearance.BlendShapes.MuscleValue = muscleSlider.value;
            ApplyBlendShapesToPreview();
        }

        private void LoadDefaultAppearanceAndRefresh()
        {
            if (appearanceBuilder == null)
            {
                _currentAppearance = BuildFallbackDefaultAppearance();
                SyncUIFromAppearance();
                PopulateHairDropdown();
                PopulateFacialHairDropdown();
                PopulateHairColorDropdown();
                PopulateSkinColorDropdown();
                RefreshPreview();
                return;
            }
            var speciesName = ClassSidekickSpeciesMap.GetSpeciesNameForClass(_selectedClassId);
            var bodySize = weightSlider != null ? weightSlider.value : 0f;
            var muscle = muscleSlider != null ? muscleSlider.value : 50f;
            appearanceBuilder.GetDefaultAppearanceData(speciesName, _selectedGender, bodySize, muscle, data =>
            {
                _currentAppearance = data ?? BuildFallbackDefaultAppearance();
                SyncUIFromAppearance();
                PopulateHairDropdown();
                PopulateFacialHairDropdown();
                PopulateHairColorDropdown();
                PopulateSkinColorDropdown();
                RefreshPreview();
            });
        }

        private CharacterAppearanceData BuildFallbackDefaultAppearance()
        {
            return new CharacterAppearanceData
            {
                SpeciesId = 0,
                Parts = new List<CharacterPartEntry>(),
                BlendShapes = new BlendShapeValues
                {
                    BodyTypeValue = _selectedGender == "female" ? 100f : 0f,
                    BodySizeValue = weightSlider != null ? weightSlider.value : 0f,
                    MuscleValue = muscleSlider != null ? muscleSlider.value : 50f
                },
                FaceBlendShapes = new Dictionary<string, float>(),
                ColorPresetId = null,
                HairColorPresetId = null,
                SkinColorPresetId = null
            };
        }

        private void SyncUIFromAppearance()
        {
            if (_currentAppearance == null) return;
            if (weightSlider != null) weightSlider.SetValueWithoutNotify(_currentAppearance.BlendShapes?.BodySizeValue ?? 0f);
            if (muscleSlider != null) muscleSlider.SetValueWithoutNotify(_currentAppearance.BlendShapes?.MuscleValue ?? 50f);
            if (hairDropdown != null) hairDropdown.SetValueWithoutNotify(0);
            if (beardDropdown != null) beardDropdown.SetValueWithoutNotify(0);
            var hairIdx = _currentAppearance.HairColorPresetId.HasValue ? _hairColorPresetIds.IndexOf(_currentAppearance.HairColorPresetId.Value) : 0;
            if (hairIdx < 0) hairIdx = 0;
            if (hairColorPresetDropdown != null) hairColorPresetDropdown.SetValueWithoutNotify(hairIdx);
            var skinIdx = _currentAppearance.SkinColorPresetId.HasValue ? _skinColorPresetIds.IndexOf(_currentAppearance.SkinColorPresetId.Value) : 0;
            if (skinIdx < 0) skinIdx = 0;
            if (skinColorPresetDropdown != null) skinColorPresetDropdown.SetValueWithoutNotify(skinIdx);
        }

        /// <summary>Пересобрать превью по _currentAppearance и поставить под previewPivot.</summary>
        private void RefreshPreview()
        {
            if (appearanceBuilder == null || previewPivot == null || _currentAppearance == null) return;
            if (_previewInstance != null)
            {
                Destroy(_previewInstance);
                _previewInstance = null;
            }
            appearanceBuilder.BuildAppearance(_currentAppearance, go =>
            {
                if (go == null) return;
                go.transform.SetParent(previewPivot, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                go.transform.localScale = new Vector3(20f, 20f, 20f);
                _previewInstance = go;
                ApplyBlendShapesToPreview();
            });
        }

        /// <summary>Применить блендшейпы тела к уже созданному превью (пол, вес, мускулы). Unity ожидает веса 0–100.</summary>
        private void ApplyBlendShapesToPreview()
        {
            if (_previewInstance == null || _currentAppearance?.BlendShapes == null) return;
            var blend = _currentAppearance.BlendShapes;
            float bodyType = (blend.BodyTypeValue + 100f) / 2f;
            float skinny = blend.BodySizeValue < 0f ? Mathf.Clamp01(-blend.BodySizeValue) * 100f : 0f;
            float heavy = blend.BodySizeValue > 0f ? Mathf.Clamp01(blend.BodySizeValue) * 100f : 0f;
            float muscle = blend.MuscleValue <= 1f && blend.MuscleValue >= 0f
                ? blend.MuscleValue * 100f
                : Mathf.Clamp((blend.MuscleValue + 100f) / 2f, 0f, 100f);

            foreach (var smr in _previewInstance.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh = smr.sharedMesh;
                if (mesh == null) continue;
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    string name = mesh.GetBlendShapeName(i);
                    if (MatchesAny(name, BlendGenderNames)) smr.SetBlendShapeWeight(i, bodyType);
                    else if (MatchesAny(name, BlendSkinnyNames)) smr.SetBlendShapeWeight(i, skinny);
                    else if (MatchesAny(name, BlendHeavyNames)) smr.SetBlendShapeWeight(i, heavy);
                    else if (MatchesAny(name, BlendMuscleNames)) smr.SetBlendShapeWeight(i, muscle);
                }
            }
        }

        private void SelectClass(string classId)
        {
            _selectedClassId = classId ?? ClassLoadoutPresets.Vanguard;
            LoadDefaultAppearanceAndRefresh();
        }

        private void SelectGender(string gender)
        {
            _selectedGender = gender ?? "male";
            if (_currentAppearance?.BlendShapes != null)
            {
                _currentAppearance.BlendShapes.BodyTypeValue = _selectedGender == "female" ? 100f : 0f;
                ApplyBlendShapesToPreview();
            }
            else
                LoadDefaultAppearanceAndRefresh();
        }

        private void PopulateHairDropdown()
        {
            if (hairDropdown == null || appearanceBuilder == null) return;
            var speciesName = ClassSidekickSpeciesMap.GetSpeciesNameForClass(_selectedClassId);
            if (string.IsNullOrEmpty(speciesName)) { _hairPartNames.Clear(); hairDropdown.ClearOptions(); return; }
            appearanceBuilder.GetPartNamesForSpecies(speciesName, PartTypeHair, names =>
            {
                _hairPartNames = names ?? new List<string>();
                hairDropdown.ClearOptions();
                var options = new List<string> { "Без волос" };
                options.AddRange(_hairPartNames);
                hairDropdown.AddOptions(options);
                hairDropdown.SetValueWithoutNotify(0);
            });
        }

        private void OnHairSelectionChanged(int index)
        {
            string partName = (index > 0 && index <= _hairPartNames.Count) ? _hairPartNames[index - 1] : "";
            SetHairPart(partName);
            if (_previewInstance != null && appearanceBuilder != null && appearanceBuilder.ReplacePartOnCharacter(_previewInstance, PartTypeHair, partName))
                return;
            RequestRefreshPreviewDebounced();
        }

        private void SetHairPart(string partName)
        {
            if (_currentAppearance?.Parts == null) return;
            _currentAppearance.Parts.RemoveAll(p => p.PartType == PartTypeHair);
            if (!string.IsNullOrEmpty(partName))
                _currentAppearance.Parts.Add(new CharacterPartEntry { PartType = PartTypeHair, PartName = partName });
        }

        private void PopulateFacialHairDropdown()
        {
            if (beardDropdown == null || appearanceBuilder == null) return;
            var speciesName = ClassSidekickSpeciesMap.GetSpeciesNameForClass(_selectedClassId);
            if (string.IsNullOrEmpty(speciesName)) { _facialHairPartNames.Clear(); beardDropdown.ClearOptions(); return; }
            appearanceBuilder.GetPartNamesForSpecies(speciesName, PartTypeFacialHair, names =>
            {
                _facialHairPartNames = names ?? new List<string>();
                beardDropdown.ClearOptions();
                var options = new List<string> { "Без бороды" };
                options.AddRange(_facialHairPartNames);
                beardDropdown.AddOptions(options);
                beardDropdown.SetValueWithoutNotify(0);
            });
        }

        private void OnFacialHairSelectionChanged(int index)
        {
            string partName = (index > 0 && index <= _facialHairPartNames.Count) ? _facialHairPartNames[index - 1] : "";
            SetFacialHairPart(partName);
            if (_previewInstance != null && appearanceBuilder != null && appearanceBuilder.ReplacePartOnCharacter(_previewInstance, PartTypeFacialHair, partName))
                return;
            RequestRefreshPreviewDebounced();
        }

        private void SetFacialHairPart(string partName)
        {
            if (_currentAppearance?.Parts == null) return;
            _currentAppearance.Parts.RemoveAll(p => p.PartType == PartTypeFacialHair);
            if (!string.IsNullOrEmpty(partName))
                _currentAppearance.Parts.Add(new CharacterPartEntry { PartType = PartTypeFacialHair, PartName = partName });
        }

        private void PopulateHairColorDropdown()
        {
            if (hairColorPresetDropdown == null || appearanceBuilder == null) return;
            var speciesName = ClassSidekickSpeciesMap.GetSpeciesNameForClass(_selectedClassId);
            if (string.IsNullOrEmpty(speciesName)) { _hairColorPresetIds.Clear(); hairColorPresetDropdown.ClearOptions(); return; }
            appearanceBuilder.GetColorPresetsForSpecies(speciesName, presets =>
            {
                _hairColorPresetIds.Clear();
                var options = new List<string>();
                if (presets != null)
                {
                    foreach (var p in presets)
                    {
                        _hairColorPresetIds.Add(p.id);
                        options.Add(p.displayName);
                    }
                }
                hairColorPresetDropdown.ClearOptions();
                if (options.Count > 0) hairColorPresetDropdown.AddOptions(options);
                var targetId = _currentAppearance?.HairColorPresetId ?? _currentAppearance?.ColorPresetId;
                var index = targetId.HasValue ? _hairColorPresetIds.IndexOf(targetId.Value) : 0;
                if (index < 0) index = 0;
                hairColorPresetDropdown.SetValueWithoutNotify(index);
            });
        }

        private void PopulateSkinColorDropdown()
        {
            if (skinColorPresetDropdown == null || appearanceBuilder == null) return;
            var speciesName = ClassSidekickSpeciesMap.GetSpeciesNameForClass(_selectedClassId);
            if (string.IsNullOrEmpty(speciesName)) { _skinColorPresetIds.Clear(); skinColorPresetDropdown.ClearOptions(); return; }
            appearanceBuilder.GetColorPresetsForSpecies(speciesName, presets =>
            {
                _skinColorPresetIds.Clear();
                var options = new List<string>();
                if (presets != null)
                {
                    foreach (var p in presets)
                    {
                        _skinColorPresetIds.Add(p.id);
                        options.Add(p.displayName);
                    }
                }
                skinColorPresetDropdown.ClearOptions();
                if (options.Count > 0) skinColorPresetDropdown.AddOptions(options);
                var targetId = _currentAppearance?.SkinColorPresetId ?? _currentAppearance?.ColorPresetId;
                var index = targetId.HasValue ? _skinColorPresetIds.IndexOf(targetId.Value) : 0;
                if (index < 0) index = 0;
                skinColorPresetDropdown.SetValueWithoutNotify(index);
            });
        }

        private void OnHairColorPresetSelectionChanged(int index)
        {
            if (_currentAppearance == null) return;
            _currentAppearance.HairColorPresetId = (index >= 0 && index < _hairColorPresetIds.Count) ? _hairColorPresetIds[index] : (int?)null;
            if (_previewInstance != null && appearanceBuilder != null && appearanceBuilder.ApplyColorsToExisting(_previewInstance, _currentAppearance))
                return;
            RequestRefreshPreviewDebounced();
        }

        private void OnSkinColorPresetSelectionChanged(int index)
        {
            if (_currentAppearance == null) return;
            _currentAppearance.SkinColorPresetId = (index >= 0 && index < _skinColorPresetIds.Count) ? _skinColorPresetIds[index] : (int?)null;
            if (_previewInstance != null && appearanceBuilder != null && appearanceBuilder.ApplyColorsToExisting(_previewInstance, _currentAppearance))
                return;
            RequestRefreshPreviewDebounced();
        }

        private const float RefreshDebounceSeconds = 0.15f;

        private void RequestRefreshPreviewDebounced()
        {
            if (_refreshPreviewRoutine != null)
                StopCoroutine(_refreshPreviewRoutine);
            _refreshPreviewRoutine = StartCoroutine(RefreshPreviewDebouncedRoutine());
        }

        private IEnumerator RefreshPreviewDebouncedRoutine()
        {
            yield return new WaitForSeconds(RefreshDebounceSeconds);
            _refreshPreviewRoutine = null;
            RefreshPreview();
        }

        private IEnumerator RefreshPreviewNextFrame()
        {
            yield return null;
            RefreshPreview();
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
            meta.CreateCharacter(auth, name, classId, _selectedGender, _currentAppearance, result =>
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
