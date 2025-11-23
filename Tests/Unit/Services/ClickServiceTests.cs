using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using System.Reflection;
using System.Collections.Generic;
using SharpDX;

namespace ClickIt.Tests.Unit.Services
{
    [TestClass]
    [TestCategory("Unit")]
    public class ClickServiceTests
    {
        private static Assembly _mainAssembly;

        [ClassInitialize]
        public static void ClassSetup(TestContext context)
        {
            _mainAssembly = Assembly.Load("ClickIt");
        }

        [TestMethod]
        public void ClickService_TypeExists()
        {
            var clickServiceType = _mainAssembly.GetType("ClickIt.Services.ClickService");
            clickServiceType.Should().NotBeNull();
        }

        [TestMethod]
        public void ClickService_HasElementAccessLock()
        {
            var clickServiceType = _mainAssembly.GetType("ClickIt.Services.ClickService");
            var lockMethod = clickServiceType.GetMethod("GetElementAccessLock", BindingFlags.Public | BindingFlags.Instance);
            lockMethod.Should().NotBeNull();
            // Don't check return type to avoid ExileCore loading
        }

        // Skip detailed checks that require loading ExileCore types
        // Full tests would be in integration tests

        // Additional tests would require full mocking of all dependencies
        // For now, these basic tests verify the class structure
    }
}