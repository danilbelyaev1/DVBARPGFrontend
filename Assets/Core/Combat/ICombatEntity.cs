namespace DVBARPG.Core.Combat
{
    public interface ICombatEntity
    {
        string EntityId { get; }
        EntityStats Stats { get; }
        int CurrentHp { get; }
        void ApplyDamage(DamageResult result);
    }
}
