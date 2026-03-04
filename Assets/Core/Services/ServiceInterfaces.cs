namespace DVBARPG.Core.Services
{
    public interface IAuthService
    {
        AuthSession Login();
    }

    /// <summary>Глобальный лоадер: модалка со спиннером при любом запросе. Ref-count: BeginRequest/EndRequest.</summary>
    public interface ILoadingOverlayService
    {
        void BeginRequest(string message = null);
        void EndRequest();
    }

    public interface IRuntimeMetaService
    {
        void FetchCurrentSeason(AuthSession session, System.Action<RuntimeSeasonSnapshot> onDone);
        void FetchCharacters(AuthSession session, System.Action<RuntimeCharactersSnapshot> onDone);
        void ValidateAuth(AuthSession session, string characterId, string seasonId, System.Action<RuntimeAuthSnapshot> onDone);
        void SetLoadout(AuthSession session, string characterId, string seasonId, RuntimeLoadoutPayload loadout, System.Action<SetLoadoutResult> onDone);
        void AllocateTalent(AuthSession session, string characterId, string seasonId, string talentCode, string requestId, System.Action<AllocateTalentResult> onDone);
        /// <summary>Создание персонажа: имя, класс (vanguard/hunter/mystic), пол (male/female), опционально внешность.</summary>
        void CreateCharacter(AuthSession session, string name, string classId, string gender, System.Action<CreateCharacterResult> onDone);
        /// <summary>Удаление персонажа (только своего).</summary>
        void DeleteCharacter(AuthSession session, string characterId, System.Action<DeleteCharacterResult> onDone);
    }

    public sealed class DeleteCharacterResult
    {
        public bool Ok { get; set; }
        public string Error { get; set; }
    }

    public sealed class CreateCharacterResult
    {
        public bool Ok { get; set; }
        public string Error { get; set; }
        public string CharacterId { get; set; }
    }

    public sealed class AllocateTalentResult
    {
        public bool Ok { get; set; }
        public string Error { get; set; }
    }

    public interface IProfileService
    {
        AuthSession CurrentAuth { get; }
        void SetAuth(AuthSession session);

        string SelectedClassId { get; }
        void SetSelectedClass(string classId);

        string SelectedCharacterId { get; }
        void SetSelectedCharacter(string characterId);

        string CurrentSeasonId { get; }
        void SetCurrentSeason(string seasonId);

        RuntimeCharacterSummary[] Characters { get; }
        void SetCharacters(RuntimeCharacterSummary[] characters);

        RuntimeLoadout ServerLoadout { get; }
        void SetServerLoadout(RuntimeLoadout loadout);

        float BaseMoveSpeed { get; }
        void SetBaseMoveSpeed(float moveSpeed);
    }

    public interface ISessionService
    {
        bool IsConnected { get; }
        void Connect(AuthSession session, string mapId, string serverUrl);
        void Disconnect();
        void Send(DVBARPG.Net.Commands.IClientCommand command);
    }

    public interface IInventoryService
    {
        void GetInventory(string characterId, string seasonId, System.Action<InventoryResult> onDone);
        void Equip(string characterId, string seasonId, string instanceId, string slot, string requestId, System.Action<InventoryActionResult> onDone);
        void Unequip(string characterId, string seasonId, string slot, string requestId, System.Action<InventoryActionResult> onDone);
        void Move(string characterId, string seasonId, string instanceId, string targetContainer, int? targetSlot, string requestId, System.Action<InventoryActionResult> onDone);
        void SplitStack(string characterId, string seasonId, string instanceId, int splitAmount, string requestId, System.Action<SplitStackResult> onDone);
        void MergeStacks(string characterId, string seasonId, string sourceInstanceId, string targetInstanceId, string requestId, System.Action<MergeStacksResult> onDone);
    }
    public interface IMarketService
    {
        void GetListings(string seasonId, int limit, int offset, System.Action<GetListingsResult> onDone);
        void ListItem(string characterId, string seasonId, string itemInstanceId, int price, string currencyCode, string requestId, System.Action<ListItemResult> onDone);
        void CancelListing(string characterId, string seasonId, string listingId, string requestId, System.Action<ListItemResult> onDone);
        void BuyListing(string characterId, string seasonId, string listingId, string requestId, System.Action<BuyListingResult> onDone);
    }

    public interface ICurrencyService
    {
        void GetBalance(string characterId, string seasonId, string currencyCode, System.Action<CurrencyBalanceResult> onDone);
        void GetLedger(string characterId, string seasonId, int limit, int offset, System.Action<CurrencyLedgerResult> onDone);
    }
    public interface IStatService {}
    public interface IItemRollService {}
}
