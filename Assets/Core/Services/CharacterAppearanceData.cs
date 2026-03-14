using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DVBARPG.Core.Services
{
    /// <summary>
    /// Данные внешности персонажа (Sidekick). Сериализуется в JSON для бэка.
    /// Без одежды: пол, вес, лицо (морфы), волосы, цвета.
    /// </summary>
    [Serializable]
    public sealed class CharacterAppearanceData
    {
        [JsonProperty("speciesId")]
        public int SpeciesId { get; set; }

        [JsonProperty("parts")]
        public List<CharacterPartEntry> Parts { get; set; } = new();

        [JsonProperty("blendShapes")]
        public BlendShapeValues BlendShapes { get; set; } = new();

        /// <summary>Тонкая настройка лица: имя блендшейпа → вес (0..100).</summary>
        [JsonProperty("faceBlendShapes")]
        public Dictionary<string, float> FaceBlendShapes { get; set; } = new();

        /// <summary>ID цветового пресета в БД Sidekick (устаревшее — используется для обратной совместимости, если не заданы hair/skin).</summary>
        [JsonProperty("colorPresetId")]
        public int? ColorPresetId { get; set; }

        /// <summary>ID пресета цвета волос (только строки пресета с ColorProperty.Name, содержащим "Hair").</summary>
        [JsonProperty("hairColorPresetId")]
        public int? HairColorPresetId { get; set; }

        /// <summary>ID пресета цвета кожи (только строки пресета без Hair в имени свойства).</summary>
        [JsonProperty("skinColorPresetId")]
        public int? SkinColorPresetId { get; set; }
    }

    [Serializable]
    public sealed class CharacterPartEntry
    {
        [JsonProperty("partType")]
        public int PartType { get; set; }

        [JsonProperty("partName")]
        public string PartName { get; set; }
    }

    [Serializable]
    public sealed class BlendShapeValues
    {
        /// <summary>Пол: 0 = masculine, 100 = feminine. Маппится на блендшейп masculineFeminine.</summary>
        [JsonProperty("bodyTypeValue")]
        public float BodyTypeValue { get; set; } = 50f;

        /// <summary>Вес: отрицательный = skinny, положительный = heavy.</summary>
        [JsonProperty("bodySizeValue")]
        public float BodySizeValue { get; set; }

        /// <summary>Мускулатура (defaultBuff).</summary>
        [JsonProperty("muscleValue")]
        public float MuscleValue { get; set; } = 50f;
    }
}
