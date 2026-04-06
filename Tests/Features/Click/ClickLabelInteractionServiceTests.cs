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

        [TestMethod]
        public void TryResolveLabelClickPosition_DoesNotRetry_WhenMechanicIsNotSettlers()
        {
            int callCount = 0;
            var service = CreateService(
                _ => true,
                (_, _, _, _) =>
                {
                    callCount++;
                    return (false, default);
                });

            bool resolved = service.TryResolveLabelClickPosition(
                (LabelOnGround)RuntimeHelpers.GetUninitializedObject(typeof(LabelOnGround)),
                mechanicId: "items",
                windowTopLeft: Vector2.Zero,
                allLabels: null,
                out Vector2 clickPos,
                explicitPath: "metadata/test");

            resolved.Should().BeFalse();
            clickPos.Should().Be(new Vector2(0, 0));
            callCount.Should().Be(1);
        }

        private static ClickLabelInteractionService CreateService(
            Func<InteractionExecutionRequest, bool> executeInteraction,
            Func<LabelOnGround, Vector2, IReadOnlyList<LabelOnGround>?, Func<Vector2, bool>?, (bool Success, Vector2 ClickPos)>? tryResolveClickPosition = null)
        {
            return new ClickLabelInteractionService(new ClickLabelInteractionServiceDependencies(
                Settings: (ClickItSettings)RuntimeHelpers.GetUninitializedObject(typeof(ClickItSettings)),
                GameController: (GameController)RuntimeHelpers.GetUninitializedObject(typeof(GameController)),
                LabelInteractionPort: (ILabelInteractionPort)RuntimeHelpers.GetUninitializedObject(typeof(LabelFilterPort)),
                TryResolveClickPosition: tryResolveClickPosition ?? (static (_, _, _, _) => (false, default)),
                IsClickableInEitherSpace: static (_, _) => true,
                IsInsideWindowInEitherSpace: static _ => true,
                ExecuteInteraction: executeInteraction,
                GroundItemsVisible: static () => true,
                DebugLog: static _ => { }));
        }
    }
}