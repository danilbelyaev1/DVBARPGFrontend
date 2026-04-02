using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DVBARPG.Core.Services;
using Synty.SidekickCharacters.API;
using Synty.SidekickCharacters.Database;
using Synty.SidekickCharacters.Database.DTO;
using Synty.SidekickCharacters.Enums;
using Synty.SidekickCharacters.SkinnedMesh;
using Synty.SidekickCharacters.Utils;
using UnityEngine;

namespace DVBARPG.Game.CharacterCreation
{
    /// <summary>
    /// Собирает GameObject персонажа из CharacterAppearanceData через Sidekick API.
    /// Используется в CharacterCreate (превью) и в Run (подстановка игрока).
    /// Поддерживает мгновенное обновление цветов и замену частей (волосы/борода) без полной пересборки.
    /// </summary>
    public sealed class SidekickAppearanceBuilder : MonoBehaviour
    {
        private const string BaseModelPath = "Meshes/SK_BaseModel";
        private const string BaseMaterialPath = "Materials/M_BaseMaterial";

        private static readonly int ShaderColorMap = Shader.PropertyToID("_ColorMap");
        private static readonly int ShaderMetallicMap = Shader.PropertyToID("_MetallicMap");
        private static readonly int ShaderSmoothnessMap = Shader.PropertyToID("_SmoothnessMap");
        private static readonly int ShaderReflectionMap = Shader.PropertyToID("_ReflectionMap");
        private static readonly int ShaderEmissionMap = Shader.PropertyToID("_EmissionMap");
        private static readonly int ShaderOpacityMap = Shader.PropertyToID("_OpacityMap");

        /// <summary>Кэш списков частей по типу после PopulateToolData — чтобы дропдауны показывали только те части, что реально подхватятся при сборке.</summary>
        private Dictionary<CharacterPartType, List<string>> _cachedPartListByType;

        /// <summary>Кэш рантайма после первой сборки — для мгновенной замены частей (волосы/борода) без полного ребилда.</summary>
        private SidekickRuntime _cachedRuntime;
        private DatabaseManager _cachedDbManager;
        private GameObject _cachedBaseModel;
        private Material _cachedBaseMaterial;
        private RuntimeAnimatorController _cachedAnimatorController;
        private bool _runtimeInitInProgress;
        private readonly HashSet<int> _preparedMaterialIds = new HashSet<int>();

        /// <summary>Собрать персонажа по данным внешности. Результат отдаётся в onDone (или null при ошибке).</summary>
        public void BuildAppearance(CharacterAppearanceData data, Action<GameObject> onDone)
        {
            if (data == null) { onDone?.Invoke(null); return; }
            StartCoroutine(BuildRoutine(data, onDone));
        }

        /// <summary>Список имён частей для вида и типа (например Hair). partType = (int)CharacterPartType. Колбек вызывается на главном потоке.</summary>
        public void GetPartNamesForSpecies(string speciesName, int partType, Action<List<string>> onDone)
        {
            if (onDone == null) return;
            StartCoroutine(GetPartNamesRoutine(speciesName, partType, onDone));
        }

        /// <summary>Дефолтная внешность: BASE-части вида, пол, вес/мускулы, первый цветовой пресет. speciesName — из ClassSidekickSpeciesMap.</summary>
        public void GetDefaultAppearanceData(string speciesName, string gender, float bodySize, float muscle, Action<CharacterAppearanceData> onDone)
        {
            if (onDone == null) return;
            StartCoroutine(GetDefaultAppearanceDataRoutine(speciesName, gender, bodySize, muscle, onDone));
        }

        /// <summary>Список цветовых пресетов для вида: (id, отображаемое имя). Для выпадающего списка.</summary>
        public void GetColorPresetsForSpecies(string speciesName, Action<List<(int id, string displayName)>> onDone)
        {
            if (onDone == null) return;
            StartCoroutine(GetColorPresetsRoutine(speciesName, onDone));
        }

        public void GetHairColorPresetsForSpecies(string speciesName, Action<List<(int id, string displayName)>> onDone)
        {
            if (onDone == null) return;
            StartCoroutine(GetHairColorPresetsRoutine(speciesName, onDone));
        }

        public void GetSkinColorPresetsForSpecies(string speciesName, Action<List<(int id, string displayName)>> onDone)
        {
            if (onDone == null) return;
            StartCoroutine(GetColorPresetsRoutine(speciesName, onDone));
        }

        public void GetOtherColorPresetsForSpecies(string speciesName, Action<List<(int id, string displayName)>> onDone)
        {
            if (onDone == null) return;
            StartCoroutine(GetOtherColorPresetsRoutine(speciesName, onDone));
        }

        /// <summary>Прогреть Sidekick runtime заранее, чтобы первая сборка превью была быстрой.</summary>
        public void Warmup(Action<bool> onDone = null)
        {
            StartCoroutine(WarmupRoutine(onDone));
        }

        /// <summary>
        /// Каноничный путь цвета — только через полный BuildRoutine.
        /// Чтобы не расходиться по логике с демо Sidekick, instant-path отключён.
        /// </summary>
        public bool ApplyColorsToExisting(GameObject character, CharacterAppearanceData data)
        {
            return false;
        }

        private void EnsureUniqueEditableTextures(Material mat)
        {
            if (mat == null) return;
            int id = mat.GetInstanceID();
            if (_preparedMaterialIds.Contains(id)) return;

            DuplicateTextureOnMaterial(mat, ShaderColorMap);
            DuplicateTextureOnMaterial(mat, ShaderMetallicMap);
            DuplicateTextureOnMaterial(mat, ShaderSmoothnessMap);
            DuplicateTextureOnMaterial(mat, ShaderReflectionMap);
            DuplicateTextureOnMaterial(mat, ShaderEmissionMap);
            DuplicateTextureOnMaterial(mat, ShaderOpacityMap);
            _preparedMaterialIds.Add(id);
        }

