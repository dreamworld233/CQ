using System;
using System.Collections.Generic;
using CQ.Core.Battle;
using CQ.Core.Combat;
using CQ.Core.Config;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CQ.Runtime
{
    /// <summary>
    /// uGUI HUD：画布 + 缩放 + 事件系统 + 左侧行动条/战报 + 底部大按钮 + 配置错误面板。
    /// 全部代码构建，零场景资产。BattleController 挂回调并驱动刷新。
    /// 文本统一 TMP（思源黑体 HW SC SDF），数值/标题用粗体。
    /// </summary>
    public sealed class BattleUi : MonoBehaviour
    {
        public RectTransform CanvasRect { get; private set; }

        public TextMeshProUGUI RoundText;
        public TextMeshProUGUI OrderText;
        public TextMeshProUGUI EventText;
        public TextMeshProUGUI WinnerText;
        public Button EndRoundButton;
        public Button RestartButton;
        public Button NextLevelButton;
        public TextMeshProUGUI NextLevelLabel;
        public GameObject LeftPanel;
        public GameObject ErrorPanel;
        public TextMeshProUGUI ErrorText;

        public Action OnEndRound;
        public Action OnRestart;
        public Action OnNextLevel;
        public Action OnRetry;

        private sealed class UltimateBinding
        {
            public string ActorId;
            public Transform Anchor;
            public Button Button;
            public TextMeshProUGUI Label;
            public BattleEngine Engine;
            public Camera Camera;
        }
        private readonly List<UltimateBinding> _ultimates = new List<UltimateBinding>();
        private bool _cardTargeting;
        private RectTransform _handContent;
        private ScrollRect _handScroll;
        private TextMeshProUGUI _handHint;
        private Button _cancelCard;
        private readonly List<Button> _handButtons = new List<Button>();
        public Action<int> OnSelectCard;
        public Action OnCancelCard;

        // LateUpdate must use the same input gate as the controller, not overwrite it with readiness.
        public void SetCardTargeting(bool targeting)
        {
            _cardTargeting = targeting;
            foreach (var binding in _ultimates)
            {
                bool ready = !targeting && binding.Engine.CanRequestUltimate(binding.ActorId);
                binding.Button.interactable = ready;
                binding.Button.targetGraphic.raycastTarget = ready;
            }
        }

        public void ClearUltimateBindings()
        {
            foreach (var binding in _ultimates)
                if (binding.Button != null)
                {
                    binding.Button.onClick.RemoveAllListeners();
                    binding.Button.gameObject.SetActive(false);
                    Destroy(binding.Button.gameObject);
                }
            _ultimates.Clear();
        }

        public void BindUltimate(string actorId, Transform anchor, BattleEngine engine, Camera camera, Action<string> request)
        {
            var button = UiKit.MakeButton(CanvasRect, "未满", 24, ButtonBg);
            var rt = (RectTransform)button.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(144f, 48f);
            button.onClick.AddListener(() => request(actorId));
            _ultimates.Add(new UltimateBinding { ActorId = actorId, Anchor = anchor, Engine = engine,
                Camera = camera, Button = button, Label = button.GetComponentInChildren<TextMeshProUGUI>() });
        }

        private void LateUpdate()
        {
            foreach (var binding in _ultimates)
            {
                if (binding.Anchor == null || binding.Camera == null) continue;
                var engine = binding.Engine;
                var actor = engine.Ctx.GetUnit(binding.ActorId);
                bool setup = engine.Flow == BattleFlow.Setup;
                binding.Button.gameObject.SetActive(!setup);
                if (setup) continue;
                bool executing = engine.CurrentAction?.ActorId == binding.ActorId &&
                    engine.CurrentAction.ActionId == engine.UltimateOf(binding.ActorId)?.id;
                bool ready = !_cardTargeting && engine.CanRequestUltimate(binding.ActorId);
                binding.Label.text = engine.IsFinished ? "结束" : actor.IsDead ? "死亡" : executing ? "释放中" :
                    engine.HasUltimateRequest(binding.ActorId) ? "排队" : ready ? "就绪 · 释放" : "未满";
                binding.Button.interactable = ready;
                ((Image)binding.Button.targetGraphic).color = ready ? new Color(0.65f, 0.38f, 0.06f, 1f) : ButtonBg;
                // Only enabled ready buttons capture input. All other presentation stays click-through.
                binding.Button.targetGraphic.raycastTarget = ready;
                var screen = binding.Camera.WorldToScreenPoint(binding.Anchor.position + Vector3.up * 1.06f);
                var safe = Screen.safeArea;
                float scale = CanvasRect.GetComponent<Canvas>().scaleFactor;
                float halfW = 72f * scale, halfH = 24f * scale;
                screen.x = Mathf.Clamp(screen.x, Mathf.Max(safe.xMin + halfW, 382f * scale + halfW), safe.xMax - halfW);
                screen.y = Mathf.Clamp(screen.y, safe.yMin + halfH, safe.yMax - halfH);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(CanvasRect, screen, null, out var local);
                ((RectTransform)binding.Button.transform).anchoredPosition = local;
            }
        }

        private static readonly Color PanelBg = new Color(0.07f, 0.10f, 0.14f, 0.92f);
        private static readonly Color ButtonBg = new Color(0.16f, 0.22f, 0.30f, 0.98f);

        public static BattleUi Build(Transform owner)
        {
            var go = new GameObject("BattleUi", typeof(BattleUi));
            go.transform.SetParent(owner, false);
            return go.GetComponent<BattleUi>();
        }

        private void Awake()
        {
            BuildAll();
        }

        private void BuildAll()
        {
            BuildCanvas();
            BuildEventSystem();
            BuildLeftPanel();
            BuildBottomControls();
            BuildHand();
            BuildErrorPanel();
        }

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            CanvasRect = (RectTransform)canvasGo.transform;
        }

        private void BuildEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            es.transform.SetParent(null);
        }

        private const float PanelW = 340f;   // 左栏宽度；过宽会盖住最左侧敌人

        private void BuildLeftPanel()
        {
            var panelRt = UiKit.AddRect(CanvasRect, "LeftPanel",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(26f, 0f), new Vector2(PanelW, 1000f));
            var img = panelRt.gameObject.AddComponent<Image>();
            img.color = PanelBg;
            img.raycastTarget = false;   // 纯展示：不挡下方世界单位的点选
            LeftPanel = panelRt.gameObject;

            float inner = PanelW - 36f;

            var title = UiKit.MakeText(panelRt, "行动条", 44, new Color(0.55f, 0.80f, 1f), TextAnchor.MiddleLeft, true);
            Rect((RectTransform)title.transform, 0f, 1f, 0f, 1f, new Vector2(18f, -16f), new Vector2(inner, 52f));

            RoundText = UiKit.MakeText(panelRt, "第 1 回合", 40, Color.white, TextAnchor.MiddleLeft, true);
            Rect((RectTransform)RoundText.transform, 0f, 1f, 0f, 1f, new Vector2(18f, -76f), new Vector2(inner, 48f));

            var orderTitle = UiKit.MakeText(panelRt, "出手顺序", 32, new Color(0.75f, 0.80f, 0.85f), TextAnchor.UpperLeft);
            Rect((RectTransform)orderTitle.transform, 0f, 1f, 0f, 1f, new Vector2(18f, -140f), new Vector2(inner, 40f));

            OrderText = UiKit.MakeText(panelRt, "", 34, Color.white, TextAnchor.UpperLeft);
            OrderText.enableWordWrapping = true;
            Rect((RectTransform)OrderText.transform, 0f, 1f, 0f, 1f, new Vector2(22f, -190f), new Vector2(inner - 8f, 380f));

            // 战报：逐条伤害/治疗（最新一条在最下）
            var logTitle = UiKit.MakeText(panelRt, "战报", 30, new Color(0.75f, 0.80f, 0.85f), TextAnchor.MiddleLeft);
            Rect((RectTransform)logTitle.transform, 0f, 0f, 0f, 0f, new Vector2(18f, 382f), new Vector2(inner, 34f));

            EventText = UiKit.MakeText(panelRt, "", 24, new Color(1f, 0.88f, 0.55f), TextAnchor.LowerLeft);
            EventText.enableWordWrapping = true;
            Rect((RectTransform)EventText.transform, 0f, 0f, 0f, 0f, new Vector2(18f, 20f), new Vector2(inner, 352f));

            WinnerText = UiKit.MakeText(panelRt, "", 46, Color.white, TextAnchor.MiddleCenter, true);
            Rect((RectTransform)WinnerText.transform, 0.5f, 0.5f, 0.5f, 0.5f, new Vector2(0f, 0f), new Vector2(inner, 60f));
            TmpKit.SetOutline(WinnerText, new Color(0f, 0f, 0f, 1f), 0.25f);
            WinnerText.gameObject.SetActive(false);
        }

        private void BuildBottomControls()
        {
            // 锚点/pivot 必须成对相同，且用 (1,0)=右下；(ax,ay,bx,by) 传不一致会得到反向锚点矩形（近全屏），
            // 会盖住战场：既吞掉所有世界点击，又让点空白触发结束回合。

            // 结束回合：右下角大按钮
            EndRoundButton = UiKit.MakeButton(CanvasRect, "开始行动", 44, ButtonBg);
            Rect((RectTransform)EndRoundButton.transform, 1f, 0f, 1f, 0f, new Vector2(-36f, 36f), new Vector2(300f, 130f));
            EndRoundButton.onClick.AddListener(() => OnEndRound?.Invoke());

            // 重开
            RestartButton = UiKit.MakeButton(CanvasRect, "重开", 36, ButtonBg);
            Rect((RectTransform)RestartButton.transform, 1f, 0f, 1f, 0f, new Vector2(-356f, 46f), new Vector2(180f, 90f));
            RestartButton.onClick.AddListener(() => OnRestart?.Invoke());

            // 下一关：胜利时显示（屏幕中上）
            NextLevelButton = UiKit.MakeButton(CanvasRect, "", 38, ButtonBg);
            Rect((RectTransform)NextLevelButton.transform, 0.5f, 0.5f, 0.5f, 0.5f, new Vector2(0f, 220f), new Vector2(420f, 100f));
            NextLevelLabel = NextLevelButton.GetComponentInChildren<TextMeshProUGUI>();
            NextLevelButton.onClick.AddListener(() => OnNextLevel?.Invoke());
            NextLevelButton.gameObject.SetActive(false);
        }

        private void BuildHand()
        {
            // The central lower gap leaves the left HUD, back-row units and both right controls free.
            var panel = UiKit.AddRect(CanvasRect, "Hand", new Vector2(0.4f, 0f), new Vector2(1f, 0f),
                Vector2.zero, Vector2.zero, Vector2.zero);
            panel.offsetMin = new Vector2(0f, 16f);
            panel.offsetMax = new Vector2(-556f, 196f);
            panel.gameObject.AddComponent<Image>().color = PanelBg;
            _handHint = UiKit.MakeText(panel, "手牌", 22, Color.white, TextAnchor.MiddleLeft);
            var hintRt = (RectTransform)_handHint.transform;
            hintRt.anchorMin = new Vector2(0f, 1f);
            hintRt.anchorMax = Vector2.one;
            hintRt.offsetMin = new Vector2(10f, -52f);
            hintRt.offsetMax = new Vector2(-132f, -4f);
            _handHint.enableWordWrapping = true;
            _handHint.enableAutoSizing = true;
            _handHint.fontSizeMin = 16f;
            _handHint.fontSizeMax = 22f;

            _cancelCard = UiKit.MakeButton(panel, "取消选牌", 22, ButtonBg);
            Rect((RectTransform)_cancelCard.transform, 1f, 1f, 1f, 1f,
                new Vector2(-8f, -8f), new Vector2(120f, 40f));
            _cancelCard.onClick.AddListener(() => OnCancelCard?.Invoke());

            var viewport = UiKit.AddRect(panel, "HandViewport", Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, Vector2.zero);
            viewport.offsetMin = new Vector2(8f, 18f);
            viewport.offsetMax = new Vector2(-8f, -56f);
            viewport.gameObject.AddComponent<Image>().color = new Color(0.06f, 0.09f, 0.13f);
            viewport.gameObject.AddComponent<RectMask2D>();
            _handContent = UiKit.AddRect(viewport, "Cards", new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            _handScroll = viewport.gameObject.AddComponent<ScrollRect>();
            _handScroll.viewport = viewport;
            _handScroll.content = _handContent;
            _handScroll.horizontal = true;
            _handScroll.vertical = false;
            _handScroll.movementType = ScrollRect.MovementType.Clamped;
            _handScroll.scrollSensitivity = 36f;

            var barRt = UiKit.AddRect(panel, "HandScrollbar", Vector2.zero, new Vector2(1f, 0f),
                Vector2.zero, new Vector2(8f, 4f), new Vector2(-16f, 10f));
            barRt.gameObject.AddComponent<Image>().color = ButtonBg;
            var handle = UiKit.AddRect(barRt, "Handle", Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, Vector2.zero);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = new Color(0.4f, 0.65f, 0.8f);
            var scrollbar = barRt.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.LeftToRight;
            _handScroll.horizontalScrollbar = scrollbar;
        }

        public void RefreshHand(BattleEngine engine, int selectedIndex, string targetHint)
        {
            int count = engine != null ? engine.Hand.Count : 0;
            while (_handButtons.Count < count)
            {
                int index = _handButtons.Count;
                var button = UiKit.MakeButton(_handContent, "", 23, ButtonBg);
                Rect((RectTransform)button.transform, 0f, 0.5f, 0f, 0.5f,
                    new Vector2(index * 212f, 0f), new Vector2(204f, 102f));
                var label = button.GetComponentInChildren<TextMeshProUGUI>();
                label.enableWordWrapping = true;
                label.enableAutoSizing = true;
                label.fontSizeMin = 16f;
                label.fontSizeMax = 23f;
                var labelRt = (RectTransform)label.transform;
                labelRt.offsetMin = new Vector2(8f, 6f);
                labelRt.offsetMax = new Vector2(-8f, -6f);
                button.onClick.AddListener(() => OnSelectCard?.Invoke(index));
                _handButtons.Add(button);
            }
            for (int i = 0; i < _handButtons.Count; i++)
            {
                var button = _handButtons[i];
                button.gameObject.SetActive(i < count);
                if (i >= count) continue;
                var card = engine.GetCard(engine.Hand[i]);
                bool selected = i == selectedIndex;
                button.GetComponentInChildren<TextMeshProUGUI>().text =
                    (selected ? "已选 " : "") + (card != null ? card.name : engine.Hand[i]) + "\n" + CardDescription(card?.effect);
                button.interactable = engine.CanPlayCards;
                ((Image)button.targetGraphic).color = selected ? new Color(0.48f, 0.34f, 0.08f) : ButtonBg;
            }
            _handContent.sizeDelta = new Vector2(Mathf.Max(0f, count * 212f - 8f), 0f);
            _cancelCard.interactable = selectedIndex >= 0;
            _handHint.text = "手牌 " + count + " · " + (selectedIndex >= 0 ? targetHint :
                engine != null && engine.CanPlayCards ? "点卡选目标；横向拖动查看全部" : "仅策略窗口可出牌");
        }

        private static string CardDescription(CardEffectSpec effect)
        {
            if (effect == null) return "未知效果";
            switch (effect.type)
            {
                case "interrupt": return "打断存活敌人本回合行动";
                case "weaken": return "存活敌人伤害降低 " + effect.percent + "%（本回合）";
                case "slow": return "存活敌人减速 " + effect.percent + "%（本回合）";
                case "haste": return "存活我方加速 " + effect.percent + "%（本回合）";
                case "shield": return "存活我方获得护盾 " + effect.amount;
                case "attackUp": return "存活我方伤害提高 " + effect.percent + "%（本回合）";
                case "tauntUp": return "任意格嘲讽 +" + effect.amount + "（" + effect.duration + "回合）";
                case "tauntDown": return "任意格嘲讽 -" + effect.amount + "（" + effect.duration + "回合）";
                default: return effect.type;
            }
        }

        private void BuildErrorPanel()
        {
            var rt = UiKit.AddRect(CanvasRect, "ErrorPanel",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.9f);
            ErrorPanel = rt.gameObject;

            ErrorText = UiKit.MakeText(rt, "", 40, Color.white, TextAnchor.MiddleCenter);
            Rect((RectTransform)ErrorText.transform, 0.5f, 0.5f, 0.5f, 0.5f, new Vector2(0f, 160f), new Vector2(1600f, 400f));

            var retry = UiKit.MakeButton(rt, "重试加载", 40, ButtonBg);
            Rect((RectTransform)retry.transform, 0.5f, 0.5f, 0.5f, 0.5f, new Vector2(0f, -100f), new Vector2(300f, 110f));
            retry.onClick.AddListener(() => OnRetry?.Invoke());

            ErrorPanel.SetActive(false);
        }

        private static void Rect(RectTransform rt, float ax, float ay, float bx, float by, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = new Vector2(ax, ay);
            rt.anchorMax = new Vector2(bx, by);
            rt.pivot = new Vector2(ax, ay);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        public void ShowError(string msg)
        {
            LeftPanel.SetActive(false);
            ErrorPanel.SetActive(true);
            ErrorText.text = msg;
        }

        public void ClearError()
        {
            LeftPanel.SetActive(true);
            ErrorPanel.SetActive(false);
        }

        public void Refresh(int round, string orderLines, string winnerText, string nextLevelLabel, bool showNextLevel)
        {
            RoundText.text = "第 " + round + " 回合";
            OrderText.text = orderLines;
            bool hasWinner = !string.IsNullOrEmpty(winnerText);
            WinnerText.text = hasWinner ? winnerText : "";
            WinnerText.gameObject.SetActive(hasWinner);
            NextLevelLabel.text = nextLevelLabel;
            NextLevelButton.gameObject.SetActive(showNextLevel);
        }

        /// <summary>战报文本（已按行拼好的整段）。</summary>
        public void SetEvent(string msg)
        {
            EventText.text = msg;
        }
    }
}
