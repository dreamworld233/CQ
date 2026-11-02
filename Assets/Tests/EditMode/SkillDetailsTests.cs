using System;
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

public class SkillDetailsTests
{
    private GameObject _owner;
    private EventSystem _createdEventSystem;
    private BattleUi _ui;
    private CharacterConfig Sword => ConfigLoader.LoadCharacters().items.Single(character => character.id == "sword");

    [SetUp]
    public void SetUp()
    {
        bool hadSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null;
        _owner = new GameObject("SkillDetailsTest");
        _ui = BattleUi.Build(_owner.transform);
        if (_ui.CanvasRect == null)
            typeof(BattleUi).GetMethod("BuildAll", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_ui, null);
        if (!hadSystem) _createdEventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(_owner);
        if (_createdEventSystem != null) UnityEngine.Object.DestroyImmediate(_createdEventSystem.gameObject);
    }

    [Test]
    public void SummariesKeepFunctionAndTargetsWithoutNumbersOrDetailedTiming()
    {
        var chars = ConfigLoader.LoadCharacters().items;
        var guard = chars.Single(character => character.id == "guard");
        var mage = chars.Single(character => character.id == "mage");
        StringAssert.Contains("连续攻击前排优先", SkillDescription.Summary(Sword.skills.Single(skill => skill.type == "special")));
        StringAssert.Contains("后排优先", SkillDescription.Summary(Sword.skills.Single(skill => skill.type == "ult")));
        StringAssert.Contains("生命比例最低", SkillDescription.Summary(guard.skills.Single(skill => skill.type == "ult")));
        StringAssert.Contains("常驻：提高自身的防御", SkillDescription.Summary(guard.skills.Single(skill => skill.type == "passive")));
        StringAssert.Contains("每回合：为能量最低的队友补充能量", SkillDescription.Summary(mage.skills.Single(skill => skill.type == "passive")));
        var all = chars.Concat(ConfigLoader.LoadEnemies().items.Cast<CharacterConfig>());
        foreach (var skill in all.SelectMany(character => character.skills))
        {
            string summary = SkillDescription.Summary(skill);
            Assert.IsFalse(summary.Any(char.IsDigit), skill.id);
            Assert.IsFalse(summary.Contains("%") || summary.Contains("行动机会") || summary.Contains("暂无"), skill.id);
        }
    }

    [Test]
    public void ThreeSkillsWithoutBasicOrTriggerRows_SwitchTeamsWithoutStaleContent()
    {
        Assert.IsFalse(_ui.Skills.Visible);
        Show(Sword);
        Assert.AreEqual("sword", _ui.Skills.UnitId);
        Assert.AreEqual(3, _ui.Skills.GetComponentsInChildren<TextMeshProUGUI>().Count(text => text.name == "SkillEffects"));
        Assert.IsNull(_ui.Skills.transform.Find("SkillCard/Skill_basic"));
        Assert.IsFalse(_ui.Skills.GetComponentsInChildren<TextMeshProUGUI>().Any(text => text.name == "SkillTrigger"));
        Assert.AreEqual("剑士", Text("SkillCard/UnitTitle").text);
        var enemy = ConfigLoader.LoadEnemies().items.Single(character => character.id == "lord");
        Show(enemy, true);
        StringAssert.Contains("敌方", Text("SkillCard/UnitTitle").text);
        StringAssert.Contains("威势", Text("SkillCard/Skill_passive/SkillName").text);
        Assert.IsFalse(_ui.Skills.GetComponentsInChildren<TextMeshProUGUI>().Any(text => text.text.Contains("疾风斩")));
    }

    [Test]
    public void BubbleHasNoCloseButtonOrFullScreenBlocker_AndExpiresAutomatically()
    {
        Show(Sword);
        Assert.IsNull(_ui.Skills.GetComponent<Image>());
        Assert.IsNull(_ui.Skills.GetComponent<Button>());
        Assert.IsNull(_ui.Skills.transform.Find("SkillCard/CloseSkillDetails"));
        _ui.Skills.transform.Find("SkillCard").GetComponent<Button>().onClick.Invoke();
        _ui.Skills.AdvanceLifetime(SkillDetailsView.DisplayDuration - 0.1f);
        Assert.IsTrue(_ui.Skills.Visible);
        _ui.Skills.AdvanceLifetime(0.2f);
        Assert.IsFalse(_ui.Skills.Visible);
        Assert.IsNull(_ui.Skills.UnitId);
    }

    [Test]
    public void SwitchingOrReopeningRestartsLifetime_AndNeverCreatesAnotherBubble()
    {
        Show(Sword);
        _ui.Skills.AdvanceLifetime(4f);
        var enemy = ConfigLoader.LoadEnemies().items.First();
        Show(enemy, true);
        _ui.Skills.AdvanceLifetime(4f);
        Assert.IsTrue(_ui.Skills.Visible);
        Assert.AreEqual(enemy.id, _ui.Skills.UnitId);
        _ui.Skills.AdvanceLifetime(1f);
        Assert.IsFalse(_ui.Skills.Visible);
        Show(Sword);
        _ui.Skills.AdvanceLifetime(-10f);
        _ui.Skills.AdvanceLifetime(4.9f);
        Assert.IsTrue(_ui.Skills.Visible);
        Assert.AreEqual(1, _ui.GetComponentsInChildren<SkillDetailsView>(true).Length);
        _ui.Skills.AdvanceLifetime(0.2f);
        Assert.IsFalse(_ui.Skills.Visible);
    }

