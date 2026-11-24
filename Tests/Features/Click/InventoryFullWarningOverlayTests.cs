namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class InventoryFullWarningOverlayTests
    {
        [TestMethod]
        public void ShouldShowInventoryPickupBlockedWarning_ReturnsTrue_WhenFullDecisionDisallowsPickup()
        {
            var snapshot = CreateInventorySnapshot(stage: "InventoryFullDecision", inventoryFull: true, allowPickup: false);

            bool blocked = InventoryFullWarningOverlay.ShouldShowInventoryPickupBlockedWarning(snapshot);
            blocked.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldShowInventoryPickupBlockedWarning_ReturnsTrue_WhenNotFullNoFitDisallowsPickup()
        {
            var snapshot = CreateInventorySnapshot(stage: "InventoryNotFullNoFit", inventoryFull: false, allowPickup: false);

            bool blocked = InventoryFullWarningOverlay.ShouldShowInventoryPickupBlockedWarning(snapshot);
            blocked.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldShowInventoryPickupBlockedWarning_ReturnsFalse_WhenNotFullNoFitHasMissingIdentity()
        {
            var snapshot = CreateInventorySnapshot(stage: "InventoryNotFullNoFit", inventoryFull: false, allowPickup: false) with
            {
                GroundItemPath = string.Empty,
                GroundItemName = string.Empty
            };

            bool blocked = InventoryFullWarningOverlay.ShouldShowInventoryPickupBlockedWarning(snapshot);
            blocked.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldShowInventoryPickupBlockedWarning_ReturnsFalse_WhenNotFullNoFitLayoutIsUnreliable()
        {
            var snapshot = CreateInventorySnapshot(stage: "InventoryNotFullNoFit", inventoryFull: false, allowPickup: false) with
            {
                Notes = "Inventory layout unreliable from PlayerInventories[0].InventorySlotItems (raw:1 parsed:0)"
            };

            bool blocked = InventoryFullWarningOverlay.ShouldShowInventoryPickupBlockedWarning(snapshot);
            blocked.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldShowInventoryPickupBlockedWarning_ReturnsFalse_WhenNotFullNoFitHasLargeFreeCellCount()
        {
            var snapshot = CreateInventorySnapshot(stage: "InventoryNotFullNoFit", inventoryFull: false, allowPickup: false) with
            {
                CapacityCells = 60,
                OccupiedCells = 2,
                UsedCellOccupancy = true,
                Notes = "Inventory fullness from PlayerInventories[0].InventorySlotItems footprint (raw:2 parsed:2)",
                GroundItemPath = "Metadata/Items/Currency/CurrencyRerollRare",
                GroundItemName = string.Empty,
                IsGroundStackable = false
            };

            bool blocked = InventoryFullWarningOverlay.ShouldShowInventoryPickupBlockedWarning(snapshot);
            blocked.Should().BeFalse();
        }

        [TestMethod]
        public void ResolveInventoryFullWarningPosition_PrefersPlayerFeet_WhenAvailable()
        {
            Vector2 result = InventoryFullWarningOverlay.ResolveInventoryFullWarningPosition(
                new RectangleF(0, 0, 1920, 1080),
                new RectangleF(10, 20, 110, 120),
                new RectangleF(1810, 20, 1910, 120),
                new Vector2(640f, 480f));

            result.Should().Be(new Vector2(640f, 480f));
        }

        [TestMethod]
        public void ResolveInventoryFullWarningPosition_UsesWindowFallback_WhenTertiariesAreMissing()
        {
            Vector2 result = InventoryFullWarningOverlay.ResolveInventoryFullWarningPosition(
                new RectangleF(100, 50, 1200, 700),
                RectangleF.Empty,
                RectangleF.Empty,
                playerFeetScreen: null);

            result.Should().Be(new Vector2(700f, 652f));
        }

        [TestMethod]
        public void ResolveInventoryFullWarningPosition_UsesWindowFallback_WhenOnlyOneTertiaryIsValid()
        {
            Vector2 result = InventoryFullWarningOverlay.ResolveInventoryFullWarningPosition(
                new RectangleF(0, 0, 1600, 900),
                new RectangleF(10, 20, 150, 220),
                RectangleF.Empty,
                playerFeetScreen: null);

            result.Should().Be(new Vector2(800f, 774f));
        }

        [TestMethod]
        public void ResolveInventoryFullWarningPosition_UsesMidpointBetweenValidTertiaries()
        {
            Vector2 result = InventoryFullWarningOverlay.ResolveInventoryFullWarningPosition(
                new RectangleF(0, 0, 1600, 900),
                new RectangleF(40, 100, 240, 300),
                new RectangleF(1160, 120, 1360, 360),
                playerFeetScreen: null);

            result.Should().Be(new Vector2(700f, 230f));
        }

        [TestMethod]
        public void ShouldShowInventoryFullWarning_ReturnsExpectedState_ForInitialExpiredAndFutureTimestamps()
        {
            InventoryFullWarningOverlay.ShouldShowInventoryFullWarning(now: 1000, lastTriggeredTimestampMs: 0)
                .Should().BeFalse();

            InventoryFullWarningOverlay.ShouldShowInventoryFullWarning(now: 15_500, lastTriggeredTimestampMs: 5_000)
                .Should().BeFalse();

            InventoryFullWarningOverlay.ShouldShowInventoryFullWarning(now: 4_000, lastTriggeredTimestampMs: 5_000)
                .Should().BeFalse();

            InventoryFullWarningOverlay.ShouldShowInventoryFullWarning(now: 14_999, lastTriggeredTimestampMs: 5_000)
                .Should().BeTrue();
        }

        [TestMethod]
        public void ShouldRefreshInventoryFullWarningTimestamp_OnlyWhenSequenceChanges()
        {
            var snapshot = CreateInventorySnapshot(stage: "InventoryFullDecision", inventoryFull: true, allowPickup: false, sequence: 7);

            InventoryFullWarningOverlay.ShouldRefreshInventoryFullWarningTimestamp(6, 7, snapshot).Should().BeTrue();
            InventoryFullWarningOverlay.ShouldRefreshInventoryFullWarningTimestamp(7, 7, snapshot).Should().BeFalse();
            InventoryFullWarningOverlay.ShouldRefreshInventoryFullWarningTimestamp(7, 7, InventoryDebugSnapshot.Empty).Should().BeFalse();
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void Draw_WritesTheEssenceCurrencyWarning_WhenItsProviderReportsOne()
        {
            const string warning = "No Vaal Orbs to corrupt essences";
            DeferredDrawQueue queue = new();
            var overlay = new InventoryFullWarningOverlay(null, static () => InventoryDebugSnapshot.Empty)
            {
                EssenceCurrencyWarning = () => warning
            };

            overlay.Draw(CreateRenderContext(queue));

            queue.GetPendingTextSnapshot().Should().Contain(warning);
        }

        [TestMethod]
        [TestCategory("Boundary")]
        public void Draw_WritesNothing_WhenNoProviderReportsAWarning()
        {
            DeferredDrawQueue queue = new();
            var overlay = new InventoryFullWarningOverlay(null, static () => InventoryDebugSnapshot.Empty);

            overlay.Draw(CreateRenderContext(queue));

            queue.GetPendingTextSnapshot().Should().BeEmpty();
        }

        [DataTestMethod]
        [TestCategory("Regression")]
        [DataRow("0/12", 0)]
        [DataRow(" 3 /12", 3)]
        [DataRow("12/12", 12)]
        [DataRow("-1/12", null)]
        [DataRow("unavailable", null)]
        [DataRow("/12", null)]
        public void ReadDelveSupplyCount_ReadsTheSpecifiedSkillBarBranch(string text, int? expected)
        {
            var flare = new SupplyElement();
            flare.Children[1] = new SupplyElement { Text = text };
            var dynamite = new SupplyElement();
            dynamite.Children[1] = new SupplyElement { Text = "7/12" };
            var row = new SupplyElement();
            row.Children[2] = flare;
            row.Children[3] = dynamite;
            var container = new SupplyElement();
            container.Children[0] = row;
            var skillBar = new SupplyElement();
            skillBar.Children[13] = container;

            InventoryFullWarningOverlay.ReadDelveSupplyCount(skillBar, 2).Should().Be(expected);
            InventoryFullWarningOverlay.ReadDelveSupplyCount(skillBar, 3).Should().Be(7);
            InventoryFullWarningOverlay.ReadDelveSupplyCount(null, 2).Should().BeNull();
        }

        public sealed class SupplyElement
        {
            public Dictionary<int, SupplyElement> Children { get; } = new();
            public string Text { get; set; } = string.Empty;
            public SupplyElement? GetChildAtIndex(int index) => Children.GetValueOrDefault(index);
        }

        [DataTestMethod]
        [TestCategory("Boundary")]
        [DataRow(null, 3, false)]
        [DataRow(0, 3, true)]
        [DataRow(2, 3, true)]
        [DataRow(3, 3, false)]
        [DataRow(4, 3, false)]
        [DataRow(10, 99, false)]
        [DataRow(9, 99, true)]
        public void IsLowDelveSupply_RequiresReadableCountBelowThreshold(int? count, int threshold, bool expected)
            => InventoryFullWarningOverlay.IsLowDelveSupply(count, threshold).Should().Be(expected);

        [TestMethod]
        [TestCategory("Settings")]
        public void DelveSupplyWarnings_DefaultToEnabledBelowThree()
        {
            var settings = new ClickItSettings();
            settings.ShowLowDelveFlaresWarning.Value.Should().BeTrue();
            settings.LowDelveFlaresThreshold.Value.Should().Be(3);
            settings.ShowLowDelveDynamiteWarning.Value.Should().BeTrue();
            settings.LowDelveDynamiteThreshold.Value.Should().Be(3);
        }

        private static OverlayRenderContext CreateRenderContext(DeferredDrawQueue queue)
            => new(
                Settings: new ClickItSettings(),
                GameController: null,
                Graphics: null,
                WindowArea: new RectangleF(0, 0, 1920, 1080),
                Labels: null,
                DrawQueue: queue);

        private static InventoryDebugSnapshot CreateInventorySnapshot(string stage, bool inventoryFull, bool allowPickup, long sequence = 0)
        {
            return new InventoryDebugSnapshot(
                HasData: true,
                Stage: stage,
                InventoryFull: inventoryFull,
                InventoryFullSource: "CellOccupancy",
                HasPrimaryInventory: true,
                UsedFullFlag: false,
                FullFlagValue: false,
                UsedCellOccupancy: true,
                CapacityCells: 60,
                OccupiedCells: 60,
                InventoryEntityCount: 24,
                LayoutEntryCount: 24,
                GroundItemPath: "Metadata/Items/Whatever",
                GroundItemName: "Item",
                IsGroundStackable: false,
                MatchingPathCount: 0,
                PartialMatchingStackCount: 0,
                HasPartialMatchingStack: false,
                DecisionAllowPickup: allowPickup,
                Notes: string.Empty,
                Sequence: sequence,
                TimestampMs: 0);
        }
    }
}
