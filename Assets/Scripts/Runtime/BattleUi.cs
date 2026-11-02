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
        public RosterView Roster { get; private set; }
        public SkillDetailsView Skills { get; private set; }
        public ElementGuideView Elements { get; private set; }
        public TextMeshProUGUI WinnerText;
        public TextMeshProUGUI SummaryText;
        public Button SpeedButton;
        public Action OnSpeedChanged;
        public int PlaybackSpeed { get; private set; } = 1;
        public bool ResultVisible => _resultPanel != null && _resultPanel.activeSelf;
        public Button StartBattleButton;
        public Button NextLevelButton;
        public TextMeshProUGUI NextLevelLabel;
        public GameObject ErrorPanel;
        public TextMeshProUGUI ErrorText;
        public Action OnStartBattle;
        public Action OnRestart;
        public Action OnNextLevel;
        public Action OnRetry;
        public Action OnReturnToMenu;
        private Button _returnButton;
        private Button _resultReturnButton;
        private Button _errorReturnButton;
        private TextMeshProUGUI _saveStatus;
        private bool _returnAvailable;
        public Action<bool> OnMuteChanged;
        public bool Muted { get; private set; }
        private TextMeshProUGUI _muteLabel;
        private TextMeshProUGUI _feedback;
        private TextMeshProUGUI _roundLabel;
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
            _roundLabel = UiKit.MakeText(CanvasRect, "1/" + TurnManager.RoundLimit, 38, Color.white, TextAnchor.MiddleCenter, true);
            _roundLabel.name = "RoundCounter";
            Rect((RectTransform)_roundLabel.transform, 0.5f, 1f, new Vector2(0f, -16f), new Vector2(520f, 72f));
            _roundLabel.outlineColor = new Color(0.02f, 0.03f, 0.05f, 1f);
            _roundLabel.outlineWidth = 0.2f;
            Roster = RosterView.Build(CanvasRect);
            Skills = SkillDetailsView.Build(CanvasRect);
            Elements = ElementGuideView.Build(CanvasRect);
            _returnButton = UiKit.MakeButton(CanvasRect, "返回主菜单", 24, ButtonBg);
            _returnButton.name = "ReturnToMenuButton";
            Rect((RectTransform)_returnButton.transform, 0.5f, 1f, new Vector2(-440f, -22f), new Vector2(220f, 60f));
            _returnButton.onClick.AddListener(() => OnReturnToMenu?.Invoke());
            _returnButton.gameObject.SetActive(false);
            _saveStatus = UiKit.MakeText(CanvasRect, "", 22, new Color(1f, 0.65f, 0.4f), TextAnchor.MiddleCenter);
            _saveStatus.name = "SaveStatus";
            _saveStatus.overflowMode = TextOverflowModes.Ellipsis;
            Rect((RectTransform)_saveStatus.transform, 0.5f, 1f, new Vector2(0f, -90f), new Vector2(800f, 55f));
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
            var mute = UiKit.MakeButton(CanvasRect, "音效：开", 22, ButtonBg);
            mute.name = "MuteButton";
            Rect((RectTransform)mute.transform, 0.5f, 0f, new Vector2(100f, 36f), new Vector2(180f, 44f));
            _muteLabel = mute.GetComponentInChildren<TextMeshProUGUI>();
            mute.onClick.AddListener(() => SetMuted(!Muted));
            _feedback = UiKit.MakeText(CanvasRect, "", 30, Color.white, TextAnchor.MiddleCenter, true);
            _feedback.name = "FeedbackText";
            Rect((RectTransform)_feedback.transform, 0.5f, 0f, new Vector2(0f, 90f), new Vector2(580f, 54f));
            _feedback.enableWordWrapping = true;
            _feedback.enableAutoSizing = true;
            _feedback.fontSizeMin = 20f;
            _feedback.fontSizeMax = 30f;
            SpeedButton = UiKit.MakeButton(CanvasRect, "速度：1×", 26, ButtonBg);
            SpeedButton.name = "SpeedButton";
            Rect((RectTransform)SpeedButton.transform, 0.5f, 0f, new Vector2(-100f, 36f), new Vector2(180f, 44f));
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
            var cardImage = panel.gameObject.AddComponent<Image>();
            UiKit.UseRoundedBackground(cardImage);
            cardImage.color = PanelBg;
            WinnerText = UiKit.MakeText(panel, "", 56, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter, true);
            Rect((RectTransform)WinnerText.transform, 0.5f, 1f, new Vector2(0f, -16f), new Vector2(1140f, 90f));
            SummaryText = UiKit.MakeText(panel, "", 26, Color.white, TextAnchor.MiddleCenter);
            Rect((RectTransform)SummaryText.transform, 0.5f, 1f, new Vector2(0f, -110f), new Vector2(1140f, 124f));
            SummaryText.enableWordWrapping = true;
            var statisticsButton = UiKit.MakeButton(panel, "查看战报", 28, ButtonBg);
            statisticsButton.name = "StatisticsButton";
            Rect((RectTransform)statisticsButton.transform, 0.5f, 1f, new Vector2(0f, -240f), new Vector2(260f, 52f));
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
            _resultReturnButton = UiKit.MakeButton(panel, "返回主菜单", 28, ButtonBg);
            _resultReturnButton.name = "ResultReturnToMenuButton";
            Rect((RectTransform)_resultReturnButton.transform, 0.5f, 0f, new Vector2(390f, 30f), new Vector2(300f, 80f));
            _resultReturnButton.onClick.AddListener(() => OnReturnToMenu?.Invoke());
            _resultReturnButton.gameObject.SetActive(false);
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
            Skills.Hide();
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
            if (_returnAvailable)
            {
                ((RectTransform)_resultRestart.transform).anchoredPosition = new Vector2(hasNext ? -390f : -180f, 30f);
                ((RectTransform)NextLevelButton.transform).anchoredPosition = new Vector2(0f, 30f);
                ((RectTransform)NextLevelButton.transform).sizeDelta = new Vector2(340f, 80f);
                ((RectTransform)_resultReturnButton.transform).anchoredPosition = new Vector2(hasNext ? 390f : 180f, 30f);
            }
            else ((RectTransform)_resultRestart.transform).anchoredPosition = new Vector2(hasNext ? -270f : 0f, 30f);
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
            _errorReturnButton = UiKit.MakeButton(panel, "返回主菜单", 30, ButtonBg);
            Rect((RectTransform)_errorReturnButton.transform, 0.5f, 0.5f, new Vector2(0f, -260f), new Vector2(300f, 80f));
            _errorReturnButton.onClick.AddListener(() => OnReturnToMenu?.Invoke());
            _errorReturnButton.gameObject.SetActive(false);
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

        public void RefreshRound(BattleEngine engine)
        {
            _roundLabel.text = (engine == null ? 1 : engine.CurrentRound) + "/" + TurnManager.RoundLimit;
        }

        public void ResetBattleFeedback()
        {
            Skills.Hide();
            RefreshRound(null);
            _feedback.text = "";
            _feedbackTime = 0f;
            WinnerText.text = "";
            WinnerText.gameObject.SetActive(false);
            SummaryText.text = "";
            _report.Clear();
            SetStatisticsVisible(false);
            HideResult();
        }

        public void SetReturnAvailable(bool available)
        {
            _returnAvailable = available;
            _returnButton.gameObject.SetActive(available);
            _resultReturnButton.gameObject.SetActive(available);
            _errorReturnButton.gameObject.SetActive(available);
        }

        public void SetSaveStatus(string error) => _saveStatus.text = error == null ? "" : "保存失败 · " + error;

        public void SetMuted(bool muted, bool notify = true)
        {
            Muted = muted;
            _muteLabel.text = muted ? "音效：关" : "音效：开";
            if (notify) OnMuteChanged?.Invoke(muted);
        }

        public void ShowFeedback(string text, Color color)
        {
            _feedback.text = text;
            _feedback.color = color;
            _feedbackTime = 1.1f;
        }

        public void ShowError(string message)
        {
            Skills.Hide();
            ErrorPanel.SetActive(true);
            ErrorText.text = message;
        }

        public void ClearError() => ErrorPanel.SetActive(false);
    }
}
