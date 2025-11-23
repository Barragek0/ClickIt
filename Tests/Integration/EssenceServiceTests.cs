using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using System.Reflection;
using ClickIt.Tests.Shared;

namespace ClickIt.Tests.Unit.Services
{
    [TestClass]
    [TestCategory("Unit")]
    public class EssenceServiceTests
    {
        private static Assembly _mainAssembly;

        [ClassInitialize]
        public static void ClassSetup(TestContext context)
        {
            _mainAssembly = Assembly.Load("ClickIt");
        }

        [TestMethod]
        public void EssenceService_TypeExists()
        {
            var essenceServiceType = _mainAssembly.GetType("ClickIt.Services.EssenceService");
            essenceServiceType.Should().NotBeNull();
        }

        // Skip detailed checks that require loading ExileCore types
        // Full tests would be in integration tests
    }
}