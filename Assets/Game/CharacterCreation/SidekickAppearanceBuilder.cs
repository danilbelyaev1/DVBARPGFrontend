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

        /// <summary>Мгновенно применить цвета к уже собранному персонажу (без ребилда). Возвращает true, если применилось.</summary>
        public bool ApplyColorsToExisting(GameObject character, CharacterAppearanceData data)
        {
            if (character == null || data == null) return false;
            var smr = character.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr == null || smr.sharedMaterial == null) return false;
            Material mat = smr.sharedMaterial;
            var dbManager = new DatabaseManager();
            if (dbManager.GetCurrentDbConnection() == null)
                dbManager.GetDbConnection(true);

            void ApplyPresetRowsToMaterial(int? presetId, bool hairOnly)
            {
                if (!presetId.HasValue) return;
                var preset = SidekickColorPreset.GetByID(dbManager, presetId.Value);
                if (preset == null) return;
                var rows = SidekickColorPresetRow.GetAllByPreset(dbManager, preset);
                if (rows == null) return;
                foreach (var row in rows)
                {
                    if (IsHairColorProperty(row) != hairOnly) continue;
                    var colorRow = SidekickColorRow.CreateFromPresetColorRow(row);
                    ApplyColorRowToMaterial(mat, colorRow);
                }
            }

            bool hasHairSkin = data.HairColorPresetId.HasValue || data.SkinColorPresetId.HasValue;
            if (hasHairSkin)
            {
                ApplyPresetRowsToMaterial(data.SkinColorPresetId, false);
                ApplyPresetRowsToMaterial(data.HairColorPresetId, true);
            }
            else if (data.ColorPresetId.HasValue)
            {
                var preset = SidekickColorPreset.GetByID(dbManager, data.ColorPresetId.Value);
                if (preset != null)
                {
                    var rows = SidekickColorPresetRow.GetAllByPreset(dbManager, preset);
                    if (rows != null)
                        foreach (var row in rows)
                        {
                            var colorRow = SidekickColorRow.CreateFromPresetColorRow(row);
                            ApplyColorRowToMaterial(mat, colorRow);
                        }
                }
            }
            return true;
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
            SetTex(ShaderMetallicMap, colorRow.NiceMetallic);
            SetTex(ShaderSmoothnessMap, colorRow.NiceSmoothness);
            SetTex(ShaderReflectionMap, colorRow.NiceReflection);
            SetTex(ShaderEmissionMap, colorRow.NiceEmission);
            SetTex(ShaderOpacityMap, colorRow.NiceOpacity);
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

            // Добавить кости новой части, которых нет в скелете (как в CreateModelFromParts), иначе верх причёски «улетает»
            var singlePartList = new List<SkinnedMeshRenderer> { partSmr };
            var additionalBones = Combiner.FindAdditionalBones(boneNameMap, singlePartList);
            if (additionalBones.Length > 0)
            {
                var bonesArray = new Transform[boneNameMap.Count];
                boneNameMap.Values.CopyTo(bonesArray, 0);
                Combiner.JoinAdditionalBonesToBoneArray(bonesArray, additionalBones, boneNameMap);
            }

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
            GameObject baseModel = Resources.Load<GameObject>(BaseModelPath);
            Material material = Resources.Load<Material>(BaseMaterialPath);
            if (baseModel == null || material == null) yield break;
            var dbManager = new DatabaseManager();
            if (dbManager.GetCurrentDbConnection() == null)
                dbManager.GetDbConnection(true);
            var baseAnimator = baseModel.GetComponentInChildren<Animator>();
            var animController = baseAnimator != null ? baseAnimator.runtimeAnimatorController : null;
            var runtime = new SidekickRuntime(baseModel, material, animController, dbManager);
            var populateTask = SidekickRuntime.PopulateToolData(runtime);
            while (!populateTask.IsCompleted)
                yield return null;
            CachePartListFromRuntime(runtime);
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

            var dbManager = new DatabaseManager();
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
            var dbManager = new DatabaseManager();
            if (dbManager.GetCurrentDbConnection() == null)
                dbManager.GetDbConnection(true);
            yield return null;
            var species = SidekickSpecies.GetByName(dbManager, speciesName);
            if (species == null) { onDone(list); yield break; }
            var presets = SidekickColorPreset.GetAllByColorGroupAndSpecies(dbManager, ColorGroup.Species, species);
            if (presets == null || presets.Count == 0)
                presets = SidekickColorPreset.GetAllBySpecies(dbManager, species);
            if (presets != null)
            {
                foreach (var p in presets)
                    list.Add((p.ID, string.IsNullOrEmpty(p.Name) ? "Preset " + p.ID : p.Name));
            }
            onDone(list);
        }

        private IEnumerator GetDefaultAppearanceDataRoutine(string speciesName, string gender, float bodySize, float muscle, Action<CharacterAppearanceData> onDone)
        {
            GameObject baseModel = Resources.Load<GameObject>(BaseModelPath);
            Material material = Resources.Load<Material>(BaseMaterialPath);
            if (baseModel == null || material == null)
            {
                onDone?.Invoke(BuildFallbackDefaultAppearance(gender, bodySize, muscle));
                yield break;
            }
            var dbManager = new DatabaseManager();
            if (dbManager.GetCurrentDbConnection() == null)
                dbManager.GetDbConnection(true);
            var runtime = new SidekickRuntime(baseModel, material, null, dbManager);
            var populateTask = SidekickRuntime.PopulateToolData(runtime);
            while (!populateTask.IsCompleted)
                yield return null;
            CachePartListFromRuntime(runtime);

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
            if (presets != null && presets.Count > 0)
                firstPresetId = presets[0].ID;

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
                SkinColorPresetId = firstPresetId
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
                SkinColorPresetId = null
            };
        }

        private static bool IsHairColorProperty(SidekickColorPresetRow row)
        {
            var name = row?.ColorProperty?.Name;
            return !string.IsNullOrEmpty(name) && name.IndexOf("Hair", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private IEnumerator BuildRoutine(CharacterAppearanceData data, Action<GameObject> onDone)
        {
            GameObject baseModel = Resources.Load<GameObject>(BaseModelPath);
            Material material = Resources.Load<Material>(BaseMaterialPath);
            if (baseModel == null || material == null)
            {
                Debug.LogWarning("SidekickAppearanceBuilder: base model or material not found in Resources.");
                onDone?.Invoke(null);
                yield break;
            }

            var dbManager = new DatabaseManager();
            if (dbManager.GetCurrentDbConnection() == null)
                dbManager.GetDbConnection(true);

            var baseAnimator = baseModel.GetComponentInChildren<Animator>();
            var animController = baseAnimator != null ? baseAnimator.runtimeAnimatorController : null;
            var runtime = new SidekickRuntime(baseModel, material, animController, dbManager);
            var populateTask = SidekickRuntime.PopulateToolData(runtime);
            while (!populateTask.IsCompleted)
                yield return null;
            CachePartListFromRuntime(runtime);

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
                if (!partLibrary.TryGetValue(type, out var dict) || !dict.TryGetValue(entry.PartName, out var sidekickPart))
                    continue;
                var partGo = sidekickPart.GetPartModel();
                if (partGo != null)
                {
                    var smr = partGo.GetComponentInChildren<SkinnedMeshRenderer>();
                    if (smr != null) partsToUse.Add(smr);
                }
            }

            if (partsToUse.Count == 0) { onDone?.Invoke(null); yield break; }

            var blend = data.BlendShapes ?? new BlendShapeValues();
            runtime.BodyTypeBlendValue = blend.BodyTypeValue;
            runtime.BodySizeHeavyBlendValue = blend.BodySizeValue > 0 ? blend.BodySizeValue : 0f;
            runtime.BodySizeSkinnyBlendValue = blend.BodySizeValue < 0 ? -blend.BodySizeValue : 0f;
            runtime.MusclesBlendValue = blend.MuscleValue;

            void ApplyPresetFiltered(int? presetId, bool hairOnly)
            {
                if (!presetId.HasValue) return;
                var preset = SidekickColorPreset.GetByID(dbManager, presetId.Value);
                if (preset == null) return;
                var rows = SidekickColorPresetRow.GetAllByPreset(dbManager, preset);
                if (rows == null) return;
                foreach (var row in rows)
                {
                    if (IsHairColorProperty(row) != hairOnly) continue;
                    var colorRow = SidekickColorRow.CreateFromPresetColorRow(row);
                    foreach (ColorType pt in Enum.GetValues(typeof(ColorType)))
                        runtime.UpdateColor(pt, colorRow);
                }
            }

            bool hasHairSkin = data.HairColorPresetId.HasValue || data.SkinColorPresetId.HasValue;
            if (hasHairSkin)
            {
                ApplyPresetFiltered(data.SkinColorPresetId, hairOnly: false);
                ApplyPresetFiltered(data.HairColorPresetId, hairOnly: true);
            }
            else if (data.ColorPresetId.HasValue)
            {
                var preset = SidekickColorPreset.GetByID(dbManager, data.ColorPresetId.Value);
                if (preset != null)
                {
                    var rows = SidekickColorPresetRow.GetAllByPreset(dbManager, preset);
                    if (rows != null)
                        foreach (var row in rows)
                        {
                            var colorRow = SidekickColorRow.CreateFromPresetColorRow(row);
                            foreach (ColorType pt in Enum.GetValues(typeof(ColorType)))
                                runtime.UpdateColor(pt, colorRow);
                        }
                }
            }

            GameObject character = runtime.CreateCharacter("Character", partsToUse, false, true);
            _cachedRuntime = runtime;

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
