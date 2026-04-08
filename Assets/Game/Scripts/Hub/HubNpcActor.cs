using DVBARPG.Core.Services;
using UnityEngine;

namespace DVBARPG.Game.Hub
{
    /// <summary>
    /// Маркер кликабельного NPC в хабе; данные приходят с бэка (<see cref="NpcInfo"/>).
    /// </summary>
    public sealed class HubNpcActor : MonoBehaviour
    {
        public NpcInfo Data { get; private set; }

        public void Bind(NpcInfo npc)
        {
            Data = npc;
        }
    }
}