        private static void DuplicateTextureOnMaterial(Material mat, int propertyId)
        {
            var src = mat.GetTexture(propertyId) as Texture2D;
            if (src == null) return;
            var copy = Instantiate(src);
            copy.name = src.name + "_RuntimeCopy";
            mat.SetTexture(propertyId, copy);
        }

        private static void ApplyColorRowToMaterial(Material mat, SidekickColorRow colorRow)
        {
            if (colorRow?.ColorProperty == null) return;
            int u = colorRow.ColorProperty.U;
            int v = colorRow.ColorProperty.V;
            void SetTex(int propId, Color color)
            {
                var tex = mat.GetTexture(propId) as Texture2D;
                if (tex == null) return;
                int su = u * 2, sv = v * 2;
                tex.SetPixel(su, sv, color);
                tex.SetPixel(su + 1, sv, color);
                tex.SetPixel(su, sv + 1, color);
                tex.SetPixel(su + 1, sv + 1, color);
                tex.Apply();
                mat.SetTexture(propId, tex);
            }
            SetTex(ShaderColorMap, colorRow.NiceColor);
        }

        /// <summary>Заменить одну часть (волосы/борода) на уже собранном персонаже. Возвращает true, если замена прошла без полного ребилда.</summary>
        public bool ReplacePartOnCharacter(GameObject character, int partType, string partName)
        {
            if (character == null) return false;
            var type = (CharacterPartType)partType;
            string typeString = CharacterPartTypeUtils.GetPartTypeString(type);
            var allSmrs = character.GetComponentsInChildren<SkinnedMeshRenderer>();
            SkinnedMeshRenderer existingPart = allSmrs.FirstOrDefault(r => r.name.Contains(typeString));
            Material characterMaterial = existingPart != null ? existingPart.sharedMaterial : (allSmrs.Length > 0 ? allSmrs[0].sharedMaterial : null);
            if (characterMaterial == null) return false;

            if (existingPart != null)
            {
                if (Application.isEditor) DestroyImmediate(existingPart.gameObject);
                else Destroy(existingPart.gameObject);
            }

            if (string.IsNullOrEmpty(partName))
                return true;

            if (_cachedRuntime?.MappedPartDictionary == null) return false;
            if (!_cachedRuntime.MappedPartDictionary.TryGetValue(type, out var dict) || !dict.TryGetValue(partName, out var sidekickPart))
                return false;
            var partGo = sidekickPart.GetPartModel();
            if (partGo == null) return false;
            var partSmr = partGo.GetComponentInChildren<SkinnedMeshRenderer>();
            if (partSmr == null) return false;

            Transform root = character.transform.Find("root");
            if (root == null) return false;
            var boneNameMap = Combiner.CreateBoneNameMap(root.gameObject);

            // Добавить отсутствующие кости рекурсивно: сначала родитель, потом ребёнок.
            // Это исключает кейс "первая смена прически ломает макушку", когда parent еще не создан.
            var partBones = partSmr.bones;
            for (int i = 0; i < partBones.Length; i++)
                EnsureBonePathExists(partBones[i], boneNameMap);

            GameObject newPartGo = new GameObject(partSmr.name);
            newPartGo.transform.SetParent(character.transform, false);
            var newSmr = newPartGo.AddComponent<SkinnedMeshRenderer>();
            newSmr.updateWhenOffscreen = true;
            newSmr.sharedMesh = MeshUtils.CopyMesh(partSmr.sharedMesh);
            newSmr.sharedMaterial = characterMaterial;
            var oldBones = partSmr.bones;
            var newBones = new Transform[oldBones.Length];
            for (int i = 0; i < oldBones.Length; i++)
            {
                if (boneNameMap[oldBones[i].name] is Transform t)
                    newBones[i] = t;
            }
            newSmr.bones = newBones;
            newSmr.rootBone = boneNameMap[partSmr.rootBone.name] as Transform;
            Combiner.MergeAndGetAllBlendShapeDataOfSkinnedMeshRenderers(new[] { partSmr }, newSmr.sharedMesh, newSmr);
            return true;
        }

        private static Transform EnsureBonePathExists(Transform sourceBone, System.Collections.Hashtable boneNameMap)
        {
            if (sourceBone == null || boneNameMap == null) return null;
            if (boneNameMap[sourceBone.name] is Transform existing) return existing;

            Transform parentTarget = null;
            if (sourceBone.parent != null)
                parentTarget = EnsureBonePathExists(sourceBone.parent, boneNameMap);
            if (parentTarget == null) return null;

            var newBone = Instantiate(sourceBone.gameObject, parentTarget);
            newBone.name = sourceBone.name;
            var t = newBone.transform;
            if (!boneNameMap.ContainsKey(sourceBone.name))
                boneNameMap.Add(sourceBone.name, t);
            else
                boneNameMap[sourceBone.name] = t;
            return t;
        }

