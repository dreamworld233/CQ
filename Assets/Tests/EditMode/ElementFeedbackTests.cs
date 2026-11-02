using System.Linq;
using System.Reflection;
using CQ.Core.Battle;
using CQ.Core.Combat;
using CQ.Core.Config;
using CQ.Runtime;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ElementFeedbackTests
{
    [TestCase(Team.Player, "metal")]
    [TestCase(Team.Enemy, "fire")]
    [TestCase(Team.Player, "")]
    public void HudHidesExactHpForBothTeams_AndPlacesElementLeftOfBar(Team team, string element)
    {
        var owner = new GameObject("ElementHudTest");
        try
        {
            var unit = new Unit("sword", team) { Name = "剑士", Element = element, Hp = 25, MaxHp = 100, MaxUlt = 100 };
            var view = owner.AddComponent<UnitView>();
            view.Setup(unit, Color.white);
            var hud = owner.transform.Find("Hud");
            var hp = hud.Find("HpText").GetComponent<TextMeshPro>();
            Assert.IsFalse(hp.gameObject.activeSelf);
            Assert.AreEqual("25/100", hp.text);
            Assert.AreEqual((1.9f - 0.12f) * 0.25f, hud.Find("HpFill").localScale.x, 0.0001f);
            Assert.IsTrue((bool)typeof(UnitView).GetField("_lowHp", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view));
            var badge = hud.Find("ElementBadge");
            Assert.AreEqual(CharacterElements.IsValid(element), badge.gameObject.activeSelf);
            if (badge.gameObject.activeSelf)
            {
                var icon = badge.Find("ElementIcon").GetComponent<SpriteRenderer>();
                Assert.AreEqual(RosterView.ElementColor(element), icon.color);
                Assert.Less(badge.GetComponent<SpriteRenderer>().bounds.max.x, hud.Find("HpBorder").GetComponent<SpriteRenderer>().bounds.min.x);
                Assert.AreEqual(hud.Find("HpBg").position.y, badge.position.y, 0.0001f);
            }
            unit.Hp = 80;
            view.Refresh(unit);
            Assert.AreEqual("80/100", hp.text);
            Assert.IsFalse(view.HealthVisible);
        }
        finally { Object.DestroyImmediate(owner); }
    }

    [TestCase("metal")]
    [TestCase("wood")]
    [TestCase("water")]
    [TestCase("fire")]
    [TestCase("earth")]
    public void ElementDamageColorsKeepIdentity_AndBrightenOrDarkenWithRelation(string element)
    {
        var normal = ElementPresentation.DamageColor(element, 100);
        var counter = ElementPresentation.DamageColor(element, 200);
        var resisted = ElementPresentation.DamageColor(element, 50);
        Assert.AreEqual(RosterView.ElementColor(element), normal);
        Assert.Greater(Luminance(counter), Luminance(normal), "克制必须比普通更亮，包括金属性");
        Assert.Less(Luminance(resisted), Luminance(normal));
        Assert.AreEqual(1f, resisted.a);
        Assert.AreEqual("克制", ElementPresentation.RelationLabel(200));
        Assert.AreEqual("抵抗", ElementPresentation.RelationLabel(50));
        Assert.AreEqual("", ElementPresentation.RelationLabel(100));
    }

    private static float Luminance(Color color) => color.linear.r * 0.2126f + color.linear.g * 0.7152f + color.linear.b * 0.0722f;

    [TestCase("metal", "wood", 200, "克制")]
    [TestCase("metal", "fire", 50, "抵抗")]
    [TestCase("metal", "water", 100, "")]
    [TestCase("", "wood", 100, "")]
    public void HitSnapshotsKeepElementAndRelation_AfterUnitsChange(string source, string target, int multiplier, string label)
    {
        var hero = AutoBattleTestData.Hero(attack: 10);
        var enemy = AutoBattleTestData.Enemy();
        hero.element = source;
        enemy.element = target;
        var engine = AutoBattleTestData.Engine(heroes: new[] { hero }, enemies: new[] { enemy });
        engine.StartBattle();
        var result = BattleTestDriver.NextAction(engine, "hero");
        var hit = result.Hits.Single();
        Assert.AreEqual(source, hit.SourceElement);
        Assert.AreEqual(multiplier, hit.ElementMultiplierPercent);
        engine.Players[0].Element = "fire";
        engine.Enemies[0].Element = "metal";
        Assert.AreEqual(source, hit.SourceElement);
        Assert.AreEqual(multiplier, hit.ElementMultiplierPercent);
        var owner = new GameObject("ElementFloatTest");
        try
        {
            var view = owner.AddComponent<UnitView>();
            view.Setup(engine.Enemies[0], Color.white);
            view.PlayDamageHits(new[] { hit.HpDamage, hit.HpDamage }, false, hit.SourceElement, hit.ElementMultiplierPercent);
            view.AdvanceDamageFeedback(UnitView.HitInterval + 0.001f);
            var floats = owner.GetComponentsInChildren<FloatingText>();
            Assert.AreEqual(2, floats.Length);
            foreach (var number in floats)
            {
                var text = number.GetComponent<TextMeshPro>();
                Assert.AreEqual("-" + hit.HpDamage, text.text);
                Assert.AreEqual(ElementPresentation.DamageColor(source, multiplier), text.color);
                var relation = number.transform.Find("Relation");
                Assert.AreEqual(label != "", relation != null);
                if (relation != null)
                {
                    var relationText = relation.GetComponent<TextMeshPro>();
                    Assert.AreEqual(label, relationText.text);
                    Assert.Greater(relationText.GetComponent<Renderer>().bounds.min.y, text.GetComponent<Renderer>().bounds.max.y);
                }
            }
        }
        finally { Object.DestroyImmediate(owner); }
    }

    [Test]
    public void PentagonMatchesCoreCounterCycle_IsPassiveAndShowsRoundLimit()
    {
        var owner = new GameObject("ElementGuideTest");
        var previous = EventSystem.current;
        try
        {
            var ui = BattleUi.Build(owner.transform);
            if (ui.CanvasRect == null) typeof(BattleUi).GetMethod("BuildAll", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);
            var guide = ui.Elements.transform;
            var guideRect = (RectTransform)guide;
            Assert.AreEqual(Vector2.one, guideRect.anchorMin);
            Assert.AreEqual(Vector2.one, guideRect.anchorMax);
            Assert.AreEqual(Vector2.one, guideRect.pivot);
            Assert.Less(guideRect.anchoredPosition.x, 0f);
            Assert.Less(guideRect.anchoredPosition.y, 0f);
            Assert.IsNull(guide.Find("Multipliers"), "克制图不再显示倍率数字");
            Assert.IsNull(guide.Find("Direction"), "克制图只保留图形与五行名称");
            var cycle = CharacterElements.CounterCycle;
            for (int i = 0; i < cycle.Count; i++)
            {
                string source = cycle[i], target = cycle[(i + 1) % cycle.Count];
                Assert.AreEqual(200, CharacterElements.DamageMultiplierPercent(source, target));
                var a = (RectTransform)guide.Find("Node_" + source);
                var b = (RectTransform)guide.Find("Node_" + target);
                Assert.AreEqual(57f, (a.anchoredPosition - new Vector2(0, -130)).magnitude, 0.001f);
                Assert.AreEqual(67.01f, Vector2.Distance(a.anchoredPosition, b.anchoredPosition), 0.1f);
                Assert.IsNotNull(guide.Find("Arrow_" + source + "_" + target));
                Assert.AreEqual(CharacterElements.Label(source), guide.Find("Label_" + source).GetComponent<TextMeshProUGUI>().text);
            }
            Assert.IsFalse(guide.GetComponentsInChildren<Graphic>().Any(graphic => graphic.raycastTarget), "克制图不能阻挡战场点击/拖动");
            Assert.IsNull(guide.Find("Round"), "回合计数不再归属克制图");
            var round = ui.CanvasRect.Find("RoundCounter").GetComponent<TextMeshProUGUI>();
            var roundRect = (RectTransform)round.transform;
            Assert.AreEqual(new Vector2(0.5f, 1f), roundRect.anchorMin);
            Assert.AreEqual(new Vector2(0.5f, 1f), roundRect.anchorMax);
            Assert.AreEqual(0f, roundRect.anchoredPosition.x);
            Assert.GreaterOrEqual(round.fontSize, 36f);
            Assert.IsFalse(round.raycastTarget);
            var engine = AutoBattleTestData.Engine();
            engine.StartBattle();
            ui.RefreshRound(engine);
            Assert.AreEqual("1/20", round.text);
            ui.ResetBattleFeedback();
            Assert.AreEqual("1/20", round.text);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            if (previous == null && EventSystem.current != null) Object.DestroyImmediate(EventSystem.current.gameObject);
        }
    }
}
