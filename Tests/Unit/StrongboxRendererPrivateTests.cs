using Microsoft.VisualStudio.TestTools.UnitTesting;
#nullable enable
using FluentAssertions;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Reflection;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class StrongboxRendererPrivateTests
    {
        private static MethodInfo GetTargetMethod()
        {
            return typeof(Rendering.StrongboxRenderer).GetMethod("TryGetVisibleLabelRect", BindingFlags.NonPublic | BindingFlags.Static)!;
        }

        [TestMethod]
        public void TryGetVisibleLabelRect_NullLabel_ReturnsFalse()
        {
            var method = GetTargetMethod();
            var args = new object?[] { null, SharpDX.RectangleF.Empty, null, null };
            var ret = (bool)method.Invoke(null, args)!;
            ret.Should().BeFalse();
        }

        [TestMethod]
        public void TryGetVisibleLabelRect_NoItemPath_ReturnsFalse()
        {
            // Use the test seam to validate the "no item path" case without touching ExileCore internals.
            var seam = typeof(Rendering.StrongboxRenderer).GetMethod("TryGetVisibleLabelRect_ForTests", BindingFlags.NonPublic | BindingFlags.Static)!;
            // seam signature: (string? itemPathRawCandidate, bool elementIsValid, object? maybeRectObj, SharpDX.RectangleF windowArea, out RectangleF rect, out string? itemPathRaw)
            var parameters = new object?[] { null, false, null, SharpDX.RectangleF.Empty, null, null };
            var ret = (bool)seam.Invoke(null, parameters)!;
            ret.Should().BeFalse();
        }

        [TestMethod]
        public void TryGetVisibleLabelRect_PathDoesNotContainStrongbox_ReturnsFalse()
        {
            // no need to create an actual LabelOnGround instance — use seam

            // Use the test seam to verify that a path not containing "strongbox" fails quickly.
            var seam = typeof(Rendering.StrongboxRenderer).GetMethod("TryGetVisibleLabelRect_ForTests", BindingFlags.NonPublic | BindingFlags.Static)!;
            var parameters = new object?[] { "some/random/path", true, null, SharpDX.RectangleF.Empty, null, null };
            var ret = (bool)seam.Invoke(null, parameters)!;
            ret.Should().BeFalse();
        }

        [TestMethod]
        public void TryGetVisibleLabelRect_StrongboxPath_ButLabelNull_ReturnsFalse()
        {
            // Use seam: path contains strongbox but element (Label) is absent -> should return false
            var seam = typeof(Rendering.StrongboxRenderer).GetMethod("TryGetVisibleLabelRect_ForTests", BindingFlags.NonPublic | BindingFlags.Static)!;
            var parameters = new object?[] { "This/contains/Strongbox", false, null, SharpDX.RectangleF.Empty, null, null };
            var ret = (bool)seam.Invoke(null, parameters)!;
            ret.Should().BeFalse();
        }

        [TestMethod]
        public void TryGetVisibleLabelRect_StrongboxPath_LabelInvalid_ReturnsFalse()
        {
            // Use seam: path contains strongbox and element present but marked invalid
            var seam = typeof(Rendering.StrongboxRenderer).GetMethod("TryGetVisibleLabelRect_ForTests", BindingFlags.NonPublic | BindingFlags.Static)!;
            var parameters = new object?[] { "Some/StrongBoxes/Strongbox", false, new SharpDX.RectangleF(1, 2, 3, 4), SharpDX.RectangleF.Empty, null, null };
            var ret = (bool)seam.Invoke(null, parameters)!;
            ret.Should().BeFalse();
        }
    }
}
