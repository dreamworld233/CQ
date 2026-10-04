using NUnit.Framework;
using CQ.Core.Config;
using CQ.Runtime;

/// <summary>表现层素材接线回归：配置 id/意图 type 都能取到图，缺素材时调用方有回退。</summary>
public class ArtLibraryTests
{
    [Test]
    public void Unit_Sprites_Load_For_All_Ids()
    {
        foreach (var id in new[] { "sword", "mage", "guard", "grunt", "claw", "lord" })
            Assert.IsNotNull(ArtLibrary.UnitSprite(id), id + " 缺示意图");
        Assert.IsNotNull(ArtLibrary.UnitSprite("grunt_0"), "敌人实例 id 应剥后缀");
        Assert.IsNotNull(ArtLibrary.UnitSprite("lord_2"), "lord 暂复用精英怪，也应能取到");
    }

    [Test]
    public void Intent_Icons_Load_For_All_Types()
    {
        foreach (var t in new[] { "attack", "multi", "charge", "attackUp", "debuff" })
            Assert.IsNotNull(ArtLibrary.IntentSprite(t), t + " 缺意图图标");
    }

    [Test]
    public void Ring_And_Skill_Placeholder_Icons_Load()
    {
        Assert.IsNotNull(ArtLibrary.RingSprite(), "缺 按钮选中框（选中圆环）");

        var dmg = new SkillSpec { id = "d", damage = new DamageSpec { amount = 1 } };
        var heal = new SkillSpec { id = "h", heal = new HealSpec { amount = 1 } };
        Assert.IsNotNull(ArtLibrary.SkillIcon(dmg), "伤害技能应有占位图标（攻击）");
        Assert.IsNotNull(ArtLibrary.SkillIcon(heal), "治疗技能应有占位图标（增益）");
        Assert.IsNull(ArtLibrary.SkillIcon(new SkillSpec { id = "x" }), "无伤害/无治疗不应给图标");
    }
}