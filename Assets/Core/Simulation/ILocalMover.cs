using UnityEngine;

namespace DVBARPG.Core.Simulation
{
    public interface ILocalMover
    {
        string EntityId { get; }
        void ApplyMove(Vector3 delta);
    }
}
