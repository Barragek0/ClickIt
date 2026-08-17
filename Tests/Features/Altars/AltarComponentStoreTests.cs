namespace ClickIt.Tests.Features.Altars
{
    [TestClass]
    public class AltarComponentStoreTests
    {
        [TestMethod]
        public void AddDuplicateKeyComponent_RefreshesTrackedComponentElementReferences()
        {
            var store = new AltarComponentStore();

            PrimaryAltarComponent first = TestBuilders.BuildPrimary();
            store.Add(first).Should().BeTrue();

            PrimaryAltarComponent refreshed = TestBuilders.BuildPrimary();
            store.Add(refreshed).Should().BeFalse();

            first.TopMods.Should().BeSameAs(refreshed.TopMods);
            first.BottomMods.Should().BeSameAs(refreshed.BottomMods);
            first.TopButton.Should().BeSameAs(refreshed.TopButton);
            first.BottomButton.Should().BeSameAs(refreshed.BottomButton);
        }
    }
}
