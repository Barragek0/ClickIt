using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Reflection;
using System.Windows.Forms;
using ClickIt.Rendering;
using ClickIt.Utils;
using SharpDX;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LazyModeRendererSeamsTests
    {
        private static MethodInfo GetComposeMethod()
        {
            return typeof(LazyModeRenderer).GetMethod("ComposeLazyModeStatus", BindingFlags.NonPublic | BindingFlags.Instance)!;
        }

        [TestMethod]
        public void ComposeLazyModeStatus_RestrictedAndHotkeyHeld_ReturnsGreenOverride()
        {
            var renderer = new LazyModeRenderer(new ClickItSettings(), new DeferredTextQueue(), null, null);
            var method = GetComposeMethod();

            var result = ((Color color, string, string, string))method.Invoke(renderer, [true, true, false, false, false, false, null, Keys.F1]);

            result.color.Should().Be(Color.LawnGreen);
            result.Item2.Should().StartWith("Blocking overridden by hotkey");
        }

        [TestMethod]
        public void ComposeLazyModeStatus_RestrictedAndHotkeyNotHeld_ReturnsRedLockedMessage()
        {
            var renderer = new LazyModeRenderer(new ClickItSettings(), new DeferredTextQueue(), null, null);
            var method = GetComposeMethod();

            var result = ((Color color, string, string, string))method.Invoke(renderer, [true, false, false, false, false, false, null, Keys.F2]);

            result.color.Should().Be(Color.Red);
            result.Item2.Should().Contain("Locked chest or tree detected");
            result.Item3.Should().Contain("Hold");
        }

        [TestMethod]
        public void ComposeLazyModeStatus_LazyDisableHeld_ReturnsRedDisableMessage()
        {
            var renderer = new LazyModeRenderer(new ClickItSettings(), new DeferredTextQueue(), null, null);
            var method = GetComposeMethod();

            var result = ((Color color, string, string, string))method.Invoke(renderer, [false, false, true, false, false, false, null, Keys.F3]);

            result.color.Should().Be(Color.Red);
            result.Item2.Should().Contain("Lazy mode disabled by hotkey");
        }

        [TestMethod]
        public void ComposeLazyModeStatus_MouseButtonsBlocking_ReturnsCorrectButtonName()
        {
            var renderer = new LazyModeRenderer(new ClickItSettings(), new DeferredTextQueue(), null, null);
            var method = GetComposeMethod();

            // left mouse blocks only
            var leftResult = ((Color color, string, string, string))method.Invoke(renderer, [false, false, false, true, true, false, null, Keys.F1]);
            leftResult.color.Should().Be(Color.Red);
            leftResult.Item2.Should().StartWith("Left mouse button held");

            // both mouse buttons block
            var bothResult = ((Color color, string, string, string))method.Invoke(renderer, [false, false, false, true, true, true, null, Keys.F1]);
            bothResult.color.Should().Be(Color.Red);
            bothResult.Item2.Should().StartWith("both mouse buttons held");
        }
    }
}
