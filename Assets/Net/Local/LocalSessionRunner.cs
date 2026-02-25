using System.Collections.Generic;
using DVBARPG.Core.Services;
using DVBARPG.Core.Simulation;
using DVBARPG.Net.Commands;
using UnityEngine;

namespace DVBARPG.Net.Local
{
    public sealed class LocalSessionRunner : ISessionService
    {
        private readonly Dictionary<string, ILocalMover> _movers = new();

        public bool IsConnected => true;

        public void Connect(AuthSession session, string mapId, string serverUrl)
        {
        }

        public void RegisterLocalMover(ILocalMover mover)
        {
            if (mover == null || string.IsNullOrEmpty(mover.EntityId)) return;
            _movers[mover.EntityId] = mover;
        }

        public void Send(IClientCommand command)
        {
            if (command is CmdMove move)
            {
                if (string.IsNullOrEmpty(move.EntityId)) return;
                if (!_movers.TryGetValue(move.EntityId, out var mover)) return;

                var dir = move.Direction;
                if (dir.sqrMagnitude > 1f) dir.Normalize();

                var delta = dir * move.Speed * move.DeltaTime;
                mover.ApplyMove(delta);
            }
        }
    }
}
