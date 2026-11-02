using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CQ.Core.Battle;
using CQ.Core.Combat;
using CQ.Runtime;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class HealthInspectionTests
{
    [TestCase(Team.Player)]
    [TestCase(Team.Enemy)]
    public void HealthNumberIsHiddenUntilRequested_AndFadesLikeSkillTips(Team team)
    {
        var owner = new GameObject("HealthFadeTest");
        try
        {
            var unit = new Unit("sword", team) { Name = "剑士", Hp = 80, MaxHp = 100, MaxUlt = 100 };
            var view = owner.AddComponent<UnitView>();
            view.Setup(unit, Color.white);
            var hp = owner.transform.Find("Hud/HpText").GetComponent<TextMeshPro>();
            Assert.IsFalse(view.HealthVisible);
            view.ShowHealth();
            Assert.IsTrue(view.HealthVisible);
            Assert.AreEqual("80/100", hp.text);
            Assert.AreEqual(0f, hp.alpha);
            float expectedHeight = Mathf.Min(0.32f, (1.9f - 0.12f) * hp.textBounds.size.y / hp.textBounds.size.x);
            Assert.AreEqual(expectedHeight, hp.textBounds.size.y * hp.transform.localScale.y, 0.001f,
                "文字局部包围盒定标，目标字高同时受血条宽度约束");
            var previousSize = TmpKit.MakeWorldText(owner.transform, "PreviousHpSize", Vector3.zero,
                UiKit.FontBold, 0.20f, Color.white, 56, FontStyles.Normal, "80/100");
            Assert.Greater(hp.GetComponent<Renderer>().bounds.size.y,
                previousSize.GetComponent<Renderer>().bounds.size.y * 1.2f, "同字体同数字的实际字形比旧尺寸更大");
            view.AdvanceHealthLifetime(SkillDetailsView.FadeInDuration * 0.5f);
            Assert.AreEqual(0.5f, hp.alpha, 0.001f);
            view.AdvanceHealthLifetime(SkillDetailsView.FadeInDuration * 0.5f);
            Assert.AreEqual(1f, hp.alpha, 0.001f);
            view.AdvanceHealthLifetime(SkillDetailsView.DisplayDuration - SkillDetailsView.FadeInDuration - SkillDetailsView.FadeOutDuration * 0.5f);
            Assert.IsTrue(view.HealthVisible);
            Assert.AreEqual(0.5f, hp.alpha, 0.001f);
            view.AdvanceHealthLifetime(SkillDetailsView.FadeOutDuration);
            Assert.IsFalse(view.HealthVisible);
            Assert.AreEqual(0f, hp.alpha);
            Assert.IsFalse(hp.raycastTarget);
        }
        finally { Object.DestroyImmediate(owner); }
    }

    [Test]
    public void LiveHpRefreshPreservesLifetimeAndAlpha_AndLargeNumbersStayInsideBar()
    {
        var owner = new GameObject("LiveHealthTest");
        try
        {
            var unit = new Unit("sword", Team.Player) { Name = "剑士", Hp = 1000000, MaxHp = 1000000, MaxUlt = 100 };
            var view = owner.AddComponent<UnitView>();
            view.Setup(unit, Color.white);
            view.ShowHealth();
            view.AdvanceHealthLifetime(SkillDetailsView.FadeInDuration * 0.5f);
            var hp = owner.transform.Find("Hud/HpText").GetComponent<TextMeshPro>();
            Assert.LessOrEqual(hp.textBounds.size.x * hp.transform.localScale.x, 1.9f - 0.12f + 0.001f);
            unit.Hp = 50;
            view.Refresh(unit);
            Assert.AreEqual("50/1000000", hp.text);
            Assert.AreEqual(0.5f, hp.alpha, 0.001f, "数据刷新不能重播淡入");
            view.AdvanceHealthLifetime(SkillDetailsView.DisplayDuration - SkillDetailsView.FadeInDuration * 0.5f - 0.05f);
            unit.Hp = 0;
            view.Refresh(unit);
            Assert.AreEqual("0/1000000", hp.text);
            Assert.IsTrue(view.HealthVisible, "死亡时仍公开0HP，不伪造复活或隐藏查询结果");
            view.AdvanceHealthLifetime(0.06f);
            Assert.IsFalse(view.HealthVisible, "数据更新不延长寿命");
        }
        finally { Object.DestroyImmediate(owner); }
    }

    [Test]
    public void ReopeningRestartsFadeAndLifetime_AndDisableCancelsIt()
    {
        var owner = new GameObject("HealthReopenTest");
        try
        {
            var view = owner.AddComponent<UnitView>();
            view.Setup(new Unit("sword", Team.Player) { Hp = 100, MaxHp = 100, MaxUlt = 100 }, Color.white);
            var hp = owner.transform.Find("Hud/HpText").GetComponent<TextMeshPro>();
            view.ShowHealth();
            view.AdvanceHealthLifetime(4.9f);
            view.ShowHealth();
            Assert.AreEqual(0f, hp.alpha);
            view.AdvanceHealthLifetime(-10f);
            view.AdvanceHealthLifetime(4f);
            Assert.IsTrue(view.HealthVisible);
            Assert.AreEqual(1, owner.GetComponentsInChildren<TextMeshPro>(true).Count(text => text.name == "HpText"));
            typeof(UnitView).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
            Assert.IsFalse(view.HealthVisible);
            view.AdvanceHealthLifetime(10f);
            Assert.IsFalse(view.HealthVisible);
        }
        finally { Object.DestroyImmediate(owner); }
    }

    [Test]
    public void BattleInspectionOnlyShowsLatestPresentedHealth_WithoutPausingOrOpeningSkills()
    {
        var owner = new GameObject("HealthControllerTest");
        var previous = EventSystem.current;
        float timeScale = Time.timeScale;
        try
        {
            var engine = AutoBattleTestData.Engine(heroes: new[] { AutoBattleTestData.Hero("sword") });
            var controller = owner.AddComponent<BattleController>();
            var ui = BattleUi.Build(owner.transform);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            if (ui.CanvasRect == null) typeof(BattleUi).GetMethod("BuildAll", flags).Invoke(ui, null);
            typeof(BattleController).GetField("_engine", flags).SetValue(controller, engine);
            typeof(BattleController).GetField("_ui", flags).SetValue(controller, ui);
            var views = (Dictionary<string, UnitView>)typeof(BattleController).GetField("_views", flags).GetValue(controller);
            foreach (var unit in engine.Ctx.Units)
            {
                var go = new GameObject(unit.Id);
                go.transform.SetParent(owner.transform);
                var view = go.AddComponent<UnitView>();
                view.Setup(unit, Color.white);
                views.Add(unit.Id, view);
            }
            var enemy = engine.Enemies[0];
            string initial = enemy.Hp + "/" + enemy.MaxHp;
            engine.StartBattle();
            BattleTestDriver.NextAction(engine, "sword");
            Assert.Less(enemy.Hp, enemy.MaxHp);
            int count = engine.ActionResults.Count;
            var inspect = typeof(BattleController).GetMethod("InspectUnit", flags);
            inspect.Invoke(controller, new object[] { enemy.Id });
            Assert.IsTrue(views[enemy.Id].HealthVisible);
            Assert.AreEqual(initial, views[enemy.Id].transform.Find("Hud/HpText").GetComponent<TextMeshPro>().text,
                "接敌展示前不能泄露Core已经计算的未来扣血");
            typeof(BattleController).GetMethod("RefreshWorld", flags).Invoke(controller, null);
            Assert.AreEqual(enemy.Hp + "/" + enemy.MaxHp, views[enemy.Id].transform.Find("Hud/HpText").GetComponent<TextMeshPro>().text);
            inspect.Invoke(controller, new object[] { "sword" });
            Assert.IsTrue(views["sword"].HealthVisible);
            Assert.IsFalse(views[enemy.Id].HealthVisible, "切换角色只保留一份可见数字");
            Assert.IsFalse(ui.Skills.Visible, "战中仍禁止技能tips");
            Assert.AreEqual(timeScale, Time.timeScale);
            Assert.AreEqual(count, engine.ActionResults.Count);
            Assert.AreEqual(BattleFlow.Resolving, engine.Flow);
            inspect.Invoke(controller, new object[] { null });
            Assert.IsFalse(views["sword"].HealthVisible);
            inspect.Invoke(controller, new object[] { "sword" });
            typeof(BattleController).GetMethod("StopDriver", flags).Invoke(controller, null);
            Assert.IsFalse(views["sword"].HealthVisible);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            if (previous == null && EventSystem.current != null) Object.DestroyImmediate(EventSystem.current.gameObject);
        }
    }
}
