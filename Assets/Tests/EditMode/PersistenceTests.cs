using System;
using System.IO;
using System.Linq;
using CQ.Core.Battle;
using CQ.Core.Persistence;
using CQ.Runtime;
using CQ.Runtime.App;
using CQ.Runtime.Persistence;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class PersistenceTests
{
    private string _root;
    private LocalSaveStore _store;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "CQ-SaveTests-" + Guid.NewGuid().ToString("N"));
        _store = new LocalSaveStore(_root);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private static GameSave Save(string name = "旅程") => GameSave.Create(name, 1, 42, DateTime.UtcNow.Ticks);

    [Test]
    public void NewStoreIsEmpty_AndSlotsAreBounded()
    {
        for (int slot = 0; slot < LocalSaveStore.SlotCount; slot++) Assert.AreEqual(SaveStatus.Empty, _store.ReadSlot(slot).Status);
        Assert.AreEqual(SaveStatus.Empty, _store.ReadSettings().Status);
        Assert.IsFalse(Directory.Exists(_root));
        Assert.Throws<ArgumentOutOfRangeException>(() => _store.ReadSlot(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => _store.ReadSlot(3));
    }

    [Test]
    public void SaveReloadsInAnotherStore_WithUnicodeAndIndependentSlots()
    {
        var data = Save("伙伴之旅");
        data.partners = new[] { new PartnerSave { id = "sword", slot = "back3", deployed = true },
            new PartnerSave { id = "guard", slot = "front1", deployed = false } };
        data.completedLevels = new[] { 1 };
        Assert.IsTrue(_store.WriteSlot(0, data, true, out var error), error);
        Assert.IsTrue(_store.WriteSlot(1, Save("另一旅程"), true, out error), error);
        var reloaded = new LocalSaveStore(_root).ReadSlot(0);
        Assert.AreEqual(SaveStatus.Ready, reloaded.Status);
        Assert.AreEqual("伙伴之旅", reloaded.Data.name);
        Assert.AreEqual(42, reloaded.Data.seed);
        Assert.AreEqual("back3", reloaded.Data.partners[0].slot);
        Assert.IsFalse(reloaded.Data.partners[1].deployed);
        Assert.AreEqual("另一旅程", _store.ReadSlot(1).Data.name);
        Assert.AreEqual(SaveStatus.Empty, _store.ReadSlot(2).Status);
        Assert.IsEmpty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Test]
    public void UpdateKeepsPreviousValidBackup_AndRecoveryDoesNotDestroyIt()
    {
        Assert.IsTrue(_store.WriteSlot(0, Save("旧进度"), true, out _));
        Assert.IsTrue(_store.WriteSlot(0, Save("新进度"), false, out _));
        File.WriteAllText(_store.SlotPath(0), "not json");
        var read = _store.ReadSlot(0);
        Assert.AreEqual(SaveStatus.Recovered, read.Status);
        Assert.AreEqual("旧进度", read.Data.name);
        Assert.IsTrue(_store.WriteSlot(0, read.Data, false, out var error), error);
        Assert.AreEqual(SaveStatus.Ready, _store.ReadSlot(0).Status);
        File.Delete(_store.SlotPath(0));
        Assert.AreEqual(SaveStatus.Recovered, _store.ReadSlot(0).Status);
        Assert.AreEqual("旧进度", _store.ReadSlot(0).Data.name);
    }

    [Test]
    public void InvalidFileIsNotSilentlyOverwritten_AndFutureVersionDoesNotFallBack()
    {
        Directory.CreateDirectory(_root);
        var path = _store.SlotPath(0);
        File.WriteAllText(path, "{}");
        Assert.AreEqual(SaveStatus.Invalid, _store.ReadSlot(0).Status);
        Assert.IsFalse(_store.WriteSlot(0, Save(), false, out _));
        Assert.AreEqual("{}", File.ReadAllText(path));
        Assert.IsTrue(_store.WriteSlot(0, Save(), true, out _));
        Assert.IsTrue(_store.WriteSlot(0, Save(), false, out _));
        var future = Save();
        future.version++;
        File.WriteAllText(path, JsonUtility.ToJson(future));
        Assert.AreEqual(SaveStatus.Unsupported, _store.ReadSlot(0).Status);
        Assert.IsFalse(_store.WriteSlot(0, Save(), false, out _));
        Assert.AreEqual(future.version, JsonUtility.FromJson<GameSave>(File.ReadAllText(path)).version);
    }

    [Test]
    public void ExplicitReplacementCannotResurrectPreviousGamesBackup()
    {
        Assert.IsTrue(_store.WriteSlot(0, Save("旧游戏"), true, out _));
        Assert.IsTrue(_store.WriteSlot(0, Save("旧游戏更新"), false, out _));
        Assert.IsTrue(_store.WriteSlot(0, Save("新游戏"), true, out _));
        File.WriteAllText(_store.SlotPath(0), "broken");
        var read = _store.ReadSlot(0);
        Assert.IsFalse(read.CanLoad && read.Data.name.StartsWith("旧游戏"));
    }

    [Test]
    public void DeleteRemovesPrimaryAndBackup_AndDoesNotAffectOtherSlotsOrSettings()
    {
        _store.WriteSlot(0, Save(), true, out _);
        _store.WriteSlot(0, Save(), false, out _);
        _store.WriteSlot(1, Save("保留"), true, out _);
        _store.WriteSettings(UserSettings.Defaults(), out _);
        Assert.IsTrue(_store.DeleteSlot(0, out var error), error);
        Assert.AreEqual(SaveStatus.Empty, _store.ReadSlot(0).Status);
        Assert.IsTrue(_store.ReadSlot(1).CanLoad);
        Assert.IsTrue(_store.ReadSettings().CanLoad);
    }

    [Test]
    public void WriteFailureDoesNotReportSuccessOrMutateSessionProgress()
    {
        _store.WriteSlot(0, Save(), true, out _);
        var session = new GameSession(_store, 0, _store.ReadSlot(0).Data);
        // 锁住主档模拟Windows上替换失败。
        using (var locked = new FileStream(_store.SlotPath(0), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.IsFalse(session.SaveCheckpoint(2, new PartnerSave[0], 1, out var error));
            Assert.IsNotEmpty(error);
        }
        Assert.AreEqual(1, session.Data.levelId);
        Assert.IsEmpty(session.Data.completedLevels);
        Assert.AreEqual(1, _store.ReadSlot(0).Data.levelId);
        Assert.IsEmpty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Test]
    public void SessionCheckpointRecordsCompletionOnce_WithoutHealthOrActionState()
    {
        _store.WriteSlot(0, Save(), true, out _);
        var session = new GameSession(_store, 0, _store.ReadSlot(0).Data);
        Assert.IsTrue(session.SaveCheckpoint(2, new[] { new PartnerSave { id = "sword", slot = "front2", deployed = true } }, 1, out _));
        Assert.IsTrue(session.SaveCheckpoint(2, session.Data.partners, 1, out _));
        var saved = _store.ReadSlot(0).Data;
        Assert.AreEqual(2, saved.levelId);
        CollectionAssert.AreEqual(new[] { 1 }, saved.completedLevels);
        Assert.AreEqual("front2", saved.partners[0].slot);
        Assert.AreEqual(42, saved.seed);
        Assert.GreaterOrEqual(saved.savedUtcTicks, saved.createdUtcTicks);
    }

    [Test]
    public void SettingsAreGlobal_AndInvalidValuesAreRejected()
    {
        var settings = UserSettings.Defaults();
        settings.masterVolume = 0.36f;
        settings.effectsVolume = 0.72f;
        settings.muted = true;
        Assert.IsTrue(_store.WriteSettings(settings, out _));
        _store.WriteSlot(2, Save(), true, out _);
        var read = new LocalSaveStore(_root).ReadSettings();
        Assert.AreEqual(0.36f, read.Data.masterVolume);
        Assert.AreEqual(0.72f, read.Data.effectsVolume);
        Assert.IsTrue(read.Data.muted);
        settings.masterVolume = float.NaN;
        Assert.IsFalse(_store.WriteSettings(settings, out _));
        settings.masterVolume = 1.1f;
        Assert.IsFalse(_store.WriteSettings(settings, out _));
        Assert.AreEqual(0.36f, _store.ReadSettings().Data.masterVolume);
    }

    [TestCase("name")]
    [TestCase("time")]
    [TestCase("level")]
    [TestCase("duplicate")]
    [TestCase("slot")]
    public void MalformedDataIsRejectedBeforeTouchingDisk(string field)
    {
        var data = Save();
        switch (field)
        {
            case "name": data.name = new string('x', 21); break;
            case "time": data.savedUtcTicks = 0; break;
            case "level": data.levelId = 0; break;
            case "duplicate": data.partners = new[] { new PartnerSave { id = "a", slot = "front1", deployed = true },
                new PartnerSave { id = "b", slot = "front1", deployed = true } }; break;
            case "slot": data.partners = new[] { new PartnerSave { id = "a", slot = "unknown" } }; break;
        }
        Assert.IsFalse(_store.WriteSlot(0, data, true, out var error));
        Assert.IsNotEmpty(error);
        Assert.IsFalse(Directory.Exists(_root));
    }

    [Test]
    public void RosterSnapshotPreservesBenchedPositionsAndExplicitEmptyFormation()
    {
        var chars = ConfigLoader.LoadCharacters().items;
        var party = new PartyRoster(chars, new[] { "sword", "guard" });
        party.RestoreSave(new[] { new PartnerSave { id = "sword", slot = "back3", deployed = false },
            new PartnerSave { id = "guard", slot = "front2", deployed = false } });
        Assert.IsFalse(party.CanStart);
        var restored = new PartyRoster(chars, new[] { "sword", "guard", "mage" });
        restored.RestoreSave(party.CaptureSave());
        Assert.IsFalse(restored.CanStart);
        Assert.IsFalse(restored.IsDeployed("mage"));
        restored.TryToggle("sword", out _);
        Assert.AreEqual("back3", restored.Deployment["sword"]);
        restored.TryToggle("guard", out _);
        Assert.AreEqual("front2", restored.Deployment["guard"]);
    }

    [Test]
    public void InvalidRosterRestoreIsTransactional_AndUnavailableContentDoesNotLoad()
    {
        var chars = ConfigLoader.LoadCharacters().items;
        var party = new PartyRoster(chars, new[] { "sword", "guard" });
        var before = party.Deployment.ToArray();
        Assert.Throws<ArgumentException>(() => party.RestoreSave(new[] { new PartnerSave { id = "unknown", slot = "front1" } }));
        CollectionAssert.AreEquivalent(before, party.Deployment.ToArray());
        var save = Save();
        save.levelId = 999;
        Assert.IsNotNull(GameSession.ValidateContent(save, ConfigLoader.LoadLevels().items, chars));
    }

    [Test]
    public void SettingsRecoveryNeverReplacesGoodBackupWithBrokenPrimary()
    {
        var settings = UserSettings.Defaults();
        settings.masterVolume = 0.35f;
        _store.WriteSettings(settings, out _);
        settings.masterVolume = 0.8f;
        _store.WriteSettings(settings, out _);
        var path = Path.Combine(_root, "settings.json");
        File.WriteAllText(path, "broken");
        var recovered = _store.ReadSettings();
        Assert.AreEqual(SaveStatus.Recovered, recovered.Status);
        Assert.AreEqual(0.35f, recovered.Data.masterVolume);
        Assert.IsTrue(_store.WriteSettings(recovered.Data, out var error), error);
        File.WriteAllText(path, "broken again");
        Assert.AreEqual(SaveStatus.Recovered, _store.ReadSettings().Status);
        Assert.AreEqual(0.35f, _store.ReadSettings().Data.masterVolume);
    }

    [Test]
    public void LockedBackupPreventsExplicitReplacementBeforePrimaryChanges()
    {
        _store.WriteSlot(0, Save("原旅程"), true, out _);
        _store.WriteSlot(0, Save("原旅程更新"), false, out _);
        string before = File.ReadAllText(_store.SlotPath(0));
        using (var locked = new FileStream(_store.SlotPath(0) + ".bak", FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.IsFalse(_store.WriteSlot(0, Save("新旅程"), true, out _));
        Assert.AreEqual(before, File.ReadAllText(_store.SlotPath(0)));
        Assert.AreEqual("原旅程更新", _store.ReadSlot(0).Data.name);
    }

    [Test]
    public void AtomicReplacementFailureCleansTemporaryFileAndKeepsPrimary()
    {
        _store.WriteSlot(0, Save("原旅程"), true, out _);
        _store.WriteSlot(0, Save("原旅程更新"), false, out _);
        string before = File.ReadAllText(_store.SlotPath(0));
        using (var locked = new FileStream(_store.SlotPath(0) + ".bak", FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.IsFalse(_store.WriteSlot(0, Save("不应写入"), false, out _));
        Assert.AreEqual(before, File.ReadAllText(_store.SlotPath(0)));
        Assert.IsEmpty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Test]
    public void OversizedOrTruncatedFilesAreNotTreatedAsEmptySlots()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(_store.SlotPath(0), new string('x', 1024 * 1024 + 1));
        Assert.AreEqual(SaveStatus.Invalid, _store.ReadSlot(0).Status);
        File.WriteAllText(_store.SlotPath(1), "{\"signature\":\"CQ.GameSave\",\"version\":1}");
        Assert.AreEqual(SaveStatus.Invalid, _store.ReadSlot(1).Status);
        Assert.IsFalse(_store.WriteSlot(1, Save(), false, out _));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MenuRequiresExplicitSlotSelection_InsteadOfContinuingLatestSave(bool hasSaves)
    {
        if (hasSaves)
        {
            var older = Save("昨天的旅程");
            var newer = Save("另一段旅程");
            newer.savedUtcTicks += TimeSpan.TicksPerHour;
            Assert.IsTrue(_store.WriteSlot(0, older, true, out _));
            Assert.IsTrue(_store.WriteSlot(1, newer, true, out _));
        }
        var appRoot = new GameObject("InactiveApplication");
        appRoot.SetActive(false);
        var menuRoot = new GameObject("MenuTest");
        try
        {
            var app = appRoot.AddComponent<GameApplication>();
            typeof(GameApplication).GetProperty("Store").SetValue(app, _store);
            var menu = menuRoot.AddComponent<MainMenuView>();
            menu.Build(app);
            var home = menuRoot.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Home");
            var buttons = home.GetComponentsInChildren<UnityEngine.UI.Button>();
            CollectionAssert.AreEquivalent(new[] { "NewGame", "LoadGame", "Settings", "Quit" }, buttons.Select(button => button.name));
            buttons.Single(button => button.name == "LoadGame").onClick.Invoke();
            Assert.IsFalse(home.gameObject.activeSelf);
            Assert.IsNull(app.Session, "打开加载列表不能自动选择最新存档");
            Assert.IsFalse(app.Busy);
            var slots = menuRoot.GetComponentsInChildren<Transform>(true).Single(item => item.name == "SaveSlots");
            Assert.IsTrue(slots.gameObject.activeSelf);
            var actions = slots.GetComponentsInChildren<UnityEngine.UI.Button>().Where(button => button.name == "SlotAction").ToArray();
            Assert.AreEqual(3, actions.Length);
            Assert.AreEqual(hasSaves, actions[0].interactable);
            Assert.AreEqual(hasSaves, actions[1].interactable);
            Assert.IsFalse(actions[2].interactable);
            if (hasSaves)
            {
                var descriptions = slots.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Where(text => text.name == "SlotDescription").ToArray();
                StringAssert.Contains("昨天的旅程", descriptions[0].text);
                StringAssert.Contains("另一段旅程", descriptions[1].text);
                StringAssert.Contains("第 1 关", descriptions[0].text);
                StringAssert.Contains(new DateTime(_store.ReadSlot(0).Data.savedUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm"), descriptions[0].text);
            }
            menu.Back();
            Assert.IsTrue(home.gameObject.activeSelf);
            Assert.IsFalse(slots.gameObject.activeSelf);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(menuRoot);
            UnityEngine.Object.DestroyImmediate(appRoot);
        }
    }

    [Test]
    public void TransitionCameraBelongsToApplication_AndIsReenabledBeforeFadeOrUnload()
    {
        var root = new GameObject("InactiveApplication");
        root.SetActive(false);
        try
        {
            var app = root.AddComponent<GameApplication>();
            var overlay = root.AddComponent<ApplicationOverlay>();
            overlay.Build();
            typeof(GameApplication).GetProperty("Overlay").SetValue(app, overlay);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(GameApplication).GetMethod("BuildTransitionCamera", flags).Invoke(app, null);
            var camera = root.GetComponentInChildren<Camera>(true);
            Assert.IsNotNull(camera);
            Assert.AreSame(root.transform, camera.transform.parent);
            Assert.IsTrue(camera.enabled, "首次菜单加载前也必须有摄像机兜底");
            Assert.AreEqual(CameraClearFlags.SolidColor, camera.clearFlags);
            Assert.AreEqual(0, camera.cullingMask);
            Assert.Less(camera.depth, 0f);
            Assert.AreEqual(new Rect(0f, 0f, 1f, 1f), camera.rect);
            Assert.AreEqual(0, camera.targetDisplay);
            Assert.IsNull(camera.targetTexture);
            Assert.IsFalse(camera.CompareTag("MainCamera"), "不能被战斗Camera.main复用");
            Assert.IsNull(camera.GetComponent<AudioListener>(), "不能引入重复音频监听器");
            camera.enabled = false;
            var transition = (System.Collections.IEnumerator)typeof(GameApplication).GetMethod("SwitchContent", flags)
                .Invoke(app, new object[] { GameApplication.MenuScene });
            Assert.IsTrue(transition.MoveNext());
            Assert.IsTrue(camera.enabled, "转场第一步必须先启用兜底，不等待旧场景卸载");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void BootstrapAndContentScenesAreEnabledInBuildOrder()
    {
        var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).ToArray();
        Assert.AreEqual("Assets/Scenes/Bootstrap.unity", scenes[0].path);
        Assert.IsTrue(scenes.Any(scene => scene.path == "Assets/Scenes/MainMenu.unity"));
        Assert.IsTrue(scenes.Any(scene => scene.path == "Assets/Scenes/Battle.unity"));
        foreach (var scene in scenes.Take(3)) Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path));
    }
}
