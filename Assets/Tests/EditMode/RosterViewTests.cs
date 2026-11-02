using System.Linq;
using System.Reflection;
using CQ.Core.Battle;
using CQ.Runtime;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RosterViewTests
{
    private GameObject _owner;
    private EventSystem _createdEventSystem;
    private BattleUi _ui;
    private PartyRoster _party;
    private RosterView View => _ui.Roster;

    [SetUp]
    public void SetUp()
    {
        bool hasEventSystem = Object.FindFirstObjectByType<EventSystem>() != null;
        _owner = new GameObject("RosterViewTest");
        _ui = BattleUi.Build(_owner.transform);
        if (_ui.CanvasRect == null)
            typeof(BattleUi).GetMethod("BuildAll", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_ui, null);
        if (!hasEventSystem) _createdEventSystem = Object.FindFirstObjectByType<EventSystem>();
        _party = new PartyRoster(ConfigLoader.LoadCharacters().items, new[] { "sword", "guard", "mage" });
        View.OnToggle = id => { _party.TryToggle(id, out _); View.Show(_party, null); };
        View.Show(_party, null);
        Canvas.ForceUpdateCanvases();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_owner);
        if (_createdEventSystem != null) Object.DestroyImmediate(_createdEventSystem.gameObject);
    }

    [Test]
    public void LeftPanelHasVerticalElementFiltersAndTwoColumnCards_WithoutStarsOrLocks()
    {
        Assert.AreEqual(0f, View.PanelRect.anchorMin.x);
        Assert.AreEqual(0f, View.PanelRect.anchorMax.x);
        Assert.AreEqual(RosterView.PanelWidth, View.PanelRect.rect.width);
        var filters = View.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Filter_")).ToArray();
        Assert.AreEqual(6, filters.Length);
        Assert.AreEqual(1, filters.Select(button => ((RectTransform)button.transform).anchoredPosition.x).Distinct().Count());
        Assert.AreEqual(6, filters.Select(button => ((RectTransform)button.transform).anchoredPosition.y).Distinct().Count());
        var sword = (RectTransform)Card("sword").transform;
        var guard = (RectTransform)Card("guard").transform;
        Assert.AreEqual(sword.anchoredPosition.y, guard.anchoredPosition.y);
        Assert.Less(sword.anchoredPosition.x, guard.anchoredPosition.x);
        Assert.IsFalse(View.GetComponentsInChildren<Transform>(true).Any(t => t.name.Contains("Star") || t.name.Contains("Lock")));
        foreach (var card in View.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("RosterCard_")))
        {
            Assert.IsNotNull(card.transform.Find("ElementBadge"));
            Assert.IsNotNull(card.transform.Find("Portrait").GetComponent<Image>().sprite);
        }
    }

    [Test]
    public void ElementBadgesAndFiltersShareTextFreeWhiteGreenBlueRedYellowIcons()
    {
        string[] elements = { "metal", "wood", "water", "fire", "earth" };
        Color[] colors = {
            new Color(0.95f, 0.97f, 1f), new Color(0.35f, 0.82f, 0.46f), new Color(0.30f, 0.62f, 1f),
            new Color(0.98f, 0.30f, 0.29f), new Color(1f, 0.83f, 0.26f)
        };
        for (int i = 0; i < elements.Length; i++)
        {
            var filter = View.transform.Find("Filter_" + elements[i]);
            var icon = filter.Find("ElementIcon").GetComponent<Image>();
            Assert.IsNotNull(icon.sprite);
            Assert.AreEqual(colors[i], icon.color);
            Assert.IsFalse(icon.raycastTarget);
            Assert.IsFalse(filter.GetComponentsInChildren<TextMeshProUGUI>().Any(text => !string.IsNullOrEmpty(text.text)));
        }
        foreach (var character in _party.Available)
        {
            var badge = Card(character.id).transform.Find("ElementBadge");
            Assert.AreEqual(RosterView.ElementColor(character.element), badge.GetComponent<Image>().color);
            Assert.AreEqual(0, badge.GetComponentsInChildren<TextMeshProUGUI>().Length);
        }
    }

    [Test]
    public void ClickTogglesDeploymentAndCheckmark_AndBenchPortraitRemainsVisible()
    {
        Assert.IsTrue(Card("mage").transform.Find("DeployedCheck").gameObject.activeSelf);
        Card("mage").onClick.Invoke();
        Assert.IsFalse(_party.IsDeployed("mage"));
        Assert.IsFalse(Card("mage").transform.Find("DeployedCheck").gameObject.activeSelf);
        Assert.AreEqual(1f, Card("mage").transform.Find("Portrait").GetComponent<Image>().color.a);
        Assert.AreEqual("待命", Card("mage").transform.Find("DeploymentState").GetComponent<TextMeshProUGUI>().text);
        Card("mage").onClick.Invoke();
        Assert.IsTrue(_party.IsDeployed("mage"));
        Assert.IsTrue(Card("mage").transform.Find("DeployedCheck").gameObject.activeSelf);
    }

    [Test]
    public void FilterChangesOnlyVisibleCards_AndEmptyCategoriesExplainResult()
    {
        View.SetFilter("water");
        Assert.AreEqual(1, View.VisibleCount);
        Assert.IsNotNull(Card("mage"));
        Assert.AreEqual(3, _party.Deployment.Count);
        View.SetFilter("wood");
        Assert.AreEqual(0, View.VisibleCount);
        StringAssert.Contains("木", View.transform.Find("RosterViewport/EmptyFilter").GetComponent<TextMeshProUGUI>().text);
        Assert.IsTrue(View.transform.Find("RosterViewport/EmptyFilter").gameObject.activeSelf);
        Assert.AreEqual(3, _party.Deployment.Count);
        View.SetFilter("");
        Assert.AreEqual(3, View.VisibleCount);
        Assert.IsTrue(Card("sword").transform.Find("DeployedCheck").gameObject.activeSelf);
    }

    [Test]
    public void NewlyAvailablePartnerResetsFilterAndGetsNoticeWithoutAutoDeploy()
    {
        var chars = ConfigLoader.LoadCharacters().items;
        var first = new PartyRoster(chars, new[] { "sword", "guard" });
        View.Show(first, null);
        View.SetFilter("metal");
        _party = new PartyRoster(chars, new[] { "sword", "guard", "mage" }, first);
        View.Show(_party, "mage");
        Assert.AreEqual("", View.Filter);
        Assert.AreEqual(3, View.VisibleCount);
        Assert.IsFalse(_party.IsDeployed("mage"));
        Assert.IsFalse(Card("mage").transform.Find("DeployedCheck").gameObject.activeSelf);
        StringAssert.Contains("新伙伴：术士", View.transform.Find("RosterNotice").GetComponent<TextMeshProUGUI>().text);
        Assert.AreEqual("新伙伴", Card("mage").transform.Find("DeploymentState").GetComponent<TextMeshProUGUI>().text);
    }

    [Test]
    public void EmptyDeploymentRemainsEditableWithInstructionAndZeroCount()
    {
        foreach (var id in _party.Deployment.Keys.ToArray()) _party.TryToggle(id, out _);
        View.Show(_party, null);
        Assert.IsTrue(View.gameObject.activeSelf);
        Assert.AreEqual(3, View.VisibleCount);
        StringAssert.Contains("上阵 0", View.transform.Find("DeployedCount").GetComponent<TextMeshProUGUI>().text);
        StringAssert.Contains("上阵后开战", View.transform.Find("RosterNotice").GetComponent<TextMeshProUGUI>().text);
        Card("sword").onClick.Invoke();
        Assert.IsTrue(_party.CanStart);
    }

    [Test]
    public void ManyPartnersUseScrollableContentAndNeverExceedFiveChecks()
    {
        _party = new PartyRoster(PartyRosterTests.Characters(24), null);
        View.Show(_party, null);
        Canvas.ForceUpdateCanvases();
        var scroll = View.GetComponentInChildren<ScrollRect>();
        Assert.IsTrue(scroll.vertical);
        Assert.IsFalse(scroll.horizontal);
        Assert.Greater(scroll.content.rect.height, scroll.viewport.rect.height);
        Assert.AreEqual(24, View.VisibleCount);
        Assert.AreEqual(5, View.GetComponentsInChildren<Transform>().Count(t => t.name == "DeployedCheck"));
        View.SetFilter("fire");
        Assert.Less(View.VisibleCount, 24);
        Assert.AreEqual(Vector2.zero, scroll.content.anchoredPosition);
    }

    [Test]
    public void CurrentRosterLabelsFitAndGridDoesNotCrossSidebarBounds()
    {
        Canvas.ForceUpdateCanvases();
        foreach (var text in View.GetComponentsInChildren<TextMeshProUGUI>())
        {
            text.ForceMeshUpdate();
            Assert.IsFalse(text.isTextOverflowing, text.name + ": " + text.text);
        }
        foreach (var button in View.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("RosterCard_")))
        {
            var rect = (RectTransform)button.transform;
            Assert.LessOrEqual(rect.anchoredPosition.x + rect.rect.width, ((RectTransform)rect.parent).rect.width);
        }
    }

    private Button Card(string id) => View.GetComponentsInChildren<Button>().Single(button => button.name == "RosterCard_" + id);
}
