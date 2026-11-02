using System;
using System.Collections.Generic;
using System.Linq;
using CQ.Core.Battle;
using CQ.Core.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CQ.Runtime
{
    /// <summary>角色轻触查看与战前拖拽分流；仅在合法拖拽松手时提交站位。</summary>
    public sealed class FormationDrag : MonoBehaviour
    {
        private const float DropRadius = 0.95f;
        private const float SettleDuration = 0.20f;
        private Camera _camera;
        private BattleEngine _engine;
        private BattleLayout _layout;
        private IReadOnlyDictionary<string, UnitView> _views;
        private Transform _slots;
        private TextMeshPro _dropHint;
        private Action _onChanged;
        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
        private readonly Dictionary<string, Vector3> _settleFrom = new Dictionary<string, Vector3>();
        private string _actorId;
        private int _pointerId;
        private bool _touch;
        private bool _canDrag;
        private bool _tapMoved;
        public Action<string> OnInspect;
        public Action OnDragStarted;
        private Vector2 _pressPosition;
        private Vector3 _grabOffset;
        private Vector3 _origin;
        private string _sourceSlot;
        private float _settleAge;

        public bool IsDragging { get; private set; }
        public bool IsSettling => _settleFrom.Count > 0;
        public string CandidateSlot { get; private set; }
        public float DragThreshold => Mathf.Max(8f, Mathf.Min(Screen.width, Screen.height) * 0.015f);

        public void Configure(Camera camera, BattleEngine engine, BattleLayout layout,
            IReadOnlyDictionary<string, UnitView> views, Transform slots, Action onChanged)
        {
            CancelAndSnap();
            _camera = camera;
            _engine = engine;
            _layout = layout;
            _views = views;
            _slots = slots;
            _dropHint = _slots != null ? _slots.Find("DropHint")?.GetComponent<TextMeshPro>() : null;
            if (_slots != null && _dropHint == null)
            {
                _dropHint = TmpKit.MakeWorldText(_slots, "DropHint", Vector3.zero,
                    UiKit.FontBold, 0.19f, new Color(0.78f, 1f, 0.88f), 90);
                TmpKit.SetOutline(_dropHint, Color.black, 0.25f);
                _dropHint.gameObject.SetActive(false);
            }
            _onChanged = onChanged;
            RefreshAvailability();
        }

        public void RefreshAvailability()
        {
            bool setup = _engine != null && _engine.Flow == BattleFlow.Setup;
            if (!setup && (_actorId != null || IsSettling)) CancelAndSnap();
            if (_slots != null) _slots.gameObject.SetActive(setup);
            RefreshPreview();
        }

        private void Update()
        {
            if (_engine == null || _engine.Flow != BattleFlow.Setup)
            {
                if (_actorId != null || IsSettling) CancelAndSnap();
                return;
            }
            if (IsSettling) { AdvanceSettlement(Time.unscaledDeltaTime); return; }
            if (_actorId != null && _touch)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var touch = Input.GetTouch(i);
                    if (touch.fingerId != _pointerId) continue;
                    if (touch.phase == TouchPhase.Canceled) EndPointer(touch.fingerId, touch.position, true);
                    else if (touch.phase == TouchPhase.Ended) EndPointer(touch.fingerId, touch.position);
                    else MovePointer(touch.fingerId, touch.position);
                    return;
                }
                EndPointer(_pointerId, _pressPosition, true);
                return;
            }
            if (Input.touchCount > 0)
            {
                // 已有手势只认原触点；第二根手指不能接管，也不走触控模拟鼠标路径。
                if (_actorId != null) EndPointer(_pointerId, _pressPosition, true);
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var touch = Input.GetTouch(i);
                    if (touch.phase == TouchPhase.Began && BeginPointer(touch.fingerId, touch.position, true)) break;
                }
                return;
            }
            if (Input.GetMouseButtonDown(0)) BeginPointer(-1, Input.mousePosition, false);
            if (_actorId == null) return;
            if (Input.GetMouseButtonUp(0)) EndPointer(-1, Input.mousePosition);
            else if (Input.GetMouseButton(0)) MovePointer(-1, Input.mousePosition);
            else EndPointer(-1, Input.mousePosition, true);
        }

        public bool BeginPointer(int pointerId, Vector2 screenPosition, bool touch)
        {
            if (_actorId != null || IsSettling || _camera == null || _engine == null || _engine.Flow != BattleFlow.Setup ||
                OverUi(screenPosition, pointerId)) return false;
            Vector2 point = WorldPoint(screenPosition);
            var hits = Physics2D.OverlapPointAll(point);
            if (hits.Any(hit => hit.GetComponent<StatusIconView>() != null)) return false;
            var hitView = PickView(hits, point) ?? PickView(Physics2D.OverlapCircleAll(point, 0.35f), point);
            var unit = hitView != null ? _engine.Ctx.GetUnit(hitView.UnitId) : null;
            if (unit == null) { OnInspect?.Invoke(null); return false; }
            _canDrag = _engine.Flow == BattleFlow.Setup && !unit.IsDead && unit.Team == Team.Player;
            _tapMoved = false;
            _actorId = unit.Id;
            _pointerId = pointerId;
            _touch = touch;
            _pressPosition = screenPosition;
            _origin = hitView.transform.position;
            _grabOffset = _origin - WorldPoint(screenPosition);
            _sourceSlot = unit.SlotId;
            return true;
        }

        public void MovePointer(int pointerId, Vector2 screenPosition)
        {
            if (_actorId == null || pointerId != _pointerId) return;
            if (_engine.Flow != BattleFlow.Setup) { CancelAndSnap(); return; }
            if (!_canDrag)
            {
                if ((screenPosition - _pressPosition).sqrMagnitude >= DragThreshold * DragThreshold) _tapMoved = true;
                return;
            }
            if (!IsDragging)
            {
                if ((screenPosition - _pressPosition).sqrMagnitude < DragThreshold * DragThreshold) return;
                IsDragging = true;
                OnDragStarted?.Invoke();
                foreach (var view in _views.Values) view.SetFormationInteraction(true);
            }
            Vector3 position = WorldPoint(screenPosition) + _grabOffset;
            if (_touch) position.y += _camera.orthographicSize * 0.10f;
            position.z = _origin.z;
            _views[_actorId].transform.position = position;
            CandidateSlot = OverUi(screenPosition, pointerId) || !OnScreen(screenPosition)
                ? null : FindCandidate(position);
            RefreshPreview();
        }

        public void EndPointer(int pointerId, Vector2 screenPosition, bool canceled = false)
        {
            if (_actorId == null || pointerId != _pointerId) return;
            if (!canceled) MovePointer(pointerId, screenPosition);
            if (_actorId == null) return;
            string actorId = _actorId;
            bool dragged = IsDragging;
            bool inspect = !canceled && !dragged && !_tapMoved && OnScreen(screenPosition) && !OverUi(screenPosition, pointerId);
            bool canDrag = _canDrag;
            bool changed = !canceled && dragged && CandidateSlot != null && CandidateSlot != _sourceSlot &&
                _engine.Flow == BattleFlow.Setup && _engine.TryMovePlayer(actorId, CandidateSlot);
            _actorId = null;
            _canDrag = false;
            _tapMoved = false;
            IsDragging = false;
            CandidateSlot = null;
            _settleAge = 0f;
            if (canDrag)
                foreach (var unit in _engine.Players)
                {
                    var view = _views[unit.Id];
                    Vector3 destination = PositioningView.Position(unit, _layout);
                    if ((view.transform.position - destination).sqrMagnitude > 0.00001f)
                        _settleFrom[unit.Id] = view.transform.position;
                    view.SetFormationFeedback(dragged && unit.Id == actorId, false);
                }
            if (canDrag && !IsSettling) ResetFeedback();
            if (canDrag) RefreshPreview();
            if (changed) _onChanged?.Invoke();
            if (inspect) OnInspect?.Invoke(actorId);
        }

        public void AdvanceSettlement(float dt)
        {
            if (!IsSettling) return;
            _settleAge += Mathf.Max(0f, dt);
            float t = Mathf.SmoothStep(0f, 1f, _settleAge / SettleDuration);
            foreach (var pair in _settleFrom)
            {
                var unit = _engine.Ctx.GetUnit(pair.Key);
                _views[pair.Key].transform.position = Vector3.Lerp(pair.Value, PositioningView.Position(unit, _layout), t);
            }
            if (_settleAge < SettleDuration) return;
            foreach (var pair in _settleFrom)
            {
                var unit = _engine.Ctx.GetUnit(pair.Key);
                _views[pair.Key].SetPosition(PositioningView.Position(unit, _layout), unit);
            }
            _settleFrom.Clear();
            ResetFeedback();
        }

        public void CancelAndSnap()
        {
            bool active = (_actorId != null && _canDrag) || IsSettling;
            _actorId = null;
            _canDrag = false;
            _tapMoved = false;
            IsDragging = false;
            CandidateSlot = null;
            _settleFrom.Clear();
            if (active && _views != null && _engine != null)
                foreach (var unit in _engine.Players)
                    if (_views.TryGetValue(unit.Id, out var view) && view != null)
                        view.SetPosition(PositioningView.Position(unit, _layout), unit);
            ResetFeedback();
            RefreshPreview();
        }

        private void ResetFeedback()
        {
            if (_views == null) return;
            foreach (var view in _views.Values)
            {
                if (view == null) continue;
                view.SetFormationFeedback(false, false);
                view.SetFormationInteraction(false);
            }
        }

        private string FindCandidate(Vector3 position)
        {
            string nearest = null;
            float best = DropRadius * DropRadius;
            float previous = float.MaxValue;
            foreach (var slot in _engine.Ctx.FormationOf(Team.Player).Slots)
            {
                float distance = ((Vector2)(PositioningView.Position(Team.Player, slot.Id, _layout) - position)).sqrMagnitude;
                if (slot.Id == CandidateSlot) previous = distance;
                if (distance > best) continue;
                nearest = slot.Id;
                best = distance;
            }
            // 小滞回区防止相邻吸附范围边界反复闪烁。
            return previous <= DropRadius * DropRadius && previous <= best + 0.08f ? CandidateSlot : nearest;
        }

        private void RefreshPreview()
        {
            if (_slots == null || _engine == null) return;
            foreach (var slot in _engine.Ctx.FormationOf(Team.Player).Slots)
            {
                var marker = _slots.Find("Player_" + slot.Id);
                if (marker == null) continue;
                bool target = IsDragging && CandidateSlot == slot.Id && slot.Id != _sourceSlot;
                bool origin = IsDragging && slot.Id == _sourceSlot;
                marker.localScale = target ? new Vector3(1.15f, 0.50f, 1f) : new Vector3(0.95f, 0.42f, 1f);
                marker.GetComponent<SpriteRenderer>().color = target ? new Color(0.55f, 1f, 0.78f, 0.65f) :
                    new Color(0.3f, 0.8f, 1f, origin ? 0.10f : IsDragging ? 0.32f : 0.22f);
            }
            if (_dropHint != null)
            {
                bool show = IsDragging && CandidateSlot != null && CandidateSlot != _sourceSlot;
                _dropHint.gameObject.SetActive(show);
                if (show)
                {
                    var target = _engine.Ctx.FormationOf(Team.Player).GetSlot(CandidateSlot);
                    _dropHint.transform.position = PositioningView.Position(Team.Player, CandidateSlot, _layout) + Vector3.down * 1.08f;
                    _dropHint.text = target.Occupant == null ? "松手放置" : "松手交换";
                    TmpKit.FitWorldHeight(_dropHint, 0.19f);
                }
            }
            if (_views == null || IsSettling) return;
            var occupant = CandidateSlot == null ? null : _engine.Ctx.FormationOf(Team.Player).GetSlot(CandidateSlot)?.Occupant;
            foreach (var pair in _views)
                pair.Value.SetFormationFeedback(IsDragging && pair.Key == _actorId,
                    IsDragging && occupant != null && occupant.Id != _actorId && occupant.Id == pair.Key);
        }

        private Vector3 WorldPoint(Vector2 screenPosition)
        {
            var point = _camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -_camera.transform.position.z));
            point.z = 0f;
            return point;
        }

        private static bool OnScreen(Vector2 point) => point.x >= 0f && point.x <= Screen.width && point.y >= 0f && point.y <= Screen.height;

        private bool OverUi(Vector2 position, int pointerId)
        {
            var system = EventSystem.current;
            if (system == null) return false;
            var data = new PointerEventData(system) { position = position, pointerId = pointerId };
            _uiHits.Clear();
            system.RaycastAll(data, _uiHits);
            return _uiHits.Count > 0;
        }

        private UnitView PickView(Collider2D[] hits, Vector2 point)
        {
            UnitView nearest = null;
            float best = float.MaxValue;
            foreach (var hit in hits)
            {
                var view = hit.GetComponent<UnitView>();
                if (view == null || !_views.TryGetValue(view.UnitId, out var own) || own != view) continue;
                float distance = ((Vector2)hit.bounds.center - point).sqrMagnitude;
                if (distance >= best) continue;
                nearest = view;
                best = distance;
            }
            return nearest;
        }

        private void OnApplicationFocus(bool focused) { if (!focused) CancelAndSnap(); }
        private void OnApplicationPause(bool paused) { if (paused) CancelAndSnap(); }
        private void OnDisable() => CancelAndSnap();
    }
}
