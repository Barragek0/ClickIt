using System;
using System.Reflection;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpDX;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickServiceEssenceOverlapTests
    {
        private static object? InvokePrivateStatic(string name, params object[] args)
        {
            var method = typeof(Services.ClickService).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();
            return method!.Invoke(null, args);
        }

        [TestMethod]
        public void AreRectanglesOverlapping_ReturnsTrue_WhenRectanglesIntersect()
        {
            var a = new RectangleF(0, 0, 10, 10);
            var b = new RectangleF(5, 5, 10, 10);

            var res = (bool)InvokePrivateStatic("AreRectanglesOverlapping", a, b)!;
            res.Should().BeTrue();
        }

        [TestMethod]
        public void AreRectanglesOverlapping_ReturnsFalse_WhenRectanglesDoNotIntersect()
        {
            var a = new RectangleF(0, 0, 10, 10);
            var b = new RectangleF(20, 20, 10, 10);

            var res = (bool)InvokePrivateStatic("AreRectanglesOverlapping", a, b)!;
            res.Should().BeFalse();
        }
    }
}
