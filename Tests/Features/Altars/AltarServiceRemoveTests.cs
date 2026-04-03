namespace ClickIt.Tests.Features.Altars
{
    [TestClass]
    public class AltarServiceRemoveTests
    {
        [TestMethod]
        public void RemoveAltarComponentsByElement_NullElement_Noop()
        {
            var clickIt = new ClickIt();
            var settings = new ClickItSettings();
            var service = new AltarService(clickIt, settings, null);

            var component = TestBuilders.BuildPrimary();
            service.AddAltarComponent(component).Should().BeTrue();

            service.RemoveAltarComponentsByElement(null!);

            service.GetAltarComponentsReadOnly().Should().Contain(component);
        }
    }
}
