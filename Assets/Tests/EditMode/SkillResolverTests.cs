using NUnit.Framework;
using System.Collections.Generic;
using CQ.Core.Battle;
using CQ.Core.Combat;
using CQ.Core.Config;
using CQ.Runtime;

public class SkillResolverTests
{
    // 对齐 sample JSON 的固定数值（测试常量，非运行时硬编码）。
    private const int BasicEnergyDelta = 1;
    private const int BasicUltDelta = 1;
    private const int SkillEnergyCost = 2;
    private const int SkillUltDelta = 1;
    private const int MaxEnergyFull = 3;
    private const int MaxUltFull = 3;

    private const int SwordBasicDmg = 12;
    private const int SwordSkillDmg = 30;
    private const int SwordUltDmg = 45;

    private const int GuardBasicDmg = 8;
    private const int GuardSkillHeal = 15;
    private const int GuardUltHeal = 20;

    private const int MageBasicDmg = 10;
    private const int MageSkillDmg = 28;
    private const int MageUltDmg = 38;

    private static Unit P(string id, int hp) => new Unit(id, Team.Player)
    {
        MaxHp = hp, Hp = hp, MaxEnergy = MaxEnergyFull, MaxUlt = MaxUltFull
    };

    private static Unit E(string id, int hp) => new Unit(id, Team.Enemy) { MaxHp = hp, Hp = hp };

    private static CombatContext Ctx(params Unit[] units) => new CombatContext(new Rng(1), units);

    private static SkillSpec Basic(string id, string kind, int dmg) => new SkillSpec
    {
        id = id, type = "basic", energyDelta = BasicEnergyDelta, ultDelta = BasicUltDelta,
        damage = new DamageSpec { kind = kind, target = "single", amount = dmg }
    };

    private static SkillSpec DamageSkill(string id, string kind, int dmg) => new SkillSpec
    {
        id = id, type = "skill", energyCost = SkillEnergyCost, ultDelta = SkillUltDelta,
        damage = new DamageSpec { kind = kind, target = "single", amount = dmg }
    };

    private static SkillSpec DamageUlt(string id, string kind, int dmg) => new SkillSpec
    {
        id = id, type = "ult",
        damage = new DamageSpec { kind = kind, target = "all", amount = dmg }
    };

    private static SkillSpec HealSkill(int amount) => new SkillSpec
    {
        id = "guard_skill", type = "skill", energyCost = SkillEnergyCost, ultDelta = SkillUltDelta,
        heal = new HealSpec { target = "self", amount = amount }
    };

    private static SkillSpec HealUlt(int amount) => new SkillSpec
    {
        id = "guard_ult", type = "ult",
        heal = new HealSpec { target = "all", amount = amount }
    };

    // ---------- sword × basic ----------
    [Test]
    public void Sword_Basic_GainsEnergyAndUlt_DealsDamage()
    {
        var caster = P("sword", 100);
        var target = E("e1", 100);
        var ctx = Ctx(caster, target);

        SkillResolver.Resolve(Basic("sword_basic", "physical", SwordBasicDmg), ctx, caster, target,
            new List<Unit> { caster }, new List<Unit> { target });

        Assert.AreEqual(BasicEnergyDelta, caster.Energy);
        Assert.AreEqual(BasicUltDelta, caster.Ult);
        Assert.AreEqual(100 - SwordBasicDmg, target.Hp);
    }

    // ---------- sword × skill ----------
    [Test]
    public void Sword_Skill_CostsEnergy_GainsUlt_DealsDamage()
    {
        var caster = P("sword", 100);
        caster.Energy = SkillEnergyCost;
        var target = E("e1", 100);
        var ctx = Ctx(caster, target);

        SkillResolver.Resolve(DamageSkill("sword_skill", "physical", SwordSkillDmg), ctx, caster, target,
            new List<Unit> { caster }, new List<Unit> { target });

        Assert.AreEqual(0, caster.Energy, "能量应扣到 0");
        Assert.AreEqual(SkillUltDelta, caster.Ult);
        Assert.AreEqual(100 - SwordSkillDmg, target.Hp);
    }

