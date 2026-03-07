namespace DVBARPG.Net.Commands
{
    /// <summary>
    /// Команда подбора дропа по индексу (золото/предмет). Сервер проверяет индекс и добавляет в pickedIndices для run/finish.
    /// </summary>
    public sealed class CmdPickup : IClientCommand
    {
        /// <summary>Индекс дропа из снапшота (LootDrops[].Index).</summary>
        public int DropIndex { get; set; }
    }
}
