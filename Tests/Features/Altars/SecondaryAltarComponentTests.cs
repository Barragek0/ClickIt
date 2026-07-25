namespace ClickIt.Tests.Features.Altars
{
    [TestClass]
    public class SecondaryAltarComponentTests
    {
        [TestMethod]
        public void Constructor_PadsMissingSlots_AndPreservesValues()
        {
            SecondaryAltarComponent component = new(null, ["up0", "up1"], ["down0"], hasUnmatchedMods: true);

            component.HasUnmatchedMods.Should().BeTrue();
            component.GetUpsideByIndex(0).Should().Be("up0");
            component.GetUpsideByIndex(1).Should().Be("up1");
            component.GetUpsideByIndex(2).Should().BeEmpty();
            component.GetDownsideByIndex(0).Should().Be("down0");
            component.GetDownsideByIndex(1).Should().BeEmpty();
            component[0].Should().Be("up0");
            component[4].Should().Be("down0");
            component[7].Should().BeEmpty();
            component[8].Should().BeEmpty();
        }

        [TestMethod]
        public void Constructor_UsesEmptyArrays_WhenListsAreNull()
        {
            SecondaryAltarComponent component = new(null, null!, null!);

            component.GetAllUpsides().Should().HaveCount(8);
            component.GetAllDownsides().Should().HaveCount(8);
            component.GetModByIndex(-1).Should().BeEmpty();
            component.GetUpsideByIndex(8).Should().BeEmpty();
            component.GetDownsideByIndex(8).Should().BeEmpty();
        }
    }
}