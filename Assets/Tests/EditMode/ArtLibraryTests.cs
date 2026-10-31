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

    [TestCase("damage")]
    [TestCase("heal")]
    [TestCase("attackUp")]
    [TestCase("defenseUp")]
    [TestCase("energy")]
    [TestCase("stun")]
    public void Skill_Placeholder_Icons_Load_For_Current_Effects(string type)
    {
        var skill = new SkillSpec { id = "skill", effects = new System.Collections.Generic.List<SkillEffectSpec>
            { new SkillEffectSpec { type = type } } };
        Assert.IsNotNull(ArtLibrary.SkillIcon(skill), type);
    }

    [Test]
    public void Ring_And_Empty_Skill_Icon_Behavior_Are_Preserved()
    {
        Assert.IsNotNull(ArtLibrary.RingSprite(), "缺 按钮选中框（选中圆环）");
        Assert.IsNull(ArtLibrary.SkillIcon(new SkillSpec { id = "empty" }));
        Assert.IsNull(ArtLibrary.SkillIcon(null));
    }
}
