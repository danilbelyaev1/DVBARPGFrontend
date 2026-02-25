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
        [Header("Идентификатор")]
        [Tooltip("Уникальный идентификатор класса.")]
        public ClassId Id;
        [Tooltip("Отображаемое имя класса.")]
        public string DisplayName;

        [Header("Базовые статы")]
        [Tooltip("Базовое максимальное здоровье.")]
        public int BaseMaxHp;
        [Tooltip("Базовый урон.")]
        public int BaseDamage;
        [Tooltip("Базовая скорость атаки.")]
        public float BaseAttackSpeed;
        [Tooltip("Базовый шанс крита (0..1).")]
        public float BaseCritChance;
        [Tooltip("Базовый множитель крита.")]
        public float BaseCritMulti;
        [Tooltip("Базовая броня.")]
        public int BaseArmor;

        [Header("Пассив")]
        [Tooltip("Название пассивного умения.")]
        public string PassiveName;
        [Tooltip("Описание пассивного умения.")]
        public string PassiveDescription;

        [Header("Активные умения")]
        [Tooltip("Название умения 1.")]
        public string Skill1Name;
        [Tooltip("Название умения 2.")]
        public string Skill2Name;
        [Tooltip("Название умения 3.")]
        public string Skill3Name;
    }
}
