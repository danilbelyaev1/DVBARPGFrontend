using System;
using Newtonsoft.Json;

namespace DVBARPG.Core.Services
{
    public sealed class InventoryItemDto
    {
        [JsonProperty("instanceId")] public string InstanceId { get; set; }
        [JsonProperty("instance_id")] public string InstanceIdSnake { set => InstanceId = value; }
        [JsonProperty("characterId")] public string CharacterId { get; set; }
        [JsonProperty("seasonId")] public string SeasonId { get; set; }
        [JsonProperty("definition")] public ItemDefinitionDto Definition { get; set; }
        [JsonProperty("itemLevel")] public int ItemLevel { get; set; }
        [JsonProperty("rarity")] public string Rarity { get; set; }
        [JsonProperty("state")] public string State { get; set; }
        [JsonProperty("inventoryContainer")] public string InventoryContainer { get; set; }
        [JsonProperty("inventory_container")] public string InventoryContainerSnake { set => InventoryContainer = value; }
        [JsonProperty("inventorySlot")] public string InventorySlot { get; set; }
        [JsonProperty("inventory_slot")] public string InventorySlotSnake { set => InventorySlot = value; }
        [JsonProperty("stackCount")] public int StackCount { get; set; }
        [JsonProperty("stack_count")] public int StackCountSnake { set => StackCount = value; }
    }

    public sealed class ItemDefinitionDto
    {
        [JsonProperty("code")] public string Code { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("allowedSlots")] public string[] AllowedSlots { get; set; }
        [JsonProperty("allowed_slots")] public string[] AllowedSlotsSnake { set => AllowedSlots = value; }
        [JsonProperty("requiredLevel")] public int RequiredLevel { get; set; }
        [JsonProperty("required_level")] public int RequiredLevelSnake { set => RequiredLevel = value; }
    }

    public sealed class InventoryResult
    {
        public bool Ok { get; set; }
        public string Error { get; set; }
        public InventoryItemDto[] Items { get; set; }
        public string[] EquipmentSlots { get; set; }
        public int BagCapacity { get; set; }
        public int StashCapacity { get; set; }
        public int BagUsage { get; set; }
        public int StashUsage { get; set; }
    }

    public sealed class InventoryActionResult
    {
        public bool Ok { get; set; }
        public string Error { get; set; }
        public InventoryItemDto Item { get; set; }
    }

    public sealed class SplitStackResult
    {
        public bool Ok { get; set; }
        public string Error { get; set; }
        public InventoryItemDto OriginalItem { get; set; }
        public InventoryItemDto NewItem { get; set; }
    }

    public sealed class MergeStacksResult
    {
        public bool Ok { get; set; }
        public string Error { get; set; }
        public InventoryItemDto MergedItem { get; set; }
        public string DeletedInstanceId { get; set; }
    }
}
