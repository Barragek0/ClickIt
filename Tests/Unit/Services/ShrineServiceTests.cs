using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using System.Reflection;

namespace ClickIt.Tests.Unit.Services
{
    [TestClass]
    [TestCategory("Unit")]
    public class ShrineServiceTests
    {
        private static Assembly _mainAssembly;

        [ClassInitialize]
        public static void ClassSetup(TestContext context)
        {
            _mainAssembly = Assembly.Load("ClickIt");
        }

        [TestMethod]
        public void ShrineService_TypeExists()
        {
            var shrineServiceType = _mainAssembly.GetType("ClickIt.Services.ShrineService");
            shrineServiceType.Should().NotBeNull();
        }

        // Full tests would require mocking GameController and Camera
    }
}