    [Test]
    public void BubbleFadesInAndOutTogether_AndRetiringBubbleDoesNotBlockInput()
    {
        Show(Sword);
        var group = _ui.Skills.GetComponent<CanvasGroup>();
        Assert.AreEqual(0f, group.alpha);
        Assert.IsFalse(group.blocksRaycasts);
        _ui.Skills.AdvanceLifetime(SkillDetailsView.FadeInDuration * 0.5f);
        Assert.AreEqual(0.5f, group.alpha, 0.001f);
        _ui.Skills.AdvanceLifetime(SkillDetailsView.FadeInDuration * 0.5f);
        Assert.AreEqual(1f, group.alpha, 0.001f);
        Assert.IsTrue(group.blocksRaycasts);
        _ui.Skills.AdvanceLifetime(SkillDetailsView.DisplayDuration - SkillDetailsView.FadeInDuration - SkillDetailsView.FadeOutDuration * 0.5f);
        Assert.IsTrue(_ui.Skills.Visible);
        Assert.AreEqual(0.5f, group.alpha, 0.001f);
        Assert.IsFalse(group.blocksRaycasts);
        _ui.Skills.AdvanceLifetime(SkillDetailsView.FadeOutDuration);
        Assert.IsFalse(_ui.Skills.Visible);
        Assert.AreEqual(0f, group.alpha);
        Assert.IsNull(_ui.Skills.UnitId);
    }

    [Test]
    public void ReopeningDuringFadeOutResetsAnimation_AndImmediateHideLeavesNoGhost()
    {
        Show(Sword);
        _ui.Skills.AdvanceLifetime(4.9f);
        var group = _ui.Skills.GetComponent<CanvasGroup>();
        Assert.Less(group.alpha, 1f);
        var enemy = ConfigLoader.LoadEnemies().items.First();
        Show(enemy, true);
        Assert.AreEqual(0f, group.alpha);
        _ui.Skills.AdvanceLifetime(0.2f);
        Assert.IsTrue(_ui.Skills.Visible);
        Assert.AreEqual(enemy.id, _ui.Skills.UnitId);
        Assert.AreEqual(1f, group.alpha);
        _ui.ResetBattleFeedback();
        Assert.IsFalse(_ui.Skills.Visible);
        Assert.AreEqual(0f, group.alpha);
        Assert.IsFalse(group.blocksRaycasts);
        _ui.Skills.AdvanceLifetime(10f);
        Assert.IsFalse(_ui.Skills.Visible);
        Show(Sword);
        _ui.Skills.AdvanceLifetime(0.2f);
        Assert.AreEqual(1f, group.alpha);
    }

    [Test]
    public void RoundedSpriteIsSharedAndSliced_WhileFullScreenOverlaysRemainSquare()
    {
        Sprite sprite = UiKit.RoundedRectSprite();
        Assert.AreSame(sprite, UiKit.RoundedRectSprite());
        Assert.AreEqual(Vector4.one * 13f, sprite.border);
        Assert.AreEqual(100f, sprite.pixelsPerUnit);
        Assert.AreEqual(0f, sprite.texture.GetPixel(0, 0).a);
        Assert.AreEqual(1f, sprite.texture.GetPixel(32, 32).a);
        Assert.AreEqual(1f, sprite.texture.GetPixel(0, 32).a);
        foreach (var button in _ui.GetComponentsInChildren<Button>(true))
        {
            var image = button.GetComponent<Image>();
            Assert.AreSame(sprite, image.sprite, button.name);
            Assert.AreEqual(Image.Type.Sliced, image.type, button.name);
            Assert.AreEqual(0f, image.alphaHitTestMinimumThreshold, "保留易点击的矩形热区");
        }
        Assert.AreSame(sprite, _ui.CanvasRect.Find("ResultPanel/ResultCard").GetComponent<Image>().sprite);
        Assert.IsNull(_ui.CanvasRect.Find("ResultPanel").GetComponent<Image>().sprite);
        Assert.IsNull(_ui.ErrorPanel.GetComponent<Image>().sprite);
    }

    [TestCase(false, true, true)]
    [TestCase(false, false, false)]
    [TestCase(true, true, false)]
    [TestCase(true, false, true)]
    public void SidePreferenceMirrorsFrontAndBack(bool enemy, bool front, bool right) =>
        Assert.AreEqual(right, SkillDetailsView.PreferRight(enemy, front));

