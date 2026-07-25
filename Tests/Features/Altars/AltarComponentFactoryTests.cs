namespace ClickIt.Tests.Features.Altars
{
    [TestClass]
    public class AltarComponentFactoryTests
    {
        [TestMethod]
        public void CreateFromAdapter_Throws_WhenParentOrGrandparentMissing()
        {
            var factory = CreateFactory();
            var mockElementAdapter = new Mock<IElementAdapter>();
            mockElementAdapter.SetupGet(a => a.Parent).Returns((IElementAdapter?)null);

            FluentActions.Invoking(() => factory.CreateFromAdapter(mockElementAdapter.Object, AltarType.Unknown))
                .Should().Throw<InvalidOperationException>();
        }

        [TestMethod]
        public void CreateFromAdapter_Throws_WhenTopOrBottomMissing()
        {
            var factory = CreateFactory();
            var mockElementAdapter = new Mock<IElementAdapter>();
            var mockParentAdapter = new Mock<IElementAdapter>();
            var mockAltarParentAdapter = new Mock<IElementAdapter>();

            mockElementAdapter.SetupGet(a => a.Parent).Returns(mockParentAdapter.Object);
            mockParentAdapter.SetupGet(a => a.Parent).Returns(mockAltarParentAdapter.Object);

            var mockTopAltarAdapter = new Mock<IElementAdapter>();
            mockAltarParentAdapter.Setup(a => a.GetChildFromIndices(0, 1)).Returns(mockTopAltarAdapter.Object);
            mockAltarParentAdapter.Setup(a => a.GetChildFromIndices(1, 1)).Returns((IElementAdapter?)null);

            mockTopAltarAdapter.SetupGet(a => a.Parent).Returns(mockParentAdapter.Object);
            mockTopAltarAdapter.Setup(a => a.GetText(It.IsAny<int>())).Returns(string.Empty);
            mockTopAltarAdapter.SetupGet(a => a.Underlying).Returns((Element?)null);

            FluentActions.Invoking(() => factory.CreateFromAdapter(mockElementAdapter.Object, AltarType.Unknown))
                .Should().Throw<InvalidOperationException>();
        }

        [TestMethod]
        public void CreateFromAdapter_CreatesComponent()
        {
            var factory = CreateFactory();
            var mockElementAdapter = new Mock<IElementAdapter>();
            var mockParentAdapter = new Mock<IElementAdapter>();
            var mockAltarParentAdapter = new Mock<IElementAdapter>();
            var mockTopAltarAdapter = new Mock<IElementAdapter>();
            var mockBottomAltarAdapter = new Mock<IElementAdapter>();

            mockElementAdapter.SetupGet(a => a.Parent).Returns(mockParentAdapter.Object);
            mockParentAdapter.SetupGet(a => a.Parent).Returns(mockAltarParentAdapter.Object);

            mockAltarParentAdapter.Setup(a => a.GetChildFromIndices(0, 1)).Returns(mockTopAltarAdapter.Object);
            mockAltarParentAdapter.Setup(a => a.GetChildFromIndices(1, 1)).Returns(mockBottomAltarAdapter.Object);

            mockTopAltarAdapter.SetupGet(a => a.Parent).Returns(mockParentAdapter.Object);
            mockBottomAltarAdapter.SetupGet(a => a.Parent).Returns(mockParentAdapter.Object);
            mockTopAltarAdapter.Setup(a => a.GetText(It.IsAny<int>())).Returns(string.Empty);
            mockBottomAltarAdapter.Setup(a => a.GetText(It.IsAny<int>())).Returns(string.Empty);
            mockTopAltarAdapter.SetupGet(a => a.Underlying).Returns((Element?)null);
            mockBottomAltarAdapter.SetupGet(a => a.Underlying).Returns((Element?)null);

            PrimaryAltarComponent created = factory.CreateFromAdapter(mockElementAdapter.Object, AltarType.SearingExarch);

            created.AltarType.Should().Be(AltarType.SearingExarch);
            created.TopMods.Should().NotBeNull();
            created.BottomMods.Should().NotBeNull();
        }

        [TestMethod]
        public void UpdateFromAdapter_SetsTopModsAndButtons()
        {
            var primary = TestBuilders.BuildPrimary();
            var mockElementAdapter = new Mock<IElementAdapter>();
            mockElementAdapter.SetupGet(a => a.Underlying).Returns((Element?)null);

            AltarComponentFactory.UpdateFromAdapter(true, primary, mockElementAdapter.Object, ["up1"], ["down1"], true);

            primary.TopMods.Should().NotBeNull();
            primary.TopButton.Should().NotBeNull();
            primary.TopMods.HasUnmatchedMods.Should().BeTrue();
            primary.TopMods.Upsides.Should().Contain("up1");
        }

        [TestMethod]
        public void UpdateFromAdapter_Throws_WhenElementNull()
        {
            var primary = TestBuilders.BuildPrimary();

            FluentActions.Invoking(() => AltarComponentFactory.UpdateFromAdapter(true, primary, null!, [], [], false))
                .Should().Throw<ArgumentNullException>();
        }

        [TestMethod]
        public void UpdateFromAdapter_SetsBottomModsAndButtons()
        {
            var primary = TestBuilders.BuildPrimary();
            var mockElementAdapter = new Mock<IElementAdapter>();
            mockElementAdapter.SetupGet(a => a.Underlying).Returns((Element?)null);

            AltarComponentFactory.UpdateFromAdapter(false, primary, mockElementAdapter.Object, ["up1"], ["down1"], true);

            primary.BottomMods.Should().NotBeNull();
            primary.BottomButton.Should().NotBeNull();
            primary.BottomMods.HasUnmatchedMods.Should().BeTrue();
            primary.BottomMods.Upsides.Should().Contain("up1");
        }

        [TestMethod]
        public void WarmAddedData_DoesNotPrecache_WhenComponentNotAdded()
        {
            var primary = TestBuilders.BuildPrimary();

            FluentActions.Invoking(() => AltarComponentFactory.WarmAddedData(primary, false))
                .Should().NotThrow();

            FluentActions.Invoking(() => AltarComponentFactory.WarmAddedData(primary, true))
                .Should().Throw<InvalidOperationException>();
        }

        [TestMethod]
        public void CreateFromAdapter_RecordsUnmatchedMods_WithoutTriggeringAlerts()
        {
            int alertCount = 0;
            string? unmatchedMod = null;
            string? unmatchedType = null;
            var factory = CreateFactory(
                triggerAlert: _ => alertCount++,
                recordUnmatchedMod: (mod, negativeType) =>
                {
                    unmatchedMod = mod;
                    unmatchedType = negativeType;
                });

            var mockElementAdapter = new Mock<IElementAdapter>();
            var mockParentAdapter = new Mock<IElementAdapter>();
            var mockAltarParentAdapter = new Mock<IElementAdapter>();
            var mockTopAltarAdapter = new Mock<IElementAdapter>();
            var mockBottomAltarAdapter = new Mock<IElementAdapter>();

            mockElementAdapter.SetupGet(a => a.Parent).Returns(mockParentAdapter.Object);
            mockParentAdapter.SetupGet(a => a.Parent).Returns(mockAltarParentAdapter.Object);
            mockAltarParentAdapter.Setup(a => a.GetChildFromIndices(0, 1)).Returns(mockTopAltarAdapter.Object);
            mockAltarParentAdapter.Setup(a => a.GetChildFromIndices(1, 1)).Returns(mockBottomAltarAdapter.Object);
            mockTopAltarAdapter.SetupGet(a => a.Parent).Returns(mockParentAdapter.Object);
            mockBottomAltarAdapter.SetupGet(a => a.Parent).Returns(mockParentAdapter.Object);
            mockTopAltarAdapter.SetupGet(a => a.Underlying).Returns((Element?)null);
            mockBottomAltarAdapter.SetupGet(a => a.Underlying).Returns((Element?)null);
            mockTopAltarAdapter.Setup(a => a.GetText(It.IsAny<int>())).Returns("Players\nGain 100% increased Experience");
            mockBottomAltarAdapter.Setup(a => a.GetText(It.IsAny<int>())).Returns("Players\nNot A Real Mod");

            factory.CreateFromAdapter(mockElementAdapter.Object, AltarType.SearingExarch);

            alertCount.Should().Be(0);
            unmatchedMod.Should().Be("NotARealMod");
            unmatchedType.Should().Be("Players");
        }

        private static AltarComponentFactory CreateFactory(
            Action<string>? triggerAlert = null,
            Action<int>? recordMatchedCount = null,
            Action<string, string>? recordUnmatchedMod = null)
        {
            return new AltarComponentFactory(
                new AltarMatcher(),
                triggerAlert ?? (_ => { }),
                recordMatchedCount ?? (_ => { }),
                recordUnmatchedMod ?? ((_, _) => { }));
        }
    }
}