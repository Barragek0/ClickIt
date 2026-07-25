namespace ClickIt.Tests.Features.Altars
{
    [TestClass]
    public class AltarServiceTests
    {
        [TestMethod]
        public void ProcessAltarScanningLogic_ClearsComponents_WhenNoLabels()
        {
            var clickIt = new ClickIt();
            var settings = new ClickItSettings();

            var service = new AltarService(clickIt, settings, null);

            var topMods = new SecondaryAltarComponent(null, [], []);
            var bottomMods = new SecondaryAltarComponent(null, [], []);
            var topButton = new AltarButton(null);
            var bottomButton = new AltarButton(null);

            var component = new PrimaryAltarComponent(AltarType.Unknown, topMods, topButton, bottomMods, bottomButton);

            bool added = service.AddAltarComponent(component);
            added.Should().BeTrue();
            service.GetAltarComponentsReadOnly().Should().Contain(component);

            service.ProcessAltarScanningLogic();

            service.GetAltarComponentsReadOnly().Should().BeEmpty();
        }

        [TestMethod]
        public void AddAltarComponent_RejectsDuplicateComponentsWithSameKey()
        {
            var clickIt = new ClickIt();
            var settings = new ClickItSettings();
            var service = new AltarService(clickIt, settings, null);
            var first = TestBuilders.BuildPrimary();
            var duplicate = TestBuilders.BuildPrimary();

            service.AddAltarComponent(first).Should().BeTrue();
            service.AddAltarComponent(duplicate).Should().BeFalse();
            service.GetAltarComponentCount().Should().Be(1);
        }
    }
}