        private IEnumerator EnsureRuntimeReady()
        {
            if (_cachedRuntime?.MappedPartDictionary != null && _cachedRuntime.MappedPartList != null)
                yield break;

            while (_runtimeInitInProgress)
                yield return null;

            if (_cachedRuntime?.MappedPartDictionary != null && _cachedRuntime.MappedPartList != null)
                yield break;

            _runtimeInitInProgress = true;
            try
            {
                if (_cachedBaseModel == null) _cachedBaseModel = Resources.Load<GameObject>(BaseModelPath);
                if (_cachedBaseMaterial == null) _cachedBaseMaterial = Resources.Load<Material>(BaseMaterialPath);
                if (_cachedBaseModel == null || _cachedBaseMaterial == null) yield break;

                if (_cachedDbManager == null) _cachedDbManager = new DatabaseManager();
                if (_cachedDbManager.GetCurrentDbConnection() == null)
                    _cachedDbManager.GetDbConnection(true);

                if (_cachedAnimatorController == null)
                {
                    var baseAnimator = _cachedBaseModel.GetComponentInChildren<Animator>();
                    _cachedAnimatorController = baseAnimator != null ? baseAnimator.runtimeAnimatorController : null;
                }

                var runtimeMaterial = Instantiate(_cachedBaseMaterial);
                _cachedRuntime = new SidekickRuntime(_cachedBaseModel, runtimeMaterial, _cachedAnimatorController, _cachedDbManager);
                var populateTask = SidekickRuntime.PopulateToolData(_cachedRuntime);
                while (!populateTask.IsCompleted)
                    yield return null;
                CachePartListFromRuntime(_cachedRuntime);
            }
            finally
            {
                _runtimeInitInProgress = false;
            }
        }

        private IEnumerator WarmupRoutine(Action<bool> onDone)
        {
            yield return EnsureRuntimeReady();
            onDone?.Invoke(_cachedRuntime != null && _cachedDbManager != null);
        }

        private void CachePartListFromRuntime(SidekickRuntime runtime)
        {
            if (runtime?.MappedPartList == null) return;
            _cachedPartListByType = new Dictionary<CharacterPartType, List<string>>();
            foreach (var kv in runtime.MappedPartList)
            {
                if (kv.Value != null && kv.Value.Count > 0)
                    _cachedPartListByType[kv.Key] = new List<string>(kv.Value);
            }
        }

        /// <summary>Загрузить рантайм, выполнить PopulateToolData, заполнить кэш и list для типа (Hair/FacialHair). Чтобы дропдаун показывал только те имена, что реально подхватятся при сборке.</summary>
        private IEnumerator PopulateAndFillPartListForTypeRoutine(CharacterPartType type, List<string> list)
        {
            list.Clear();
            yield return EnsureRuntimeReady();
            var runtime = _cachedRuntime;
            if (runtime == null) yield break;
            if (runtime.MappedPartList != null && runtime.MappedPartList.TryGetValue(type, out var partNames) && partNames != null)
                list.AddRange(partNames);
        }

        private IEnumerator GetPartNamesRoutine(string speciesName, int partType, Action<List<string>> onDone)
        {
            var list = new List<string>();
            var type = (CharacterPartType)partType;

            // Волосы и борода: из кэша рантайма или один раз загружаем рантайм и берём список из него (чтобы имена совпадали с теми, что подхватятся при сборке).
            if (type == CharacterPartType.Hair || type == CharacterPartType.FacialHair)
            {
                if (_cachedPartListByType != null && _cachedPartListByType.TryGetValue(type, out var cached))
                {
                    list.AddRange(cached);
                }
                else
                {
                    yield return PopulateAndFillPartListForTypeRoutine(type, list);
                }
                list.Sort(StringComparer.OrdinalIgnoreCase);
                onDone(list);
                yield break;
            }

            var dbManager = _cachedDbManager ?? new DatabaseManager();
            if (dbManager.GetCurrentDbConnection() == null)
                dbManager.GetDbConnection(true);
            yield return null;

            if (string.IsNullOrWhiteSpace(speciesName)) { onDone(list); yield break; }
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void AddPartsForSpecies(SidekickSpecies sp)
            {
                if (sp == null) return;
                var parts = SidekickPart.GetAllForSpecies(dbManager, sp, true);
                if (parts == null) return;
                foreach (var part in parts)
                {
                    if (part.Type == type && !string.IsNullOrEmpty(part.Name) && seen.Add(part.Name))
                        list.Add(part.Name);
                }
            }
            var species = SidekickSpecies.GetByName(dbManager, speciesName);
            AddPartsForSpecies(species);
            var unrestricted = SidekickSpecies.GetByName(dbManager, "Unrestricted");
            if (unrestricted != null && unrestricted.ID != species?.ID)
                AddPartsForSpecies(unrestricted);

            list.Sort(StringComparer.OrdinalIgnoreCase);
            onDone(list);
        }

        private IEnumerator GetColorPresetsRoutine(string speciesName, Action<List<(int id, string displayName)>> onDone)
        {
            var list = new List<(int id, string displayName)>();
            if (string.IsNullOrWhiteSpace(speciesName)) { onDone(list); yield break; }
            var dbManager = _cachedDbManager ?? new DatabaseManager();
            if (dbManager.GetCurrentDbConnection() == null)
                dbManager.GetDbConnection(true);
            yield return null;

            var species = SidekickSpecies.GetByName(dbManager, speciesName);
            if (species == null) { onDone(list); yield break; }

            var seen = new HashSet<int>();
            void Add(List<SidekickColorPreset> presets)
            {
                if (presets == null) return;
                for (int i = 0; i < presets.Count; i++)
                {
                    var p = presets[i];
                    if (p == null || !seen.Add(p.ID)) continue;
                    list.Add((p.ID, string.IsNullOrEmpty(p.Name) ? "Preset " + p.ID : p.Name));
                }
            }

            // Для кожи/волос в этом проекте берём species-пресеты.
            Add(SidekickColorPreset.GetAllByColorGroupAndSpecies(dbManager, ColorGroup.Species, species));
            Add(SidekickColorPreset.GetAllBySpecies(dbManager, species));
            if (list.Count == 0)
                Add(SidekickColorPreset.GetAllByColorGroup(dbManager, ColorGroup.Species));
            onDone(list);
        }