    // ---------- sword × ult ----------
    [Test]
    public void Sword_Ult_FullBarResets_DealsDamageToAll()
    {
        var caster = P("sword", 100);
        caster.Ult = MaxUltFull;
        var e1 = E("e1", 100);
        var e2 = E("e2", 100);
        var ctx = Ctx(caster, e1, e2);

        SkillResolver.Resolve(DamageUlt("sword_ult", "physical", SwordUltDmg), ctx, caster, e1,
            new List<Unit> { caster }, new List<Unit> { e1, e2 });

        Assert.AreEqual(0, caster.Ult, "满大招应归零");
        Assert.AreEqual(100 - SwordUltDmg, e1.Hp);
        Assert.AreEqual(100 - SwordUltDmg, e2.Hp);
    }

    // ---------- guard × basic ----------
    [Test]
    public void Guard_Basic_GainsEnergyAndUlt_DealsDamage()
    {
        var caster = P("guard", 160);
        var target = E("e1", 100);
        var ctx = Ctx(caster, target);

        SkillResolver.Resolve(Basic("guard_basic", "physical", GuardBasicDmg), ctx, caster, target,
            new List<Unit> { caster }, new List<Unit> { target });

        Assert.AreEqual(BasicEnergyDelta, caster.Energy);
        Assert.AreEqual(BasicUltDelta, caster.Ult);
        Assert.AreEqual(100 - GuardBasicDmg, target.Hp);
    }

    // ---------- guard × skill ----------
    [Test]
    public void Guard_Skill_CostsEnergy_SelfHeal()
    {
        var caster = P("guard", 160);
        caster.Hp = 80;
        caster.Energy = SkillEnergyCost;
        var target = E("e1", 100);
        var ctx = Ctx(caster, target);

        SkillResolver.Resolve(HealSkill(GuardSkillHeal), ctx, caster, target,
            new List<Unit> { caster }, new List<Unit> { target });

        Assert.AreEqual(0, caster.Energy);
        Assert.AreEqual(SkillUltDelta, caster.Ult);
        Assert.AreEqual(80 + GuardSkillHeal, caster.Hp, "自疗 15");
        Assert.AreEqual(100, target.Hp, "无伤害目标不应掉血");
    }

    // ---------- guard × ult ----------
    [Test]
    public void Guard_Ult_FullBarResets_HealAll()
    {
        var caster = P("guard", 160);
        caster.Hp = 80;
        caster.Ult = MaxUltFull;
        var ally2 = P("ally2", 100);
        ally2.Hp = 50;
        var target = E("e1", 100);
        var ctx = Ctx(caster, ally2, target);

        SkillResolver.Resolve(HealUlt(GuardUltHeal), ctx, caster, target,
            new List<Unit> { caster, ally2 }, new List<Unit> { target });

        Assert.AreEqual(0, caster.Ult, "满大招应归零");
        Assert.AreEqual(80 + GuardUltHeal, caster.Hp, "全体治疗含自身");
        Assert.AreEqual(50 + GuardUltHeal, ally2.Hp, "全体治疗含队友");
    }

    // ---------- mage × basic ----------
    [Test]
    public void Mage_Basic_GainsEnergyAndUlt_DealsDamage()
    {
        var caster = P("mage", 80);
        var target = E("e1", 100);
        var ctx = Ctx(caster, target);

        SkillResolver.Resolve(Basic("mage_basic", "magic", MageBasicDmg), ctx, caster, target,
            new List<Unit> { caster }, new List<Unit> { target });

        Assert.AreEqual(BasicEnergyDelta, caster.Energy);
        Assert.AreEqual(BasicUltDelta, caster.Ult);
        Assert.AreEqual(100 - MageBasicDmg, target.Hp);
    }

