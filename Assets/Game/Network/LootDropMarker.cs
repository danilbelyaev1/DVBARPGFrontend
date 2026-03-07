using UnityEngine;

namespace DVBARPG.Game.Network
{
    /// <summary>
    /// Данные одного дропа на земле: вешается на инстанс префаба лута. Нужен Collider для рейкаста (клик/ховер).
    /// </summary>
    public sealed class LootDropMarker : MonoBehaviour
    {
        public int Index { get; private set; }
        public string Type { get; private set; }
        public int GoldAmount { get; private set; }
        public int ItemDefinitionId { get; private set; }
        public int ItemLevel { get; private set; }
        public string Rarity { get; private set; }
        /// <summary>Строка для отображения в рамке (название в стиле PoE).</summary>
        public string DisplayText { get; private set; }

        public void SetData(int index, string type, int goldAmount, int itemDefinitionId, int itemLevel, string rarity, string displayText)
        {
            Index = index;
            Type = type ?? "";
            GoldAmount = goldAmount;
            ItemDefinitionId = itemDefinitionId;
            ItemLevel = itemLevel;
            Rarity = rarity ?? "common";
            DisplayText = displayText ?? "";
        }

        /// <summary>Цвет текста по рарности (как в PoE).</summary>
        public static UnityEngine.Color GetRarityColor(string rarity)
        {
            if (string.IsNullOrEmpty(rarity)) return Color.white;
            switch (rarity.ToLowerInvariant())
            {
                case "common": return new Color(0.9f, 0.9f, 0.9f);
                case "magic": return new Color(0.4f, 0.6f, 1f);
                case "rare": return new Color(1f, 0.85f, 0.2f);
                case "unique": return new Color(1f, 0.6f, 0.2f);
                default: return Color.white;
            }
        }
    }
}
