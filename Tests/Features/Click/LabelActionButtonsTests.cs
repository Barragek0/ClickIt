namespace ClickIt.Tests.Features.Click;

[TestClass]
public class LabelActionButtonsTests
{
    [TestMethod]
    [TestCategory("Regression")]
    public void HarvestIdentification_ReadsTheParentTextRatherThanModifierChild()
    {
        var unidentified = new Node(new Node(), new Node(new Node(), new Node(), new Node(),
            new Node(new Node { TextNoTags = "different modifier text" }) { TextNoTags = "Unidentified" }));
        var root = new Node(unidentified);
        HarvestEncounterUpgrade.ReadIdentificationStateFromRoot(root).Should().BeFalse();
        var identified = new Node(new Node(new Node(), new Node(new Node(), new Node(), new Node(),
            new Node(new Node { TextNoTags = "Unidentified" }) { TextNoTags = "" })));
        HarvestEncounterUpgrade.ReadIdentificationStateFromRoot(identified).Should().BeTrue();
        var identifiedWithNullParentText = new Node(new Node(new Node(), new Node(new Node(), new Node(), new Node(),
            new Node(new Node { TextNoTags = "Tier 3 Sacred Fauna drop 2 additional Threads" }))));
        HarvestEncounterUpgrade.ReadIdentificationStateFromRoot(identifiedWithNullParentText).Should().BeTrue();
        var nullTextWithoutModifiers = new Node(new Node(new Node(), new Node(new Node(), new Node(), new Node(), new Node())));
        HarvestEncounterUpgrade.ReadIdentificationStateFromRoot(nullTextWithoutModifiers).Should().BeNull();
        var nullTextWithUnidentifiedChild = new Node(new Node(new Node(), new Node(new Node(), new Node(), new Node(),
            new Node(new Node { TextNoTags = "Unidentified" }))));
        HarvestEncounterUpgrade.ReadIdentificationStateFromRoot(nullTextWithUnidentifiedChild).Should().BeNull();
        HarvestEncounterUpgrade.ReadIdentificationStateFromRoot(new Node()).Should().BeNull();
    }

    [TestMethod]
    public void VisibleIconCanBeProbedBeforeItsTooltipGetsChildren()
    {
        var icon = new Node { Tooltip = new Node() };
        var root = new Node(new Node(), new Node(new Node(icon)));

        LabelActionButtons.CountButtons(root, 1).Should().Be(1);
        LabelActionButtons.ReadVisibleButton(root, 1, 0).Should().BeSameAs(icon);
        LabelActionButtons.ReadTitle(icon).Should().BeNull();

        icon.Tooltip = Button("Scroll of Wisdom").Tooltip;

        LabelActionButtons.ReadTitle(icon).Should().Be(LabelActionButtons.ScrollOfWisdom);
        LabelActionButtons.ReadVisibleButton(root, 1, 0).Should().BeSameAs(icon);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void DescribeRow_ReportsWhereTooltipTitleIsUnavailable()
    {
        var icon = new Node { Tooltip = new Node() };
        var root = new Node(new Node(), new Node(new Node(icon)));
        LabelActionButtons.DescribeRow(root, 1).Should().Contain("tooltip children=0, body unavailable");
        icon.Tooltip = Button("Scroll of Wisdom").Tooltip;
        LabelActionButtons.DescribeRow(root, 1).Should().Contain("Scroll of Wisdom").And.Contain("body children=1");
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void HiddenIconCanBeLocatedForGeometryWithoutBeingClickable()
    {
        var icon = new Node { IsVisible = false };
        var root = new Node(new Node(), new Node(new Node(icon)));
        LabelActionButtons.ReadButtonElement(root, 1, 0).Should().BeSameAs(icon);
        LabelActionButtons.ReadVisibleButton(root, 1, 0).Should().BeNull();
    }

    [TestMethod]
    public void FindsCurrencyWhenTheRawTooltipTitleHasFormatting()
    {
        var title = new Node { Text = "formatted wisdom title", TextNoTags = "Scroll of Wisdom" };
        var button = new Node { Tooltip = new Node(new Node(title)) };
        var root = new Node(new Node(), new Node(new Node(button)));

        LabelActionButtons.FindButton(root, 1, LabelActionButtons.ScrollOfWisdom, out _).Should().BeSameAs(button);
    }

    [TestMethod]
    public void FindsTheRequestedIconAmongOtherCurrencyButtons()
    {
        var alteration = Button("Orb of Alteration");
        var wisdom = Button(" Scroll of Wisdom ");
        var scouring = Button("Orb of Scouring");
        var alchemy = Button("Orb of Alchemy");
        var root = new Node(new Node(), new Node(new Node(alteration, wisdom, scouring, alchemy)));

        LabelActionButtons.FindButton(root, 1, LabelActionButtons.ScrollOfWisdom, out int index)
            .Should().BeSameAs(wisdom);
        index.Should().Be(1);
        LabelActionButtons.FindButton(root, 1, LabelActionButtons.OrbOfScouring, out _)
            .Should().BeSameAs(scouring);
        LabelActionButtons.FindButton(root, 1, LabelActionButtons.OrbOfAlchemy, out _)
            .Should().BeSameAs(alchemy);
    }

    [TestMethod]
    public void CorruptionRequiresAnExactNameAndAVisibleIcon()
    {
        var hidden = Button("Vaal Orb");
        hidden.IsVisible = false;
        var substring = Button("Vaal Orb Shard");
        var vaal = Button("Vaal Orb");
        var root = new Node(new Node(), new Node(new Node(hidden, substring)), new Node(new Node(new Node(), vaal)));

        LabelActionButtons.FindButton(root, 1, LabelActionButtons.VaalOrb, out _).Should().BeNull();
        LabelActionButtons.FindButton(root, 2, LabelActionButtons.VaalOrb, out int index).Should().BeSameAs(vaal);
        index.Should().Be(1);
    }

    [TestMethod]
    public void RereadingChosenSlotRejectsChangedTooltip()
    {
        var button = Button("Orb of Alchemy");
        var root = new Node(new Node(), new Node(new Node(button)));
        LabelActionButtons.FindButton(root, 1, LabelActionButtons.OrbOfAlchemy, out int index)
            .Should().BeSameAs(button);

        button.Tooltip = Button("Orb of Alteration").Tooltip;

        LabelActionButtons.ReadButton(root, 1, index, LabelActionButtons.OrbOfAlchemy).Should().BeNull();
    }

    private static Node Button(string name) => new() { Tooltip = new Node(new Node(new Node { Text = name })) };

    public sealed class Node(params Node[] children)
    {
        public bool IsVisible { get; set; } = true;
        public Node? Tooltip { get; set; }
        public string? Text { get; init; }
        public string? TextNoTags { get; init; }
        public int ChildCount => children.Length;
        public Node? GetChildAtIndex(int index) => (uint)index < (uint)children.Length ? children[index] : null;
    }
}
