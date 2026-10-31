using System.Collections.Generic;
using System.Globalization;
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

public class BattlePresentationTests
{
    private GameObject _owner;
    private EventSystem _createdEventSystem;
    private BattleUi _ui;

    [SetUp]
    public void SetUp()
    {
        var previousEventSystem = Object.FindFirstObjectByType<EventSystem>();
        _owner = new GameObject("BattlePresentationTest");
        _ui = BattleUi.Build(_owner.transform);
        if (_ui.CanvasRect == null)
            typeof(BattleUi).GetMethod("BuildAll", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_ui, null);
        if (previousEventSystem == null) _createdEventSystem = Object.FindFirstObjectByType<EventSystem>();
        Canvas.ForceUpdateCanvases();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_owner);
        if (_createdEventSystem != null) Object.DestroyImmediate(_createdEventSystem.gameObject);
    }

    [Test]
    public void SpeedButtonTogglesOneAndTwo_AndReportsEachChoiceWithoutChangingGlobalTime()
    {
        float initialTimeScale = Time.timeScale;
        int callbacks = 0;
        int observedSpeed = 0;
        _ui.OnSpeedChanged = () => { callbacks++; observedSpeed = _ui.PlaybackSpeed; };
        Assert.AreEqual(1, _ui.PlaybackSpeed);
        _ui.SpeedButton.onClick.Invoke();
        Assert.AreEqual(2, _ui.PlaybackSpeed);
        Assert.AreEqual(2, observedSpeed);
        StringAssert.Contains("2", _ui.SpeedButton.GetComponentInChildren<TextMeshProUGUI>().text);
        _ui.SpeedButton.onClick.Invoke();
        Assert.AreEqual(1, _ui.PlaybackSpeed);
        Assert.AreEqual(1, observedSpeed);
        Assert.AreEqual(2, callbacks);
        Assert.AreEqual(initialTimeScale, Time.timeScale);
    }

    [Test]
    public void BattleCanvasDoesNotBuildLegacyActionOrLevelInformation()
    {
        Assert.IsNull(_ui.CanvasRect.Find("LeftPanel"));
        Assert.IsNull(_ui.CanvasRect.Find("LevelInfo"));
        Assert.IsFalse(_ui.GetComponentsInChildren<Button>(true).Any(button => button.name == "ActionRecordButton"));
        Assert.IsFalse(_ui.GetComponentsInChildren<TextMeshProUGUI>(true).Any(text =>
            text.text == "出手顺序" || text.text == "行动记录" || text.text == "战况"));
        Assert.IsTrue(_ui.StartBattleButton.gameObject.activeInHierarchy);
        Assert.IsTrue(_ui.SpeedButton.gameObject.activeInHierarchy);
    }

    [Test]
    public void ResultOpensOnlyExplicitly_AndBarReportIsOptionalAndToggleable()
    {
        Assert.IsFalse(_ui.ResultVisible);
        Assert.IsFalse(_ui.NextLevelButton.gameObject.activeSelf);
        _ui.ShowResult("我方胜利", "本场完成", TwoUnitRows(), "下一关", true);
        Assert.IsTrue(_ui.ResultVisible);
        Assert.AreEqual("我方胜利", _ui.WinnerText.text);
        Assert.AreEqual("本场完成", _ui.SummaryText.text);
        Assert.IsFalse(Report().gameObject.activeSelf);
        Assert.IsTrue(_ui.NextLevelButton.gameObject.activeInHierarchy);
        int advances = 0;
        _ui.OnNextLevel = () => advances++;
        _ui.NextLevelButton.onClick.Invoke();
        Assert.AreEqual(1, advances);
        Button("StatisticsButton").onClick.Invoke();
        Assert.IsTrue(Report().gameObject.activeInHierarchy);
        Assert.AreEqual(2, Rows().Length);
        Button("StatisticsButton").onClick.Invoke();
        Assert.IsFalse(Report().gameObject.activeSelf);
        _ui.HideResult();
        Assert.IsFalse(_ui.ResultVisible);
        Assert.IsFalse(_ui.NextLevelButton.gameObject.activeSelf);
    }

    [TestCase("我方失败", "调整站位，再试一次。")]
    [TestCase("我方胜利", "三关完成，感谢你的指挥！")]
    public void DefeatAndFinalVictoryAllowReportAndRetry_WithoutNextLevel(string title, string summary)
    {
        _ui.ShowResult(title, summary, TwoUnitRows(), "", false);
        Assert.IsTrue(_ui.ResultVisible);
        Assert.IsFalse(_ui.NextLevelButton.gameObject.activeSelf);
        Button("StatisticsButton").onClick.Invoke();
        Assert.IsTrue(Report().gameObject.activeInHierarchy);
        int retries = 0;
        _ui.OnRestart = () => retries++;
        var retry = Button("ResultRestartButton");
        Assert.IsTrue(retry.gameObject.activeInHierarchy);
        retry.onClick.Invoke();
        Assert.AreEqual(1, retries);
    }

    [Test]
    public void ResetClosesAndClearsReport_ButPreservesSpeedAndMute()
    {
        _ui.SpeedButton.onClick.Invoke();
        _ui.SetMuted(true);
        _ui.ShowResult("我方胜利", "上一场成果", TwoUnitRows(), "下一关", true);
        Button("StatisticsButton").onClick.Invoke();
        Button("ExactValuesButton").onClick.Invoke();
        _ui.ResetBattleFeedback();
        Assert.AreEqual(2, _ui.PlaybackSpeed);
        Assert.IsTrue(_ui.Muted);
        Assert.IsFalse(_ui.ResultVisible);
        Assert.IsFalse(_ui.NextLevelButton.gameObject.activeSelf);
        Assert.IsFalse(Report().gameObject.activeSelf);
        Assert.IsEmpty(Rows());
        Assert.IsEmpty(_ui.WinnerText.text);
        Assert.IsEmpty(_ui.SummaryText.text);
        _ui.ShowResult("新一场", "新成果", TwoUnitRows(), "", false);
        Assert.IsFalse(Report().gameObject.activeSelf);
        Assert.AreEqual(2, Rows().Length);
        Assert.AreEqual("1k", Value("hero", "DamageDealt").text);
    }

    [Test]
    public void MetricColumnsHaveStableGeometryAndLabels_AcrossDifferentValues()
    {
        _ui.ShowResult("我方胜利", "本场完成", TwoUnitRows(), "下一关", true);
        Button("StatisticsButton").onClick.Invoke();
        Canvas.ForceUpdateCanvases();
        string[] metrics = { "DamageDealt", "DamageTaken", "HealingDone" };
        string[] labels = { "输出", "承伤", "治疗" };
        for (int metric = 0; metric < metrics.Length; metric++)
        {
            var header = _ui.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Single(text => text.name == "MetricHeader_" + metric);
            StringAssert.Contains(labels[metric], header.text);
            var first = (RectTransform)Row("hero").Find(metrics[metric]);
            var second = (RectTransform)Row("enemy_0").Find(metrics[metric]);
            Assert.Greater(first.rect.width, 0f);
            Assert.That(second.rect.width, Is.EqualTo(first.rect.width).Within(0.01f));
            Assert.That(second.anchoredPosition.x, Is.EqualTo(first.anchoredPosition.x).Within(0.01f));
            Assert.That(Value("hero", metrics[metric]).rectTransform.rect.width,
                Is.EqualTo(Value("enemy_0", metrics[metric]).rectTransform.rect.width).Within(0.01f));
        }
        var output = Fill("hero", "DamageDealt").color;
        var taken = Fill("hero", "DamageTaken").color;
        var healing = Fill("hero", "HealingDone").color;
        Assert.Greater(output.r, output.g);
        Assert.Greater(output.r, output.b);
        Assert.Greater(taken.b, taken.r);
        Assert.Greater(taken.b, taken.g);
        Assert.Greater(healing.g, healing.r);
        Assert.Greater(healing.g, healing.b);
    }

    [Test]
    public void BarsUseSameMaximumAcrossBothTeamsWithinEachMetric_AndZeroHasNoFill()
    {
        _ui.ShowResult("我方胜利", "本场完成", TwoUnitRows(), "", false);
        Button("StatisticsButton").onClick.Invoke();
        Canvas.ForceUpdateCanvases();
        Assert.That(VisibleRatio(Fill("hero", "DamageDealt")), Is.EqualTo(1f).Within(0.001f));
        Assert.That(VisibleRatio(Fill("enemy_0", "DamageDealt")), Is.EqualTo(0.25f).Within(0.001f));
        Assert.That(VisibleRatio(Fill("hero", "DamageTaken")), Is.EqualTo(0.25f).Within(0.001f));
        Assert.That(VisibleRatio(Fill("enemy_0", "DamageTaken")), Is.EqualTo(1f).Within(0.001f));
        Assert.That(VisibleRatio(Fill("hero", "HealingDone")), Is.EqualTo(1f).Within(0.001f));
        Assert.That(VisibleRatio(Fill("enemy_0", "HealingDone")), Is.EqualTo(0f).Within(0.001f));
        Assert.AreEqual("0", Value("enemy_0", "HealingDone").text);
    }

    [Test]
    public void ExactValuesToggleChangesLabelsWithoutChangingBarGeometry()
    {
        _ui.ShowResult("我方胜利", "本场完成", TwoUnitRows(), "", false);
        Button("StatisticsButton").onClick.Invoke();
        Canvas.ForceUpdateCanvases();
        var value = Value("hero", "DamageDealt");
        var rect = value.rectTransform.rect;
        float ratio = VisibleRatio(Fill("hero", "DamageDealt"));
        Assert.AreEqual("1k", value.text);
        Button("ExactValuesButton").onClick.Invoke();
        Assert.AreEqual("1000", value.text);
        Assert.AreEqual(rect, value.rectTransform.rect);
        Assert.That(VisibleRatio(Fill("hero", "DamageDealt")), Is.EqualTo(ratio).Within(0.001f));
        Button("ExactValuesButton").onClick.Invoke();
        Assert.AreEqual("1k", value.text);
    }

    [Test]
    public void RepeatedShowAndClearDoNotLeaveRowsFromPreviousBattle()
    {
        _ui.ShowResult("我方胜利", "本场完成", TwoUnitRows(), "", false);
        Assert.AreEqual(2, Rows().Length);
        _ui.ShowResult("我方胜利", "本场完成", SixUnitRows(), "", false);
        Assert.AreEqual(6, Rows().Length);
        Report().Clear();
        Assert.IsEmpty(Rows());
        Report().Show(TwoUnitRows());
        Assert.AreEqual(2, Rows().Length);
        Assert.IsNull(Rows().FirstOrDefault(row => row.name == "ReportRow_sword"));
    }

    [Test]
    public void SixUnitStatisticsLabelsFitWithoutTextOverflow()
    {
        _ui.ShowResult("我方胜利", "三关完成，感谢你的指挥！", SixUnitRows(), "", false);
        Button("StatisticsButton").onClick.Invoke();
        Canvas.ForceUpdateCanvases();
        Assert.AreEqual(6, Rows().Length);
        foreach (var text in Report().GetComponentsInChildren<TextMeshProUGUI>())
        {
            text.ForceMeshUpdate(true, true);
            Assert.IsFalse(text.isTextOverflowing, text.name + ": " + text.text);
        }
    }

    [TestCase(0L, "0")]
    [TestCase(999L, "999")]
    [TestCase(1000L, "1k")]
    [TestCase(1299L, "1.2k")]
    [TestCase(3000L, "3k")]
    [TestCase(999999L, "999.9k")]
    [TestCase(1000000L, "1m")]
    [TestCase(1299999L, "1.2m")]
    [TestCase(1000000000L, "1b")]
    [TestCase(2300000000L, "2.3b")]
    public void CompactValuesUseTruncatedSingleDecimalAndStableUnits(long value, string expected)
    {
        Assert.AreEqual(expected, BattleReportView.FormatValue(value));
    }

    [Test]
    public void CompactValuesUseInvariantDecimalSeparator()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.AreEqual("1.2k", BattleReportView.FormatValue(1234));
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }

    [TestCase(0L, 0L, 0f)]
    [TestCase(0L, 100L, 0f)]
    [TestCase(25L, 100L, 0.25f)]
    [TestCase(100L, 100L, 1f)]
    [TestCase(200L, 100L, 1f)]
    [TestCase(long.MaxValue, long.MaxValue, 1f)]
    public void FillRatioHandlesZeroAndLargeValuesWithoutOverflow(long value, long maximum, float expected)
    {
        Assert.That(BattleReportView.FillRatio(value, maximum), Is.EqualTo(expected).Within(0.0001f));
    }

    [Test]
    public void ResultOverlayCoversCanvasAndAcceptsRaycastsAboveUnderlyingControls()
    {
        _ui.ShowResult("我方胜利", "本场完成", TwoUnitRows(), "下一关", true);
        var overlay = (RectTransform)_ui.CanvasRect.Find("ResultPanel");
        Assert.AreEqual(Vector2.zero, overlay.anchorMin);
        Assert.AreEqual(Vector2.one, overlay.anchorMax);
        Assert.AreEqual(Vector2.zero, overlay.offsetMin);
        Assert.AreEqual(Vector2.zero, overlay.offsetMax);
        Assert.IsTrue(overlay.GetComponent<Image>().raycastTarget);
        Assert.Greater(overlay.GetSiblingIndex(), _ui.RestartButton.transform.GetSiblingIndex());
        Assert.Greater(overlay.GetSiblingIndex(), _ui.SpeedButton.transform.GetSiblingIndex());
        Canvas.ForceUpdateCanvases();
        var corners = new Vector3[4];
        overlay.GetWorldCorners(corners);
        var center = RectTransformUtility.WorldToScreenPoint(null, (corners[0] + corners[2]) * 0.5f);
        Assert.IsTrue(RectTransformUtility.RectangleContainsScreenPoint(overlay, center));
        Assert.IsTrue(overlay.GetComponent<Image>().Raycast(center, null));
        Assert.IsTrue(overlay.gameObject.activeInHierarchy);
    }

    private Button Button(string name) =>
        _ui.GetComponentsInChildren<Button>(true).Single(button => button.name == name);

    private BattleReportView Report() => _ui.GetComponentInChildren<BattleReportView>(true);

    private Transform[] Rows() => Report().GetComponentsInChildren<Transform>(true)
        .Where(child => child.name.StartsWith("ReportRow_", System.StringComparison.Ordinal)).ToArray();

    private Transform Row(string id) => Rows().Single(row => row.name == "ReportRow_" + id);

    private Image Fill(string id, string metric) => Row(id).Find(metric + "/Fill").GetComponent<Image>();

    private TextMeshProUGUI Value(string id, string metric) => Row(id).Find(metric + "/Value").GetComponent<TextMeshProUGUI>();

    private static float VisibleRatio(Image fill)
    {
        if (!fill.gameObject.activeSelf || !fill.enabled) return 0f;
        return fill.type == Image.Type.Filled ? fill.fillAmount :
            fill.rectTransform.rect.width / ((RectTransform)fill.transform.parent).rect.width;
    }

    private static IReadOnlyList<UnitBattleStatistics> TwoUnitRows()
    {
        var engine = AutoBattleTestData.Engine(heroes: new[] { AutoBattleTestData.Hero(attack: 1000) },
            enemies: new[] { AutoBattleTestData.Enemy(attack: 250) });
        Resolve(engine, engine.Players[0], AutoBattleTestData.Skill("basic", AutoBattleTestData.Effect()));
        Resolve(engine, engine.Enemies[0], AutoBattleTestData.Skill("basic", AutoBattleTestData.Effect()));
        Resolve(engine, engine.Players[0], AutoBattleTestData.Skill("basic",
            AutoBattleTestData.Effect("heal", "self", multiplier: 0, hits: 0, percent: 10)));
        return BattleStatistics.Summarize(engine);
    }

    private static IReadOnlyList<UnitBattleStatistics> SixUnitRows()
    {
        var engine = AutoBattleTestData.Engine(heroes: new[]
        {
            AutoBattleTestData.Hero("sword", "front1", attack: 3000),
            AutoBattleTestData.Hero("guard", "front2", attack: 25),
            AutoBattleTestData.Hero("mage", "back1", attack: 100)
        }, enemies: new[]
        {
            AutoBattleTestData.Enemy("lord", "front1", attack: 100),
            AutoBattleTestData.Enemy("claw", "front2"),
            AutoBattleTestData.Enemy("grunt", "back1")
        });
        Resolve(engine, engine.Players[0], AutoBattleTestData.Skill("basic", AutoBattleTestData.Effect(target: "enemyAll")));
        Resolve(engine, engine.Enemies[0], AutoBattleTestData.Skill("basic", AutoBattleTestData.Effect(target: "enemyAll")));
        Resolve(engine, engine.Players[1], AutoBattleTestData.Skill("basic",
            AutoBattleTestData.Effect("heal", "self", multiplier: 0, hits: 0, percent: 10)));
        return BattleStatistics.Summarize(engine);
    }

    private static void Resolve(BattleEngine engine, Unit actor, SkillSpec skill)
    {
        engine.Ctx.BeginAction(actor);
        engine.Ctx.StatesOf(actor.Id).BeginAction();
        engine.Ctx.CycleOf(actor.Id).Begin(false);
        SkillResolver.Resolve(engine.Ctx, actor, skill);
        engine.Ctx.CompleteAction(skill, BattleEventKind.Skill, skill.type);
    }
}
