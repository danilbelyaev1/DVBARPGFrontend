using System;
using UnityEngine;

namespace DVBARPG.Core.Combat
{
    [Serializable]
    public struct EntityStats
    {
        public int MaxHp;
        public int Damage;
        public float AttackSpeed;
        public float CritChance;
        public float CritMulti;
        public int Armor;
    }

    public struct DamageResult
    {
        public int Amount;
        public bool IsCrit;
        public int TargetRemainingHp;
    }
}
