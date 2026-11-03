using System;
using CQ.Runtime.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CQ.Runtime.App
{
    public sealed class MainMenuView : MonoBehaviour
    {
        private GameApplication _app;
        private GameObject _home;
        private GameObject _slots;
        private TextMeshProUGUI _slotTitle;
        private TextMeshProUGUI _slotHint;
        private TMP_InputField _name;
        private GameObject _nameRow;
        private readonly TextMeshProUGUI[] _descriptions = new TextMeshProUGUI[LocalSaveStore.SlotCount];
        private readonly Button[] _primary = new Button[LocalSaveStore.SlotCount];
        private readonly Button[] _delete = new Button[LocalSaveStore.SlotCount];
        private bool _creating;

        public void Build(GameApplication app)
        {
            _app = app;
            var canvas = AppUi.Canvas(transform, "MainMenuCanvas", 100);
            var background = UiKit.MakePanel(canvas, AppUi.Background);
            background.name = "MenuBackground";
            // 不把临时角色立绘烘焙进菜单；色块与排版可独立替换为正式背景/预制体。
            var rail = AppUi.Box(canvas, "IdentityCard", new Vector2(-540f, 0f), new Vector2(540f, 820f), AppUi.Card);
            AppUi.Text(rail, "ProjectLabel", "CQ  /  战斗 Demo", 28, new Vector2(0f, 300f), new Vector2(430f, 60f), AppUi.Secondary);
            AppUi.Text(rail, "Title", "与你的伙伴\n开启旅程", 58, new Vector2(0f, 115f), new Vector2(430f, 230f), AppUi.Accent);
            AppUi.Text(rail, "Subtitle", "选择伙伴 · 调整布阵\n观看每一次并肩作战", 27, new Vector2(0f, -110f), new Vector2(430f, 120f), AppUi.Secondary);
            AppUi.Text(rail, "Version", "本地存档  /  单机体验", 22, new Vector2(0f, -320f), new Vector2(430f, 50f), AppUi.Secondary);
            var body = UiKit.AddRect(canvas, "MenuBody", Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.one * 0.5f, new Vector2(310f, 0f), new Vector2(1000f, 820f));
            _home = new GameObject("Home", typeof(RectTransform));
            _home.transform.SetParent(body, false);
            AppUi.Place((RectTransform)_home.transform, Vector2.zero, new Vector2(1000f, 820f));
            AppUi.Text(_home.transform, "Welcome", "旅程从这里开始", 44, new Vector2(0f, 295f), new Vector2(720f, 90f), AppUi.Accent);
            AppUi.MakeButton(_home.transform, "NewGame", "新建游戏", new Vector2(0f, 115f), new Vector2(650f, 82f), () => ShowSlots(true));
            AppUi.MakeButton(_home.transform, "LoadGame", "加载存档", new Vector2(0f, 5f), new Vector2(650f, 82f), () => ShowSlots(false));
            AppUi.MakeButton(_home.transform, "Settings", "设置", new Vector2(-170f, -115f), new Vector2(310f, 75f), () => _app.Overlay.ShowSettings(_app.Settings));
            AppUi.MakeButton(_home.transform, "Quit", "退出游戏", new Vector2(170f, -115f), new Vector2(310f, 75f), _app.Quit);
            AppUi.Text(_home.transform, "SaveHint", "在布阵、开战与结算节点保存。\n加载后回到战前准备，不恢复战斗中途。", 23,
                new Vector2(0f, -320f), new Vector2(720f, 90f), AppUi.Secondary, true);
            BuildSlots(body);
            RefreshSlots();
        }

        private void BuildSlots(Transform parent)
        {
            var rect = AppUi.Box(parent, "SaveSlots", Vector2.zero, new Vector2(1000f, 820f), AppUi.Card);
            _slots = rect.gameObject;
            _slotTitle = AppUi.Text(rect, "SlotsTitle", "", 38, new Vector2(-80f, 345f), new Vector2(720f, 70f), AppUi.Accent);
            AppUi.MakeButton(rect, "BackToHome", "返回", new Vector2(375f, 345f), new Vector2(150f, 60f), Back);
            for (int i = 0; i < LocalSaveStore.SlotCount; i++)
            {
                int slot = i;
                float y = 210f - i * 165f;
                var card = AppUi.Box(rect, "Slot" + (i + 1), new Vector2(0f, y), new Vector2(900f, 145f), new Color(0.10f, 0.18f, 0.19f));
                _descriptions[i] = AppUi.Text(card, "SlotDescription", "", 25, new Vector2(-135f, 0f), new Vector2(570f, 125f));
                _primary[i] = AppUi.MakeButton(card, "SlotAction", "", new Vector2(310f, 28f), new Vector2(220f, 55f), () =>
                {
                    if (_creating) _app.CreateGame(slot, _name.text, false);
                    else _app.EnterSlot(slot);
                });
                _delete[i] = AppUi.MakeButton(card, "DeleteSlot", "删除", new Vector2(310f, -36f), new Vector2(220f, 45f), () => _app.DeleteGame(slot));
                _delete[i].GetComponentInChildren<TextMeshProUGUI>().fontSize = 22;
            }
            var nameRow = UiKit.AddRect(rect, "NameRow", Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.one * 0.5f,
                new Vector2(0f, -255f), new Vector2(900f, 70f));
            _nameRow = nameRow.gameObject;
            AppUi.Text(nameRow, "NameLabel", "存档名称", 27, new Vector2(-330f, 0f), new Vector2(210f, 65f));
            var inputRect = AppUi.Box(nameRow, "SaveNameInput", new Vector2(120f, 0f), new Vector2(610f, 65f), new Color(0.035f, 0.08f, 0.09f));
            _name = inputRect.gameObject.AddComponent<TMP_InputField>();
            var viewport = UiKit.AddRect(inputRect, "Viewport", Vector2.zero, Vector2.one, Vector2.one * 0.5f, Vector2.zero, new Vector2(-32f, -12f));
            viewport.gameObject.AddComponent<RectMask2D>();
            var inputText = UiKit.MakeText(viewport, "", 27, Color.white, TextAnchor.MiddleLeft);
            inputText.richText = false;
            var placeholder = UiKit.MakeText(viewport, "为这段旅程取个名字", 25, AppUi.Secondary, TextAnchor.MiddleLeft);
            _name.textViewport = viewport;
            _name.textComponent = inputText;
            _name.placeholder = placeholder;
            _name.targetGraphic = inputRect.GetComponent<Image>();
            _name.characterLimit = 20;
            _name.lineType = TMP_InputField.LineType.SingleLine;
            _name.text = "新的旅程";
            _slotHint = AppUi.Text(rect, "SlotsHint", "", 22, new Vector2(0f, -350f), new Vector2(900f, 85f), AppUi.Secondary);
            _slots.SetActive(false);
        }

        public void ShowSlots(bool creating)
        {
            if (_app.Busy) return;
            _creating = creating;
            _home.SetActive(false);
            _slots.SetActive(true);
            _slotTitle.text = creating ? "新建游戏 · 选择槽位" : "加载游戏";
            _nameRow.SetActive(creating);
            _slotHint.text = creating ? "填写名称后选择槽位。覆盖已有存档需要再次确认。" :
                "加载后从所示关卡的战前布阵开始。损坏存档可在确认后删除或覆盖。";
            RefreshSlots();
        }

        public void RefreshSlots()
        {
            for (int i = 0; i < LocalSaveStore.SlotCount; i++)
            {
                var read = _app.Store.ReadSlot(i);
                string detail;
                if (read.CanLoad)
                {
                    string timestamp = new DateTime(read.Data.savedUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                    detail = read.Data.name + "\n第 " + read.Data.levelId + " 关 · 已通关 " + read.Data.completedLevels.Length + " 关\n" + timestamp;
                    if (read.Status == SaveStatus.Recovered) detail += " · 备份可用";
                }
                else detail = read.Status == SaveStatus.Empty ? "空存档" : read.Message;
                _descriptions[i].text = "槽位 " + (i + 1) + "  /  " + detail;
                _primary[i].GetComponentInChildren<TextMeshProUGUI>().text = _creating ? "在此新建" : "加载";
                _primary[i].interactable = _creating ? read.Status != SaveStatus.IoError : read.CanLoad;
                _delete[i].gameObject.SetActive(read.Status != SaveStatus.Empty);
            }
        }

        public void Back()
        {
            if (_app.Busy) return;
            if (_slots.activeSelf)
            {
                _slots.SetActive(false);
                _home.SetActive(true);
                RefreshSlots();
                UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
            }
            else _app.Quit();
        }
    }
}
