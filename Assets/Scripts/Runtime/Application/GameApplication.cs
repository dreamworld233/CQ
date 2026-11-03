using System;
using System.Collections;
using System.IO;
using System.Linq;
using CQ.Core.Persistence;
using CQ.Runtime.Persistence;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace CQ.Runtime.App
{
    [DefaultExecutionOrder(-100)]
    public sealed class GameApplication : MonoBehaviour
    {
        public const string MenuScene = "MainMenu";
        public const string BattleScene = "Battle";
        public static GameApplication Instance { get; private set; }
        public LocalSaveStore Store { get; private set; }
        public UserSettings Settings { get; private set; }
        public GameSession Session { get; private set; }
        public ApplicationOverlay Overlay { get; private set; }
        public bool Busy { get; private set; }
        private int _escapeHandledFrame = -1;
        public bool BlocksBattleInput => Busy || Overlay.IsBlocking || _escapeHandledFrame == Time.frameCount;
        public event Action SettingsChanged;
        private Scene _content;
        private Scene _bootstrap;
        private MainMenuView _menu;
        private Camera _transitionCamera;
        private string _startupWarning;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            // 显式放置BattleController的场景仍可独立快速验证，不创建正式菜单或写玩家存档。
            if (FindFirstObjectByType<BattleController>() != null || Instance != null) return;
            new GameObject("GameApplication").AddComponent<GameApplication>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            var initial = SceneManager.GetActiveScene();
            _bootstrap = initial.name == "Bootstrap" ? initial : SceneManager.CreateScene("Bootstrap");
            SceneManager.MoveGameObjectToScene(gameObject, _bootstrap);
            if (initial != _bootstrap) _content = initial;
            var input = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            input.transform.SetParent(transform, false);
            Overlay = gameObject.AddComponent<ApplicationOverlay>();
            Overlay.Build();
            BuildTransitionCamera();
            Store = new LocalSaveStore(SaveRoot());
            var settings = Store.ReadSettings();
            Settings = settings.CanLoad ? settings.Data : UserSettings.Defaults();
            if (settings.Status != SaveStatus.Empty && settings.Status != SaveStatus.Ready)
                _startupWarning = settings.CanLoad ? settings.Message : "设置无法读取，暂用默认设置。原文件尚未覆盖。\n" + settings.Message;
            ApplyAudio();
        }

        private void BuildTransitionCamera()
        {
            // Overlay画布不能代替Camera；旧场景卸载后仍需渲染，避免Display无摄像机提示。
            _transitionCamera = new GameObject("TransitionCamera", typeof(Camera)).GetComponent<Camera>();
            _transitionCamera.transform.SetParent(transform, false);
            _transitionCamera.clearFlags = CameraClearFlags.SolidColor;
            _transitionCamera.backgroundColor = AppUi.Background;
            _transitionCamera.cullingMask = 0;
            _transitionCamera.depth = -100f;
        }

        private static string SaveRoot()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-cqSaveRoot");
            if (index >= 0 && index + 1 < args.Length) return Path.GetFullPath(args[index + 1]);
#endif
            return Path.Combine(Application.persistentDataPath, "Saves");
        }

        private void Start() => OpenMenu();

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape) || Busy) return;
            if (Overlay.ModalVisible) { _escapeHandledFrame = Time.frameCount; Overlay.CloseModal(); }
            else if (_menu != null) { _escapeHandledFrame = Time.frameCount; _menu.Back(); }
        }

        public bool SaveSettings(UserSettings settings, out string error)
        {
            if (!Store.WriteSettings(settings, out error)) return false;
            Settings = settings;
            ApplyAudio();
            SettingsChanged?.Invoke();
            return true;
        }

        private void ApplyAudio() => AudioListener.volume = Settings.muted ? 0f : Settings.masterVolume;

        public void ChangeMute(bool muted)
        {
            var next = JsonUtility.FromJson<UserSettings>(JsonUtility.ToJson(Settings));
            next.muted = muted;
            if (!SaveSettings(next, out var error)) Overlay.Notice("设置未保存", error);
        }

        public void CreateGame(int slot, string name, bool confirmed)
        {
            if (Busy) return;
            name = name?.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 20 || name.Any(char.IsControl))
            { Overlay.Notice("名称无效", "请输入1至20个字符的存档名称。"); return; }
            var previous = Store.ReadSlot(slot);
            if (previous.Status == SaveStatus.IoError) { Overlay.Notice("无法访问存档", previous.Message); return; }
            if (previous.Status != SaveStatus.Empty && !confirmed)
            {
                string capturedName = name;
                Overlay.Confirm("覆盖存档？", "槽位 " + (slot + 1) + " 已有记录。新建将替换该槽位及备份，无法撤销。",
                    () => CreateGame(slot, capturedName, true), "覆盖并新建");
                return;
            }
            var levels = ConfigLoader.LoadLevels();
            if (levels.errors.Count > 0 || levels.items.Count == 0) { Overlay.Notice("内容不可用", "关卡配置读取失败。"); return; }
            long now = DateTime.UtcNow.Ticks;
            var save = GameSave.Create(name, levels.items.Min(level => level.id), now, now);
            if (!Store.WriteSlot(slot, save, true, out var error)) { Overlay.Notice("新建失败", error); return; }
            EnterSlot(slot);
        }

        public void EnterSlot(int slot)
        {
            if (Busy) return;
            var read = Store.ReadSlot(slot);
            if (!read.CanLoad) { Overlay.Notice("无法加载", read.Message ?? "该槽位没有存档。"); return; }
            var levels = ConfigLoader.LoadLevels();
            var chars = ConfigLoader.LoadCharacters();
            string error = levels.errors.Count + chars.errors.Count > 0 ? "当前配置读取失败" :
                GameSession.ValidateContent(read.Data, levels.items, chars.items);
            if (error != null) { Overlay.Notice("存档与内容不兼容", error + "。原文件未修改。"); return; }
            if (read.Status == SaveStatus.Recovered)
            {
                Overlay.Confirm("从备份恢复", read.Message + "。确认后进入；后续保存会修复主档。",
                    () => BeginSession(slot, read.Data), "使用备份");
                return;
            }
            BeginSession(slot, read.Data);
        }

        private void BeginSession(int slot, GameSave data)
        {
            if (Busy) return;
            Session = new GameSession(Store, slot, data);
            Busy = true;
            StartCoroutine(SwitchContent(BattleScene));
        }

        public void OpenMenu()
        {
            if (Busy) return;
            Busy = true;
            StartCoroutine(SwitchContent(MenuScene));
        }

        public void RequestReturn(BattleController battle)
        {
            if (Busy || Overlay.ModalVisible) return;
            Overlay.Confirm("返回主菜单？", battle.CanCheckpoint ?
                "将保存当前阵容与关卡进度。进行中的战斗不会续存，加载后从战前布阵重新开始。" :
                "当前战斗未正确初始化，返回菜单不会改动原存档。", () =>
            {
                if (battle.CanCheckpoint && !battle.SaveCheckpoint(out var error)) { Overlay.Notice("尚未保存", error + "\n请检查存储空间后重试。"); return; }
                battle.PrepareToLeave();
                OpenMenu();
            }, "保存并返回");
        }

        public void DeleteGame(int slot)
        {
            if (Busy) return;
            Overlay.Confirm("删除存档？", "将删除槽位 " + (slot + 1) + " 的存档和备份，无法撤销。", () =>
            {
                if (!Store.DeleteSlot(slot, out var error)) Overlay.Notice("删除失败", error);
                _menu?.RefreshSlots();
            }, "删除");
        }

        public void Quit()
        {
            Overlay.Confirm("退出游戏？", "已保存的进度会保留。", () =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }, "退出");
        }

        private IEnumerator SwitchContent(string target)
        {
            _transitionCamera.enabled = true;
            Overlay.CloseModal();
            yield return Overlay.Fade(true, target == BattleScene ? "正在准备战斗…" : "正在返回主菜单…");
            if (!Application.CanStreamedLevelBeLoaded(target))
            {
                Busy = false;
                yield return Overlay.Fade(false);
                Overlay.Notice("场景不可用", "请将 Bootstrap、MainMenu、Battle 加入构建设置。");
                yield break;
            }
            // 遮罩下卸载旧内容；全局UI与EventSystem始终属于Bootstrap。
            if (_content.IsValid() && _content.isLoaded) yield return SceneManager.UnloadSceneAsync(_content);
            _content = default;
            _menu = null;
            AsyncOperation operation = null;
            string loadError = null;
            try { operation = SceneManager.LoadSceneAsync(target, LoadSceneMode.Additive); }
            catch (Exception error) { loadError = error.Message; }
            if (operation == null)
            {
                yield return TransitionFailure(loadError ?? "场景加载未能启动");
                yield break;
            }
            yield return operation;
            _content = SceneManager.GetSceneByName(target);
            if (!_content.IsValid() || !_content.isLoaded)
            {
                yield return TransitionFailure("内容场景未就绪");
                yield break;
            }
            SceneManager.SetActiveScene(_content);
            if (target == MenuScene)
            {
                Session = null;
                var camera = new GameObject("MenuCamera", typeof(Camera), typeof(AudioListener));
                camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
                camera.GetComponent<Camera>().backgroundColor = AppUi.Background;
                _menu = new GameObject("MainMenu").AddComponent<MainMenuView>();
                _menu.Build(this);
            }
            else
            {
                var controller = new GameObject("BattleController").AddComponent<BattleController>();
                // 业务就绪与场景加载完成是两个边界，异常不能让遮罩无限等待。
                float deadline = Time.realtimeSinceStartup + 15f;
                while (controller != null && !controller.Initialized && Time.realtimeSinceStartup < deadline) yield return null;
                if (controller == null || !controller.Initialized)
                {
                    yield return TransitionFailure("战斗初始化未完成，原存档保留。");
                    yield break;
                }
            }
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return Overlay.Fade(false);
            _transitionCamera.enabled = false;
            Busy = false;
            if (_startupWarning != null)
            {
                Overlay.Notice("设置读取提示", _startupWarning);
                _startupWarning = null;
            }
        }

        private IEnumerator TransitionFailure(string error)
        {
            yield return Overlay.Fade(false);
            Busy = false;
            Overlay.Confirm("进入失败", error + "\n可返回主菜单重试。", OpenMenu, "返回主菜单");
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            AudioListener.volume = 1f;
        }
    }
}
