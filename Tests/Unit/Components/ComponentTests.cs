using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Reflection;

namespace ClickIt.Tests.Unit.Components
{
    [TestClass]
    [TestCategory("Unit")]
    public class ComponentTests
    {
        private static Assembly _mainAssembly;

        [ClassInitialize]
        public static void ClassSetup(TestContext context)
        {
            _mainAssembly = Assembly.Load("ClickIt");
        }

        [TestMethod]
        public void PrimaryAltarComponent_TypeExists()
        {
            var type = _mainAssembly.GetType("ClickIt.Components.PrimaryAltarComponent");
            type.Should().NotBeNull();
        }

        [TestMethod]
        public void SecondaryAltarComponent_TypeExists()
        {
            var type = _mainAssembly.GetType("ClickIt.Components.SecondaryAltarComponent");
            type.Should().NotBeNull();
        }

        [TestMethod]
        public void AltarButton_TypeExists()
        {
            var type = _mainAssembly.GetType("ClickIt.Components.AltarButton");
            type.Should().NotBeNull();
        }

        // Full instantiation tests would require mocking Element and other ExileCore types
    }
}