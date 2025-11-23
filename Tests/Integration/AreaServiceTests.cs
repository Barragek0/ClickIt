using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using System.Reflection;
using System.IO;

namespace ClickIt.Tests.Unit.Services
{
    [TestClass]
    [TestCategory("Integration")]
    public class AreaServiceTests
    {
        private static Assembly _mainAssembly;

        [ClassInitialize]
        public static void ClassSetup(TestContext context)
        {
            // Load the main ClickIt assembly
            _mainAssembly = Assembly.Load("ClickIt");
        }

        [TestMethod]
        public void UpdateScreenAreas_MethodExists()
        {
            // Get AreaService type from loaded assembly
            var areaServiceType = _mainAssembly.GetType("ClickIt.Services.AreaService");
            areaServiceType.Should().NotBeNull();

            // Check that UpdateScreenAreas method exists
            var updateMethod = areaServiceType.GetMethod("UpdateScreenAreas", BindingFlags.Public | BindingFlags.Instance);
            updateMethod.Should().NotBeNull();
        }

        [TestMethod]
        public void AreaService_PropertiesExist()
        {
            var areaServiceType = _mainAssembly.GetType("ClickIt.Services.AreaService");
            areaServiceType.Should().NotBeNull();

            // Check properties exist
            var fullScreenProp = areaServiceType.GetProperty("FullScreenRectangle", BindingFlags.Public | BindingFlags.Instance);
            fullScreenProp.Should().NotBeNull();

            var healthProp = areaServiceType.GetProperty("HealthAndFlaskRectangle", BindingFlags.Public | BindingFlags.Instance);
            healthProp.Should().NotBeNull();

            var manaProp = areaServiceType.GetProperty("ManaAndSkillsRectangle", BindingFlags.Public | BindingFlags.Instance);
            manaProp.Should().NotBeNull();

            var buffsProp = areaServiceType.GetProperty("BuffsAndDebuffsRectangle", BindingFlags.Public | BindingFlags.Instance);
            buffsProp.Should().NotBeNull();
        }
    }

    // Simple mock classes for testing (placeholder for future full tests)
    public class MockGameController
    {
        public MockWindow Window { get; } = new MockWindow();
    }

    public class MockWindow
    {
        public object GetWindowRectangleTimeCache { get; set; }
    }
}