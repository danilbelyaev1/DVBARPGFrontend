using System.Collections.Generic;
using DVBARPG.Net.Network;
using UnityEngine;

namespace DVBARPG.Net.Commands
{
    public sealed class CmdDebug : IClientCommand
    {
        public string Type;
        public bool HasPosition;
        public Vector2 Position;
        public Dictionary<string, float> StatPatch;
        public List<SkillInstance> Skills;
        public CombatLoadout CombatLoadout;
        public bool? ReplaceSkills;
    }
}
