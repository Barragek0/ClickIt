using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Services;
using ClickIt.Components;
using System.Collections.Generic;

namespace ClickIt.Tests.Unit
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

            var component = Tests.TestUtils.TestBuilders.BuildPrimary();
            service.AddAltarComponent(component).Should().BeTrue();

            // Removing by null should return early and leave components intact
            service.RemoveAltarComponentsByElement(null);

            service.GetAltarComponentsReadOnly().Should().Contain(component);
        }
    }
}
