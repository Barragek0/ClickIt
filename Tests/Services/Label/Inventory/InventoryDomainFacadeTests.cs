using ClickIt.Services;
using ClickIt.Services.Label.Inventory;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ClickIt.Tests.Services.Label.Inventory
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
            var stackMatchService = (InventoryStackMatchService)RuntimeHelpers.GetUninitializedObject(typeof(InventoryStackMatchService));

            var facade = new InventoryDomainFacade(dynamicAdapter, probeService, snapshotProvider, layoutParser, pickupPolicy, stackMatchService);

            facade.DynamicAdapter.Should().BeSameAs(dynamicAdapter);
            facade.ProbeService.Should().BeSameAs(probeService);
            facade.SnapshotProvider.Should().BeSameAs(snapshotProvider);
            facade.LayoutParser.Should().BeSameAs(layoutParser);
            facade.PickupPolicy.Should().BeSameAs(pickupPolicy);
            facade.StackMatchService.Should().BeSameAs(stackMatchService);
        }

        [TestMethod]
        public void PublishDebug_UpdatesProbeLatestSnapshot()
        {
            var dynamicAdapter = (InventoryDynamicAdapter)RuntimeHelpers.GetUninitializedObject(typeof(InventoryDynamicAdapter));
            InventoryProbeService probeService = CreateProbeService(InventorySnapshot.Empty);
            var snapshotProvider = (InventoryReadModelService)RuntimeHelpers.GetUninitializedObject(typeof(InventoryReadModelService));
            var layoutParser = (InventoryLayoutParser)RuntimeHelpers.GetUninitializedObject(typeof(InventoryLayoutParser));
            var pickupPolicy = (InventoryPickupPolicyEngine)RuntimeHelpers.GetUninitializedObject(typeof(InventoryPickupPolicyEngine));
            var stackMatchService = (InventoryStackMatchService)RuntimeHelpers.GetUninitializedObject(typeof(InventoryStackMatchService));

            var facade = new InventoryDomainFacade(dynamicAdapter, probeService, snapshotProvider, layoutParser, pickupPolicy, stackMatchService);
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
            InventoryProbeService probeService = CreateProbeService(InventorySnapshot.Empty);
            var snapshotProvider = (InventoryReadModelService)RuntimeHelpers.GetUninitializedObject(typeof(InventoryReadModelService));
            var layoutParser = (InventoryLayoutParser)RuntimeHelpers.GetUninitializedObject(typeof(InventoryLayoutParser));
            var pickupPolicy = (InventoryPickupPolicyEngine)RuntimeHelpers.GetUninitializedObject(typeof(InventoryPickupPolicyEngine));
            var stackMatchService = (InventoryStackMatchService)RuntimeHelpers.GetUninitializedObject(typeof(InventoryStackMatchService));

            var facade = new InventoryDomainFacade(dynamicAdapter, probeService, snapshotProvider, layoutParser, pickupPolicy, stackMatchService);
            facade.PublishDebug(CreateDebugSnapshot("First", sequence: 1));
            facade.PublishDebug(CreateDebugSnapshot("Second", sequence: 2));

            IReadOnlyList<string> trail = facade.GetLatestDebugTrail();

            trail.Should().NotBeEmpty();
            trail[^1].Should().Contain("Second");
        }

        [TestMethod]
        public void ShouldAllowWorldItemWhenInventoryFull_DelegatesThroughInventoryPolicy()
        {
            var dynamicAdapter = (InventoryDynamicAdapter)RuntimeHelpers.GetUninitializedObject(typeof(InventoryDynamicAdapter));
            InventoryProbeService probeService = CreateProbeService(InventorySnapshot.Empty);
            var snapshotProvider = (InventoryReadModelService)RuntimeHelpers.GetUninitializedObject(typeof(InventoryReadModelService));
            var layoutParser = (InventoryLayoutParser)RuntimeHelpers.GetUninitializedObject(typeof(InventoryLayoutParser));
            var stackMatchService = (InventoryStackMatchService)RuntimeHelpers.GetUninitializedObject(typeof(InventoryStackMatchService));
            var groundItem = (Entity)RuntimeHelpers.GetUninitializedObject(typeof(Entity));
            var controller = (GameController)RuntimeHelpers.GetUninitializedObject(typeof(GameController));

            var pickupPolicy = new InventoryPickupPolicyEngine(new InventoryPickupPolicyDependencies(
                _ => (true, InventoryFullProbe.Empty with { HasPrimaryInventory = true, IsFull = true }),
                _ => null,
                _ => string.Empty,
                _ => true,
                (_, _, _) => (true, 1, 1),
                (_, _) => false,
                InventoryCoreLogic.ShouldAllowPickupWhenPrimaryInventoryMissing,
                InventoryCoreLogic.ShouldAllowPickupWhenGroundItemEntityMissing,
                InventoryCoreLogic.ShouldAllowPickupWhenGroundItemIdentityMissing,
                InventoryCoreLogic.ShouldPickupWhenInventoryFull,
                (stage, probe, groundItemPath, groundItemName, isStackable, matchingPathCount, partialMatchingStackCount, hasPartialMatchingStack, allowPickup)
                    => new InventoryDebugSnapshot(
                        HasData: true,
                        Stage: stage,
                        InventoryFull: probe.IsFull,
                        InventoryFullSource: probe.Source,
                        HasPrimaryInventory: probe.HasPrimaryInventory,
                        UsedFullFlag: probe.UsedFullFlag,
                        FullFlagValue: probe.FullFlagValue,
                        UsedCellOccupancy: probe.UsedCellOccupancy,
                        CapacityCells: probe.CapacityCells,
                        OccupiedCells: probe.OccupiedCells,
                        InventoryEntityCount: probe.InventoryEntityCount,
                        LayoutEntryCount: probe.LayoutEntryCount,
                        GroundItemPath: groundItemPath,
                        GroundItemName: groundItemName,
                        IsGroundStackable: isStackable,
                        MatchingPathCount: matchingPathCount,
                        PartialMatchingStackCount: partialMatchingStackCount,
                        HasPartialMatchingStack: hasPartialMatchingStack,
                        DecisionAllowPickup: allowPickup,
                        Notes: probe.Notes,
                        Sequence: 0,
                        TimestampMs: 0),
                _ => { }));

            var facade = new InventoryDomainFacade(dynamicAdapter, probeService, snapshotProvider, layoutParser, pickupPolicy, stackMatchService);

            bool result = facade.ShouldAllowWorldItemWhenInventoryFull(groundItem, controller);

            result.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldAllowClosedDoorPastMechanic_AllowsWhenInventoryProbeNotesAreUnreliable()
        {
            var dynamicAdapter = (InventoryDynamicAdapter)RuntimeHelpers.GetUninitializedObject(typeof(InventoryDynamicAdapter));
            InventorySnapshot snapshot = InventorySnapshot.Empty with
            {
                HasPrimaryInventory = true,
                FullProbe = InventoryFullProbe.Empty with
                {
                    HasPrimaryInventory = true,
                    Notes = "Inventory layout unreliable from inventory slots (raw:5 parsed:0)"
                }
            };
            InventoryProbeService probeService = CreateProbeService(snapshot);
            var snapshotProvider = (InventoryReadModelService)RuntimeHelpers.GetUninitializedObject(typeof(InventoryReadModelService));
            var layoutParser = (InventoryLayoutParser)RuntimeHelpers.GetUninitializedObject(typeof(InventoryLayoutParser));
            var pickupPolicy = (InventoryPickupPolicyEngine)RuntimeHelpers.GetUninitializedObject(typeof(InventoryPickupPolicyEngine));
            var stackMatchService = (InventoryStackMatchService)RuntimeHelpers.GetUninitializedObject(typeof(InventoryStackMatchService));
            var controller = (GameController)RuntimeHelpers.GetUninitializedObject(typeof(GameController));

            var facade = new InventoryDomainFacade(dynamicAdapter, probeService, snapshotProvider, layoutParser, pickupPolicy, stackMatchService);

            bool result = facade.ShouldAllowClosedDoorPastMechanic(controller, "Incursion/IncursionKey");

            result.Should().BeTrue();
        }

        private static InventoryProbeService CreateProbeService(InventorySnapshot snapshot)
        {
            return new InventoryProbeService(new InventoryProbeServiceDependencies(
                CacheWindowMs: 50,
                DebugTrailCapacity: 8,
                TryBuildInventorySnapshot: _ => (true, snapshot),
                TryGetPrimaryServerInventory: _ => (false, null),
                TryGetPrimaryServerInventorySlotItems: _ => (false, null),
                EnumerateObjects: _ => System.Array.Empty<object?>(),
                TryGetInventoryItemEntityFromEntry: _ => null,
                ClassifyInventoryItemEntity: _ => (false, string.Empty)));
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

