using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CQ.Core.Battle;
using CQ.Core.Combat;
using CQ.Runtime;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

public class FormationDragTests
{
    private GameObject _owner;
    private Camera _camera;
    private BattleEngine _engine;
    private FormationDrag _drag;
    private BattleLayout _layout;
    private Transform _slots;
    private readonly Dictionary<string, UnitView> _views = new Dictionary<string, UnitView>();
    private int _changes;

    [SetUp]
    public void SetUp()
    {
        _owner = new GameObject("FormationDragTest");
        _camera = new GameObject("DragCamera").AddComponent<Camera>();
        _camera.transform.SetParent(_owner.transform);
        _camera.transform.position = new Vector3(0f, 0f, -10f);
        _camera.orthographic = true;
        _camera.orthographicSize = 5f;
        _camera.aspect = 16f / 9f;
        _layout = new BattleLayout();
        _engine = AutoBattleTestData.Engine(heroes: new[]
        {
            AutoBattleTestData.Hero("sword", "front1"), AutoBattleTestData.Hero("guard", "front2")
        });
        _slots = new GameObject("Slots").transform;
        _slots.SetParent(_owner.transform);
        foreach (var slot in _engine.Ctx.FormationOf(Team.Player).Slots)
        {
            var marker = new GameObject("Player_" + slot.Id);
            marker.transform.SetParent(_slots);
            marker.AddComponent<SpriteRenderer>();
        }
        foreach (var unit in _engine.Ctx.Units)
        {
            var go = new GameObject("Unit_" + unit.Id);
            go.transform.SetParent(_owner.transform);
            go.transform.position = PositioningView.Position(unit, _layout);
            go.transform.localScale = Vector3.one * 0.72f;
            var view = go.AddComponent<UnitView>();
            view.Setup(unit, Color.white);
            _views[unit.Id] = view;
        }
        _drag = _owner.AddComponent<FormationDrag>();
        _drag.Configure(_camera, _engine, _layout, _views, _slots, () => _changes++);
        Physics2D.SyncTransforms();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_owner);
        _views.Clear();
        _changes = 0;
    }

    [Test]
    public void TapAndSmallJitterNeverEnterSelectionModeOrChangeFormation()
    {
        var source = ScreenSlot("front1");
        Assert.IsTrue(_drag.BeginPointer(-1, source, false));
        _drag.MovePointer(-1, source + Vector2.right * (_drag.DragThreshold * 0.5f));
        Assert.IsFalse(_drag.IsDragging);
        AssertPosition("sword", "front1");
        _drag.EndPointer(-1, source);
        Assert.IsFalse(_drag.IsSettling);
        Assert.AreEqual(0, _changes);
        Assert.IsFalse(_views["sword"].GetComponent<SortingGroup>().enabled);
        Assert.IsNull(_views["sword"].transform.Find("HighlightRing"));
        Assert.IsNull(_views["sword"].transform.Find("Highlight"));
        Assert.IsTrue(_drag.BeginPointer(-1, ScreenSlot("back1"), false) == false);
        Assert.AreEqual("front1", _engine.Players[0].SlotId);
    }

    [Test]
    public void ExpandedSetupCoordinatesRemainSharedByPickingDropAndSettlement()
    {
        _layout.setupSpread = 1.15f;
        foreach (var unit in _engine.Ctx.Units)
            _views[unit.Id].SetPosition(PositioningView.Position(unit, _layout), unit);
        Physics2D.SyncTransforms();
        BeginSword();
        _drag.MovePointer(-1, ScreenSlot("back1"));
        Assert.AreEqual("back1", _drag.CandidateSlot);
        Assert.AreEqual("front1", _engine.Players[0].SlotId);
        _drag.EndPointer(-1, ScreenSlot("back1"));
        _drag.AdvanceSettlement(1f);
        AssertPosition("sword", "back1");
        Assert.AreEqual(1, _changes);
    }

    [Test]
    public void DragMovesWholeViewWithHud_ButNotCoreSlot_AndRaisesRenderGroup()
    {
        var source = ScreenSlot("front1");
        var view = _views["sword"];
        var hpOffset = view.transform.Find("Hud/HpText").localPosition;
        var artScale = view.transform.Find("Art").localScale;
        Assert.IsTrue(_drag.BeginPointer(-1, source, false));
        _drag.MovePointer(-1, ScreenSlot("back1"));
        Assert.IsTrue(_drag.IsDragging);
        Assert.AreEqual("back1", _drag.CandidateSlot);
        Assert.AreEqual("front1", _engine.Players[0].SlotId);
        var destination = PositioningView.Position(Team.Player, "back1", _layout);
        destination.z = PositioningView.Position(Team.Player, "front1", _layout).z;
        Assert.That(Vector3.Distance(view.transform.position, destination), Is.LessThan(0.001f));
        Assert.AreEqual(hpOffset, view.transform.Find("Hud/HpText").localPosition);
        Assert.IsTrue(view.GetComponent<SortingGroup>().enabled);
        Assert.Greater(view.transform.Find("Art").localScale.x, artScale.x);
        Assert.AreEqual("松手放置", _slots.Find("DropHint").GetComponent<TextMeshPro>().text);
        Assert.AreEqual(0, _changes);
    }

    [TestCase("back1")]
    [TestCase("back2")]
    [TestCase("back3")]
    public void EmptySlotDropCommitsOnce_ThenSettlesToExactPosition(string destination)
    {
        BeginSword();
        _drag.EndPointer(-1, ScreenSlot(destination));
        Assert.AreEqual(destination, _engine.Players[0].SlotId);
        Assert.AreEqual(1, _changes);
        _drag.AdvanceSettlement(1f);
        AssertPosition("sword", destination);
        Assert.AreEqual(destination.StartsWith("front") ? 10 : 5,
            _views["sword"].transform.Find("Art").GetComponent<SpriteRenderer>().sortingOrder);
        Assert.IsFalse(_drag.IsSettling);
        Assert.IsFalse(_views["sword"].GetComponent<SortingGroup>().enabled);
        _drag.EndPointer(-1, ScreenSlot(destination));
        Assert.AreEqual(1, _changes);
    }

    [Test]
    public void OccupiedPreviewDoesNotMoveOtherUnit_AndReleaseAnimatesBoth()
    {
        BeginSword();
        var guardPosition = _views["guard"].transform.position;
        _drag.MovePointer(-1, ScreenSlot("front2") + Vector2.right * 12f);
        Assert.AreEqual("front2", _drag.CandidateSlot);
        Assert.AreEqual(guardPosition, _views["guard"].transform.position);
        Assert.AreEqual("front2", _engine.Players[1].SlotId);
        Assert.AreEqual("松手交换", _slots.Find("DropHint").GetComponent<TextMeshPro>().text);
        Assert.IsTrue(_views["guard"].transform.Find("Hud/HpText").GetComponent<Renderer>().forceRenderingOff);
        Assert.IsTrue(_views["guard"].transform.Find("Hud/Label").GetComponent<Renderer>().forceRenderingOff);
        Assert.Less(_views["guard"].transform.Find("Art").GetComponent<SpriteRenderer>().color.a, 0.5f);
        Assert.IsFalse(_views["sword"].transform.Find("Hud/HpText").GetComponent<Renderer>().forceRenderingOff);
        _drag.EndPointer(-1, ScreenSlot("front2") + Vector2.right * 12f);
        var swordStart = _views["sword"].transform.position;
        Assert.AreEqual("front2", _engine.Players[0].SlotId);
        Assert.AreEqual("front1", _engine.Players[1].SlotId);
        Assert.AreEqual(guardPosition, _views["guard"].transform.position, "松手不瞬移被交换角色");
        Assert.IsTrue(_drag.IsSettling);
        Assert.IsFalse(_drag.BeginPointer(1, ScreenSlot("front2"), true), "落位动画不被新拖拽打断");
        _drag.AdvanceSettlement(0.10f);
        Assert.AreNotEqual(guardPosition, _views["guard"].transform.position);
        Assert.AreNotEqual(swordStart, _views["sword"].transform.position);
        Assert.Greater(Vector3.Distance(_views["guard"].transform.position, PositioningView.Position(Team.Player, "front1", _layout)), 0.01f);
        _drag.AdvanceSettlement(0.10f);
        AssertPosition("sword", "front2");
        AssertPosition("guard", "front1");
        Assert.IsFalse(_views["guard"].transform.Find("Hud/HpText").GetComponent<Renderer>().forceRenderingOff);
        Assert.AreEqual(1f, _views["guard"].transform.Find("Art").GetComponent<SpriteRenderer>().color.a);
        Assert.AreEqual(1, _changes);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InvalidOrCanceledDropReturnsWithoutMutatingSlots(bool canceled)
    {
        BeginSword();
        _drag.MovePointer(-1, ScreenWorld(new Vector3(0f, -3f, 0f)));
        Assert.IsNull(_drag.CandidateSlot);
        Assert.AreEqual("front1", _engine.Players[0].SlotId);
        _drag.EndPointer(-1, canceled ? ScreenSlot("back1") : new Vector2(-100f, -100f), canceled);
        Assert.IsTrue(_drag.IsSettling);
        Assert.AreEqual(0, _changes);
        _drag.AdvanceSettlement(1f);
        AssertPosition("sword", "front1");
        Assert.AreEqual("front1", _engine.Players[0].SlotId);
    }

    [Test]
    public void DragBackToSourceDoesNotProduceChangeOrKeepPreview()
    {
        BeginSword();
        _drag.MovePointer(-1, ScreenSlot("back1"));
        _drag.EndPointer(-1, ScreenSlot("front1"));
        _drag.AdvanceSettlement(1f);
        AssertPosition("sword", "front1");
        Assert.AreEqual(0, _changes);
        Assert.IsFalse(_slots.Find("DropHint").gameObject.activeSelf);
    }

    [Test]
    public void TouchKeepsGrabOffsetAndLift_AndOtherFingerCannotTakeOver()
    {
        var press = ScreenSlot("front1") + new Vector2(4f, 5f);
        Assert.IsTrue(_drag.BeginPointer(7, press, true));
        Assert.IsFalse(_drag.BeginPointer(8, ScreenSlot("front2"), true));
        _drag.MovePointer(8, ScreenSlot("back3"));
        Assert.IsFalse(_drag.IsDragging);
        var delta = new Vector2(100f, -40f);
        _drag.MovePointer(7, press + delta);
        var expected = PositioningView.Position(Team.Player, "front1", _layout) +
            (_camera.ScreenToWorldPoint(press + delta) - _camera.ScreenToWorldPoint(press)) + Vector3.up * 0.5f;
        Assert.That(Vector3.Distance(expected, _views["sword"].transform.position), Is.LessThan(0.001f));
        _drag.EndPointer(8, ScreenSlot("front2"));
        Assert.IsTrue(_drag.IsDragging);
        // 手指位于角色下方；吸附按提起后的角色位置而非手指位置判断。
        var finger = ScreenWorld(PositioningView.Position(Team.Player, "back1", _layout) - Vector3.up * 0.5f) + new Vector2(4f, 5f);
        _drag.EndPointer(7, finger);
        _drag.AdvanceSettlement(1f);
        AssertPosition("sword", "back1");
        Assert.AreEqual(1, _changes);
    }

    [Test]
    public void EnemyCannotBeDragged_AndEnemySideCannotBeDropTarget()
    {
        var enemy = _engine.Enemies[0];
        var enemyPosition = _views[enemy.Id].transform.position;
        Assert.IsTrue(_drag.BeginPointer(-1, ScreenWorld(enemyPosition), false));
        _drag.MovePointer(-1, ScreenSlot("back1"));
        Assert.IsFalse(_drag.IsDragging);
        Assert.AreEqual(enemyPosition, _views[enemy.Id].transform.position);
        _drag.EndPointer(-1, ScreenSlot("back1"));
        BeginSword();
        _drag.MovePointer(-1, ScreenWorld(_views[enemy.Id].transform.position));
        Assert.IsNull(_drag.CandidateSlot);
        _drag.EndPointer(-1, ScreenWorld(_views[enemy.Id].transform.position));
        _drag.AdvanceSettlement(1f);
        AssertPosition("sword", "front1");
        Assert.AreEqual(0, _changes);
    }

    [TestCase("OnApplicationFocus", false)]
    [TestCase("OnApplicationPause", true)]
    public void FocusAndPauseCancelAndRestoreFeedback(string method, bool value)
    {
        BeginSword();
        _drag.MovePointer(-1, ScreenSlot("back1"));
        typeof(FormationDrag).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_drag, new object[] { value });
        Assert.IsFalse(_drag.IsDragging);
        Assert.IsFalse(_drag.IsSettling);
        AssertPosition("sword", "front1");
        Assert.IsFalse(_views["sword"].GetComponent<SortingGroup>().enabled);
        Assert.AreEqual(0, _changes);
    }

    [Test]
    public void DisableCancelsDrag_AndBattleStartRejectsFurtherFormationInput()
    {
        BeginSword();
        _drag.MovePointer(-1, ScreenSlot("back1"));
        _drag.enabled = false;
        // 普通MonoBehaviour在EditMode不派发禁用消息；真实Play另验生命周期。
        typeof(FormationDrag).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_drag, null);
        AssertPosition("sword", "front1");
        _drag.enabled = true;
        BeginSword();
        _drag.MovePointer(-1, ScreenSlot("back1"));
        _engine.StartBattle();
        _drag.RefreshAvailability();
        AssertPosition("sword", "front1");
        Assert.IsFalse(_slots.gameObject.activeSelf);
        Assert.IsTrue(_drag.BeginPointer(-1, ScreenSlot("front1"), false));
        _drag.MovePointer(-1, ScreenSlot("back1"));
        Assert.IsFalse(_drag.IsDragging);
        AssertPosition("sword", "front1");
        _drag.EndPointer(-1, ScreenSlot("back1"));
        Assert.AreEqual(0, _changes);
    }

    [Test]
    public void DragClosesPinnedStatusDetailsAndSuppressesHoverUntilSettlementFinishes()
    {
        var view = _views["sword"];
        view.Refresh(_engine.Players[0], new[] { new StatusEffect { Type = StatusType.AttackUp, Duration = 2, Turn = "action", Percent = 20 } });
        var icon = view.GetComponentsInChildren<StatusIconView>().Single();
        var down = typeof(StatusIconView).GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic);
        var enter = typeof(StatusIconView).GetMethod("OnMouseEnter", BindingFlags.Instance | BindingFlags.NonPublic);
        down.Invoke(icon, null);
        Assert.IsTrue(icon.transform.Find("Detail").gameObject.activeSelf);
        BeginSword();
        _drag.MovePointer(-1, ScreenSlot("back1"));
        Assert.IsFalse(icon.transform.Find("Detail").gameObject.activeSelf);
        enter.Invoke(icon, null);
        Assert.IsFalse(icon.transform.Find("Detail").gameObject.activeSelf);
        _drag.EndPointer(-1, ScreenSlot("back1"));
        _drag.AdvanceSettlement(1f);
        enter.Invoke(icon, null);
        Assert.IsTrue(icon.transform.Find("Detail").gameObject.activeSelf);
    }

    [Test]
    public void LeavingOccupiedPreviewRestoresGhostHudBeforeRelease()
    {
        BeginSword();
        _drag.MovePointer(-1, ScreenSlot("front2"));
        Assert.IsTrue(_views["guard"].transform.Find("Hud/HpText").GetComponent<Renderer>().forceRenderingOff);
        _drag.MovePointer(-1, ScreenSlot("back1"));
        Assert.IsFalse(_views["guard"].transform.Find("Hud/HpText").GetComponent<Renderer>().forceRenderingOff);
        Assert.AreEqual(1f, _views["guard"].transform.Find("Art").GetComponent<SpriteRenderer>().color.a);
        AssertPosition("guard", "front2");
        _drag.CancelAndSnap();
        AssertPosition("sword", "front1");
    }

    [Test]
    public void InterruptingSettlementSnapsToCommittedFormation_NotOldSlots()
    {
        BeginSword();
        _drag.EndPointer(-1, ScreenSlot("front2") + Vector2.right * 12f);
        _drag.AdvanceSettlement(0.04f);
        _drag.CancelAndSnap();
        AssertPosition("sword", "front2");
        AssertPosition("guard", "front1");
        Assert.IsFalse(_drag.IsSettling);
        Assert.AreEqual(1, _changes);
        Assert.IsTrue(_views.Values.All(view => !view.GetComponent<SortingGroup>().enabled));
    }

    [Test]
    public void ReconfiguringClearsPendingDragAndDoesNotDuplicateHint()
    {
        BeginSword();
        _drag.MovePointer(-1, ScreenSlot("back1"));
        _drag.Configure(_camera, _engine, _layout, _views, _slots, () => _changes++);
        AssertPosition("sword", "front1");
        Assert.IsFalse(_drag.IsDragging);
        Assert.IsFalse(_drag.IsSettling);
        Assert.AreEqual(1, _slots.Cast<Transform>().Count(child => child.name == "DropHint"));
        Assert.IsFalse(_slots.Find("DropHint").gameObject.activeSelf);
        _drag.Configure(_camera, null, _layout, _views, _slots, null);
        Assert.IsFalse(_slots.gameObject.activeSelf);
        Assert.IsFalse(_drag.BeginPointer(-1, ScreenSlot("front1"), false), "配置失败/重建期间不持有旧战斗输入");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SmallMouseOrTouchTapInspectsOnce_WithoutMovingFormation(bool touch)
    {
        int callbacks = 0;
        string inspected = null;
        _drag.OnInspect = id => { callbacks++; inspected = id; };
        var source = ScreenSlot("front1");
        Assert.IsTrue(_drag.BeginPointer(7, source, touch));
        _drag.MovePointer(7, source + Vector2.right * (_drag.DragThreshold * 0.4f));
        _drag.EndPointer(8, source);
        Assert.AreEqual(0, callbacks);
        _drag.EndPointer(7, source);
        _drag.EndPointer(7, source);
        Assert.AreEqual(1, callbacks);
        Assert.AreEqual("sword", inspected);
        AssertPosition("sword", "front1");
        Assert.AreEqual(0, _changes);
    }

    [Test]
    public void DragCancelAndMovedEnemyGestureNeverOpenInspection()
    {
        int opens = 0;
        int drags = 0;
        _drag.OnInspect = id => { if (id != null) opens++; };
        _drag.OnDragStarted = () => drags++;
        BeginSword();
        _drag.EndPointer(-1, ScreenSlot("back1"));
        _drag.AdvanceSettlement(1f);
        Assert.AreEqual(0, opens);
        Assert.AreEqual(1, drags);
        Physics2D.SyncTransforms();
        Assert.IsTrue(_drag.BeginPointer(-1, ScreenSlot("back1"), false));
        _drag.EndPointer(-1, ScreenSlot("back1"), true);
        Assert.AreEqual(0, opens);
        var enemy = _engine.Enemies[0];
        var point = ScreenWorld(_views[enemy.Id].transform.position);
        Assert.IsTrue(_drag.BeginPointer(-1, point, false));
        _drag.MovePointer(-1, point + Vector2.right * 100f);
        _drag.EndPointer(-1, point);
        Assert.AreEqual(0, opens);
    }

    [Test]
    public void EnemySetupAndBothTeamsBattleTapsInspect_WithoutMovingFormation()
    {
        string inspected = null;
        _drag.OnInspect = id => inspected = id;
        var enemy = _engine.Enemies[0];
        var enemyPoint = ScreenWorld(_views[enemy.Id].transform.position);
        Assert.IsTrue(_drag.BeginPointer(-1, enemyPoint, false));
        _drag.EndPointer(-1, enemyPoint);
        Assert.AreEqual(enemy.Id, inspected);
        _engine.StartBattle();
        inspected = "unchanged";
        Assert.IsTrue(_drag.BeginPointer(-1, ScreenSlot("front1"), false));
        _drag.RefreshAvailability();
        _drag.EndPointer(-1, ScreenSlot("front1"));
        Assert.AreEqual("sword", inspected);
        Assert.IsTrue(_drag.BeginPointer(7, enemyPoint, true));
        _drag.EndPointer(7, enemyPoint);
        Assert.AreEqual(enemy.Id, inspected);
        Assert.AreEqual(BattleFlow.Resolving, _engine.Flow);
        Assert.IsFalse(_drag.IsDragging);
        Assert.AreEqual(0, _changes);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SetupGestureReleasedAfterBattleStartsNeverOpensInspection(bool enemy)
    {
        int callbacks = 0;
        _drag.OnInspect = id => callbacks++;
        string id = enemy ? _engine.Enemies[0].Id : "sword";
        Vector2 point = ScreenWorld(_views[id].transform.position);
        Assert.IsTrue(_drag.BeginPointer(7, point, true));
        _engine.StartBattle();
        _drag.EndPointer(7, point);
        Assert.AreEqual(0, callbacks);
        Assert.IsFalse(_drag.IsDragging);
        Assert.IsFalse(_drag.IsSettling);
    }

    [Test]
    public void MovingBattleArtCanBeInspected_WithoutInterruptingAnimation()
    {
        _engine.StartBattle();
        var art = _views["sword"].transform.Find("Art").GetComponent<SpriteRenderer>();
        art.transform.localPosition = new Vector3(1.5f, 0.5f, 0f);
        var position = art.transform.localPosition;
        string inspected = null;
        _drag.OnInspect = id => inspected = id;
        var point = ScreenWorld(art.bounds.center);
        Assert.IsTrue(_drag.BeginPointer(-1, point, false));
        _drag.EndPointer(-1, point);
        Assert.AreEqual("sword", inspected);
        Assert.AreEqual(position, art.transform.localPosition, "战中点击不能打断近战位移动画");
        Assert.AreEqual(0, _changes);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void BattleGesturesRespectOwnershipAndMovementThreshold_AndNeverDrag(bool touch)
    {
        _engine.StartBattle();
        int inspections = 0;
        _drag.OnInspect = id => { if (id != null) inspections++; };
        var point = ScreenSlot("front1");
        Assert.IsTrue(_drag.BeginPointer(7, point, touch));
        _drag.EndPointer(8, point);
        Assert.AreEqual(0, inspections);
        _drag.MovePointer(7, point + Vector2.right * (_drag.DragThreshold + 1f));
        _drag.EndPointer(7, point);
        Assert.AreEqual(0, inspections);
        Assert.IsFalse(_drag.IsDragging);
        AssertPosition("sword", "front1");
        Assert.AreEqual(0, _changes);
        Assert.IsTrue(_drag.BeginPointer(7, point, touch));
        _drag.EndPointer(7, point, true);
        Assert.AreEqual(0, inspections);
    }

    [Test]
    public void FinishingBattleCancelsPressedHealthInspection()
    {
        _engine.StartBattle();
        int inspections = 0;
        _drag.OnInspect = id => inspections++;
        var point = ScreenSlot("front1");
        Assert.IsTrue(_drag.BeginPointer(-1, point, false));
        foreach (var enemy in _engine.Enemies) enemy.Hp = 0;
        _engine.Advance();
        Assert.IsTrue(_engine.IsFinished);
        _drag.RefreshAvailability();
        _drag.EndPointer(-1, point);
        Assert.AreEqual(0, inspections);
    }

    private void BeginSword() => Assert.IsTrue(_drag.BeginPointer(-1, ScreenSlot("front1"), false));
    private Vector2 ScreenSlot(string slot) => ScreenWorld(PositioningView.Position(Team.Player, slot, _layout));
    private Vector2 ScreenWorld(Vector3 position) => _camera.WorldToScreenPoint(position);
    private void AssertPosition(string id, string slot) => Assert.That(
        Vector3.Distance(_views[id].transform.position, PositioningView.Position(Team.Player, slot, _layout)), Is.LessThan(0.001f));
}
