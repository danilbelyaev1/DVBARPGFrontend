using UnityEngine;

namespace DVBARPG.Net.Commands
{
    public sealed class CmdDebug : IClientCommand
    {
        public string Type;
        public bool HasPosition;
        public Vector2 Position;
    }
}