        private IEnumerator GetHairColorPresetsRoutine(string speciesName, Action<List<(int id, string displayName)>> onDone)
        {
            var all = new List<(int id, string displayName)>();
            yield return GetColorPresetsRoutine(speciesName, list => all = list ?? new List<(int id, string displayName)>());

            var filtered = new List<(int id, string displayName)>();
            var dbManager = _cachedDbManager ?? new DatabaseManager();
            if (dbManager.GetCurrentDbConnection() == null)
                dbManager.GetDbConnection(true);
            yield return EnsureRuntimeReady();
            var runtime = _cachedRuntime;
            var species = SidekickSpecies.GetByName(dbManager, speciesName);

            var hairUvs = new HashSet<int>();
            if (runtime != null && species != null &&
                runtime.MappedBasePartDictionary != null &&
                runtime.MappedBasePartDictionary.TryGetValue(species, out var baseParts) &&
                baseParts != null && baseParts.Count > 0)
            {
                var partLibrary = runtime.MappedPartDictionary;
                if (partLibrary != null)
                {
                    var usedParts = new List<SkinnedMeshRenderer>();
                    foreach (var kv in baseParts)
                    {
                        var type = kv.Key;
                        if (type != CharacterPartType.Hair &&
                            type != CharacterPartType.FacialHair &&
                            type != CharacterPartType.EyebrowLeft &&
                            type != CharacterPartType.EyebrowRight)
                            continue;
                        if (kv.Value == null || kv.Value.Count == 0) continue;
                        if (!TryResolvePartByName(partLibrary, type, kv.Value[0], out var part))
                            continue;
                        var go = part.GetPartModel();
                        var smr = go != null ? go.GetComponentInChildren<SkinnedMeshRenderer>() : null;
                        if (smr != null) usedParts.Add(smr);
                    }

                    if (usedParts.Count > 0)
                    {
                        runtime.PopulateUVDictionary(usedParts);
                        hairUvs = BuildUvSet(
                            runtime.CurrentUVDictionary,
                            ColorPartType.Hair,
                            ColorPartType.FacialHair,
                            ColorPartType.EyebrowLeft,
                            ColorPartType.EyebrowRight);
                    }
                }
            }

            for (int i = 0; i < all.Count; i++)
            {
                var entry = all[i];
                var preset = SidekickColorPreset.GetByID(dbManager, entry.id);
                if (preset == null) continue;
                var rows = SidekickColorPresetRow.GetAllByPreset(dbManager, preset);
                if (rows == null || rows.Count == 0) continue;

                bool hasHairRows = false;
                for (int r = 0; r < rows.Count; r++)
                {
                    var prop = rows[r]?.ColorProperty;
                    if (prop == null) continue;
                    var propName = prop.Name;
                    if (!string.IsNullOrWhiteSpace(propName))
                    {
                        bool isHairNamed =
                            propName.IndexOf("hair", StringComparison.OrdinalIgnoreCase) >= 0
                            || propName.IndexOf("beard", StringComparison.OrdinalIgnoreCase) >= 0
                            || propName.IndexOf("brow", StringComparison.OrdinalIgnoreCase) >= 0
                            || propName.IndexOf("facial", StringComparison.OrdinalIgnoreCase) >= 0;
                        if (isHairNamed)
                        {
                            // Если есть UV-контекст текущей внешности, считаем валидными только совпадающие UV.
                            if (hairUvs.Count > 0 && !hairUvs.Contains(PackUv(prop.U, prop.V)))
                                continue;
                            hasHairRows = true;
                            break;
                        }
                        continue;
                    }

                    // Fallback only for unnamed properties: try UV matching against current hair-related UVs.
                    if (hairUvs.Count > 0 && hairUvs.Contains(PackUv(prop.U, prop.V)))
                    {
                        hasHairRows = true;
                        break;
                    }
                }
                if (!hasHairRows) continue;
                filtered.Add(entry);
            }

            onDone(filtered);
        }

        private IEnumerator GetZoneColorPresetsRoutine(
            string speciesName,
            Func<SidekickColorPresetRow, bool> zoneFilter,
            string zoneLabel,
            Action<List<(int id, string displayName)>> onDone)
        {
            yield return GetColorPresetsRoutine(speciesName, onDone);
        }

        private IEnumerator GetOtherColorPresetsRoutine(string speciesName, Action<List<(int id, string displayName)>> onDone)
        {
            var list = new List<(int id, string displayName)>();
            if (string.IsNullOrWhiteSpace(speciesName)) { onDone(list); yield break; }
            var dbManager = _cachedDbManager ?? new DatabaseManager();
            if (dbManager.GetCurrentDbConnection() == null)
                dbManager.GetDbConnection(true);
            yield return null;
            var species = SidekickSpecies.GetByName(dbManager, speciesName);
            if (species == null) { onDone(list); yield break; }
            var groups = new[] { ColorGroup.Materials, ColorGroup.Attachments, ColorGroup.Outfits, ColorGroup.Elements };
            var seen = new HashSet<int>();
            foreach (var group in groups)
            {
                var presets = SidekickColorPreset.GetAllByColorGroupAndSpecies(dbManager, group, species);
                if (presets == null || presets.Count == 0)
                    presets = SidekickColorPreset.GetAllByColorGroup(dbManager, group);
                if (presets == null) continue;
                foreach (var p in presets)
                {
                    if (!seen.Add(p.ID)) continue;
                    list.Add((p.ID, $"{group}: {(string.IsNullOrEmpty(p.Name) ? "Preset " + p.ID : p.Name)}"));
                }
            }
            onDone(list);
        }

