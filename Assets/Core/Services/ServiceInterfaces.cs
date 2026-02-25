namespace DVBARPG.Core.Services
{
    public interface IAuthService
    {
        AuthSession Login();
    }

    public interface IProfileService
    {
        AuthSession CurrentAuth { get; }
        void SetAuth(AuthSession session);

        string SelectedClassId { get; }
        void SetSelectedClass(string classId);
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
