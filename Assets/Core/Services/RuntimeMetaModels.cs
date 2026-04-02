using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DVBARPG.Core.Services
{
    public sealed class RuntimeSeasonSnapshot
    {
        public bool Ok;
        public string Error;
        public string SeasonId;
    }

    public sealed class RuntimeCharactersSnapshot
    {
        public bool Ok;
        public string Error;
        public string CurrentSeasonId;
        public RuntimeCharacterSummary[] Characters = Array.Empty<RuntimeCharacterSummary>();
    }

    public sealed class RuntimeCharacterSummary
    {
        public string Id;
        public string Name;
        /// <summary>Пол с бэка: male / female.</summary>
        public string Gender;
        /// <summary>Внешность (Sidekick). JSON-объект или null.</summary>
        public object Appearance;
        public string[] Seasons = Array.Empty<string>();
    }

    public static class RuntimeAppearanceParser
    {
        private static JObject NormalizeAppearanceObject(JObject obj)
        {
            if (obj == null) return null;

            // В старых/битых записях faceBlendShapes может храниться как [] вместо {}.
            // Для клиента это словарь, поэтому нормализуем в пустой объект.
            if (obj.TryGetValue("faceBlendShapes", StringComparison.OrdinalIgnoreCase, out var faceToken))
            {
                if (faceToken == null || faceToken.Type == JTokenType.Null || faceToken.Type == JTokenType.Array)
                    obj["faceBlendShapes"] = new JObject();
            }
            else
            {
                obj["faceBlendShapes"] = new JObject();
            }

            if (!obj.TryGetValue("parts", StringComparison.OrdinalIgnoreCase, out var partsToken) ||
                partsToken == null || partsToken.Type != JTokenType.Array)
            {
                obj["parts"] = new JArray();
            }

            if (!obj.TryGetValue("blendShapes", StringComparison.OrdinalIgnoreCase, out var blendToken) ||
                blendToken == null || blendToken.Type != JTokenType.Object)
            {
                obj["blendShapes"] = new JObject();
            }

            return obj;
        }

        public static CharacterAppearanceData Parse(object rawAppearance)
        {
            TryParse(rawAppearance, out var data, out _);
            return data;
        }

        public static bool TryParse(object rawAppearance, out CharacterAppearanceData data, out string error)
        {
            data = null;
            error = null;
            if (rawAppearance == null)
            {
                error = "raw_appearance_null";
                return false;
            }
            try
            {
                data = rawAppearance switch
                {
                    CharacterAppearanceData ready => ready,
                    JObject obj => NormalizeAppearanceObject(obj).ToObject<CharacterAppearanceData>(),
                    JToken token when token.Type == JTokenType.Object => NormalizeAppearanceObject((JObject)token).ToObject<CharacterAppearanceData>(),
                    JToken token when token.Type == JTokenType.String => ParseJsonString(token.Value<string>()),
                    string json when !string.IsNullOrWhiteSpace(json) => ParseJsonString(json),
                    _ => JsonConvert.DeserializeObject<CharacterAppearanceData>(JsonConvert.SerializeObject(rawAppearance))
                };

                if (data == null)
                {
                    error = "appearance_deserialized_null";
                    return false;
                }
                data.Parts ??= new System.Collections.Generic.List<CharacterPartEntry>();
                data.BlendShapes ??= new BlendShapeValues();
                data.FaceBlendShapes ??= new System.Collections.Generic.Dictionary<string, float>();

                if (!data.HairColorPresetId.HasValue && data.ColorPresetId.HasValue)
                    data.HairColorPresetId = data.ColorPresetId;
                if (!data.SkinColorPresetId.HasValue && data.ColorPresetId.HasValue)
                    data.SkinColorPresetId = data.ColorPresetId;
                if (!data.ColorPresetId.HasValue)
                    data.ColorPresetId = data.SkinColorPresetId ?? data.HairColorPresetId ?? data.OtherColorPresetId;
                if (!data.OtherColorPresetId.HasValue)
                    data.OtherColorPresetId = null;

                return data != null;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                data = null;
                return false;
            }
        }

        private static CharacterAppearanceData ParseJsonString(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            var token = JToken.Parse(json);
            if (token.Type == JTokenType.Object)
                return NormalizeAppearanceObject((JObject)token).ToObject<CharacterAppearanceData>();
            return JsonConvert.DeserializeObject<CharacterAppearanceData>(json);
        }
    }

    public sealed class RuntimeLoadout
    {
        public string AttackSkillId;
        public string SupportASkillId;
        public string SupportBSkillId;
        public string MovementSkillId;
    }

    public sealed class RuntimeAuthSnapshot
    {
        public bool Ok;
        public string Error;
        public RuntimeLoadout Loadout;
        public RuntimeSkillSnapshot[] Skills = Array.Empty<RuntimeSkillSnapshot>();
        public float MoveSpeed;
        public int Level;
        public int XpTotal;
        public int XpToNextLevel;
        public int UnspentTalentPoints;
    }

    /// <summary>Прогресс персонажа из Laravel-профиля.</summary>
    public sealed class RuntimeProgressionSnapshot
    {
        /// <summary>Текущий уровень (серверная правда).</summary>
        public int Level;
        /// <summary>Суммарный XP (как в progression.xpTotal на бэке).</summary>
        public int XpTotal;
        /// <summary>Сколько XP осталось до следующего уровня.</summary>
        public int XpToNextLevel;
        /// <summary>XP, необходимый для входа в текущий уровень (нижний порог).</summary>
        public int XpCurrentLevelBase;
        /// <summary>Глобальный XP, необходимый для следующего уровня (верхний порог).</summary>
        public int XpNextLevelTotal;
    }

    /// <summary>Снимок профиля персонажа (уровень/опыт и т.п.).</summary>
    public sealed class RuntimeProfileSnapshot
    {
        public bool Ok;
        public string Error;
        public RuntimeProgressionSnapshot Progression;
    }

    public sealed class RuntimeSkillSnapshot
    {
        public string SkillId;
        public int Level;
        public string ModifiersJson;
    }

    /// <summary>Пayload для PUT loadout (совпадает с combatLoadout на бэке).</summary>
    public sealed class RuntimeLoadoutPayload
    {
        public string AttackSkillId;
        public string SupportASkillId;
        public string SupportBSkillId;
        public string MovementSlot; // "supportA" или "supportB"
    }

    public sealed class SetLoadoutResult
    {
        public bool Ok;
        public string Error;
    }
}
