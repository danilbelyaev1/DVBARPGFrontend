using System;
using DVBARPG.Core.Combat;
using DVBARPG.Net.Events;

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

    public interface ICombatService
    {
        event Action<EvtDamage> Damage;
        event Action<EvtDeath> Death;
        event Action<EvtDrop> Drop;

        void RegisterEntity(ICombatEntity entity);
        void UnregisterEntity(string entityId);
        void RequestHit(string attackerId, string targetId, string skillId);
    }

    public interface IInventoryService {}
    public interface IMarketService {}
    public interface IStatService {}
    public interface IItemRollService {}
}
