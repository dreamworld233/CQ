using System;
using System.Collections;
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
using UnityEngine.UI;

public class BattleFeedbackTests
{
    [Test]
    public void HitFeedbackUsesActualSegmentsAtIntervals_AndPendingHitsKeepPresentationOccupied()
    {
        var owner = new GameObject("SegmentFeedbackTest");
        try
        {
            var view = owner.AddComponent<UnitView>();
            view.Setup(new Unit("grunt_0", Team.Enemy) { Hp = 100, MaxHp = 100, MaxUlt = 100, SlotId = "front1" }, Color.white);
            view.PlayDamageHits(new[] { 20, 7, 0 }, false);
            Assert.AreEqual(1, owner.GetComponentsInChildren<FloatingText>().Length);
            Assert.AreEqual("-20", owner.GetComponentInChildren<FloatingText>().GetComponent<TextMeshPro>().text);
            typeof(UnitView).GetField("_flashT", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, 0f);
            typeof(UnitView).GetField("_shakeT", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, 0f);
            Assert.IsFalse(view.FeedbackComplete, "待播段不能被提前确认");
            view.AdvanceDamageFeedback(UnitView.HitInterval * 0.5f);
            Assert.AreEqual(1, owner.GetComponentsInChildren<FloatingText>().Length);
            view.AdvanceDamageFeedback(UnitView.HitInterval * 0.5f + 0.001f);
            var floats = owner.GetComponentsInChildren<FloatingText>();
            CollectionAssert.AreEquivalent(new[] { "-20", "-7" }, floats.Select(text => text.GetComponent<TextMeshPro>().text));
            Assert.AreEqual(2, floats.Select(text => text.transform.localPosition.x).Distinct().Count());
            Assert.AreEqual(2, owner.GetComponentsInChildren<BattleEffect>().Count(effect => effect.name == "Effect_slash"));
            view.AdvanceDamageFeedback(10f);
            Assert.AreEqual(2, owner.GetComponentsInChildren<FloatingText>().Length, "不能重播或显示未实际发生的段");
        }
        finally { UnityEngine.Object.DestroyImmediate(owner); }
    }

    [Test]
    public void ControllerPresentsHitRecordsRatherThanAggregateDelta_AndIgnoresDuplicateSequence()
    {
        var owner = new GameObject("ConsumeSegmentsTest");
        try
        {
            var hero = AutoBattleTestData.Hero(attack: 10);
            AutoBattleTestData.SetSkill(hero, AutoBattleTestData.Skill("basic", AutoBattleTestData.Effect(hits: 3)));
            var engine = AutoBattleTestData.Engine(heroes: new[] { hero }, enemies: new[] { AutoBattleTestData.Enemy(hp: 25) });
            engine.StartBattle();
            var result = BattleTestDriver.NextAction(engine, "hero");
            var controller = owner.AddComponent<BattleController>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(BattleController).GetField("_engine", flags).SetValue(controller, engine);
            typeof(BattleController).GetField("_audio", flags).SetValue(controller, BattleAudio.Build(owner.transform));
            var views = (Dictionary<string, UnitView>)typeof(BattleController).GetField("_views", flags).GetValue(controller);
            var target = engine.Enemies[0];
            var targetGo = new GameObject("Target");
            targetGo.transform.SetParent(owner.transform);
            var view = targetGo.AddComponent<UnitView>();
            view.Setup(target, Color.white);
            views.Add(target.Id, view);
            var consume = typeof(BattleController).GetMethod("ConsumeAction", flags);
            consume.Invoke(controller, new object[] { result });
            Assert.AreEqual(1, targetGo.GetComponentsInChildren<FloatingText>().Length);
            view.AdvanceDamageFeedback(UnitView.HitInterval * 2f + 0.001f);
            var floats = targetGo.GetComponentsInChildren<FloatingText>();
            CollectionAssert.AreEqual(new[] { "-10", "-10", "-5" }, floats.Select(text => text.GetComponent<TextMeshPro>().text));
            Assert.IsFalse(floats.Any(text => text.GetComponent<TextMeshPro>().text == "-25"));
            consume.Invoke(controller, new object[] { result });
            view.AdvanceDamageFeedback(10f);
            Assert.AreEqual(3, targetGo.GetComponentsInChildren<FloatingText>().Length);
            Assert.AreEqual(0, target.Hp, "展示不得重复扣血");
        }
        finally { UnityEngine.Object.DestroyImmediate(owner); }
    }