        private IEnumerator GetDefaultAppearanceDataRoutine(string speciesName, string gender, float bodySize, float muscle, Action<CharacterAppearanceData> onDone)
        {
            yield return EnsureRuntimeReady();
            var runtime = _cachedRuntime;
            var dbManager = _cachedDbManager;
            if (runtime == null || dbManager == null)
            {
                onDone?.Invoke(BuildFallbackDefaultAppearance(gender, bodySize, muscle));
                yield break;
            }

            var species = SidekickSpecies.GetByName(dbManager, speciesName);
            if (species == null)
            {
                onDone?.Invoke(BuildFallbackDefaultAppearance(gender, bodySize, muscle));
                yield break;
            }

            var baseParts = new Dictionary<CharacterPartType, List<string>>();
            foreach (var kv in runtime.MappedBasePartDictionary)
            {
                if (string.Equals(kv.Key.Name, speciesName, StringComparison.OrdinalIgnoreCase))
                {
                    baseParts = kv.Value;
                    break;
                }
            }

            // Если для вида нет BASE-частей (имя не совпало или пусто), берём первый вид у которого есть части — чтобы превью хоть что-то показало
            if (baseParts == null || baseParts.Count == 0)
            {
                foreach (var kv in runtime.MappedBasePartDictionary)
                {
                    if (kv.Value != null && kv.Value.Count > 0)
                    {
                        baseParts = kv.Value;
                        species = kv.Key;
                        break;
                    }
                }
            }

            bool isFemale = string.Equals(gender, "female", StringComparison.OrdinalIgnoreCase);
            var parts = new List<CharacterPartEntry>();
            if (baseParts != null)
            {
                foreach (var kv in baseParts)
                {
                    if (kv.Key == CharacterPartType.Wrap && !isFemale) continue;
                    if (kv.Value != null && kv.Value.Count > 0)
                        parts.Add(new CharacterPartEntry { PartType = (int)kv.Key, PartName = kv.Value[0] });
                }
            }

            if (parts.Count == 0 && runtime.MappedPartList != null)
            {
                foreach (var kv in runtime.MappedPartList)
                {
                    if (kv.Key == CharacterPartType.Wrap && !isFemale) continue;
                    if (kv.Value != null && kv.Value.Count > 0)
                        parts.Add(new CharacterPartEntry { PartType = (int)kv.Key, PartName = kv.Value[0] });
                }
            }

            int? firstPresetId = null;
            var presets = SidekickColorPreset.GetAllByColorGroupAndSpecies(dbManager, ColorGroup.Species, species);
            if ((presets == null || presets.Count == 0) && species != null)
                presets = SidekickColorPreset.GetAllBySpecies(dbManager, species);
            if (presets == null || presets.Count == 0)
                presets = SidekickColorPreset.GetAllByColorGroup(dbManager, ColorGroup.Species);
            if (presets != null && presets.Count > 0) firstPresetId = presets[0].ID;

            var data = new CharacterAppearanceData
            {
                SpeciesId = species != null ? species.ID : 0,
                Parts = parts,
                BlendShapes = new BlendShapeValues
                {
                    BodyTypeValue = string.Equals(gender, "female", StringComparison.OrdinalIgnoreCase) ? 100f : 0f,
                    BodySizeValue = bodySize,
                    MuscleValue = muscle
                },
                FaceBlendShapes = new Dictionary<string, float>(),
                ColorPresetId = firstPresetId,
                HairColorPresetId = firstPresetId,
                SkinColorPresetId = firstPresetId,
                OtherColorPresetId = null
            };
            onDone?.Invoke(data);
        }

        private static CharacterAppearanceData BuildFallbackDefaultAppearance(string gender, float bodySize, float muscle)
        {
            return new CharacterAppearanceData
            {
                SpeciesId = 0,
                Parts = new List<CharacterPartEntry>(),
                BlendShapes = new BlendShapeValues
                {
                    BodyTypeValue = string.Equals(gender, "female", StringComparison.OrdinalIgnoreCase) ? 100f : 0f,
                    BodySizeValue = bodySize,
                    MuscleValue = muscle
                },
                FaceBlendShapes = new Dictionary<string, float>(),
                ColorPresetId = null,
                HairColorPresetId = null,
                SkinColorPresetId = null,
                OtherColorPresetId = null
            };
        }