    // ---------- mage × skill ----------
    [Test]
    public void Mage_Skill_CostsEnergy_GainsUlt_DealsDamage()
    {
        var caster = P("mage", 80);
        caster.Energy = SkillEnergyCost;
        var target = E("e1", 100);
        var ctx = Ctx(caster, target);

        SkillResolver.Resolve(DamageSkill("mage_skill", "magic", MageSkillDmg), ctx, caster, target,
            new List<Unit> { caster }, new List<Unit> { target });

        Assert.AreEqual(0, caster.Energy);
        Assert.AreEqual(SkillUltDelta, caster.Ult);
        Assert.AreEqual(100 - MageSkillDmg, target.Hp);
    }

    // ---------- mage × ult ----------
    [Test]
    public void Mage_Ult_FullBarResets_DealsDamageToAll()
    {
        var caster = P("mage", 80);
        caster.Ult = MaxUltFull;
        var e1 = E("e1", 100);
        var e2 = E("e2", 100);
        var ctx = Ctx(caster, e1, e2);

        SkillResolver.Resolve(DamageUlt("mage_ult", "magic", MageUltDmg), ctx, caster, e1,
            new List<Unit> { caster }, new List<Unit> { e1, e2 });

        Assert.AreEqual(0, caster.Ult, "满大招应归零");
        Assert.AreEqual(100 - MageUltDmg, e1.Hp);
        Assert.AreEqual(100 - MageUltDmg, e2.Hp);
    }

    // ---------- gating：能量不足 / 大招未满 ----------
    [Test]
    public void Skill_NotEnoughEnergy_DoesNothing()
    {
        var caster = P("sword", 100);
        caster.Energy = SkillEnergyCost - 1;
        var target = E("e1", 100);
        var ctx = Ctx(caster, target);

        SkillResolver.Resolve(DamageSkill("sword_skill", "physical", SwordSkillDmg), ctx, caster, target,
            new List<Unit> { caster }, new List<Unit> { target });

        Assert.AreEqual(SkillEnergyCost - 1, caster.Energy, "能量不足不扣");
        Assert.AreEqual(0, caster.Ult, "能量不足不攒大");
        Assert.AreEqual(100, target.Hp, "能量不足不打");
    }

    [Test]
    public void Ult_NotFull_DoesNothing()
    {
        var caster = P("sword", 100);
        caster.Ult = MaxUltFull - 1;
        var target = E("e1", 100);
        var ctx = Ctx(caster, target);

        SkillResolver.Resolve(DamageUlt("sword_ult", "physical", SwordUltDmg), ctx, caster, target,
            new List<Unit> { caster }, new List<Unit> { target });

        Assert.AreEqual(MaxUltFull - 1, caster.Ult, "大招未满不触发不消耗");
        Assert.AreEqual(100, target.Hp, "大招未满不打");
    }

    // ---------- 集成：从 StreamingAssets 加载三角色全部技能跑通 ----------
    [Test]
    public void StreamingAssets_Characters_AllSkills_Resolve()
    {
        var result = ConfigLoader.LoadCharacters();
        Assert.IsEmpty(result.errors);
        Assert.AreEqual(3, result.items.Count);

        foreach (var cfg in result.items)
        {
            var unit = new Unit(cfg.id, Team.Player)
            {
                MaxHp = cfg.maxHp, Hp = cfg.maxHp,
                MaxEnergy = cfg.maxEnergy, MaxUlt = cfg.maxUlt
            };
            var allies = new List<Unit> { unit };
            var enemies = new List<Unit> { E("e1", 100), E("e2", 100) };
            var ctx = Ctx(unit, enemies[0], enemies[1]);

            foreach (var skill in cfg.skills)
            {
                if (skill.type == "skill") unit.Energy = cfg.maxEnergy;
                if (skill.type == "ult") unit.Ult = cfg.maxUlt;
                Assert.DoesNotThrow(
                    () => SkillResolver.Resolve(skill, ctx, unit, enemies[0], allies, enemies),
                    cfg.id + "/" + skill.id);
            }
        }
    }
}