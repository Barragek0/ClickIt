namespace ClickIt.Tests.Features.Altars
{
    [TestClass]
    public class AltarServiceStateTests
    {
        [TestMethod]
        public void Add_Get_Clear_AltarComponents_RepositoryPaths()
        {
            var clickIt = new ClickIt();
            var settings = new ClickItSettings();
            var service = new AltarService(clickIt, settings, null);

            service.GetAltarComponents().Should().BeEmpty();

            var component = TestBuilders.BuildPrimary();
            service.AddAltarComponent(component).Should().BeTrue();
            service.GetAltarComponentsReadOnly().Should().Contain(component);

            service.ClearAltarComponents();
            service.GetAltarComponents().Should().BeEmpty();
        }

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