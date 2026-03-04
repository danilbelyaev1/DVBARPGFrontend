namespace DVBARPG.Core.Services
{
    public interface IAuthService
    {
        AuthSession Login();
    }

    public interface IRuntimeMetaService
    {
        void FetchCurrentSeason(AuthSession session, System.Action<RuntimeSeasonSnapshot> onDone);
        void FetchCharacters(AuthSession session, System.Action<RuntimeCharactersSnapshot> onDone);
        void ValidateAuth(AuthSession session, string characterId, string seasonId, System.Action<RuntimeAuthSnapshot> onDone);
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
        void Send(DVBARPG.Net.Commands.IClientCommand command);
    }

    public interface IInventoryService {}
    public interface IMarketService {}
    public interface IStatService {}
    public interface IItemRollService {}
}
