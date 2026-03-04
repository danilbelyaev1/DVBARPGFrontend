using DVBARPG.Core.Services;

namespace DVBARPG.Net.Local
{
    /// <summary>
    /// Заглушка инвентаря (все операции возвращают ошибку). Для работы с Laravel используется BackendInventoryService.
    /// </summary>
    public sealed class LocalInventoryService : IInventoryService
    {
        public void GetInventory(string characterId, string seasonId, System.Action<InventoryResult> onDone)
            => onDone?.Invoke(new InventoryResult { Ok = false, Error = "use_backend" });

        public void Equip(string characterId, string seasonId, string instanceId, string slot, string requestId, System.Action<InventoryActionResult> onDone)
            => onDone?.Invoke(new InventoryActionResult { Ok = false, Error = "use_backend" });

        public void Unequip(string characterId, string seasonId, string slot, string requestId, System.Action<InventoryActionResult> onDone)
            => onDone?.Invoke(new InventoryActionResult { Ok = false, Error = "use_backend" });

        public void Move(string characterId, string seasonId, string instanceId, string targetContainer, int? targetSlot, string requestId, System.Action<InventoryActionResult> onDone)
            => onDone?.Invoke(new InventoryActionResult { Ok = false, Error = "use_backend" });

        public void SplitStack(string characterId, string seasonId, string instanceId, int splitAmount, string requestId, System.Action<SplitStackResult> onDone)
            => onDone?.Invoke(new SplitStackResult { Ok = false, Error = "use_backend" });

        public void MergeStacks(string characterId, string seasonId, string sourceInstanceId, string targetInstanceId, string requestId, System.Action<MergeStacksResult> onDone)
            => onDone?.Invoke(new MergeStacksResult { Ok = false, Error = "use_backend" });
    }
}
