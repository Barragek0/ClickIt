using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Collections.Generic;
namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class AltarServiceGetAltarLabelsTests
    {
        [TestMethod]
        public void Add_Get_Clear_AltarComponents_RepositoryPaths()
        {
            var clickIt = new ClickIt();
            var settings = new ClickItSettings();
            var svc = new Services.AltarService(clickIt, settings, null);

            // initially empty
            svc.GetAltarComponents().Should().BeEmpty();

            // add primary component and verify present
            var comp = TestUtils.TestBuilders.BuildPrimary();
            svc.AddAltarComponent(comp).Should().BeTrue();
            svc.GetAltarComponentsReadOnly().Should().Contain(comp);

            // clear repository -> empty
            svc.ClearAltarComponents();
            svc.GetAltarComponents().Should().BeEmpty();
        }
    }
}
