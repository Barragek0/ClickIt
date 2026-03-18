using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClickIt.Rendering;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class DebugRendererClickingSettingsTests
    {
        [TestMethod]
        public void BuildClickSettingsDebugSnapshotLines_IncludesCoreClickConfiguration()
        {
            var settings = new ClickItSettings
            {
                ClickHotkeyToggleMode = { Value = false },
                ClickOnManualUiHoverOnly = { Value = false },
                LazyMode = { Value = false },
                LeftHanded = { Value = false },
                ClickDistance = { Value = 100 },
                ClickFrequencyTarget = { Value = 80 },
                VerifyCursorInGameWindowBeforeClick = { Value = true },
                VerifyUIHoverWhenNotLazy = { Value = false },
                AvoidOverlappingLabelClickPoints = { Value = true }
            };

            var lines = DebugRenderer.BuildClickSettingsDebugSnapshotLines(settings);

            lines.Should().NotBeNull();
            lines.Count.Should().BeGreaterOrEqualTo(4);

            string payload = string.Join(" | ", lines);
            payload.Should().Contain("hotkeyToggle:False");
            payload.Should().Contain("manualCursor:False");
            payload.Should().Contain("lazyMode:False");
            payload.Should().Contain("radius:100");
            payload.Should().Contain("freqTarget:80ms");
            payload.Should().Contain("verifyCursorInWindow:True");
            payload.Should().Contain("verifyUiHoverNonLazy:False");
            payload.Should().Contain("avoidOverlap:True");
        }

        [TestMethod]
        public void BuildClickSettingsDebugSnapshotLines_IncludesToggleAndPathingConfiguration()
        {
            var settings = new ClickItSettings
            {
                ToggleItems = { Value = true },
                ToggleItemsIntervalMs = { Value = 1900 },
                ToggleItemsPostToggleClickBlockMs = { Value = 35 },
                WalkTowardOffscreenLabels = { Value = false },
                PrioritizeOnscreenClickableMechanicsOverPathfinding = { Value = true },
                OffscreenPathfindingSearchBudget = { Value = 7000 }
            };

            var lines = DebugRenderer.BuildClickSettingsDebugSnapshotLines(settings);
            string payload = string.Join(" | ", lines);

            payload.Should().Contain("toggleItems:True");
            payload.Should().Contain("toggleItemsInterval:1900ms");
            payload.Should().Contain("postToggleBlock:35ms");
            payload.Should().Contain("walkOffscreen:False");
            payload.Should().Contain("prioritizeOnscreen:True");
            payload.Should().Contain("pathBudget:7000");
        }
    }
}