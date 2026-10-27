using System;
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
    public void Synthesized_Clips_Are_Short_Quiet_And_Have_Silent_Endpoints()
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
        Assert.IsTrue(clips.All(clip => clip == null), "销毁音效组件时必须释放生成的AudioClip");
    }

    [TestCase("inject", BattleSound.Inject)]
    [TestCase("remove", BattleSound.Remove)]
    [TestCase("heal", BattleSound.Heal)]
    [TestCase("card", BattleSound.Card)]
    [TestCase("hit", BattleSound.Hit)]
    public void Sound_Classification_Uses_Action_Facts_Once(string action, BattleSound expected)
    {
        var caster = new Unit("source", Team.Player) { Hp = 50, MaxHp = 100, SlotId = "front1" };
        var target = new Unit("target", Team.Enemy) { Hp = 100, MaxHp = 100, SlotId = "front1" };
        var ctx = new CombatContext(new Rng(7), new[] { caster, target });
        ctx.BeginAction(action == "card" || action == "heal" || action == "remove"
            ? BattleEventKind.Card : BattleEventKind.Skill, caster, action, false);
        if (action == "heal") caster.Heal(30);
        if (action == "card" || action == "hit")
            for (int i = 0; i < 3; i++)
                Damage.Apply(ctx, new DamageRequest { SourceUnitId = caster.Id, TargetUnitId = target.Id, Amount = 1 });
        var result = ctx.CompleteAction(injectedCardIds: action == "inject" ? new[] { "lightning", "slag" } : null,
            removedCardId: action == "remove" ? "slag" : null);
        Assert.AreEqual(expected, BattleAudio.SoundFor(result));
        if (action == "card" || action == "hit") Assert.AreEqual(3, result.Hits.Count);
        Assert.AreEqual(1, ctx.ActionResults.Count);
    }

    [Test]
    public void Skipped_Attack_Does_Not_Pretend_To_Hit()
    {
        var ctx = new CombatContext(new Rng(7), Array.Empty<Unit>());
        ctx.BeginAction(BattleEventKind.Intent, null, "interrupted", false);
        Assert.IsNull(BattleAudio.SoundFor(ctx.CompleteAction()));
    }

    [Test]
    public void Draw_Feedback_Is_Not_Replayed_By_Hud_Refresh_And_Mute_Survives_Reset()
    {
        var oldEventSystem = EventSystem.current;
        var owner = new GameObject("FeedbackUiTest");
        try
        {
            var ui = BattleUi.Build(owner.transform);
            if (ui.CanvasRect == null)
                typeof(BattleUi).GetMethod("BuildAll", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);
            var level = ConfigLoader.LoadLevels().items.Single(item => item.id == 1);
            var engine = BattleEngine.Create(7, ConfigLoader.LoadCharacters().items, level,
                ConfigLoader.LoadEnemies().items.ToDictionary(enemy => enemy.id), ConfigLoader.LoadCards().items);
            int draws = 0;
            ui.OnDrawCards = () => draws++;
            ui.RefreshHand(engine, -1, "");
            Assert.AreEqual(0, draws);
            engine.StartBattle();
            ui.RefreshHand(engine, -1, "");
            Assert.AreEqual(1, draws);
            var feedback = ui.CanvasRect.Find("FeedbackText").GetComponent<TextMeshProUGUI>();
            StringAssert.Contains("抽到：", feedback.text);
            Assert.IsFalse(feedback.raycastTarget);
            ui.ShowFeedback("注入抽牌堆", Color.cyan);
            ui.RefreshHand(engine, -1, "");
            Assert.AreEqual(1, draws);
            Assert.AreEqual("注入抽牌堆", feedback.text);
            bool? muteRequest = null;
            ui.OnMuteChanged = muted => muteRequest = muted;
            var mute = ui.CanvasRect.Find("MuteButton").GetComponent<Button>();
            mute.onClick.Invoke();
            Assert.IsTrue(ui.Muted);
            Assert.AreEqual(true, muteRequest);
            Assert.AreEqual("音效：关", mute.GetComponentInChildren<TextMeshProUGUI>().text);
            ui.RefreshHand(null, -1, "");
            Assert.IsTrue(ui.Muted);
            Assert.AreEqual("", feedback.text);
            var hand = ui.CanvasRect.Find("Hand/HandViewport/Cards");
            foreach (Transform card in hand) Assert.AreEqual(Vector3.one, card.localScale);
            ui.RefreshHand(engine, -1, "");
            Assert.AreEqual(2, draws);
            Assert.IsTrue(ui.Muted);
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
}
