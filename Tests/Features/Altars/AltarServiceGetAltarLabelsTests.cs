using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
namespace ClickIt.Tests.Features.Altars
{
    [TestClass]
    public class AltarServiceGetAltarLabelsTests
    {
        [TestMethod]
        public void Add_Get_Clear_AltarComponents_RepositoryPaths()
        {
            var clickIt = new ClickIt();
            var settings = new ClickItSettings();
            var svc = new global::ClickIt.Features.Altars.AltarService(clickIt, settings, null);

            svc.GetAltarComponents().Should().BeEmpty();

            var comp = TestUtils.TestBuilders.BuildPrimary();
            svc.AddAltarComponent(comp).Should().BeTrue();
            svc.GetAltarComponentsReadOnly().Should().Contain(comp);

            svc.ClearAltarComponents();
            svc.GetAltarComponents().Should().BeEmpty();
        }
    }
}
