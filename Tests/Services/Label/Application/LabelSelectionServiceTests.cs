using ClickIt.Services;
using ClickIt.Services.Label.Application;
using ClickIt.Services.Label.Diagnostics;
using ClickIt.Services.Label.Selection;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Label.Application
{
    [TestClass]
    public class LabelSelectionServiceTests
    {
        [TestMethod]
        public void GetNextLabelToClick_PublishesNoLabelsDebugEvent_WhenInputIsNull()
        {
            LabelDebugEvent? publishedEvent = null;
            var service = new LabelSelectionService(new LabelSelectionServiceDependencies(
                GameController: null,
                CreateClickSettings: static _ => new ClickSettings(),
                ShouldCaptureLabelDebug: static () => true,
                PublishLabelDebugStage: debugEvent => publishedEvent = debugEvent,
                TryBuildLabelCandidate: static (ExileCore.PoEMemory.Elements.LabelOnGround _, ClickSettings _, out ExileCore.PoEMemory.MemoryObjects.Entity? item, out string? mechanicId, out LabelCandidateRejectReason rejectReason) =>
                {
                    item = null;
                    mechanicId = null;
                    rejectReason = LabelCandidateRejectReason.None;
                    return false;
                },
                GetMechanicIdForLabelCore: static _ => null));

            var selected = service.GetNextLabelToClick(null, 0, 10);

            selected.Should().BeNull();
            publishedEvent.Should().NotBeNull();
            publishedEvent!.Stage.Should().Be("NoLabels");
            publishedEvent.TotalLabels.Should().Be(0);
        }

        [TestMethod]
        public void GetMechanicIdForLabel_DelegatesToConfiguredResolver()
        {
            var service = new LabelSelectionService(new LabelSelectionServiceDependencies(
                GameController: null,
                CreateClickSettings: static _ => new ClickSettings(),
                ShouldCaptureLabelDebug: static () => false,
                PublishLabelDebugStage: static _ => { },
                TryBuildLabelCandidate: static (ExileCore.PoEMemory.Elements.LabelOnGround _, ClickSettings _, out ExileCore.PoEMemory.MemoryObjects.Entity? item, out string? mechanicId, out LabelCandidateRejectReason rejectReason) =>
                {
                    item = null;
                    mechanicId = null;
                    rejectReason = LabelCandidateRejectReason.None;
                    return false;
                },
                GetMechanicIdForLabelCore: static _ => "some-mechanic"));

            service.GetMechanicIdForLabel(null).Should().Be("some-mechanic");
        }
    }
}