        private static bool TryResolvePartByName(
            Dictionary<CharacterPartType, Dictionary<string, SidekickPart>> partLibrary,
            CharacterPartType type,
            string requestedName,
            out SidekickPart sidekickPart)
        {
            sidekickPart = null;
            if (partLibrary == null || string.IsNullOrWhiteSpace(requestedName)) return false;
            if (!partLibrary.TryGetValue(type, out var dict) || dict == null || dict.Count == 0) return false;

            if (dict.TryGetValue(requestedName, out sidekickPart) && sidekickPart != null)
                return true;

            foreach (var kv in dict)
            {
                if (string.Equals(kv.Key, requestedName, StringComparison.OrdinalIgnoreCase))
                {
                    sidekickPart = kv.Value;
                    return sidekickPart != null;
                }
            }

            foreach (var kv in dict)
            {
                if (kv.Key != null &&
                    (kv.Key.IndexOf(requestedName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                     requestedName.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    sidekickPart = kv.Value;
                    return sidekickPart != null;
                }
            }
            return false;
        }

        private static int PackUv(int u, int v) => (u << 16) ^ (v & 0xFFFF);

        private static HashSet<int> BuildUvSet(Dictionary<ColorPartType, List<Vector2>> uvDict, params ColorPartType[] types)
        {
            var set = new HashSet<int>();
            if (uvDict == null || types == null) return set;
            for (int i = 0; i < types.Length; i++)
            {
                if (!uvDict.TryGetValue(types[i], out var list) || list == null) continue;
                for (int j = 0; j < list.Count; j++)
                    set.Add(PackUv((int)list[j].x, (int)list[j].y));
            }
            return set;
        }

        private static bool IsHairLikePartName(string partName)
        {
            if (string.IsNullOrWhiteSpace(partName)) return false;
            return partName.IndexOf("hair", StringComparison.OrdinalIgnoreCase) >= 0
                || partName.IndexOf("beard", StringComparison.OrdinalIgnoreCase) >= 0
                || partName.IndexOf("brow", StringComparison.OrdinalIgnoreCase) >= 0
                || partName.IndexOf("facial", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddScaledUvsFromMesh(HashSet<int> target, Mesh mesh)
        {
            if (target == null || mesh == null) return;
            var uvs = mesh.uv;
            if (uvs == null || uvs.Length == 0) return;
            for (int i = 0; i < uvs.Length; i++)
            {
                int u = (int)Math.Floor(uvs[i].x * 16f);
                int v = (int)Math.Floor(uvs[i].y * 16f);
                if (u == 16) u = 15;
                if (v == 16) v = 15;
                target.Add(PackUv(u, v));
            }
        }

        private static SidekickColorRow TryResolveHairFallbackColorRow(DatabaseManager dbManager, int? presetId)
        {
            if (!presetId.HasValue || dbManager == null) return null;
            var preset = SidekickColorPreset.GetByID(dbManager, presetId.Value);
            if (preset == null) return null;
            var rows = SidekickColorPresetRow.GetAllByPreset(dbManager, preset);
            if (rows == null || rows.Count == 0) return null;

            SidekickColorPresetRow firstRow = null;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row?.ColorProperty == null) continue;
                firstRow ??= row;
                var n = row.ColorProperty.Name;
                if (!string.IsNullOrWhiteSpace(n)
                    && (n.IndexOf("hair", StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("beard", StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("brow", StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("facial", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return SidekickColorRow.CreateFromPresetColorRow(row);
                }
            }

            return firstRow != null ? SidekickColorRow.CreateFromPresetColorRow(firstRow) : null;
        }

        private static bool TryGetPresetMainColor(DatabaseManager dbManager, int? presetId, out Color color)
        {
            color = Color.black;
            if (!presetId.HasValue || dbManager == null) return false;
            var preset = SidekickColorPreset.GetByID(dbManager, presetId.Value);
            if (preset == null) return false;
            var rows = SidekickColorPresetRow.GetAllByPreset(dbManager, preset);
            if (rows == null || rows.Count == 0) return false;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null || string.IsNullOrWhiteSpace(row.MainColor)) continue;
                if (ColorUtility.TryParseHtmlString("#" + row.MainColor, out var parsed))
                {
                    color = parsed;
                    return true;
                }
            }
            return false;
        }

        private static void ApplyHairMaterialOverride(GameObject character, Material baseMaterial, Color hairColor)
        {
            if (character == null || baseMaterial == null) return;
            var smrs = character.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < smrs.Length; i++)
            {
                var smr = smrs[i];
                if (smr == null || !IsHairLikePartName(smr.name)) continue;
                var hairMat = new Material(baseMaterial);
                hairMat.name = baseMaterial.name + "_HairOverride";
                bool specApplied = false;
                if (hairMat.HasProperty("_SpecularColor"))
                {
                    hairMat.SetColor("_SpecularColor", hairColor);
                    specApplied = true;
                }
                if (hairMat.HasProperty("_SpecColor"))
                {
                    hairMat.SetColor("_SpecColor", hairColor);
                    specApplied = true;
                }
                // Для совместимости с разными вариантами Sidekick-материалов.
                if (hairMat.HasProperty("_Specular"))
                {
                    hairMat.SetColor("_Specular", hairColor);
                    specApplied = true;
                }
                if (!specApplied)
                {
                    // Fallback, если в шейдере нет specular property.
                    if (hairMat.HasProperty("_BaseColor"))
                        hairMat.SetColor("_BaseColor", hairColor);
                    if (hairMat.HasProperty("_Color"))
                        hairMat.SetColor("_Color", hairColor);
                }
                // Safer cel-shading defaults for bright hair readability.
                if (hairMat.HasProperty("_SpecularAlbedoMix"))
                    hairMat.SetFloat("_SpecularAlbedoMix", 0.12f);
                if (hairMat.HasProperty("_SpecularIntensity"))
                    hairMat.SetFloat("_SpecularIntensity", 0.35f);
                if (hairMat.HasProperty("_Glossiness"))
                    hairMat.SetFloat("_Glossiness", 14f);
                if (hairMat.HasProperty("_MaxOutputRgb"))
                    hairMat.SetFloat("_MaxOutputRgb", 1.32f);
                if (hairMat.HasProperty("_SpecularBrightAlbedoReduce"))
                    hairMat.SetFloat("_SpecularBrightAlbedoReduce", 0.55f);
                var mats = smr.sharedMaterials;
                if (mats == null || mats.Length == 0)
                {
                    smr.sharedMaterial = hairMat;
                    continue;
                }
                for (int m = 0; m < mats.Length; m++)
                    mats[m] = hairMat;
                smr.sharedMaterials = mats;
            }
        }

        private IEnumerator BuildRoutine(CharacterAppearanceData data, Action<GameObject> onDone)
        {
            yield return EnsureRuntimeReady();
            var runtime = _cachedRuntime;
            var dbManager = _cachedDbManager;
            if (runtime == null || dbManager == null)
            {
                onDone?.Invoke(null);
                yield break;
            }
            // Каждый билд начинаем с чистого материала, чтобы цвета не "протекали" с прошлого превью/персонажа.
            if (_cachedBaseMaterial != null)
            {
                runtime.CurrentMaterial = Instantiate(_cachedBaseMaterial);
                EnsureUniqueEditableTextures(runtime.CurrentMaterial);
            }

            var partsToBuild = data.Parts;
            if (partsToBuild == null || partsToBuild.Count == 0)
            {
                partsToBuild = new List<CharacterPartEntry>();
                bool isFemaleFallback = (data.BlendShapes?.BodyTypeValue ?? 0f) > 50f;
                if (runtime.MappedPartList != null)
                {
                    foreach (var kv in runtime.MappedPartList)
                    {
                        if (kv.Key == CharacterPartType.Wrap && !isFemaleFallback) continue;
                        if (kv.Value != null && kv.Value.Count > 0)
                            partsToBuild.Add(new CharacterPartEntry { PartType = (int)kv.Key, PartName = kv.Value[0] });
                    }
                }
                if (partsToBuild.Count == 0) { onDone?.Invoke(null); yield break; }
            }

            var partsToUse = new List<SkinnedMeshRenderer>();
            var partLibrary = runtime.MappedPartDictionary;
            if (partLibrary == null) { onDone?.Invoke(null); yield break; }

            bool isFemaleBuild = (data.BlendShapes?.BodyTypeValue ?? 0f) > 50f;
            foreach (var entry in partsToBuild)
            {
                if (string.IsNullOrEmpty(entry?.PartName)) continue;
                var type = (CharacterPartType)entry.PartType;
                if (type == CharacterPartType.Wrap && !isFemaleBuild) continue;
                if (!TryResolvePartByName(partLibrary, type, entry.PartName, out var sidekickPart))
                    continue;
                var partGo = sidekickPart.GetPartModel();
                if (partGo != null)
                {
                    var smr = partGo.GetComponentInChildren<SkinnedMeshRenderer>();
                    if (smr != null) partsToUse.Add(smr);
                }
            }

            // Если часть из сохранённого JSON не нашлась, добираем отсутствующие типы из BASE для вида,
            // чтобы не получить персонажа "только с прической".
            if (partsToUse.Count > 0 && data.SpeciesId > 0)
            {
                var species = SidekickSpecies.GetByID(dbManager, data.SpeciesId);
                if (species != null && runtime.MappedBasePartDictionary != null &&
                    runtime.MappedBasePartDictionary.TryGetValue(species, out var baseParts) &&
                    baseParts != null)
                {
                    var already = new HashSet<CharacterPartType>();
                    for (int i = 0; i < partsToUse.Count; i++)
                    {
                        var n = partsToUse[i] != null ? partsToUse[i].name : null;
                        if (string.IsNullOrEmpty(n)) continue;
                        already.Add(runtime.ExtractPartType(n));
                    }

                    foreach (var kv in baseParts)
                    {
                        var type = kv.Key;
                        if (type == CharacterPartType.Wrap && !isFemaleBuild) continue;
                        if (already.Contains(type)) continue;
                        if (kv.Value == null || kv.Value.Count == 0) continue;
                        if (!TryResolvePartByName(partLibrary, type, kv.Value[0], out var fallbackPart)) continue;
                        var partGo = fallbackPart.GetPartModel();
                        var smr = partGo != null ? partGo.GetComponentInChildren<SkinnedMeshRenderer>() : null;
                        if (smr != null)
                        {
                            partsToUse.Add(smr);
                            already.Add(type);
                        }
                    }
                }
            }

            if (partsToUse.Count == 0) { onDone?.Invoke(null); yield break; }

            var blend = data.BlendShapes ?? new BlendShapeValues();
            runtime.BodyTypeBlendValue = blend.BodyTypeValue;
            runtime.BodySizeHeavyBlendValue = blend.BodySizeValue > 0 ? blend.BodySizeValue : 0f;
            runtime.BodySizeSkinnyBlendValue = blend.BodySizeValue < 0 ? -blend.BodySizeValue : 0f;
            runtime.MusclesBlendValue = blend.MuscleValue;

            runtime.PopulateUVDictionary(partsToUse);
            var hairUvs = BuildUvSet(
                runtime.CurrentUVDictionary,
                ColorPartType.Hair,
                ColorPartType.FacialHair,
                ColorPartType.EyebrowLeft,
                ColorPartType.EyebrowRight);
            // Для надежности всегда добираем UV напрямую из hair-like частей:
            // в некоторых паках маппинг CharacterPartType -> ColorPartType неполный,
            // и тогда UV-словарь runtime может не содержать все реальные зоны волос.
            for (int i = 0; i < partsToUse.Count; i++)
            {
                var part = partsToUse[i];
                if (part == null || !IsHairLikePartName(part.name)) continue;
                AddScaledUvsFromMesh(hairUvs, part.sharedMesh);
            }

            int ApplyFullPreset(int? presetId)
            {
                if (!presetId.HasValue) return 0;
                var preset = SidekickColorPreset.GetByID(dbManager, presetId.Value);
                if (preset == null) return 0;
                var rows = SidekickColorPresetRow.GetAllByPreset(dbManager, preset);
                if (rows == null) return 0;
                int applied = 0;
                Color firstMainColorParsed = Color.white;
                bool firstMainColorValid = false;
                foreach (var row in rows)
                {
                    var colorRow = SidekickColorRow.CreateFromPresetColorRow(row);
                    foreach (ColorType property in Enum.GetValues(typeof(ColorType)))
                        runtime.UpdateColor(property, colorRow);
                    if (!firstMainColorValid)
                    {
                        if (!string.IsNullOrEmpty(row.MainColor) && ColorUtility.TryParseHtmlString("#" + row.MainColor, out var c))
                        {
                            firstMainColorParsed = c;
                            firstMainColorValid = true;
                        }
                    }
                    applied++;
                }
                // Некоторые shader-графы визуально берут tint из _BaseColor/_Color,
                // поэтому дублируем основной цвет пресета в tint-каналы.
                if (runtime.CurrentMaterial != null && firstMainColorValid)
                {
                    if (runtime.CurrentMaterial.HasProperty("_BaseColor"))
                        runtime.CurrentMaterial.SetColor("_BaseColor", firstMainColorParsed);
                    if (runtime.CurrentMaterial.HasProperty("_Color"))
                        runtime.CurrentMaterial.SetColor("_Color", firstMainColorParsed);
                }
                return applied;
            }

            bool IsHairRow(SidekickColorPresetRow row)
            {
                var prop = row?.ColorProperty;
                if (prop == null) return false;

                // Основной критерий: попадание в UV текущих hair-like частей.
                if (hairUvs.Count > 0 && hairUvs.Contains(PackUv(prop.U, prop.V)))
                    return true;

                // Fallback для нестандартных пресетов без корректной UV-привязки.
                var n = prop.Name;
                bool isHairNamed = !string.IsNullOrWhiteSpace(n) &&
                    (n.IndexOf("hair", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("beard", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("brow", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("facial", StringComparison.OrdinalIgnoreCase) >= 0);
                if (hairUvs.Count > 0)
                    return false;
                if (isHairNamed)
                    return true;
                return false;
            }

            bool IsSkinRow(SidekickColorPresetRow row)
            {
                var n = row?.ColorProperty?.Name;
                if (string.IsNullOrWhiteSpace(n)) return false;
                return n.IndexOf("skin", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("body", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("flesh", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("face", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            int ApplyFilteredPreset(int? presetId, Func<SidekickColorPresetRow, bool> filter)
            {
                if (!presetId.HasValue) return 0;
                var preset = SidekickColorPreset.GetByID(dbManager, presetId.Value);
                if (preset == null) return 0;
                var rows = SidekickColorPresetRow.GetAllByPreset(dbManager, preset);
                if (rows == null) return 0;
                int applied = 0;
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    if (!filter(row)) continue;
                    var colorRow = SidekickColorRow.CreateFromPresetColorRow(row);
                    foreach (ColorType property in Enum.GetValues(typeof(ColorType)))
                        runtime.UpdateColor(property, colorRow);
                    if (runtime.CurrentMaterial != null)
                    {
                        // Прямое обновление ColorMap по UV как надежный fallback:
                        // некоторые пресеты/шейдерные варианты не всегда корректно проходят через runtime.UpdateColor.
                        ApplyColorRowToMaterial(runtime.CurrentMaterial, colorRow);
                    }
                    applied++;
                }
                return applied;
            }

            int? basePresetId = data.SkinColorPresetId ?? data.ColorPresetId ?? data.HairColorPresetId ?? data.OtherColorPresetId;
            int appliedRows = ApplyFullPreset(basePresetId);
            int appliedSkinRows = ApplyFilteredPreset(data.SkinColorPresetId, IsSkinRow);
            int appliedHairRows = ApplyFilteredPreset(data.HairColorPresetId, IsHairRow);
            if (appliedHairRows == 0 && data.HairColorPresetId.HasValue && hairUvs.Count > 0)
            {
                var fallbackColorRow = TryResolveHairFallbackColorRow(dbManager, data.HairColorPresetId);
                if (fallbackColorRow != null)
                {
                    foreach (var packedUv in hairUvs)
                    {
                        int u = (packedUv >> 16) & 0xFFFF;
                        int v = packedUv & 0xFFFF;
                        var rowForUv = new SidekickColorRow
                        {
                            MainColor = fallbackColorRow.MainColor,
                            Metallic = fallbackColorRow.Metallic,
                            Smoothness = fallbackColorRow.Smoothness,
                            Reflection = fallbackColorRow.Reflection,
                            Emission = fallbackColorRow.Emission,
                            Opacity = fallbackColorRow.Opacity,
                            ColorProperty = new SidekickColorProperty { ID = -1, Name = "runtime_hair_uv", U = u, V = v }
                        };
                        foreach (ColorType property in Enum.GetValues(typeof(ColorType)))
                            runtime.UpdateColor(property, rowForUv);
                        ApplyColorRowToMaterial(runtime.CurrentMaterial, rowForUv);
                        appliedHairRows++;
                    }
                }
            }
            appliedRows += appliedSkinRows + appliedHairRows;
            Debug.Log(
                $"[SidekickAppearanceBuilder] Applied color rows: base={appliedRows - appliedSkinRows - appliedHairRows}, skin={appliedSkinRows}, hair={appliedHairRows}. " +
                $"Presets: base={basePresetId?.ToString() ?? "null"}, skin={data.SkinColorPresetId?.ToString() ?? "null"}, hair={data.HairColorPresetId?.ToString() ?? "null"}");

            GameObject character = runtime.CreateCharacter("Character", partsToUse, false, true);
            if (character != null && runtime.CurrentMaterial != null)
            {
                var smrs = character.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                for (int i = 0; i < smrs.Length; i++)
                {
                    var smr = smrs[i];
                    if (smr == null) continue;
                    var mats = smr.sharedMaterials;
                    if (mats == null || mats.Length == 0)
                    {
                        smr.sharedMaterial = runtime.CurrentMaterial;
                        continue;
                    }
                    for (int m = 0; m < mats.Length; m++)
                        mats[m] = runtime.CurrentMaterial;
                    smr.sharedMaterials = mats;
                }

                // Гарантированный визуальный путь для волос:
                // отдельный material instance на hair-части с tint из HairColorPresetId.
                if (TryGetPresetMainColor(dbManager, data.HairColorPresetId, out var hairTint))
                    ApplyHairMaterialOverride(character, runtime.CurrentMaterial, hairTint);
            }

            if (character != null && data.FaceBlendShapes != null && data.FaceBlendShapes.Count > 0)
            {
                foreach (var smr in character.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var mesh = smr.sharedMesh;
                    if (mesh == null) continue;
                    for (int i = 0; i < mesh.blendShapeCount; i++)
                    {
                        var name = mesh.GetBlendShapeName(i);
                        if (data.FaceBlendShapes.TryGetValue(name, out var weight))
                            smr.SetBlendShapeWeight(i, weight);
                    }
                }
            }

            onDone?.Invoke(character);
        }
    }
}
