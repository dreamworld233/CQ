using System;
using System.Collections;
using CQ.Core.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CQ.Runtime.App
{
    public sealed class ApplicationOverlay : MonoBehaviour
    {
        private RectTransform _canvas;
        private GameObject _dialog;
        private TextMeshProUGUI _dialogTitle;
        private TextMeshProUGUI _dialogText;
        private Button _confirm;
        private Button _cancel;
        private Action _onConfirm;
        private GameObject _settingsPanel;
        private Slider _master;
        private Slider _effects;
        private TextMeshProUGUI _mute;
        private TextMeshProUGUI _settingsError;
        private bool _muted;
        private CanvasGroup _transition;
        private TextMeshProUGUI _loading;
        public bool ModalVisible => _dialog.activeSelf || _settingsPanel.activeSelf;
        public bool IsBlocking => ModalVisible || _transition.blocksRaycasts;

        public void Build()
        {
            _canvas = AppUi.Canvas(transform, "ApplicationOverlay", 500);
            _dialog = Shade(_canvas, "Confirmation");
            var card = AppUi.Box(_dialog.transform, "DialogCard", Vector2.zero, new Vector2(900f, 450f), AppUi.Card);
            _dialogTitle = AppUi.Text(card, "Title", "", 36, new Vector2(0f, 145f), new Vector2(800f, 65f), AppUi.Accent);
            _dialogText = AppUi.Text(card, "Message", "", 27, new Vector2(0f, 25f), new Vector2(800f, 150f));
            _confirm = AppUi.MakeButton(card, "ConfirmButton", "确认", new Vector2(180f, -140f), new Vector2(280f, 70f), () =>
            {
                var callback = _onConfirm;
                CloseModal();
                callback?.Invoke();
            });
            _cancel = AppUi.MakeButton(card, "CancelButton", "取消", new Vector2(-180f, -140f), new Vector2(280f, 70f), CloseModal);
            _dialog.SetActive(false);
            BuildSettings();
            var transition = Shade(_canvas, "Transition");
            transition.GetComponent<Image>().color = AppUi.Background;
            _transition = transition.AddComponent<CanvasGroup>();
            _loading = AppUi.Text(transition.transform, "LoadingText", "", 32, Vector2.zero, new Vector2(1300f, 100f), AppUi.Accent, true);
            _transition.alpha = 0f;
            _transition.blocksRaycasts = false;
        }

        private static GameObject Shade(Transform parent, string name)
        {
            var rect = UiKit.AddRect(parent, name, Vector2.zero, Vector2.one, Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
            rect.gameObject.AddComponent<Image>().color = new Color(0.015f, 0.025f, 0.03f, 0.88f);
            return rect.gameObject;
        }

        public void Confirm(string title, string message, Action confirm, string label = "确认")
        {
            CloseModal();
            _dialogTitle.text = title;
            _dialogText.text = message;
            _confirm.GetComponentInChildren<TextMeshProUGUI>().text = label;
            _cancel.gameObject.SetActive(true);
            _onConfirm = confirm;
            _dialog.SetActive(true);
        }

        public void Notice(string title, string message)
        {
            Confirm(title, message, null, "知道了");
            _cancel.gameObject.SetActive(false);
        }

        public void CloseModal()
        {
            _dialog.SetActive(false);
            _settingsPanel?.SetActive(false);
            _onConfirm = null;
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
        }

        private void BuildSettings()
        {
            _settingsPanel = Shade(_canvas, "SettingsPanel");
            var card = AppUi.Box(_settingsPanel.transform, "SettingsCard", Vector2.zero, new Vector2(950f, 680f), AppUi.Card);
            AppUi.Text(card, "Title", "设置", 42, new Vector2(0f, 260f), new Vector2(830f, 70f), AppUi.Accent);
            _master = MakeSlider(card, "MasterVolume", "主音量", 130f);
            _effects = MakeSlider(card, "EffectsVolume", "音效音量", 15f);
            var mute = AppUi.MakeButton(card, "SettingsMute", "", new Vector2(0f, -100f), new Vector2(450f, 65f), () =>
            { _muted = !_muted; RefreshMute(); });
            _mute = mute.GetComponentInChildren<TextMeshProUGUI>();
            _settingsError = AppUi.Text(card, "SettingsError", "", 23, new Vector2(0f, -185f), new Vector2(830f, 70f));
            AppUi.MakeButton(card, "SaveSettings", "保存设置", new Vector2(200f, -270f), new Vector2(300f, 70f), () =>
            {
                var data = UserSettings.Defaults();
                data.masterVolume = _master.value;
                data.effectsVolume = _effects.value;
                data.muted = _muted;
                if (GameApplication.Instance.SaveSettings(data, out var error)) CloseModal();
                else _settingsError.text = error;
            });
            AppUi.MakeButton(card, "CancelSettings", "取消", new Vector2(-200f, -270f), new Vector2(300f, 70f), CloseModal);
            _settingsPanel.SetActive(false);
        }

        private static Slider MakeSlider(Transform parent, string name, string label, float y)
        {
            AppUi.Text(parent, name + "Label", label, 28, new Vector2(-240f, y), new Vector2(300f, 55f));
            var track = AppUi.Box(parent, name, new Vector2(130f, y), new Vector2(420f, 46f), new Color(0.13f, 0.20f, 0.21f));
            var slider = track.gameObject.AddComponent<Slider>();
            var handle = AppUi.Box(track, "Handle", Vector2.zero, new Vector2(24f, 54f), AppUi.Accent);
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.direction = Slider.Direction.LeftToRight;
            var value = AppUi.Text(parent, name + "Value", "100%", 24, new Vector2(395f, y), new Vector2(90f, 55f), AppUi.Secondary, true);
            slider.onValueChanged.AddListener(number => value.text = Mathf.RoundToInt(number * 100f) + "%");
            return slider;
        }

        private void RefreshMute() => _mute.text = _muted ? "静音：开" : "静音：关";

        public void ShowSettings(UserSettings settings)
        {
            CloseModal();
            _master.value = settings.masterVolume;
            _effects.value = settings.effectsVolume;
            _muted = settings.muted;
            RefreshMute();
            _settingsError.text = "设置全局生效，不随存档切换。";
            _settingsPanel.SetActive(true);
        }

        public IEnumerator Fade(bool cover, string message = "")
        {
            _transition.blocksRaycasts = true;
            _loading.text = message;
            float start = _transition.alpha;
            float target = cover ? 1f : 0f;
            for (float elapsed = 0f; elapsed < 0.22f; elapsed += Time.unscaledDeltaTime)
            {
                _transition.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / 0.22f));
                yield return null;
            }
            _transition.alpha = target;
            if (!cover) { _transition.blocksRaycasts = false; _loading.text = ""; }
        }
    }
}
