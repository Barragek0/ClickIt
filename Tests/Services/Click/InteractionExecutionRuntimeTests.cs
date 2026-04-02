using ClickIt.Services.Click.Interaction;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpDX;

namespace ClickIt.Tests.Click
{
    [TestClass]
    public class InteractionExecutionRuntimeTests
    {
        [TestMethod]
        public void Execute_ReturnsFalse_WhenCursorGateRejects()
        {
            int clickCalls = 0;
            int holdCalls = 0;
            int intervalCalls = 0;

            var runtime = new InteractionExecutionRuntime(new InteractionExecutionRuntimeDependencies(
                _ => false,
                (_, _, _, _, _, _) => clickCalls++,
                (_, _, _, _, _, _, _) => holdCalls++,
                () => intervalCalls++));

            bool executed = runtime.Execute(new InteractionExecutionRequest(
                ClickPosition: new Vector2(100, 200),
                ExpectedElement: null,
                Controller: null,
                UseHoldClick: false,
                HoldDurationMs: 0,
                ForceUiHoverVerification: false,
                AllowWhenHotkeyInactive: false,
                AvoidCursorMove: false,
                OutsideWindowLogMessage: "outside"));

            executed.Should().BeFalse();
            clickCalls.Should().Be(0);
            holdCalls.Should().Be(0);
            intervalCalls.Should().Be(0);
        }

        [TestMethod]
        public void Execute_UsesRegularClickPath_WhenHoldDisabled()
        {
            int clickCalls = 0;
            int holdCalls = 0;
            int intervalCalls = 0;

            var runtime = new InteractionExecutionRuntime(new InteractionExecutionRuntimeDependencies(
                _ => true,
                (_, _, _, _, _, _) => clickCalls++,
                (_, _, _, _, _, _, _) => holdCalls++,
                () => intervalCalls++));

            bool executed = runtime.Execute(new InteractionExecutionRequest(
                ClickPosition: new Vector2(100, 200),
                ExpectedElement: null,
                Controller: null,
                UseHoldClick: false,
                HoldDurationMs: 0,
                ForceUiHoverVerification: false,
                AllowWhenHotkeyInactive: false,
                AvoidCursorMove: false,
                OutsideWindowLogMessage: "outside"));

            executed.Should().BeTrue();
            clickCalls.Should().Be(1);
            holdCalls.Should().Be(0);
            intervalCalls.Should().Be(1);
        }

        [TestMethod]
        public void Execute_UsesHoldClickPath_WhenEnabled()
        {
            int clickCalls = 0;
            int holdCalls = 0;
            int intervalCalls = 0;

            var runtime = new InteractionExecutionRuntime(new InteractionExecutionRuntimeDependencies(
                _ => true,
                (_, _, _, _, _, _) => clickCalls++,
                (_, _, _, _, _, _, _) => holdCalls++,
                () => intervalCalls++));

            bool executed = runtime.Execute(new InteractionExecutionRequest(
                ClickPosition: new Vector2(100, 200),
                ExpectedElement: null,
                Controller: null,
                UseHoldClick: true,
                HoldDurationMs: 150,
                ForceUiHoverVerification: false,
                AllowWhenHotkeyInactive: false,
                AvoidCursorMove: false,
                OutsideWindowLogMessage: "outside"));

            executed.Should().BeTrue();
            clickCalls.Should().Be(0);
            holdCalls.Should().Be(1);
            intervalCalls.Should().Be(1);
        }
    }
}