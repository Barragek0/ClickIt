namespace ClickIt.Tests.Features.Click
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
                _ => true,
                _ => { },
                (_, _, _, _, _, _) => { clickCalls++; return true; },
                (_, _, _, _, _, _, _) => { holdCalls++; return true; },
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
                _ => true,
                _ => { },
                (_, _, _, _, _, _) => { clickCalls++; return true; },
                (_, _, _, _, _, _, _) => { holdCalls++; return true; },
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
        public void Execute_ReturnsFalse_WhenClickExecutorRejects_AndSkipsSuccessAftermath()
        {
            int clickCalls = 0;
            int intervalCalls = 0;

            var runtime = new InteractionExecutionRuntime(new InteractionExecutionRuntimeDependencies(
                _ => true,
                _ => true,
                _ => { },
                (_, _, _, _, _, _) => { clickCalls++; return false; },
                (_, _, _, _, _, _, _) => { return false; },
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

            executed.Should().BeFalse("an internally-rejected click must not count as executed");
            clickCalls.Should().Be(1);
            intervalCalls.Should().Be(0, "no click interval may be recorded for a click that was not sent");
        }

        [TestMethod]
        public void Execute_UsesHoldClickPath_WhenEnabled()
        {
            int clickCalls = 0;
            int holdCalls = 0;
            int intervalCalls = 0;

            var runtime = new InteractionExecutionRuntime(new InteractionExecutionRuntimeDependencies(
                _ => true,
                _ => true,
                _ => { },
                (_, _, _, _, _, _) => { clickCalls++; return true; },
                (_, _, _, _, _, _, _) => { holdCalls++; return true; },
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

        [TestMethod]
        public void Execute_ReturnsFalse_WhenBlockedUiGateRejects()
        {
            int clickCalls = 0;
            int holdCalls = 0;
            int intervalCalls = 0;
            string? debugMessage = null;

            var runtime = new InteractionExecutionRuntime(new InteractionExecutionRuntimeDependencies(
                _ => true,
                _ => false,
                message => debugMessage = message,
                (_, _, _, _, _, _) => { clickCalls++; return true; },
                (_, _, _, _, _, _, _) => { holdCalls++; return true; },
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
            debugMessage.Should().Contain("blocked UI rectangle");
        }
    }
}