    [Test]
    public void BubbleFollowsAnchorAndFlipsAtEdges_WhileStayingInsideBattleViewport()
    {
        Show(Sword);
        var bounds = new Rect(-600f, -400f, 1200f, 800f);
        var card = (RectTransform)_ui.Skills.transform.Find("SkillCard");
        _ui.Skills.PlaceNear(new Vector2(-50f, 30f), new Vector2(50f, 30f), true, bounds);
        Assert.IsTrue(_ui.Skills.OnRight);
        Assert.Greater(card.anchoredPosition.x, 50f);
        float previousY = card.anchoredPosition.y;
        _ui.Skills.PlaceNear(new Vector2(-50f, 100f), new Vector2(50f, 100f), true, bounds);
        Assert.AreEqual(previousY + 70f, card.anchoredPosition.y);
        _ui.Skills.PlaceNear(new Vector2(480f, 390f), new Vector2(560f, 390f), true, bounds);
        Assert.IsFalse(_ui.Skills.OnRight);
        Assert.GreaterOrEqual(card.anchoredPosition.x, bounds.xMin);
        Assert.LessOrEqual(card.anchoredPosition.x + card.rect.width, bounds.xMax);
        Assert.LessOrEqual(card.anchoredPosition.y + card.rect.height, bounds.yMax);
        _ui.Skills.PlaceNear(new Vector2(-560f, -390f), new Vector2(-480f, -390f), false, bounds);
        Assert.IsTrue(_ui.Skills.OnRight);
        Assert.GreaterOrEqual(card.anchoredPosition.y, bounds.yMin);
    }

    [Test]
    public void SetupSpreadOnlyChangesPresentation_NotSlotsOrMirroring()
    {
        var layout = new BattleLayout();
        foreach (Team team in new[] { Team.Player, Team.Enemy })
            foreach (string slot in new[] { "front1", "front2", "back1", "back2", "back3" })
            {
                layout.setupSpread = 0f;
                Vector3 original = PositioningView.Position(team, slot, layout);
                layout.setupSpread = 1.15f;
                Vector3 setup = PositioningView.Position(team, slot, layout);
                Assert.AreEqual(original.x + (team == Team.Player ? -1.15f : 1.15f), setup.x, 0.0001f);
                Assert.AreEqual(original.y, setup.y);
                Assert.AreEqual(original.z, setup.z);
                layout.setupSpread = 0f;
                Assert.AreEqual(original, PositioningView.Position(team, slot, layout));
            }
    }

    [Test]
    public void InspectionDoesNotChangeTimeScaleOrBattle_AndResetResultErrorCloseIt()
    {
        float timeScale = Time.timeScale;
        var engine = AutoBattleTestData.Engine();
        engine.StartBattle();
        int round = engine.Round;
        Show(Sword);
        Assert.AreEqual(timeScale, Time.timeScale);
        Assert.AreEqual(round, engine.Round);
        Assert.AreEqual(BattleFlow.Resolving, engine.Flow);
        _ui.ResetBattleFeedback();
        Assert.IsFalse(_ui.Skills.Visible);
        Show(Sword);
        _ui.ShowResult("我方胜利", "完成", Array.Empty<UnitBattleStatistics>(), "", false);
        Assert.IsFalse(_ui.Skills.Visible);
        _ui.HideResult();
        Show(Sword);
        _ui.ShowError("配置错误");
        Assert.IsFalse(_ui.Skills.Visible);
    }

    [Test]
    public void ControllerRejectsBattleInspectionAndClearsStaleBubble()
    {
        var controller = _owner.AddComponent<BattleController>();
        var engine = AutoBattleTestData.Engine();
        engine.StartBattle();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(BattleController).GetField("_engine", flags).SetValue(controller, engine);
        typeof(BattleController).GetField("_ui", flags).SetValue(controller, _ui);
        Show(Sword);
        typeof(BattleController).GetMethod("InspectUnit", flags).Invoke(controller, new object[] { "sword" });
        Assert.IsFalse(_ui.Skills.Visible);
        Show(Sword);
        typeof(BattleController).GetMethod("UpdateSkillBubble", flags).Invoke(controller, null);
        Assert.IsFalse(_ui.Skills.Visible);
        Assert.IsNull(_ui.Skills.UnitId);
    }

    [Test]
    public void AllConfiguredSkillLabelsFitCompactBubble()
    {
        var all = ConfigLoader.LoadCharacters().items.Concat(ConfigLoader.LoadEnemies().items.Cast<CharacterConfig>());
        foreach (var character in all)
        {
            Show(character);
            Canvas.ForceUpdateCanvases();
            foreach (var text in _ui.Skills.GetComponentsInChildren<TextMeshProUGUI>())
            {
                text.ForceMeshUpdate();
                Assert.IsFalse(text.isTextOverflowing, character.id + "/" + text.name + ": " + text.text);
            }
        }
    }

    private void Show(CharacterConfig character, bool enemy = false) => _ui.Skills.Show(character.id, character.name, enemy, character.skills);
    private TextMeshProUGUI Text(string path) => _ui.Skills.transform.Find(path).GetComponent<TextMeshProUGUI>();
}
