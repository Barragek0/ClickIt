using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class VisibleMechanicTargetSelectorTests
    {
        [TestMethod]
        public void ResolveNextLostShipmentCandidate_ReturnsNull_WhenFeatureDisabled()
        {
            var selector = new VisibleMechanicTargetSelector(new VisibleMechanicTargetSelectorDependencies(
                Settings: new ClickItSettings
                {
                    ClickLostShipmentCrates = new ExileCore.Shared.Nodes.ToggleNode(false),
                    ClickSettlersOre = new ExileCore.Shared.Nodes.ToggleNode(true),
                    ClickDistance = new ExileCore.Shared.Nodes.RangeNode<int>(600, 0, 1000)
                },
                GameController: null!,
                ShouldCaptureClickDebug: static () => false,
                SetLatestClickDebug: static _ => { },
                DebugLog: static _ => { },
                IsInsideWindowInEitherSpace: static _ => false,
                IsClickableInEitherSpace: static (_, _) => false,
                IsSettlersMechanicEnabled: static _ => true,
                CollectGroundLabelEntityAddresses: static () => new HashSet<long>()));

            selector.ResolveNextLostShipmentCandidate().Should().BeNull();
        }

        [TestMethod]
        public void ResolveNextSettlersOreCandidate_ReturnsNull_WithoutCollectingLabels_WhenFeatureDisabled()
        {
            bool collectedAddresses = false;
            var selector = new VisibleMechanicTargetSelector(new VisibleMechanicTargetSelectorDependencies(
                Settings: new ClickItSettings
                {
                    ClickLostShipmentCrates = new ExileCore.Shared.Nodes.ToggleNode(true),
                    ClickSettlersOre = new ExileCore.Shared.Nodes.ToggleNode(false),
                    ClickDistance = new ExileCore.Shared.Nodes.RangeNode<int>(600, 0, 1000)
                },
                GameController: null!,
                ShouldCaptureClickDebug: static () => false,
                SetLatestClickDebug: static _ => { },
                DebugLog: static _ => { },
                IsInsideWindowInEitherSpace: static _ => false,
                IsClickableInEitherSpace: static (_, _) => false,
                IsSettlersMechanicEnabled: static _ => true,
                CollectGroundLabelEntityAddresses: () =>
                {
                    collectedAddresses = true;
                    return new HashSet<long>();
                }));

            selector.ResolveNextSettlersOreCandidate().Should().BeNull();
            collectedAddresses.Should().BeFalse();
        }
    }
}