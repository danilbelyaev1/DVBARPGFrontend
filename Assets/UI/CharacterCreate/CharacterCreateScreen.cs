using System.Collections;
using System.Collections.Generic;
using System;
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
        [Tooltip("Animator Controller для превью (например PlayerAnimator). Если не задан, ищется по имени 'PlayerAnimator'.")]
        [SerializeField] private RuntimeAnimatorController previewAnimatorController;

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
        [Tooltip("Дополнительные цветовые зоны (materials/attachments/elements).")]
        [SerializeField] private TMP_Dropdown otherColorPresetDropdown;

        [Header("Части головы (без одежды)")]
        [SerializeField] private List<PartDropdownBinding> headPartDropdowns = new List<PartDropdownBinding>();

        [Header("Морфы лица")]
        [SerializeField] private List<FaceBlendSliderBinding> faceBlendSliders = new List<FaceBlendSliderBinding>();

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
        private List<int> _otherColorPresetIds = new List<int>();
        private readonly Dictionary<int, List<string>> _partNamesByType = new Dictionary<int, List<string>>();
        private Coroutine _refreshPreviewRoutine;
        private int _previewBuildVersion;

        [Serializable]
        public sealed class PartDropdownBinding
        {
            public string label;
            public int partType;
            public TMP_Dropdown dropdown;
            public string noneOptionLabel = "Без части";
        }

        [Serializable]
        public sealed class FaceBlendSliderBinding
        {
            public string blendShapeName;
            public Slider slider;
        }

        private static readonly string[] BlendGenderNames = { "masculineFeminine", "Feminine", "Gender" };
        private static readonly string[] BlendSkinnyNames = { "defaultSkinny", "Skinny" };
        private static readonly string[] BlendHeavyNames = { "defaultHeavy", "Heavy" };
        private static readonly string[] BlendMuscleNames = { "defaultBuff", "Buff", "Muscle" };

        private RuntimeAnimatorController ResolvePreviewAnimatorController()
        {
            if (previewAnimatorController != null) return previewAnimatorController;
            var animators = FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < animators.Length; i++)
            {
                var c = animators[i] != null ? animators[i].runtimeAnimatorController : null;
                if (c != null && string.Equals(c.name, "PlayerAnimator", System.StringComparison.OrdinalIgnoreCase))
                {
                    previewAnimatorController = c;
                    return previewAnimatorController;
                }
            }
            return null;
        }

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
            if (otherColorPresetDropdown != null) otherColorPresetDropdown.onValueChanged.AddListener(OnOtherColorPresetSelectionChanged);
            BindHeadPartDropdownListeners();
            BindFaceBlendSliderListeners();
        }

        private void Start()
        {
            StartCoroutine(InitializeWithWarmup());
        }

        private IEnumerator InitializeWithWarmup()
        {
            if (appearanceBuilder == null)
            {
                LoadDefaultAppearanceAndRefresh();
                yield break;
            }

            bool warmupDone = false;
            appearanceBuilder.Warmup(_ => warmupDone = true);
            while (!warmupDone)
                yield return null;

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
            if (otherColorPresetDropdown != null) otherColorPresetDropdown.onValueChanged.RemoveListener(OnOtherColorPresetSelectionChanged);
            UnbindHeadPartDropdownListeners();
            UnbindFaceBlendSliderListeners();
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
                PopulateOtherColorDropdown();
                PopulateHeadPartDropdowns();
                SyncFaceBlendSlidersFromAppearance();
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
                PopulateOtherColorDropdown();
                PopulateHeadPartDropdowns();
                SyncFaceBlendSlidersFromAppearance();
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
                SkinColorPresetId = null,
                OtherColorPresetId = null
            };
        }

        private void SyncUIFromAppearance()
        {
            if (_currentAppearance == null) return;
            if (weightSlider != null) weightSlider.SetValueWithoutNotify(_currentAppearance.BlendShapes?.BodySizeValue ?? 0f);
            if (muscleSlider != null) muscleSlider.SetValueWithoutNotify(_currentAppearance.BlendShapes?.MuscleValue ?? 50f);
            if (hairDropdown != null) hairDropdown.SetValueWithoutNotify(0);
            if (beardDropdown != null) beardDropdown.SetValueWithoutNotify(0);
            var hairColorId = _currentAppearance.HairColorPresetId ?? _currentAppearance.ColorPresetId ?? _currentAppearance.SkinColorPresetId;
            var hairIdx = hairColorId.HasValue ? _hairColorPresetIds.IndexOf(hairColorId.Value) : 0;
            if (hairIdx < 0) hairIdx = 0;
            if (hairColorPresetDropdown != null) hairColorPresetDropdown.SetValueWithoutNotify(hairIdx);
            var skinColorId = _currentAppearance.SkinColorPresetId ?? _currentAppearance.ColorPresetId ?? _currentAppearance.HairColorPresetId;
            var skinIdx = skinColorId.HasValue ? _skinColorPresetIds.IndexOf(skinColorId.Value) : 0;
            if (skinIdx < 0) skinIdx = 0;
            if (skinColorPresetDropdown != null) skinColorPresetDropdown.SetValueWithoutNotify(skinIdx);
            var otherIdx = _currentAppearance.OtherColorPresetId.HasValue ? _otherColorPresetIds.IndexOf(_currentAppearance.OtherColorPresetId.Value) : 0;
            if (otherIdx < 0) otherIdx = 0;
            if (otherColorPresetDropdown != null) otherColorPresetDropdown.SetValueWithoutNotify(otherIdx);
        }

        private void SetUnifiedSpeciesColor(int? presetId)
        {
            if (_currentAppearance == null) return;
            _currentAppearance.ColorPresetId = presetId;
            _currentAppearance.HairColorPresetId = presetId;
            _currentAppearance.SkinColorPresetId = presetId;
        }

        /// <summary>Пересобрать превью по _currentAppearance и поставить под previewPivot.</summary>
        private void RefreshPreview()
        {
            if (appearanceBuilder == null || previewPivot == null || _currentAppearance == null) return;
            int buildVersion = ++_previewBuildVersion;
            if (_previewInstance != null)
            {
                Destroy(_previewInstance);
                _previewInstance = null;
            }
            appearanceBuilder.BuildAppearance(_currentAppearance, go =>
            {
                // Ignore stale async builds that completed after a newer request.
                if (buildVersion != _previewBuildVersion)
                {
                    if (go != null) Destroy(go);
                    return;
                }
                if (go == null) return;
                go.transform.SetParent(previewPivot, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                go.transform.localScale = new Vector3(20f, 20f, 20f);
                EnsurePreviewAnimator(go, ResolvePreviewAnimatorController());
                _previewInstance = go;
                ApplyBlendShapesToPreview();
                ApplyFaceBlendShapesToPreview();
            });
        }

        private static void EnsurePreviewAnimator(GameObject go, RuntimeAnimatorController controller)
        {
            if (go == null) return;
            var animator = go.GetComponentInChildren<Animator>(true);
            if (animator == null) return;
            if (controller != null) animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.enabled = true;
            animator.Rebind();
            animator.Update(0f);
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

        private void BindHeadPartDropdownListeners()
        {
            for (int i = 0; i < headPartDropdowns.Count; i++)
            {
                var binding = headPartDropdowns[i];
                if (binding?.dropdown == null) continue;
                int idx = i;
                binding.dropdown.onValueChanged.AddListener(v => OnHeadPartSelectionChanged(idx, v));
            }
        }

        private void UnbindHeadPartDropdownListeners()
        {
            for (int i = 0; i < headPartDropdowns.Count; i++)
            {
                var binding = headPartDropdowns[i];
                if (binding?.dropdown == null) continue;
                binding.dropdown.onValueChanged.RemoveAllListeners();
            }
        }

        private void PopulateHeadPartDropdowns()
        {
            if (appearanceBuilder == null || headPartDropdowns == null) return;
            var speciesName = ClassSidekickSpeciesMap.GetSpeciesNameForClass(_selectedClassId);
            for (int i = 0; i < headPartDropdowns.Count; i++)
            {
                var binding = headPartDropdowns[i];
                if (binding?.dropdown == null) continue;
                if (string.IsNullOrWhiteSpace(speciesName))
                {
                    _partNamesByType[binding.partType] = new List<string>();
                    binding.dropdown.ClearOptions();
                    continue;
                }
                int partType = binding.partType;
                appearanceBuilder.GetPartNamesForSpecies(speciesName, partType, names =>
                {
                    _partNamesByType[partType] = names ?? new List<string>();
                    var options = new List<string> { string.IsNullOrWhiteSpace(binding.noneOptionLabel) ? "Без части" : binding.noneOptionLabel };
                    options.AddRange(_partNamesByType[partType]);
                    binding.dropdown.ClearOptions();
                    binding.dropdown.AddOptions(options);
                    var selectedIndex = 0;
                    var selectedName = GetCurrentPartName(partType);
                    if (!string.IsNullOrWhiteSpace(selectedName))
                    {
                        var idx = _partNamesByType[partType].IndexOf(selectedName);
                        if (idx >= 0) selectedIndex = idx + 1;
                    }
                    binding.dropdown.SetValueWithoutNotify(selectedIndex);
                });
            }
        }

        private string GetCurrentPartName(int partType)
        {
            if (_currentAppearance?.Parts == null) return "";
            for (int i = 0; i < _currentAppearance.Parts.Count; i++)
                if (_currentAppearance.Parts[i].PartType == partType)
                    return _currentAppearance.Parts[i].PartName ?? "";
            return "";
        }

        private void OnHeadPartSelectionChanged(int bindingIndex, int index)
        {
            if (bindingIndex < 0 || bindingIndex >= headPartDropdowns.Count) return;
            var binding = headPartDropdowns[bindingIndex];
            if (binding == null) return;
            if (!_partNamesByType.TryGetValue(binding.partType, out var names))
                names = new List<string>();
            string partName = (index > 0 && index <= names.Count) ? names[index - 1] : "";
            SetPart(binding.partType, partName);
            if (_previewInstance != null && appearanceBuilder != null && appearanceBuilder.ReplacePartOnCharacter(_previewInstance, binding.partType, partName))
                return;
            RequestRefreshPreviewDebounced();
        }

        private void SetPart(int partType, string partName)
        {
            if (_currentAppearance?.Parts == null) return;
            _currentAppearance.Parts.RemoveAll(p => p.PartType == partType);
            if (!string.IsNullOrEmpty(partName))
                _currentAppearance.Parts.Add(new CharacterPartEntry { PartType = partType, PartName = partName });
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
            SetPart(PartTypeFacialHair, partName);
        }

        private void PopulateHairColorDropdown()
        {
            if (hairColorPresetDropdown == null || appearanceBuilder == null) return;
            var speciesName = ClassSidekickSpeciesMap.GetSpeciesNameForClass(_selectedClassId);
            if (string.IsNullOrEmpty(speciesName)) { _hairColorPresetIds.Clear(); hairColorPresetDropdown.ClearOptions(); return; }
            appearanceBuilder.GetHairColorPresetsForSpecies(speciesName, presets =>
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
                var targetId = _currentAppearance?.HairColorPresetId ?? _currentAppearance?.ColorPresetId ?? _currentAppearance?.SkinColorPresetId;
                var index = targetId.HasValue ? _hairColorPresetIds.IndexOf(targetId.Value) : 0;
                if (index < 0) index = 0;
                hairColorPresetDropdown.SetValueWithoutNotify(index);
                if (_currentAppearance != null && _hairColorPresetIds.Count > 0 && !_currentAppearance.ColorPresetId.HasValue)
                    SetUnifiedSpeciesColor(_hairColorPresetIds[Mathf.Clamp(index, 0, _hairColorPresetIds.Count - 1)]);

                var logLines = new List<string>();
                for (int i = 0; i < _hairColorPresetIds.Count; i++)
                {
                    var optionName = i < options.Count ? options[i] : "<no-name>";
                    logLines.Add($"[{i}] id={_hairColorPresetIds[i]}, name='{optionName}'");
                }
                var selectedId = (index >= 0 && index < _hairColorPresetIds.Count) ? _hairColorPresetIds[index] : (int?)null;
                Debug.Log(
                    $"[CharacterCreate] Hair color options for species '{speciesName}' (count={_hairColorPresetIds.Count})\n" +
                    $"{string.Join("\n", logLines)}\n" +
                    $"Selected index={index}, selectedId={(selectedId.HasValue ? selectedId.Value.ToString() : "null")}");
            });
        }

        private void PopulateSkinColorDropdown()
        {
            if (skinColorPresetDropdown == null || appearanceBuilder == null) return;
            var speciesName = ClassSidekickSpeciesMap.GetSpeciesNameForClass(_selectedClassId);
            if (string.IsNullOrEmpty(speciesName)) { _skinColorPresetIds.Clear(); skinColorPresetDropdown.ClearOptions(); return; }
            appearanceBuilder.GetSkinColorPresetsForSpecies(speciesName, presets =>
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
                var targetId = _currentAppearance?.SkinColorPresetId ?? _currentAppearance?.ColorPresetId ?? _currentAppearance?.HairColorPresetId;
                var index = targetId.HasValue ? _skinColorPresetIds.IndexOf(targetId.Value) : 0;
                if (index < 0) index = 0;
                skinColorPresetDropdown.SetValueWithoutNotify(index);
                if (_currentAppearance != null && _skinColorPresetIds.Count > 0 && !_currentAppearance.ColorPresetId.HasValue)
                    SetUnifiedSpeciesColor(_skinColorPresetIds[Mathf.Clamp(index, 0, _skinColorPresetIds.Count - 1)]);
            });
        }

        private void OnHairColorPresetSelectionChanged(int index)
        {
            if (_currentAppearance == null) return;
            var selected = (index >= 0 && index < _hairColorPresetIds.Count) ? _hairColorPresetIds[index] : (int?)null;
            _currentAppearance.HairColorPresetId = selected;
            if (!_currentAppearance.ColorPresetId.HasValue && selected.HasValue)
                _currentAppearance.ColorPresetId = selected;
            RefreshPreview();
        }

        private void OnSkinColorPresetSelectionChanged(int index)
        {
            if (_currentAppearance == null) return;
            var selected = (index >= 0 && index < _skinColorPresetIds.Count) ? _skinColorPresetIds[index] : (int?)null;
            _currentAppearance.SkinColorPresetId = selected;
            _currentAppearance.ColorPresetId = selected;
            RefreshPreview();
        }

        private void PopulateOtherColorDropdown()
        {
            if (otherColorPresetDropdown == null || appearanceBuilder == null) return;
            var speciesName = ClassSidekickSpeciesMap.GetSpeciesNameForClass(_selectedClassId);
            if (string.IsNullOrEmpty(speciesName)) { _otherColorPresetIds.Clear(); otherColorPresetDropdown.ClearOptions(); return; }
            appearanceBuilder.GetOtherColorPresetsForSpecies(speciesName, presets =>
            {
                _otherColorPresetIds.Clear();
                var options = new List<string> { "Без доп. цвета" };
                if (presets != null)
                {
                    foreach (var p in presets)
                    {
                        _otherColorPresetIds.Add(p.id);
                        options.Add(p.displayName);
                    }
                }
                otherColorPresetDropdown.ClearOptions();
                otherColorPresetDropdown.AddOptions(options);
                int index = 0;
                if (_currentAppearance?.OtherColorPresetId.HasValue == true)
                {
                    var idIndex = _otherColorPresetIds.IndexOf(_currentAppearance.OtherColorPresetId.Value);
                    if (idIndex >= 0) index = idIndex + 1;
                }
                otherColorPresetDropdown.SetValueWithoutNotify(index);
            });
        }

        private void OnOtherColorPresetSelectionChanged(int index)
        {
            if (_currentAppearance == null) return;
            var selected = (index > 0 && index <= _otherColorPresetIds.Count) ? _otherColorPresetIds[index - 1] : (int?)null;
            _currentAppearance.OtherColorPresetId = selected;
            if (selected.HasValue)
                _currentAppearance.ColorPresetId = selected;
            RefreshPreview();
        }

        private void BindFaceBlendSliderListeners()
        {
            for (int i = 0; i < faceBlendSliders.Count; i++)
            {
                var binding = faceBlendSliders[i];
                if (binding?.slider == null || string.IsNullOrWhiteSpace(binding.blendShapeName)) continue;
                int idx = i;
                binding.slider.onValueChanged.AddListener(v => OnFaceBlendChanged(idx, v));
            }
        }

        private void UnbindFaceBlendSliderListeners()
        {
            for (int i = 0; i < faceBlendSliders.Count; i++)
            {
                var binding = faceBlendSliders[i];
                if (binding?.slider == null) continue;
                binding.slider.onValueChanged.RemoveAllListeners();
            }
        }

        private void SyncFaceBlendSlidersFromAppearance()
        {
            if (_currentAppearance == null) return;
            _currentAppearance.FaceBlendShapes ??= new Dictionary<string, float>();
            for (int i = 0; i < faceBlendSliders.Count; i++)
            {
                var binding = faceBlendSliders[i];
                if (binding?.slider == null || string.IsNullOrWhiteSpace(binding.blendShapeName)) continue;
                float value = _currentAppearance.FaceBlendShapes.TryGetValue(binding.blendShapeName, out var v) ? v : 0f;
                binding.slider.SetValueWithoutNotify(value);
            }
        }

        private void OnFaceBlendChanged(int index, float value)
        {
            if (_currentAppearance == null || index < 0 || index >= faceBlendSliders.Count) return;
            var binding = faceBlendSliders[index];
            if (binding == null || string.IsNullOrWhiteSpace(binding.blendShapeName)) return;
            _currentAppearance.FaceBlendShapes ??= new Dictionary<string, float>();
            _currentAppearance.FaceBlendShapes[binding.blendShapeName] = value;
            ApplyFaceBlendShapesToPreview();
        }

        private void ApplyFaceBlendShapesToPreview()
        {
            if (_previewInstance == null || _currentAppearance?.FaceBlendShapes == null) return;
            foreach (var smr in _previewInstance.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh = smr.sharedMesh;
                if (mesh == null) continue;
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    string name = mesh.GetBlendShapeName(i);
                    if (_currentAppearance.FaceBlendShapes.TryGetValue(name, out var value))
                        smr.SetBlendShapeWeight(i, value);
                }
            }
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
            if (_currentAppearance != null && !_currentAppearance.ColorPresetId.HasValue)
            {
                _currentAppearance.ColorPresetId =
                    _currentAppearance.SkinColorPresetId
                    ?? _currentAppearance.HairColorPresetId
                    ?? _currentAppearance.OtherColorPresetId;
            }
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
