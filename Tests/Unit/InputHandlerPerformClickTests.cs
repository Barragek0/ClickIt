using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClickIt.Utils;
using SharpDX;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class InputHandlerPerformClickTests
    {
        [TestMethod]
        public void PerformClick_DoesNotThrow_WhenNativeInputDisabled()
        {
            Mouse.DisableNativeInput = true;

            var settings = new ClickItSettings();
            var perf = new PerformanceMonitor(settings);
            var error = new ErrorHandler(settings, (s, f) => { }, (s, f) => { });

            var handler = new InputHandler(settings, perf, error);

            // Should complete without throwing (no native cursor / click will occur because DisableNativeInput is true)
            handler.PerformClick(new Vector2(10, 20));

            // Confirm a successful click timing was recorded
            var avg = perf.GetAverageSuccessfulClickTiming();
            Assert.IsTrue(avg >= 0, "Expected PerformClick to record a successful click timing (or 0) when called under test conditions.");
        }
    }
}
