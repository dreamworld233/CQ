namespace CQ.Core.Combat
{
    /// <summary>战斗单位，终结技条通过 GainUlt 限幅。</summary>
    public sealed class Unit
    {
        public string Id;
        public string Name;
        public Team Team;

        public int Hp;
        public int MaxHp;
        public int BaseSpeed;
        public int BaseAttack;
        public int BaseDefense;

        public string SlotId;
        public string Row => Formation.RowOf(SlotId);

        public int Ult;
        public int MaxUlt;

        public bool IsDead => Hp <= 0;

        public Unit(string id, Team team)
        {
            Id = id;
            Team = team;
        }

        public void TakeDamage(int amount)
        {
            if (amount < 0) amount = 0;
            Hp -= amount;
            if (Hp < 0) Hp = 0;
        }

        public void Heal(int amount)
        {
            if (amount < 0) amount = 0;
            Hp += amount;
            if (Hp > MaxHp) Hp = MaxHp;
        }

        public void GainUlt(int delta)
        {
            Ult = (int)System.Math.Max(0L, System.Math.Min(System.Math.Max(0, MaxUlt), (long)Ult + delta));
        }
    }
}