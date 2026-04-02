using ClickIt.Services;
using ClickIt.Services.Label.Inventory;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class InventoryDomainFacadeTests
    {
        [TestMethod]
        public void Constructor_ExposesProvidedServices()
        {
            var dynamicAdapter = (InventoryDynamicAdapter)RuntimeHelpers.GetUninitializedObject(typeof(InventoryDynamicAdapter));
            var probeService = (InventoryProbeService)RuntimeHelpers.GetUninitializedObject(typeof(InventoryProbeService));
            var snapshotProvider = (InventoryReadModelService)RuntimeHelpers.GetUninitializedObject(typeof(InventoryReadModelService));
            var layoutParser = (InventoryLayoutParser)RuntimeHelpers.GetUninitializedObject(typeof(InventoryLayoutParser));
            var pickupPolicy = (InventoryPickupPolicyEngine)RuntimeHelpers.GetUninitializedObject(typeof(InventoryPickupPolicyEngine));

            var facade = new InventoryDomainFacade(dynamicAdapter, probeService, snapshotProvider, layoutParser, pickupPolicy);

            facade.DynamicAdapter.Should().BeSameAs(dynamicAdapter);
            facade.ProbeService.Should().BeSameAs(probeService);
            facade.SnapshotProvider.Should().BeSameAs(snapshotProvider);
            facade.LayoutParser.Should().BeSameAs(layoutParser);
            facade.PickupPolicy.Should().BeSameAs(pickupPolicy);
        }

        [TestMethod]
        public void PublishDebug_UpdatesProbeLatestSnapshot()
        {
            var dynamicAdapter = (InventoryDynamicAdapter)RuntimeHelpers.GetUninitializedObject(typeof(InventoryDynamicAdapter));
            InventoryProbeService probeService = CreateProbeService();
            var snapshotProvider = (InventoryReadModelService)RuntimeHelpers.GetUninitializedObject(typeof(InventoryReadModelService));
            var layoutParser = (InventoryLayoutParser)RuntimeHelpers.GetUninitializedObject(typeof(InventoryLayoutParser));
            var pickupPolicy = (InventoryPickupPolicyEngine)RuntimeHelpers.GetUninitializedObject(typeof(InventoryPickupPolicyEngine));

            var facade = new InventoryDomainFacade(dynamicAdapter, probeService, snapshotProvider, layoutParser, pickupPolicy);
            InventoryDebugSnapshot snapshot = CreateDebugSnapshot("InventoryFullDecision", sequence: 42);

            facade.PublishDebug(snapshot);

            InventoryDebugSnapshot latest = facade.GetLatestDebug();
            latest.Stage.Should().Be(snapshot.Stage);
            latest.GroundItemPath.Should().Be(snapshot.GroundItemPath);
            latest.DecisionAllowPickup.Should().Be(snapshot.DecisionAllowPickup);
            latest.Sequence.Should().BeGreaterThan(0);
        }

        [TestMethod]
        public void GetLatestDebugTrail_ReturnsProbeTrailEntries()
        {
            var dynamicAdapter = (InventoryDynamicAdapter)RuntimeHelpers.GetUninitializedObject(typeof(InventoryDynamicAdapter));
            InventoryProbeService probeService = CreateProbeService();
            var snapshotProvider = (InventoryReadModelService)RuntimeHelpers.GetUninitializedObject(typeof(InventoryReadModelService));
            var layoutParser = (InventoryLayoutParser)RuntimeHelpers.GetUninitializedObject(typeof(InventoryLayoutParser));
            var pickupPolicy = (InventoryPickupPolicyEngine)RuntimeHelpers.GetUninitializedObject(typeof(InventoryPickupPolicyEngine));

            var facade = new InventoryDomainFacade(dynamicAdapter, probeService, snapshotProvider, layoutParser, pickupPolicy);
            facade.PublishDebug(CreateDebugSnapshot("First", sequence: 1));
            facade.PublishDebug(CreateDebugSnapshot("Second", sequence: 2));

            IReadOnlyList<string> trail = facade.GetLatestDebugTrail();

            trail.Should().NotBeEmpty();
            trail[^1].Should().Contain("Second");
        }

        private static InventoryProbeService CreateProbeService()
        {
            return InventoryProbeService.CreateDiagnosticsOnlyForTests(debugTrailCapacity: 8);
        }

        private static InventoryDebugSnapshot CreateDebugSnapshot(string stage, long sequence)
        {
            return new InventoryDebugSnapshot(
                HasData: true,
                Stage: stage,
                InventoryFull: false,
                InventoryFullSource: "CellOccupancy",
                HasPrimaryInventory: true,
                UsedFullFlag: false,
                FullFlagValue: false,
                UsedCellOccupancy: true,
                CapacityCells: 60,
                OccupiedCells: 12,
                InventoryEntityCount: 10,
                LayoutEntryCount: 10,
                GroundItemPath: "Metadata/Items/Test",
                GroundItemName: "Test",
                IsGroundStackable: false,
                MatchingPathCount: 0,
                PartialMatchingStackCount: 0,
                HasPartialMatchingStack: false,
                DecisionAllowPickup: true,
                Notes: string.Empty,
                Sequence: sequence,
                TimestampMs: 0);
        }
    }
}