    [Test]
    public void DisablingViewCancelsRemainingHitFeedback()
    {
        var owner = new GameObject("CancelHitFeedbackTest");
        try
        {
            var view = owner.AddComponent<UnitView>();
            view.Setup(new Unit("enemy_0", Team.Enemy) { Hp = 100, MaxHp = 100, MaxUlt = 100, SlotId = "front1" }, Color.white);
            view.PlayDamageHits(new[] { 10, 10, 10 }, false);
            typeof(UnitView).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
            view.AdvanceDamageFeedback(10f);
            Assert.AreEqual(1, owner.GetComponentsInChildren<FloatingText>().Length);
        }
        finally { UnityEngine.Object.DestroyImmediate(owner); }
    }

    [Test]
    public void PermanentStatusLabel_UsesStrongestEffect_AndReturnsAfterTemporaryBuffExpires()
    {
        var owner = new GameObject("PermanentLabelTest");
        try
        {
            var hero = AutoBattleTestData.Hero();
            hero.skills[3].trigger = "permanent";
            hero.skills[3].effects[0].duration = 0;
            hero.skills[3].effects[0].percent = 10;
            var engine = AutoBattleTestData.Engine(heroes: new[] { hero });
            var unit = engine.Players[0];
            var view = owner.AddComponent<UnitView>();
            view.Setup(unit, Color.white);
            view.Refresh(unit, "", engine.Ctx.GetStatuses(unit.Id));
            var icon = owner.GetComponentsInChildren<StatusIconView>(true).Single(i => i.Type == StatusType.DefenseUp);
            Assert.AreEqual("防御 +10% · 常驻", icon.Description);
            Assert.IsTrue(icon.transform.Find("Permanent").gameObject.activeSelf);
            var statuses = engine.Ctx.StatesOf(unit.Id);
            statuses.Apply(StatusType.DefenseUp, 25, 1);
            view.Refresh(unit, "", engine.Ctx.GetStatuses(unit.Id));
            Assert.AreEqual("防御 +25% · 剩余 1 次行动", icon.Description);
            Assert.IsFalse(icon.transform.Find("Permanent").gameObject.activeSelf);
            Assert.AreEqual("1", icon.transform.Find("Duration").GetComponent<TextMeshPro>().text);
            statuses.BeginAction();
            statuses.CompleteAction();
            view.Refresh(unit, "", engine.Ctx.GetStatuses(unit.Id));
            Assert.AreEqual("防御 +10% · 常驻", icon.Description);
            unit.TakeDamage(unit.Hp);
            view.Refresh(unit, "", engine.Ctx.GetStatuses(unit.Id));
            Assert.IsFalse(icon.gameObject.activeSelf);
        }
        finally { UnityEngine.Object.DestroyImmediate(owner); }
    }

