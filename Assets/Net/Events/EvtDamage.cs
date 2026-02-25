namespace DVBARPG.Net.Events
{
    public struct EvtDamage
    {
        public string AttackerId;
        public string TargetId;
        public int Amount;
        public bool IsCrit;
        public int TargetRemainingHp;
    }
}
