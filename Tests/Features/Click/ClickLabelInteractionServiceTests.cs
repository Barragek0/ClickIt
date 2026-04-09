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
        public void PerformMechanicClick_UsesDefaultClickFlags()
        {
            InteractionExecutionRequest? capturedRequest = null;
            var service = CreateService(request =>
            {
                capturedRequest = request;
                return true;
            });

            bool executed = service.PerformMechanicClick(new Vector2(11, 22));

            executed.Should().BeTrue();
            capturedRequest.Should().NotBeNull();
            capturedRequest!.Value.UseHoldClick.Should().BeFalse();
            capturedRequest.Value.AllowWhenHotkeyInactive.Should().BeFalse();
            capturedRequest.Value.AvoidCursorMove.Should().BeFalse();
            capturedRequest.Value.OutsideWindowLogMessage.Should().Be("[PerformLabelClick] Skipping label click - cursor outside PoE window");
        }

        [TestMethod]
        public void PerformLabelHoldClick_UsesHoldDefaults()
        {
            InteractionExecutionRequest? capturedRequest = null;
            var service = CreateService(request =>
            {
                capturedRequest = request;
                return true;
            });

            bool executed = service.PerformLabelHoldClick(new Vector2(44, 55), expectedElement: null, controller: null, holdDurationMs: 120);

            executed.Should().BeTrue();
            capturedRequest.Should().NotBeNull();
            capturedRequest!.Value.UseHoldClick.Should().BeTrue();
            capturedRequest.Value.HoldDurationMs.Should().Be(120);
            capturedRequest.Value.OutsideWindowLogMessage.Should().Be("[PerformLabelHoldClick] Skipping hold click - cursor outside PoE window");
        }

        [TestMethod]
        public void ExecuteInteraction_UsesCustomOutsideWindowMessage_WhenProvided()
        {
            InteractionExecutionRequest? capturedRequest = null;
            var service = CreateService(request =>
            {
                capturedRequest = request;
                return true;
            });

            bool executed = service.ExecuteInteraction(
                new Vector2(77, 88),
                expectedElement: null,
                controller: null,
                useHoldClick: false,
                outsideWindowLogMessage: "custom-outside-window");

            executed.Should().BeTrue();
            capturedRequest.Should().NotBeNull();
            capturedRequest!.Value.OutsideWindowLogMessage.Should().Be("custom-outside-window");
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

        [TestMethod]
        public void TryResolveLabelClickPosition_DoesNotRetry_WhenSettlersFallbackHasNoItemProjection()
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
                CreateOpaqueLabel(),
                mechanicId: MechanicIds.SettlersCrimsonIron,
                windowTopLeft: Vector2.Zero,
                allLabels: null,
                out Vector2 clickPos,
                explicitPath: "Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/CrimsonIron");

            resolved.Should().BeFalse();
            clickPos.Should().Be(new Vector2(0, 0));
            callCount.Should().Be(1);
        }

        [TestMethod]
        public void TryResolveLabelClickPositionResult_ReturnsResolvedTuple_WhenFirstAttemptSucceeds()
        {
            var expectedClickPos = new Vector2(12, 34);
            var service = CreateService(
                _ => true,
                (_, _, _, _) => (true, expectedClickPos));

            (bool success, Vector2 clickPos) = service.TryResolveLabelClickPositionResult(
                CreateOpaqueLabel(),
                mechanicId: MechanicIds.SettlersCrimsonIron,
                windowTopLeft: Vector2.Zero,
                allLabels: null,
                explicitPath: "Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/CrimsonIron");

            success.Should().BeTrue();
            clickPos.Should().Be(expectedClickPos);
        }

        [TestMethod]
        public void TryGetCursorDistanceSquaredToEntity_ReturnsNull_WhenEntityIsNull()
        {
            var service = CreateService(static _ => true);

            float? distance = service.TryGetCursorDistanceSquaredToEntity(null, new Vector2(10, 20), new Vector2(100, 200));

            distance.Should().BeNull();
        }

        [TestMethod]
        public void BuildNoLabelDebugSummary_ReturnsDefaultSelectionRange_WhenLabelsAreMissing()
        {
            var service = CreateService(static _ => true, groundItemsVisible: static () => false);

            string summary = service.BuildNoLabelDebugSummary(allLabels: null);

            summary.Should().Be("visible:0 cached:0 groundVisible:False | selection:r:0-0 t:0");
        }

        [TestMethod]
        public void BuildNoLabelDebugSummary_UsesSelectionSummary_WhenLabelsExist()
        {
            SelectionDebugSummary selectionSummary = new(0, 2, 2, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0);
            var service = CreateService(
                static _ => true,
                labelInteractionPort: new SummaryLabelInteractionPort(selectionSummary),
                groundItemsVisible: static () => true);

            string summary = service.BuildNoLabelDebugSummary([CreateOpaqueLabel(), CreateOpaqueLabel()]);

            summary.Should().Be("visible:0 cached:2 groundVisible:True | selection:r:0-2 t:2 nl:0 ne:1 d:0 u:0 nm:1 wi:0/0 sp:0 sm:0 sd:0");
        }

        [TestMethod]
        public void BuildLabelRangeRejectionDebugSummary_FormatsRangeAndExaminedCount()
        {
            SelectionDebugSummary selectionSummary = new(1, 3, 4, 0, 0, 1, 0, 2, 0, 0, 0, 0, 0);
            var service = CreateService(
                static _ => true,
                labelInteractionPort: new SummaryLabelInteractionPort(selectionSummary));

            string summary = service.BuildLabelRangeRejectionDebugSummary(
                [CreateOpaqueLabel(), CreateOpaqueLabel(), CreateOpaqueLabel(), CreateOpaqueLabel()],
                start: 1,
                endExclusive: 3,
                examined: 2);

            summary.Should().Be("range:1-3 examined:2 | r:1-3 t:4 nl:0 ne:0 d:1 u:0 nm:2 wi:0/0 sp:0 sm:0 sd:0");
        }

        private static ClickLabelInteractionService CreateService(
            Func<InteractionExecutionRequest, bool> executeInteraction,
            Func<LabelOnGround, Vector2, IReadOnlyList<LabelOnGround>?, Func<Vector2, bool>?, (bool Success, Vector2 ClickPos)>? tryResolveClickPosition = null,
            ILabelInteractionPort? labelInteractionPort = null,
            Func<bool>? groundItemsVisible = null)
        {
            return new ClickLabelInteractionService(new ClickLabelInteractionServiceDependencies(
                Settings: (ClickItSettings)RuntimeHelpers.GetUninitializedObject(typeof(ClickItSettings)),
                GameController: (GameController)RuntimeHelpers.GetUninitializedObject(typeof(GameController)),
                LabelInteractionPort: labelInteractionPort ?? (ILabelInteractionPort)RuntimeHelpers.GetUninitializedObject(typeof(LabelFilterPort)),
                TryResolveClickPosition: tryResolveClickPosition ?? (static (_, _, _, _) => (false, default)),
                IsClickableInEitherSpace: static (_, _) => true,
                IsInsideWindowInEitherSpace: static _ => true,
                ExecuteInteraction: executeInteraction,
                GroundItemsVisible: groundItemsVisible ?? (static () => true),
                DebugLog: static _ => { }));
        }

        private static LabelOnGround CreateOpaqueLabel()
            => (LabelOnGround)RuntimeHelpers.GetUninitializedObject(typeof(LabelOnGround));

        private sealed class SummaryLabelInteractionPort(SelectionDebugSummary summary) : ILabelInteractionPort
        {
            public SelectionDebugSummary GetSelectionDebugSummary(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
                => summary;

            public void LogSelectionDiagnostics(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
            {
            }

            public string? GetMechanicIdForLabel(LabelOnGround? label)
                => null;

            public LabelOnGround? GetNextLabelToClick(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
                => null;

            public bool ShouldCorruptEssence(LabelOnGround label)
                => false;
        }
    }
}