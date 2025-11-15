using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using System.Reflection;

namespace ClickIt.Tests.Services
{
    [TestClass]
    public class ClickServiceSurfaceMoreTests
    {
        [TestMethod]
        public void ProcessMethods_ShouldReturn_IEnumerator()
        {
            var csType = Type.GetType("ClickIt.Services.ClickService, ClickIt");
            csType.Should().NotBeNull();

            var procAltar = csType.GetMethod("ProcessAltarClicking", BindingFlags.Public | BindingFlags.Instance);
            var procRegular = csType.GetMethod("ProcessRegularClick", BindingFlags.Public | BindingFlags.Instance);

            procAltar.Should().NotBeNull();
            procRegular.Should().NotBeNull();

            procAltar.ReturnType.FullName.Should().Be("System.Collections.IEnumerator");
            procRegular.ReturnType.FullName.Should().Be("System.Collections.IEnumerator");
        }

        [TestMethod]
        public void ShouldClickAltar_Signature_IsExpected()
        {
            var csType = Type.GetType("ClickIt.Services.ClickService, ClickIt");
            csType.Should().NotBeNull();

            var shouldClick = csType.GetMethod("ShouldClickAltar", BindingFlags.Public | BindingFlags.Instance);
            shouldClick.Should().NotBeNull();

            shouldClick.ReturnType.Should().Be(typeof(bool));
            shouldClick.GetParameters().Length.Should().Be(3);
        }

        [TestMethod]
        public void ClickAltarElement_Exists_AsMethod()
        {
            var csType = Type.GetType("ClickIt.Services.ClickService, ClickIt");
            csType.Should().NotBeNull();

            var clickAltar = csType.GetMethod("ClickAltarElement", BindingFlags.NonPublic | BindingFlags.Instance)
                             ?? csType.GetMethod("ClickAltarElement", BindingFlags.Public | BindingFlags.Instance);
            clickAltar.Should().NotBeNull("ClickAltarElement helper method should exist (private or public)");
        }
    }
}