    [Test]
    public void SynthesizedClipsAreShortQuietHaveSilentEndpoints_AndMuteSurvivesStop()
    {
        var owner = new GameObject("AudioTest");
        AudioClip[] clips = null;
        BattleAudio audio = null;
        try
        {
            audio = BattleAudio.Build(owner.transform);
            var source = audio.GetComponent<AudioSource>();
            Assert.IsFalse(source.playOnAwake);
            Assert.AreEqual(0f, source.spatialBlend);
            Assert.LessOrEqual(source.volume, 0.25f);
            var catalog = (Dictionary<BattleSound, AudioClip>)typeof(BattleAudio).GetField("_clips",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(audio);
            Assert.AreEqual(Enum.GetValues(typeof(BattleSound)).Length, catalog.Count);
            clips = catalog.Values.ToArray();
            foreach (var clip in clips)
            {
                Assert.AreEqual(1, clip.channels);
                Assert.Greater(clip.length, 0.04f);
                Assert.LessOrEqual(clip.length, 0.4f);
                var samples = new float[clip.samples];
                Assert.IsTrue(clip.GetData(samples, 0));
                Assert.AreEqual(0f, samples[0]);
                Assert.AreEqual(0f, samples[samples.Length - 1]);
                Assert.IsTrue(samples.All(sample => !float.IsNaN(sample) && !float.IsInfinity(sample)));
                Assert.LessOrEqual(samples.Max(sample => Mathf.Abs(sample)), 0.351f);
                Assert.Greater(samples.Max(sample => Mathf.Abs(sample)), 0.1f);
            }
            audio.SetMuted(true);
            audio.Play(BattleSound.Hit);
            Assert.IsTrue(audio.Muted);
            Assert.IsFalse(source.isPlaying);
            audio.StopPlayback();
            Assert.IsTrue(audio.Muted);
            audio.SetMuted(false);
            Assert.IsFalse(audio.Muted);
        }
        finally
        {
            if (audio != null)
                typeof(BattleAudio).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(audio, null);
            UnityEngine.Object.DestroyImmediate(owner);
        }
        Assert.IsTrue(clips.All(clip => clip == null));
    }

    [TestCase("hit", BattleSound.Hit)]
    [TestCase("heal", BattleSound.Heal)]
    [TestCase("buff", BattleSound.Skill)]
    [TestCase("ult", BattleSound.Ultimate)]
    public void SoundClassificationUsesOneCompleteAction_NotNumberOfHitSegments(string action, BattleSound expected)
    {
        var hero = AutoBattleTestData.Hero();
        if (action == "hit")
            AutoBattleTestData.SetSkill(hero, AutoBattleTestData.Skill("basic", AutoBattleTestData.Effect(hits: 3)));
        if (action == "heal")
            AutoBattleTestData.SetSkill(hero, AutoBattleTestData.Skill("special",
                AutoBattleTestData.Effect("heal", "self", multiplier: 0, hits: 0, percent: 25)));
        if (action == "buff")
            AutoBattleTestData.SetSkill(hero, AutoBattleTestData.Skill("special",
                AutoBattleTestData.Effect("attackUp", "self", multiplier: 0, hits: 0, percent: 25, duration: 2)));
        var engine = AutoBattleTestData.Engine(heroes: new[] { hero });
        if (action == "ult") engine.Players[0].GainUlt(100);
        if (action == "heal") engine.Players[0].Hp = 5000;
        engine.StartBattle();
        var result = BattleTestDriver.NextAction(engine, "hero");
        if (action == "heal" || action == "buff") result = BattleTestDriver.NextAction(engine, "hero");
        Assert.AreEqual(expected, BattleAudio.SoundFor(result));
        if (action == "hit") Assert.AreEqual(3, result.Hits.Count);
    }

    [Test]
    public void StunnedActionDoesNotPretendToHitOrPlayUltimateSound()
    {
        var engine = AutoBattleTestData.Engine();
        engine.Ctx.StatesOf("hero").Apply(StatusType.Stun, 0, 1);
        engine.Players[0].GainUlt(100);
        engine.StartBattle();
        var skipped = BattleTestDriver.NextAction(engine, "hero");
        Assert.AreEqual(BattleEventKind.Skipped, skipped.Kind);
        Assert.IsEmpty(skipped.Hits);
        Assert.IsNull(BattleAudio.SoundFor(skipped));
    }

    [Test]
    public void UiContainsOnlySetupControls_AndFeedbackResetPreservesMute()
    {
        var oldEventSystem = EventSystem.current;
        var owner = new GameObject("AutomaticBattleUiTest");
        try
        {
            var ui = BattleUi.Build(owner.transform);
            if (ui.CanvasRect == null)
                typeof(BattleUi).GetMethod("BuildAll", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);
            foreach (var removed in new[] { "Hand", "PilePanel", "PileButton" })
                Assert.IsNull(ui.CanvasRect.Find(removed));
            Assert.IsNull(typeof(BattleUi).GetMethod("BindUltimate"));
            Assert.IsNull(typeof(BattleUi).GetMethod("RefreshHand"));
            int starts = 0;
            ui.OnStartBattle = () => starts++;
            ui.StartBattleButton.onClick.Invoke();
            Assert.AreEqual(1, starts);
            bool? muteRequest = null;
            ui.OnMuteChanged = muted => muteRequest = muted;
            var mute = ui.CanvasRect.Find("MuteButton").GetComponent<Button>();
            mute.onClick.Invoke();
            Assert.IsTrue(ui.Muted);
            Assert.AreEqual(true, muteRequest);
            ui.ShowFeedback("终结技", Color.cyan);
            var feedback = ui.CanvasRect.Find("FeedbackText").GetComponent<TextMeshProUGUI>();
            Assert.AreEqual("终结技", feedback.text);
            Assert.IsFalse(feedback.raycastTarget);
            ui.ResetBattleFeedback();
            Assert.AreEqual("", feedback.text);
            Assert.IsFalse(ui.ResultVisible);
            Assert.IsTrue(ui.Muted);
            Assert.AreEqual("音效：关", mute.GetComponentInChildren<TextMeshProUGUI>().text);
            mute.onClick.Invoke();
            Assert.IsFalse(ui.Muted);
            Assert.AreEqual(false, muteRequest);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
            if (oldEventSystem == null && EventSystem.current != null)
                UnityEngine.Object.DestroyImmediate(EventSystem.current.gameObject);
        }
    }

    [TestCase(Team.Player)]
    [TestCase(Team.Enemy)]
    public void UnitViewShowsEnergyForBothTeams_AndSeparatesCurrentActionFromStatuses(Team team)
    {
        var owner = new GameObject("UnitViewTest");
        try
        {
            var unit = new Unit(team == Team.Player ? "sword" : "grunt_0", team)
            {
                Name = "角色", Hp = 80, MaxHp = 100, Ult = 20, MaxUlt = 100, SlotId = "front1"
            };
            var view = owner.AddComponent<UnitView>();
            view.Setup(unit, Color.white);
            view.Refresh(unit, "特殊 · 增益", new[] { new StatusEffect { Type = StatusType.AttackUp, Percent = 25, Duration = 2 } });
            var type = typeof(UnitView);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var stat = (TextMeshPro)type.GetField("_stat", flags).GetValue(view);
            var action = (TextMeshPro)type.GetField("_action", flags).GetValue(view);
            var status = owner.GetComponentsInChildren<StatusIconView>(true).Single(i => i.Type == StatusType.AttackUp);
            var energy = (SpriteRenderer)type.GetField("_ultFill", flags).GetValue(view);
            Assert.AreEqual("能量 20/100", stat.text);
            Assert.AreEqual("特殊 · 增益", action.text);
            Assert.AreEqual("攻击 +25% · 剩余 2 次行动", status.Description);
            Assert.IsNull(owner.transform.Find("StatusText"));
            var hud = owner.transform.Find("Hud");
            Assert.IsNotNull(hud);
            Assert.Less(hud.localPosition.y, -0.75f);
            Assert.Less(hud.Find("HpBg").position.y, owner.transform.Find("Art").GetComponent<SpriteRenderer>().bounds.min.y);
            Assert.Less(hud.Find("UltBg").position.y, hud.Find("HpBg").position.y);
            Assert.AreEqual(hud, status.transform.parent);
            Assert.Less(status.transform.position.y, hud.Find("UltBg").position.y);
            Assert.IsTrue(energy.gameObject.activeSelf);
            float partialWidth = energy.transform.localScale.x;
            unit.Ult = 100;
            view.Refresh(unit, "终结技");
            Assert.Greater(energy.transform.localScale.x, partialWidth);
            Assert.AreEqual("能量 100/100", stat.text);
            Assert.IsFalse(status.gameObject.activeSelf);
        }
        finally { UnityEngine.Object.DestroyImmediate(owner); }
    }

    [Test]
    public void FootHudFitsAllFiveSlotsWithoutShrinkingArtOrOverlappingRows()
    {
        var owner = new GameObject("FootHudLayoutTest");
        try
        {
            var layout = new BattleLayout { setupSpread = 1.15f };
            foreach (Team team in new[] { Team.Player, Team.Enemy })
            {
                var artBounds = new Dictionary<string, Bounds>();
                var hudBounds = new Dictionary<string, Bounds>();
                foreach (string slot in new[] { "front1", "front2", "back1", "back2", "back3" })
                {
                    var go = new GameObject(team + "_" + slot);
                    go.transform.SetParent(owner.transform);
                    go.transform.localScale = Vector3.one * 0.72f;
                    go.transform.position = PositioningView.Position(team, slot, layout);
                    var unit = new Unit(team == Team.Player ? "sword" : "lord_0", team)
                        { Name = "布局验证", Hp = 100, MaxHp = 100, Ult = 20, MaxUlt = 100, SlotId = slot };
                    var view = go.AddComponent<UnitView>();
                    view.Setup(unit, Color.white);
                    view.Refresh(unit, "特殊 · 连斩", new[] {
                        new StatusEffect { Type = StatusType.AttackUp, Percent = 10, Turn = "permanent" },
                        new StatusEffect { Type = StatusType.DefenseUp, Percent = 20, Duration = 2 },
                        new StatusEffect { Type = StatusType.Stun, Duration = 1 }
                    });
                    var art = go.transform.Find("Art").GetComponent<SpriteRenderer>();
                    var hud = go.transform.Find("Hud");
                    Assert.AreEqual(go.transform, hud.parent);
                    Assert.AreEqual(art.transform.localScale.x, art.transform.localScale.y, 0.0001f);
                    Assert.AreEqual(1.08f, art.bounds.size.y, 0.001f, "不能缩小立绘给HUD腾空间");
                    var bounds = hud.GetComponentInChildren<Renderer>().bounds;
                    foreach (var renderer in hud.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
                    Assert.Less(bounds.max.y, art.bounds.min.y, team + "/" + slot + " HUD遮住立绘");
                    Assert.Greater(bounds.min.y, -5f, team + "/" + slot + " HUD被屏幕底部裁切");
                    Assert.Less(art.bounds.max.y, 5f, team + "/" + slot + " 立绘顶部被裁切");
                    artBounds.Add(slot, art.bounds);
                    hudBounds.Add(slot, bounds);
                }
                foreach (var pair in new[] { new[] { "front1", "front2" }, new[] { "back1", "back2" }, new[] { "back2", "back3" } })
                    Assert.Greater(hudBounds[pair[0]].min.y, artBounds[pair[1]].max.y, "上下排HUD和立绘重叠");
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(owner); }
    }

    [Test]
    public void StatusIconsUseDistinctShapesAndPolarity_AndReuseSlotsOnRefresh()
    {
        var owner = new GameObject("StatusIconsTest");
        try
        {
            var unit = new Unit("sword", Team.Player) { Name = "剑士", Hp = 100, MaxHp = 100, MaxUlt = 100 };
            var view = owner.AddComponent<UnitView>();
            view.Setup(unit, Color.white);
            var statuses = new[]
            {
                new StatusEffect { Type = StatusType.AttackUp, Percent = 10, Turn = "permanent" },
                new StatusEffect { Type = StatusType.DefenseUp, Percent = 25, Duration = 2 },
                new StatusEffect { Type = StatusType.Stun, Duration = 1 }
            };
            view.Refresh(unit, "", statuses);
            Assert.Greater(StatusIconView.PositiveColor.r, StatusIconView.PositiveColor.b);
            Assert.Greater(StatusIconView.NegativeColor.b, StatusIconView.NegativeColor.r);
            var icons = owner.GetComponentsInChildren<StatusIconView>();
            Assert.AreEqual(3, icons.Length);
            Assert.AreEqual(3, icons.Select(i => i.transform.Find("Symbol").GetComponent<SpriteRenderer>().sprite).Distinct().Count());
            foreach (var icon in icons)
            {
                Assert.AreEqual(icon.Type == StatusType.Stun ? StatusIconView.NegativeColor : StatusIconView.PositiveColor,
                    icon.transform.Find("Tile").GetComponent<SpriteRenderer>().color);
                Assert.IsFalse(icon.transform.Find("Detail").gameObject.activeSelf);
            }
            view.Refresh(unit, "", statuses);
            CollectionAssert.AreEquivalent(icons, owner.GetComponentsInChildren<StatusIconView>());
            view.Refresh(unit, "");
            Assert.AreEqual(0, owner.GetComponentsInChildren<StatusIconView>().Length);
        }
        finally { UnityEngine.Object.DestroyImmediate(owner); }
    }

    [TestCase("basic", 0.24f)]
    [TestCase("special", 0.34f)]
    [TestCase("ult", 0.60f)]
    [TestCase("passive", 0f)]
    [TestCase("skipped", 0f)]
    public void ActionMotionHasBoundedTierDurations_AndDoesNotMoveHudOrCollider(string action, float duration)
    {
        var owner = new GameObject("ActionMotionTest");
        try
        {
            var unit = new Unit("sword", Team.Player) { Name = "剑士", Hp = 100, MaxHp = 100, MaxUlt = 100 };
            var view = owner.AddComponent<UnitView>();
            view.Setup(unit, Color.white);
            var position = owner.transform.position;
            var hpPosition = owner.transform.Find("Hud/HpBg").position;
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            view.PlayAction(action);
            float remaining = (float)typeof(UnitView).GetField("_actionT", flags).GetValue(view);
            Assert.AreEqual(duration, remaining);
            Assert.AreEqual(duration == 0f, view.FeedbackComplete);
            Assert.AreEqual(action == "special" || action == "ult", owner.GetComponentInChildren<BattleEffect>() != null);
            if (duration > 0f) typeof(UnitView).GetField("_actionT", flags).SetValue(view, duration * 0.5f);
            typeof(UnitView).GetMethod("UpdateMotion", flags).Invoke(view, new object[] { 0f });
            Assert.AreEqual(position, owner.transform.position);
            Assert.AreEqual(hpPosition, owner.transform.Find("Hud/HpBg").position);
            Assert.AreEqual(Vector2.zero, owner.GetComponent<BoxCollider2D>().offset);
            if (duration > 0f) Assert.AreNotEqual(Vector3.zero, owner.transform.Find("Art").localPosition);
        }
        finally { UnityEngine.Object.DestroyImmediate(owner); }
    }

    [TestCase("sword", Team.Player, "basic")]
    [TestCase("sword", Team.Player, "ult")]
    [TestCase("guard", Team.Player, "basic")]
    [TestCase("grunt_0", Team.Enemy, "basic")]
    [TestCase("claw_0", Team.Enemy, "special")]
    [TestCase("lord_0", Team.Enemy, "ult")]
    public void MeleeApproachesActualTargetAndReturns_WithoutMovingFormationOrHud(string id, Team team, string action)
    {
        var owner = new GameObject("MeleeApproachTest");
        try
        {
            owner.transform.localScale = Vector3.one * 0.72f;
            owner.transform.position = new Vector3(team == Team.Player ? -4f : 4f, -1f, 0f);
            var home = owner.transform.position;
            Vector3 target = new Vector3(-home.x, 2f, 0f);
            var unit = new Unit(id, team) { Name = id, Hp = 100, MaxHp = 100, MaxUlt = 100, SlotId = "front1" };
            var view = owner.AddComponent<UnitView>();
            view.Setup(unit, Color.white);
            var hp = owner.transform.Find("Hud/HpBg").position;
            var art = owner.transform.Find("Art");
            float delay = view.PlayAction(action, target);
            Assert.Greater(delay, 0f);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            float duration = (float)typeof(UnitView).GetField("_actionDuration", flags).GetValue(view);
            typeof(UnitView).GetField("_actionT", flags).SetValue(view, duration * 0.60f);
            typeof(UnitView).GetMethod("UpdateMotion", flags).Invoke(view, new object[] { 0f });
            Assert.That(Vector3.Distance(art.position, target), Is.EqualTo(0.9f).Within(0.01f));
            Assert.Greater(Vector3.Distance(art.position, home), 2f);
            Assert.AreEqual(home, owner.transform.position);
            Assert.AreEqual(hp, owner.transform.Find("Hud/HpBg").position);
            Assert.AreEqual("front1", unit.SlotId);
            Assert.AreEqual(25, art.GetComponent<SpriteRenderer>().sortingOrder);
            typeof(UnitView).GetField("_actionT", flags).SetValue(view, 0f);
            typeof(UnitView).GetMethod("UpdateMotion", flags).Invoke(view, new object[] { 0f });
            Assert.That(art.localPosition.magnitude, Is.LessThan(0.0001f));
            Assert.AreEqual(10, art.GetComponent<SpriteRenderer>().sortingOrder);
            Assert.IsTrue(view.FeedbackComplete);
        }
        finally { UnityEngine.Object.DestroyImmediate(owner); }
    }

    [TestCase("mage", "basic", true)]
    [TestCase("guard", "special", false)]
    [TestCase("guard", "ult", false)]
    [TestCase("sword", "skipped", true)]
    public void RangedSupportAndSkippedActionsDoNotApproach(string id, string action, bool hasTarget)
    {
        var owner = new GameObject("StationaryActionTest");
        try
        {
            var unit = new Unit(id, Team.Player) { Name = id, Hp = 100, MaxHp = 100, MaxUlt = 100 };
            var view = owner.AddComponent<UnitView>();
            view.Setup(unit, Color.white);
            Assert.AreEqual(0f, view.PlayAction(action, hasTarget ? new Vector3(5f, 2f, 0f) : (Vector3?)null));
            var offset = (Vector3)typeof(UnitView).GetField("_approachOffset", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
            Assert.AreEqual(Vector3.zero, offset);
        }
        finally { UnityEngine.Object.DestroyImmediate(owner); }
    }

    [Test]
    public void InvalidatedApproachCannotPresentDamageFromPreviousBattle()
    {
        var owner = new GameObject("ApproachGenerationTest");
        try
        {
            var hero = AutoBattleTestData.Hero("sword");
            var enemy = AutoBattleTestData.Enemy();
            hero.skills[3].trigger = enemy.skills[3].trigger = "permanent";
            hero.skills[3].effects[0].duration = enemy.skills[3].effects[0].duration = 0;
            var engine = AutoBattleTestData.Engine(heroes: new[] { hero }, enemies: new[] { enemy });
            var controller = owner.AddComponent<BattleController>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(BattleController).GetField("_engine", flags).SetValue(controller, engine);
            var views = (Dictionary<string, UnitView>)typeof(BattleController).GetField("_views", flags).GetValue(controller);
            foreach (var unit in engine.Ctx.Units)
            {
                var go = new GameObject(unit.Id);
                go.transform.SetParent(owner.transform);
                go.transform.position = new Vector3(unit.Team == Team.Player ? -2f : 2f, 0f, 0f);
                var view = go.AddComponent<UnitView>();
                view.Setup(unit, Color.white);
                views.Add(unit.Id, view);
            }
            var victim = engine.Ctx.Units.Single(u => u.Team == Team.Enemy);
            string before = views[victim.Id].transform.Find("Hud/HpText").GetComponent<TextMeshPro>().text;
            engine.StartBattle();
            int generation = (int)typeof(BattleController).GetField("_generation", flags).GetValue(controller);
            var drive = (IEnumerator)typeof(BattleController).GetMethod("Drive", flags).Invoke(controller, new object[] { engine, generation });
            Assert.IsTrue(drive.MoveNext());
            Assert.IsTrue(drive.MoveNext());
            Assert.AreEqual("basic", engine.CurrentAction.ActionType);
            Assert.AreEqual(before, views[victim.Id].transform.Find("Hud/HpText").GetComponent<TextMeshPro>().text);
            Assert.AreEqual(0L, typeof(BattleController).GetField("_presentedSequence", flags).GetValue(controller));
            typeof(BattleController).GetMethod("StopDriver", flags).Invoke(controller, null);
            Assert.IsFalse(drive.MoveNext());
            Assert.AreEqual(before, views[victim.Id].transform.Find("Hud/HpText").GetComponent<TextMeshPro>().text);
        }
        finally { UnityEngine.Object.DestroyImmediate(owner); }
    }

    [Test]
    public void DeadUnitCannotStartAttack_AndClearsStatusDetails()
    {
        var owner = new GameObject("DeadMotionTest");
        try
        {
            var unit = new Unit("sword", Team.Player) { Name = "剑士", Hp = 100, MaxHp = 100, MaxUlt = 100 };
            var view = owner.AddComponent<UnitView>();
            view.Setup(unit, Color.white);
            view.Refresh(unit, "", new[] { new StatusEffect { Type = StatusType.Stun, Duration = 1 } });
            var icon = owner.GetComponentInChildren<StatusIconView>();
            typeof(StatusIconView).GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(icon, null);
            Assert.IsTrue(icon.transform.Find("Detail").gameObject.activeSelf);
            unit.TakeDamage(100);
            view.Refresh(unit, "");
            view.SetAppearance(Color.white, true);
            view.PlayAction("ult");
            Assert.IsNull(owner.GetComponentInChildren<BattleEffect>());
            Assert.IsFalse(icon.gameObject.activeSelf);
            Assert.IsFalse(icon.transform.Find("Detail").gameObject.activeSelf);
        }
        finally { UnityEngine.Object.DestroyImmediate(owner); }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void StaleRuntimeDriverCannotAdvancePreviousBattle(bool invalidateGeneration)
    {
        var owner = new GameObject("DriverGenerationTest");
        try
        {
            var controller = owner.AddComponent<BattleController>();
            var engine = AutoBattleTestData.Engine();
            engine.StartBattle();
            var type = typeof(BattleController);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            type.GetField("_engine", flags).SetValue(controller, engine);
            int generation = (int)type.GetField("_generation", flags).GetValue(controller);
            var drive = (IEnumerator)type.GetMethod("Drive", flags).Invoke(controller, new object[] { engine, generation });
            Assert.IsTrue(drive.MoveNext());
            if (invalidateGeneration) type.GetMethod("StopDriver", flags).Invoke(controller, null);
            else type.GetField("_engine", flags).SetValue(controller, AutoBattleTestData.Engine());
            Assert.IsFalse(drive.MoveNext());
            Assert.IsEmpty(engine.ActionResults);
            Assert.IsFalse(engine.IsActionOccupied);
        }
        finally { UnityEngine.Object.DestroyImmediate(owner); }
    }
}
