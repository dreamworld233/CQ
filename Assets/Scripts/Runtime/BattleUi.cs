using System;
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
            EndRoundButton = UiKit.MakeButton(CanvasRect, "结束回合", 44, ButtonBg);
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
