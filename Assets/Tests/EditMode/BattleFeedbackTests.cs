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
            var controller = owner.AddComponent<BattleController>();
            typeof(BattleController).GetField("_engine", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(controller, engine);
            var method = typeof(BattleController).GetMethod("UnitStatusText", BindingFlags.Instance | BindingFlags.NonPublic);
            var unit = engine.Players[0];
            Assert.AreEqual("防+10%（常驻）", method.Invoke(controller, new object[] { unit }));
            var statuses = engine.Ctx.StatesOf(unit.Id);
            statuses.Apply(StatusType.DefenseUp, 25, 1);
            Assert.AreEqual("防+25%（1次）", method.Invoke(controller, new object[] { unit }));
            statuses.BeginAction();
            statuses.CompleteAction();
            Assert.AreEqual("防+10%（常驻）", method.Invoke(controller, new object[] { unit }));
            unit.TakeDamage(unit.Hp);
            Assert.AreEqual("", method.Invoke(controller, new object[] { unit }));
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
            view.Refresh(unit, "特殊 · 增益", "攻+25%（2次）");
            var type = typeof(UnitView);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var stat = (TextMeshPro)type.GetField("_stat", flags).GetValue(view);
            var action = (TextMeshPro)type.GetField("_action", flags).GetValue(view);
            var status = (TextMeshPro)type.GetField("_status", flags).GetValue(view);
            var energy = (SpriteRenderer)type.GetField("_ultFill", flags).GetValue(view);
            Assert.AreEqual("能量 20/100", stat.text);
            Assert.AreEqual("特殊 · 增益", action.text);
            Assert.AreEqual("攻+25%（2次）", status.text);
            Assert.IsTrue(energy.gameObject.activeSelf);
            float partialWidth = energy.transform.localScale.x;
            unit.Ult = 100;
            view.Refresh(unit, "终结技", "");
            Assert.Greater(energy.transform.localScale.x, partialWidth);
            Assert.AreEqual("能量 100/100", stat.text);
            Assert.IsFalse(status.gameObject.activeSelf);
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
