using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using System.Linq;

namespace ClickIt.Tests.Services
{
    [TestClass]
    public class ClickServiceSurfaceTests
    {
        [TestMethod]
        public void ClickService_ShouldExposeExpectedPublicSurface()
        {
            // Use reflection to assert presence of key public members without needing heavy dependencies
            var csType = Type.GetType("ClickIt.Services.ClickService, ClickIt");
            csType.Should().NotBeNull("ClickService type should be present in the ClickIt assembly");

            // Public method GetElementAccessLock()
            var getLock = csType.GetMethod("GetElementAccessLock", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            getLock.Should().NotBeNull("GetElementAccessLock should be a public instance method");
            getLock.ReturnType.Should().Be(typeof(object), "GetElementAccessLock should return object for external synchronization");

            // Public enumerator methods for process flows
            var procAltar = csType.GetMethod("ProcessAltarClicking", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var procRegular = csType.GetMethod("ProcessRegularClick", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            procAltar.Should().NotBeNull("ProcessAltarClicking should be a public instance method exposing coroutine workflow");
            procRegular.Should().NotBeNull("ProcessRegularClick should be a public instance method exposing coroutine workflow");

            // Ensure there is a ShouldClickAltar method with expected signature
            var shouldClick = csType.GetMethod("ShouldClickAltar", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            shouldClick.Should().NotBeNull("ShouldClickAltar should be present to allow unit testing of altar selection logic");

            // Verify that ClickAltarElement exists as private or public (implementation detail)
            var clickAltar = csType.GetMethod("ClickAltarElement", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                             ?? csType.GetMethod("ClickAltarElement", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            clickAltar.Should().NotBeNull("ClickAltarElement helper should exist (private or public)");
        }
    }
}
