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
        void Send(DVBARPG.Net.Commands.IClientCommand command);
        void RegisterLocalMover(DVBARPG.Core.Simulation.ILocalMover mover);
    }

    public interface ICombatService {}
    public interface IInventoryService {}
    public interface IMarketService {}
    public interface IStatService {}
    public interface IItemRollService {}
}
