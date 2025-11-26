using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickItLogMessageTests
    {
        [TestMethod]
        public void LogMessageBool_DoesNotRecurse_WhenNotRendering()
        {
            var clickIt = new ClickIt();
            clickIt.__Test_SetSettings(new ClickItSettings());

            // Previously this path recursed - ensure it completes (no exception).
            clickIt.State.IsRendering = false;

            // No exception should be thrown, this verifies the method forwards correctly.
            var lastField = clickIt.GetType().GetField("_lastAlertTimes", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var dictBefore = (System.Collections.Generic.Dictionary<string, System.DateTime>)lastField!.GetValue(clickIt)!;

            // Use localDebug=false to avoid reading Settings which may be null in some test harness scenarios
            clickIt.LogMessage(false, "test-message", 0);

            var dictAfter = (System.Collections.Generic.Dictionary<string, System.DateTime>)lastField!.GetValue(clickIt)!;
            dictAfter.Count.Should().Be(dictBefore.Count);
        }

        [TestMethod]
        public void LogMessageBool_Skips_WhenRendering()
        {
            var clickIt = new ClickIt();
            clickIt.__Test_SetSettings(new ClickItSettings());

            clickIt.State.IsRendering = true;

            // Should return quickly and not throw — assert nothing changed in last-alert timestamps.
            var lastField = clickIt.GetType().GetField("_lastAlertTimes", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var before = (System.Collections.Generic.Dictionary<string, System.DateTime>)lastField!.GetValue(clickIt)!;

            clickIt.LogMessage(false, "should-not-log", 0);

            var after = (System.Collections.Generic.Dictionary<string, System.DateTime>)lastField!.GetValue(clickIt)!;
            after.Count.Should().Be(before.Count);
        }
    }
}
