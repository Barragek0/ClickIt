namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class ClickLabelInteractionServiceTests
    {
        [TestMethod]
        public void PerformManualCursorInteraction_AllowsHotkeyInactiveAndAvoidsCursorMove()
        {
            InteractionExecutionRequest? capturedRequest = null;
            var service = CreateService(request =>
            {
                capturedRequest = request;
                return true;
            });

            bool executed = service.PerformManualCursorInteraction(new Vector2(12, 34), useHoldClick: true);

            executed.Should().BeTrue();
            capturedRequest.Should().NotBeNull();
            capturedRequest!.Value.UseHoldClick.Should().BeTrue();
            capturedRequest.Value.AllowWhenHotkeyInactive.Should().BeTrue();
            capturedRequest.Value.AvoidCursorMove.Should().BeTrue();
            capturedRequest.Value.ExpectedElement.Should().BeNull();
        }

        [TestMethod]
        public void PerformMechanicInteraction_UsesStandardMechanicInteractionFlags()
        {
            InteractionExecutionRequest? capturedRequest = null;
            var service = CreateService(request =>
            {
                capturedRequest = request;
                return true;
            });

            bool executed = service.PerformMechanicInteraction(new Vector2(55, 66), useHoldClick: false);

            executed.Should().BeTrue();
            capturedRequest.Should().NotBeNull();
            capturedRequest!.Value.UseHoldClick.Should().BeFalse();
            capturedRequest.Value.ForceUiHoverVerification.Should().BeFalse();
            capturedRequest.Value.AllowWhenHotkeyInactive.Should().BeFalse();
            capturedRequest.Value.AvoidCursorMove.Should().BeFalse();
            capturedRequest.Value.ExpectedElement.Should().BeNull();
        }

        private static ClickLabelInteractionService CreateService(Func<InteractionExecutionRequest, bool> executeInteraction)
        {
            return new ClickLabelInteractionService(new ClickLabelInteractionServiceDependencies(
                Settings: (ClickItSettings)RuntimeHelpers.GetUninitializedObject(typeof(ClickItSettings)),
                GameController: (GameController)RuntimeHelpers.GetUninitializedObject(typeof(GameController)),
                InputHandler: (InputHandler)RuntimeHelpers.GetUninitializedObject(typeof(InputHandler)),
                LabelInteractionPort: (ILabelInteractionPort)RuntimeHelpers.GetUninitializedObject(typeof(LabelFilterPort)),
                IsClickableInEitherSpace: static (_, _) => true,
                IsInsideWindowInEitherSpace: static _ => true,
                ExecuteInteraction: executeInteraction,
                GroundItemsVisible: static () => true,
                DebugLog: static _ => { }));
        }
    }
}