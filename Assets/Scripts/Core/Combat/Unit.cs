namespace CQ.Core.Combat
{
    /// <summary>
    /// 单位。字段与 T1 冻结契约一致（Id/Hp/MaxHp/BaseSpeed/Row/AggroWeight/Energy/MaxEnergy/Ult/MaxUlt）；
    /// 方法 TakeDamage/Heal/GainEnergy/GainUlt/IsDead。能量/大招条初值由配置注入，代码零硬编码。
    /// </summary>
    public sealed class Unit
    {
        public string Id;
        public string Name;
        public Team Team;

        public int Hp;
        public int MaxHp;
        public int BaseSpeed;

        public string Row;        // "front" | "back"
        public int AggroWeight;

        public int Energy;
        public int MaxEnergy;
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

        public void GainEnergy(int delta)
        {
            Energy += delta;
            if (Energy < 0) Energy = 0;
            if (Energy > MaxEnergy) Energy = MaxEnergy;
        }

        public void GainUlt(int delta)
        {
            Ult += delta;
            if (Ult < 0) Ult = 0;
            if (Ult > MaxUlt) Ult = MaxUlt;
        }
    }
}