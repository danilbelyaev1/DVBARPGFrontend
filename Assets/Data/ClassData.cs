using UnityEngine;

namespace DVBARPG.Data
{
    public enum ClassId
    {
        Melee,
        Ranged,
        Mage
    }

    [CreateAssetMenu(menuName = "DVBARPG/Data/Class Data", fileName = "ClassData")]
    public sealed class ClassData : ScriptableObject
    {
        public ClassId Id;
        public string DisplayName;
        public int BaseMaxHp;
        public int BaseDamage;
        public float BaseAttackSpeed;
        public float BaseCritChance;
        public float BaseCritMulti;
        public int BaseArmor;

        public string PassiveName;
        public string PassiveDescription;

        public string Skill1Name;
        public string Skill2Name;
        public string Skill3Name;
    }
}
