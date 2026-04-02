using System;
using ClickIt.Services.Click.Selection;
using ClickIt.Services.Click.Runtime;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Services.Click
{
    [TestClass]
    public class OffscreenMechanicTargetSelectorTests
    {
        [TestMethod]
        public void ShouldAvoidOffscreenPathfindingBecauseOnscreenMechanicIsClickable_PublishesStage_WhenVisibleMechanicBlocks()
        {
            var settings = new ClickItSettings
            {
                PrioritizeOnscreenClickableMechanicsOverPathfinding = new ExileCore.Shared.Nodes.ToggleNode(true),
                ClickShrines = new ExileCore.Shared.Nodes.ToggleNode(true),
                ClickLostShipmentCrates = new ExileCore.Shared.Nodes.ToggleNode(true),
                ClickSettlersOre = new ExileCore.Shared.Nodes.ToggleNode(true),
                ClickEaterAltars = new ExileCore.Shared.Nodes.ToggleNode(false),
                ClickExarchAltars = new ExileCore.Shared.Nodes.ToggleNode(false)
            };

            string? stage = null;
            string? notes = null;

            var selector = new OffscreenMechanicTargetSelector(new OffscreenMechanicTargetSelectorDependencies(
                settings,
                GameController: null!,
                ShrineService: null!,
                HasClickableAltars: static () => false,
                HasClickableShrine: static () => false,
                GetVisibleMechanicAvailability: static () => (true, false),
                PublishClickFlowDebugStage: (s, n) =>
                {
                    stage = s;
                    notes = n;
                },
                IsClickableInEitherSpace: static (_, _) => false,
                IsInsideWindowInEitherSpace: static _ => false,
                ShouldSuppressPathfindingLabel: static _ => false,
                GetMechanicIdForLabel: static _ => null,
                TryResolveLabelClickPosition: static (_, _, _, _, _) => (false, default),
                GetLabelsForOffscreenSelection: static () => null,
                RefreshMechanicPriorityCaches: static () => { },
                BuildMechanicRank: static (_, _) => default));

            bool blocked = selector.ShouldAvoidOffscreenPathfindingBecauseOnscreenMechanicIsClickable();

            blocked.Should().BeTrue();
            stage.Should().Be("OffscreenPathingBlocked");
            notes.Should().Contain("lost=True").And.Contain("settlers=False");
        }

        [TestMethod]
        public void ShouldAvoidOffscreenPathfindingBecauseOnscreenMechanicIsClickable_ReturnsFalse_WhenPriorityDisabled()
        {
            var settings = new ClickItSettings
            {
                PrioritizeOnscreenClickableMechanicsOverPathfinding = new ExileCore.Shared.Nodes.ToggleNode(false),
                ClickShrines = new ExileCore.Shared.Nodes.ToggleNode(true),
                ClickLostShipmentCrates = new ExileCore.Shared.Nodes.ToggleNode(true),
                ClickSettlersOre = new ExileCore.Shared.Nodes.ToggleNode(true),
                ClickEaterAltars = new ExileCore.Shared.Nodes.ToggleNode(true),
                ClickExarchAltars = new ExileCore.Shared.Nodes.ToggleNode(true)
            };

            bool published = false;
            var selector = new OffscreenMechanicTargetSelector(new OffscreenMechanicTargetSelectorDependencies(
                settings,
                GameController: null!,
                ShrineService: null!,
                HasClickableAltars: static () => true,
                HasClickableShrine: static () => true,
                GetVisibleMechanicAvailability: static () => (true, true),
                PublishClickFlowDebugStage: (_, _) => published = true,
                IsClickableInEitherSpace: static (_, _) => false,
                IsInsideWindowInEitherSpace: static _ => false,
                ShouldSuppressPathfindingLabel: static _ => false,
                GetMechanicIdForLabel: static _ => null,
                TryResolveLabelClickPosition: static (_, _, _, _, _) => (false, default),
                GetLabelsForOffscreenSelection: static () => null,
                RefreshMechanicPriorityCaches: static () => { },
                BuildMechanicRank: static (_, _) => default));

            bool blocked = selector.ShouldAvoidOffscreenPathfindingBecauseOnscreenMechanicIsClickable();

            blocked.Should().BeFalse();
            published.Should().BeFalse();
        }
    }
}