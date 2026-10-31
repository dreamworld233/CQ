using System;
using System.Collections.Generic;
using CQ.Core.Battle;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CQ.Runtime
{
    public sealed class BattleUi : MonoBehaviour
    {
        private static readonly Color PanelBg = new Color(0.07f, 0.10f, 0.14f, 0.98f);
        private static readonly Color ButtonBg = new Color(0.16f, 0.22f, 0.30f, 0.98f);
        public RectTransform CanvasRect { get; private set; }
        public TextMeshProUGUI WinnerText;
        public TextMeshProUGUI SummaryText;
        public Button SpeedButton;
        public Action OnSpeedChanged;
        public int PlaybackSpeed { get; private set; } = 1;
        public bool ResultVisible => _resultPanel != null && _resultPanel.activeSelf;
        public Button StartBattleButton;
        public Button RestartButton;
        public Button NextLevelButton;
        public TextMeshProUGUI NextLevelLabel;
        public GameObject ErrorPanel;
        public TextMeshProUGUI ErrorText;
        public Action OnStartBattle;
        public Action OnRestart;
        public Action OnNextLevel;
        public Action OnRetry;
        public Action<bool> OnMuteChanged;
        public bool Muted { get; private set; }
        private TextMeshProUGUI _muteLabel;
        private TextMeshProUGUI _feedback;
        private float _feedbackTime;
        private GameObject _resultPanel;
        private TextMeshProUGUI _speedLabel;
        private BattleReportView _report;
        private TextMeshProUGUI _statisticsLabel;
        private Button _resultRestart;

        public static BattleUi Build(Transform owner)
        {
            var go = new GameObject("BattleUi", typeof(BattleUi));
            go.transform.SetParent(owner, false);
            return go.GetComponent<BattleUi>();
        }

        private void Awake() => BuildAll();

        private void BuildAll()
        {
            BuildCanvas();
            BuildEventSystem();
            BuildBottomControls();
            BuildResultPanel();
            BuildErrorPanel();
        }

        private void BuildCanvas()
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            CanvasRect = (RectTransform)go.transform;
        }

        private static void BuildEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private void BuildBottomControls()
        {
            StartBattleButton = UiKit.MakeButton(CanvasRect, "开战", 44, ButtonBg);
            StartBattleButton.name = "StartBattleButton";
            Rect((RectTransform)StartBattleButton.transform, 1f, 0f, new Vector2(-36f, 36f), new Vector2(300f, 130f));
            StartBattleButton.onClick.AddListener(() => OnStartBattle?.Invoke());
            RestartButton = UiKit.MakeButton(CanvasRect, "重开", 36, ButtonBg);
            Rect((RectTransform)RestartButton.transform, 1f, 0f, new Vector2(-356f, 46f), new Vector2(180f, 90f));
            RestartButton.onClick.AddListener(() => OnRestart?.Invoke());
            var mute = UiKit.MakeButton(CanvasRect, "音效：开", 22, ButtonBg);
            mute.name = "MuteButton";
            Rect((RectTransform)mute.transform, 1f, 0f, new Vector2(-36f, 180f), new Vector2(180f, 44f));
            _muteLabel = mute.GetComponentInChildren<TextMeshProUGUI>();
            mute.onClick.AddListener(() => SetMuted(!Muted));
            _feedback = UiKit.MakeText(CanvasRect, "", 30, Color.white, TextAnchor.MiddleRight, true);
            _feedback.name = "FeedbackText";
            Rect((RectTransform)_feedback.transform, 1f, 0f, new Vector2(-36f, 236f), new Vector2(740f, 54f));
            _feedback.enableWordWrapping = true;
            _feedback.enableAutoSizing = true;
            _feedback.fontSizeMin = 20f;
            _feedback.fontSizeMax = 30f;
            SpeedButton = UiKit.MakeButton(CanvasRect, "速度：1×", 26, ButtonBg);
            SpeedButton.name = "SpeedButton";
            Rect((RectTransform)SpeedButton.transform, 1f, 0f, new Vector2(-236f, 180f), new Vector2(180f, 44f));
            _speedLabel = SpeedButton.GetComponentInChildren<TextMeshProUGUI>();
            SpeedButton.onClick.AddListener(() =>
            {
                PlaybackSpeed = PlaybackSpeed == 1 ? 2 : 1;
                _speedLabel.text = "速度：" + PlaybackSpeed + "×";
                OnSpeedChanged?.Invoke();
            });
        }

        private void BuildResultPanel()
        {
            var overlay = UiKit.AddRect(CanvasRect, "ResultPanel", Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            overlay.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.04f, 0.07f, 0.88f);
            _resultPanel = overlay.gameObject;
            var panel = UiKit.AddRect(overlay, "ResultCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1260f, 880f));
            panel.gameObject.AddComponent<Image>().color = PanelBg;
            WinnerText = UiKit.MakeText(panel, "", 56, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter, true);
            Rect((RectTransform)WinnerText.transform, 0.5f, 1f, new Vector2(0f, -24f), new Vector2(1140f, 74f));
            SummaryText = UiKit.MakeText(panel, "", 26, Color.white, TextAnchor.MiddleCenter);
            Rect((RectTransform)SummaryText.transform, 0.5f, 1f, new Vector2(0f, -110f), new Vector2(1140f, 100f));
            SummaryText.enableWordWrapping = true;
            var statisticsButton = UiKit.MakeButton(panel, "查看战报", 28, ButtonBg);
            statisticsButton.name = "StatisticsButton";
            Rect((RectTransform)statisticsButton.transform, 0.5f, 1f, new Vector2(0f, -225f), new Vector2(260f, 52f));
            _statisticsLabel = statisticsButton.GetComponentInChildren<TextMeshProUGUI>();
            statisticsButton.onClick.AddListener(() => SetStatisticsVisible(!_report.gameObject.activeSelf));
            _report = BattleReportView.Build(panel);
            NextLevelButton = UiKit.MakeButton(panel, "", 30, ButtonBg);
            NextLevelButton.name = "NextLevelButton";
            Rect((RectTransform)NextLevelButton.transform, 0.5f, 0f, new Vector2(210f, 30f), new Vector2(480f, 80f));
            NextLevelLabel = NextLevelButton.GetComponentInChildren<TextMeshProUGUI>();
            NextLevelLabel.enableAutoSizing = true;
            NextLevelLabel.fontSizeMin = 22f;
            NextLevelButton.onClick.AddListener(() => OnNextLevel?.Invoke());
            _resultRestart = UiKit.MakeButton(panel, "重新挑战", 30, ButtonBg);
            _resultRestart.name = "ResultRestartButton";
            Rect((RectTransform)_resultRestart.transform, 0.5f, 0f, new Vector2(-270f, 30f), new Vector2(300f, 80f));
            _resultRestart.onClick.AddListener(() => OnRestart?.Invoke());
            NextLevelButton.gameObject.SetActive(false);
            SetStatisticsVisible(false);
            _resultPanel.SetActive(false);
        }

        private void SetStatisticsVisible(bool visible)
        {
            _report.gameObject.SetActive(visible);
            _statisticsLabel.text = visible ? "收起战报" : "查看战报";
        }

        public void ShowResult(string title, string summary, IReadOnlyList<UnitBattleStatistics> rows, string nextLabel, bool hasNext)
        {
            WinnerText.text = title;
            WinnerText.color = title == "我方失败" ? new Color(1f, 0.45f, 0.4f) : new Color(1f, 0.85f, 0.3f);
            WinnerText.gameObject.SetActive(true);
            _feedback.text = "";
            _feedbackTime = 0f;
            SummaryText.text = summary;
            _report.Show(rows);
            SetStatisticsVisible(false);
            NextLevelLabel.text = nextLabel;
            NextLevelButton.gameObject.SetActive(hasNext);
            ((RectTransform)_resultRestart.transform).anchoredPosition = new Vector2(hasNext ? -270f : 0f, 30f);
            _resultPanel.SetActive(true);
        }

        public void HideResult()
        {
            _resultPanel.SetActive(false);
            NextLevelButton.gameObject.SetActive(false);
        }

        private void BuildErrorPanel()
        {
            var panel = UiKit.AddRect(CanvasRect, "ErrorPanel", Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            panel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.9f);
            ErrorPanel = panel.gameObject;
            ErrorText = UiKit.MakeText(panel, "", 40, Color.white, TextAnchor.MiddleCenter);
            Rect((RectTransform)ErrorText.transform, 0.5f, 0.5f, new Vector2(0f, 160f), new Vector2(1600f, 400f));
            var retry = UiKit.MakeButton(panel, "重试加载", 40, ButtonBg);
            Rect((RectTransform)retry.transform, 0.5f, 0.5f, new Vector2(0f, -100f), new Vector2(300f, 110f));
            retry.onClick.AddListener(() => OnRetry?.Invoke());
            ErrorPanel.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_feedbackTime <= 0f) return;
            _feedbackTime = Mathf.Max(0f, _feedbackTime - Time.unscaledDeltaTime);
            _feedback.alpha = Mathf.Clamp01(_feedbackTime / 0.25f);
        }

        private static void Rect(RectTransform rect, float x, float y, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(x, y);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public void ResetBattleFeedback()
        {
            _feedback.text = "";
            _feedbackTime = 0f;
            WinnerText.text = "";
            WinnerText.gameObject.SetActive(false);
            SummaryText.text = "";
            _report.Clear();
            SetStatisticsVisible(false);
            HideResult();
        }

        public void SetMuted(bool muted)
        {
            Muted = muted;
            _muteLabel.text = muted ? "音效：关" : "音效：开";
            OnMuteChanged?.Invoke(muted);
        }

        public void ShowFeedback(string text, Color color)
        {
            _feedback.text = text;
            _feedback.color = color;
            _feedbackTime = 1.1f;
        }

        public void ShowError(string message)
        {
            ErrorPanel.SetActive(true);
            ErrorText.text = message;
        }

        public void ClearError() => ErrorPanel.SetActive(false);
    }
}
