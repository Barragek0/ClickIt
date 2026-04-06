namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class OffscreenStickyTargetHandlerTests
    {
        [TestMethod]
        public void TryResolveStickyOffscreenTarget_ReturnsFalse_WhenNoStickyTargetAddressIsSet()
        {
            var runtimeState = new ClickRuntimeState();
            var settings = new ClickItSettings();
            var pathfindingLabelSuppression = new PathfindingLabelSuppressionEvaluator(new PathfindingLabelSuppressionEvaluatorDependencies(
                settings,
                runtimeState));
            var handler = new OffscreenStickyTargetHandler(new OffscreenStickyTargetHandlerDependencies(
                GameController: null!,
                ShrineService: null!,
                RuntimeState: runtimeState,
                LabelInteraction: null!,
                ChestLootSettlement: null!,
                IsClickableInEitherSpace: static (_, _) => false,
                PathfindingLabelSuppression: pathfindingLabelSuppression,
                LabelInteractionPort: null!,
                HoldDebugTelemetryAfterSuccess: static _ => { }));

            bool resolved = handler.TryResolveStickyOffscreenTarget(out var target);

            resolved.Should().BeFalse();
            target.Should().BeNull();
            runtimeState.StickyOffscreenTargetAddress.Should().Be(0);
        }

        [TestMethod]
        public void TryResolveStickyOffscreenTarget_ClearsStickyAddress_WhenEntityCannotBeResolved()
        {
            var runtimeState = new ClickRuntimeState
            {
                StickyOffscreenTargetAddress = 42
            };
            var settings = new ClickItSettings();
            var pathfindingLabelSuppression = new PathfindingLabelSuppressionEvaluator(new PathfindingLabelSuppressionEvaluatorDependencies(
                settings,
                runtimeState));
            var handler = new OffscreenStickyTargetHandler(new OffscreenStickyTargetHandlerDependencies(
                GameController: null!,
                ShrineService: null!,
                RuntimeState: runtimeState,
                LabelInteraction: null!,
                ChestLootSettlement: null!,
                IsClickableInEitherSpace: static (_, _) => false,
                PathfindingLabelSuppression: pathfindingLabelSuppression,
                LabelInteractionPort: null!,
                HoldDebugTelemetryAfterSuccess: static _ => { }));

            bool resolved = handler.TryResolveStickyOffscreenTarget(out var target);

            resolved.Should().BeFalse();
            target.Should().BeNull();
            runtimeState.StickyOffscreenTargetAddress.Should().Be(0);
        }

        private static OffscreenStickyTargetHandler CreateHandler(ClickRuntimeState runtimeState, GameController gameController)
        {
            var settings = new ClickItSettings();
            var pathfindingLabelSuppression = new PathfindingLabelSuppressionEvaluator(new PathfindingLabelSuppressionEvaluatorDependencies(
                settings,
                runtimeState));

            return new OffscreenStickyTargetHandler(new OffscreenStickyTargetHandlerDependencies(
                GameController: gameController,
                ShrineService: null!,
                RuntimeState: runtimeState,
                LabelInteraction: null!,
                ChestLootSettlement: null!,
                IsClickableInEitherSpace: static (_, _) => false,
                PathfindingLabelSuppression: pathfindingLabelSuppression,
                LabelInteractionPort: null!,
                HoldDebugTelemetryAfterSuccess: static _ => { }));
        }
    }
}