using ClickIt.Features.Labels.Inventory;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ClickIt.Tests.Labels.Inventory
{
    [TestClass]
    public class InventoryInteractionPolicyTests
    {
        [TestMethod]
        public void PublishDebug_UpdatesProbeLatestSnapshot()
        {
            InventoryProbeService probeService = CreateProbeService(InventorySnapshot.Empty);
            var pickupPolicy = (InventoryPickupPolicyEngine)RuntimeHelpers.GetUninitializedObject(typeof(InventoryPickupPolicyEngine));

            var policy = new InventoryInteractionPolicy(probeService, pickupPolicy, "Incursion/IncursionKey");
            InventoryDebugSnapshot snapshot = CreateDebugSnapshot("InventoryFullDecision", sequence: 42);

            policy.PublishDebug(snapshot);

            InventoryDebugSnapshot latest = policy.GetLatestDebug();
            latest.Stage.Should().Be(snapshot.Stage);
            latest.GroundItemPath.Should().Be(snapshot.GroundItemPath);
            latest.DecisionAllowPickup.Should().Be(snapshot.DecisionAllowPickup);
            latest.Sequence.Should().BeGreaterThan(0);
        }

        [TestMethod]
        public void GetLatestDebugTrail_ReturnsProbeTrailEntries()
        {
            InventoryProbeService probeService = CreateProbeService(InventorySnapshot.Empty);
            var pickupPolicy = (InventoryPickupPolicyEngine)RuntimeHelpers.GetUninitializedObject(typeof(InventoryPickupPolicyEngine));

            var policy = new InventoryInteractionPolicy(probeService, pickupPolicy, "Incursion/IncursionKey");
            policy.PublishDebug(CreateDebugSnapshot("First", sequence: 1));
            policy.PublishDebug(CreateDebugSnapshot("Second", sequence: 2));

            IReadOnlyList<string> trail = policy.GetLatestDebugTrail();

            trail.Should().NotBeEmpty();
            trail[^1].Should().Contain("Second");
        }

        [TestMethod]
        public void ShouldAllowWorldItemWhenInventoryFull_DelegatesThroughInventoryPolicy()
        {
            InventoryProbeService probeService = CreateProbeService(InventorySnapshot.Empty);
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

            var policy = new InventoryInteractionPolicy(probeService, pickupPolicy, "Incursion/IncursionKey");

            bool result = policy.ShouldAllowWorldItemWhenInventoryFull(groundItem, controller);

            result.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldAllowClosedDoorPastMechanic_AllowsWhenInventoryProbeNotesAreUnreliable()
        {
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
            var pickupPolicy = (InventoryPickupPolicyEngine)RuntimeHelpers.GetUninitializedObject(typeof(InventoryPickupPolicyEngine));
            var controller = (GameController)RuntimeHelpers.GetUninitializedObject(typeof(GameController));

            var policy = new InventoryInteractionPolicy(probeService, pickupPolicy, "Incursion/IncursionKey");

            bool result = policy.ShouldAllowClosedDoorPastMechanic(controller);

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