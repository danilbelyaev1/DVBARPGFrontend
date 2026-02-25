using UnityEngine;

namespace DVBARPG.Net.Commands
{
    public sealed class CmdMove : IClientCommand
    {
        public string EntityId;
        public Vector3 Direction;
        public float Speed;
        public float DeltaTime;
    }
}
