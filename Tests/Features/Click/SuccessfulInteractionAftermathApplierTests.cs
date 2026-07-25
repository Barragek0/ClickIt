namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class SuccessfulInteractionAftermathApplierTests
    {
        [TestMethod]
        public void Apply_InvokesRequestedCallbacks_InExpectedOrder()
        {
            LabelOnGround label = ExileCoreOpaqueFactory.CreateOpaqueLabel();
            List<string> events = [];

            SuccessfulInteractionAftermathApplier.Apply(
                new SuccessfulInteractionAftermath(
                    Reason: "sticky success",
                    ShouldClearStickyTarget: true,
                    ShouldClearPath: true,
                    ShouldInvalidateShrineCache: true,
                    PendingChestMechanicId: "Chest.Basic",
                    PendingChestLabel: label,
                    ShouldRecordLeverClick: true),
                holdDebugTelemetryAfterSuccess: reason => events.Add($"hold:{reason}"),
                clearStickyTarget: () => events.Add("clear-sticky"),
                clearPath: () => events.Add("clear-path"),
                invalidateShrineCache: () => events.Add("invalidate-shrine"),
                markPendingChestOpenConfirmation: (mechanicId, pendingLabel) => events.Add($"mark:{mechanicId}:{ReferenceEquals(pendingLabel, label)}"),
                recordLeverClick: pendingLabel => events.Add($"lever:{ReferenceEquals(pendingLabel, label)}"));

            events.Should().Equal(
                "hold:sticky success",
                "clear-sticky",
                "clear-path",
                "invalidate-shrine",
                "mark:Chest.Basic:True",
                "lever:True");
        }

        [TestMethod]
        public void Apply_DoesNotMarkPendingChest_WhenMechanicIdIsBlank()
        {
            LabelOnGround label = ExileCoreOpaqueFactory.CreateOpaqueLabel();
            int pendingCalls = 0;
            int leverCalls = 0;

            SuccessfulInteractionAftermathApplier.Apply(
                new SuccessfulInteractionAftermath(
                    Reason: "blank mechanic",
                    PendingChestMechanicId: "   ",
                    PendingChestLabel: label,
                    ShouldRecordLeverClick: true),
                holdDebugTelemetryAfterSuccess: static _ => { },
                markPendingChestOpenConfirmation: (_, _) => pendingCalls++,
                recordLeverClick: _ => leverCalls++);

            pendingCalls.Should().Be(0);
            leverCalls.Should().Be(1);
        }

        [TestMethod]
        public void Apply_DoesNotInvokeLabelCallbacks_WhenPendingLabelIsMissing()
        {
            int pendingCalls = 0;
            int leverCalls = 0;

            SuccessfulInteractionAftermathApplier.Apply(
                new SuccessfulInteractionAftermath(
                    Reason: "no label",
                    PendingChestMechanicId: "Chest.Basic",
                    PendingChestLabel: null,
                    ShouldRecordLeverClick: true),
                holdDebugTelemetryAfterSuccess: static _ => { },
                markPendingChestOpenConfirmation: (_, _) => pendingCalls++,
                recordLeverClick: _ => leverCalls++);

            pendingCalls.Should().Be(0);
            leverCalls.Should().Be(0);
        }
    }
}