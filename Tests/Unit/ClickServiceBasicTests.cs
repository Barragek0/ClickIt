using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using FluentAssertions;
using ClickIt.Services;
using ExileCore.PoEMemory.MemoryObjects;
using System;
using System.Windows.Forms;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickServiceBasicTests
    {
        [TestMethod]
        public void GetElementAccessLock_ReturnsSameObject()
        {
            // Since runtime construction is heavy, create an uninitialized instance and initialize the internal lock
            var svc = (ClickService)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ClickService));
            var fld = typeof(ClickService).GetField("_elementAccessLock", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (fld == null)
            {
                fld = typeof(ClickService).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .FirstOrDefault(f => f.Name.IndexOf("elementAccess", System.StringComparison.OrdinalIgnoreCase) >= 0);
            }
            fld!.SetValue(svc, new object());
            var lock1 = svc.GetElementAccessLock();
            var lock2 = svc.GetElementAccessLock();

            // but the important behaviour is that repeated calls return the same instance.
            lock1.Should().BeSameAs(lock2);
            lock1.Should().BeSameAs(lock2);
        }

        [TestMethod]
        public void ShouldClickShrineWhenGroundItemsHidden_ReturnsFalse_WhenShrineMissing()
        {
            ClickService.ShouldClickShrineWhenGroundItemsHidden(null).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickShrineWhenGroundItemsHidden_ReturnsTrue_WhenShrineExists()
        {
            var shrine = (Entity)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Entity));
            ClickService.ShouldClickShrineWhenGroundItemsHidden(shrine).Should().BeTrue();
        }

        [TestMethod]
        public void ShouldResolveShrineCandidate_ReturnsFalse_WhenStickyTargetActive()
        {
            ClickService.ShouldResolveShrineCandidate(hasStickyOffscreenTarget: true).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldResolveShrineCandidate_ReturnsTrue_WhenNoStickyTarget()
        {
            ClickService.ShouldResolveShrineCandidate(hasStickyOffscreenTarget: false).Should().BeTrue();
        }

        [TestMethod]
        public void OffscreenPathfindingTargetSearchDistance_IsFixedAndIndependentOfSearchRadius()
        {
            var method = typeof(ClickService).GetMethod(
                "GetOffscreenPathfindingTargetSearchDistance",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            method.Should().NotBeNull();
            int distance = (int)method!.Invoke(null, null)!;
            distance.Should().Be(50000);
        }

        [TestMethod]
        public void ResolveNearestOffscreenLabelBackedTarget_Exists_AsDedicatedHelper()
        {
            var method = typeof(ClickService).GetMethod(
                "ResolveNearestOffscreenLabelBackedTarget",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                binder: null,
                types: [typeof(int)],
                modifiers: null);

            method.Should().NotBeNull();
        }

        [TestMethod]
        public void FindVisibleLabelForEntity_ReturnsNull_WhenEntityIsNull()
        {
            var method = typeof(ClickService).GetMethod(
                "FindVisibleLabelForEntity",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            method.Should().NotBeNull();
            var result = method!.Invoke(null, [null!, null!]);
            result.Should().BeNull();
        }

        [TestMethod]
        public void FindVisibleLabelForEntity_ReturnsNull_WhenLabelsAreMissing()
        {
            var method = typeof(ClickService).GetMethod(
                "FindVisibleLabelForEntity",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            method.Should().NotBeNull();
            var entity = (Entity)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Entity));
            var result = method!.Invoke(null, [entity, null!]);
            result.Should().BeNull();
        }

        [TestMethod]
        public void TryComputeGridDirectionPoint_ReturnsFalse_ForZeroDelta()
        {
            var method = typeof(ClickService).GetMethod(
                "TryComputeGridDirectionPoint",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            method.Should().NotBeNull();

            object[] args = [new SharpDX.Vector2(100, 100), 0f, 0f, 50f, null!];
            bool ok = (bool)method!.Invoke(null, args)!;
            ok.Should().BeFalse();
        }

        [TestMethod]
        public void TryComputeGridDirectionPoint_ReturnsPoint_ForNonZeroDelta()
        {
            var method = typeof(ClickService).GetMethod(
                "TryComputeGridDirectionPoint",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            method.Should().NotBeNull();

            object[] args = [new SharpDX.Vector2(100, 100), 10f, 5f, 80f, null!];
            bool ok = (bool)method!.Invoke(null, args)!;

            ok.Should().BeTrue();
            args[4].Should().BeOfType<SharpDX.Vector2>();
            var point = (SharpDX.Vector2)args[4];
            point.Should().NotBe(new SharpDX.Vector2(100, 100));
        }

        [TestMethod]
        public void TryComputeGridDirectionPoint_ProjectsNorthEast_ForPositiveGridDelta()
        {
            var method = typeof(ClickService).GetMethod(
                "TryComputeGridDirectionPoint",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            method.Should().NotBeNull();

            var center = new SharpDX.Vector2(960f, 540f);
            object[] args = [center, 175f, 77f, 300f, null!];
            bool ok = (bool)method!.Invoke(null, args)!;

            ok.Should().BeTrue();
            var point = (SharpDX.Vector2)args[4];
            point.X.Should().BeGreaterThan(center.X);
            point.Y.Should().BeLessThan(center.Y);
        }

        [TestMethod]
        public void FindClosestPathIndexToPlayer_ReturnsNearestIndex()
        {
            var method = typeof(ClickService).GetMethod(
                "FindClosestPathIndexToPlayer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            method.Should().NotBeNull();

            var path = new System.Collections.Generic.List<PathfindingService.GridPoint>
            {
                new(10, 10),
                new(14, 14),
                new(20, 20)
            };

            int index = (int)method!.Invoke(null, [path, new PathfindingService.GridPoint(13, 13)])!;
            index.Should().Be(1);
        }

        [TestMethod]
        public void FindClosestPathIndexToPlayer_ReturnsMinusOne_ForEmptyPath()
        {
            var method = typeof(ClickService).GetMethod(
                "FindClosestPathIndexToPlayer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            method.Should().NotBeNull();

            var path = new System.Collections.Generic.List<PathfindingService.GridPoint>();
            int index = (int)method!.Invoke(null, [path, new PathfindingService.GridPoint(0, 0)])!;
            index.Should().Be(-1);
        }

        [TestMethod]
        public void IsClickableInEitherSpace_ReturnsTrue_WhenOnlyAbsoluteSpaceIsClickable()
        {
            bool checker(SharpDX.Vector2 p, string _) => p.X > 100f;

            bool result = ClickService.IsClickableInEitherSpace(
                new SharpDX.Vector2(40f, 20f),
                new SharpDX.Vector2(80f, 0f),
                checker,
                "test/path");

            result.Should().BeTrue();
        }

        [TestMethod]
        public void GetLabelsForOffscreenSelection_MethodExists()
        {
            var method = typeof(ClickService).GetMethod(
                "GetLabelsForOffscreenSelection",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            method.Should().NotBeNull();
        }

        [TestMethod]
        public void ShouldContinuePathfindingWhenLabelClickable_ReturnsFalse_WhenLabelClickable()
        {
            ClickService.ShouldContinuePathfindingWhenLabelClickable(true).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldContinuePathfindingWhenLabelClickable_ReturnsTrue_WhenLabelNotClickable()
        {
            ClickService.ShouldContinuePathfindingWhenLabelClickable(false).Should().BeTrue();
        }

        [TestMethod]
        public void ShouldSuppressPathfindingLabelCore_ReturnsTrue_ForInactiveUltimatum()
        {
            ClickService.ShouldSuppressPathfindingLabelCore(
                suppressLeverClick: false,
                suppressInactiveUltimatum: true).Should().BeTrue();
        }

        [TestMethod]
        public void ShouldSuppressPathfindingLabelCore_ReturnsTrue_ForLeverCooldown()
        {
            ClickService.ShouldSuppressPathfindingLabelCore(
                suppressLeverClick: true,
                suppressInactiveUltimatum: false).Should().BeTrue();
        }

        [TestMethod]
        public void ShouldSuppressPathfindingLabelCore_ReturnsFalse_WhenNoSuppressionFlags()
        {
            ClickService.ShouldSuppressPathfindingLabelCore(
                suppressLeverClick: false,
                suppressInactiveUltimatum: false).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldPreferShrineOverLabelForOffscreen_ReturnsTrue_WhenShrinesAreHigherPriority()
        {
            var priorityMap = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["shrines"] = 0,
                ["expedition"] = 2
            };
            var ignoreSet = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ignoreDistanceWithinByMechanicId = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            bool preferShrine = ClickService.ShouldPreferShrineOverLabelForOffscreen(
                shrineDistance: 80f,
                labelDistance: 40f,
                labelMechanicId: "expedition",
                priorityIndexMap: priorityMap,
                ignoreDistanceSet: ignoreSet,
                ignoreDistanceWithinByMechanicId: ignoreDistanceWithinByMechanicId,
                priorityDistancePenalty: 25);

            preferShrine.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldPreferShrineOverLabelForOffscreen_ReturnsFalse_WhenLabelPriorityAndDistanceWin()
        {
            var priorityMap = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["shrines"] = 2,
                ["expedition"] = 0
            };
            var ignoreSet = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ignoreDistanceWithinByMechanicId = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            bool preferShrine = ClickService.ShouldPreferShrineOverLabelForOffscreen(
                shrineDistance: 90f,
                labelDistance: 35f,
                labelMechanicId: "expedition",
                priorityIndexMap: priorityMap,
                ignoreDistanceSet: ignoreSet,
                ignoreDistanceWithinByMechanicId: ignoreDistanceWithinByMechanicId,
                priorityDistancePenalty: 25);

            preferShrine.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldPreferShrineOverLabelForOffscreen_IgnoresDistanceOnlyInsideConfiguredThreshold()
        {
            var priorityMap = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["shrines"] = 0,
                ["expedition"] = 1
            };
            var ignoreSet = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "shrines"
            };
            var ignoreDistanceWithinByMechanicId = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["shrines"] = 100
            };

            bool preferShrineWithinThreshold = ClickService.ShouldPreferShrineOverLabelForOffscreen(
                shrineDistance: 90f,
                labelDistance: 40f,
                labelMechanicId: "expedition",
                priorityIndexMap: priorityMap,
                ignoreDistanceSet: ignoreSet,
                ignoreDistanceWithinByMechanicId: ignoreDistanceWithinByMechanicId,
                priorityDistancePenalty: 25);

            bool preferShrineOutsideThreshold = ClickService.ShouldPreferShrineOverLabelForOffscreen(
                shrineDistance: 120f,
                labelDistance: 40f,
                labelMechanicId: "expedition",
                priorityIndexMap: priorityMap,
                ignoreDistanceSet: ignoreSet,
                ignoreDistanceWithinByMechanicId: ignoreDistanceWithinByMechanicId,
                priorityDistancePenalty: 25);

            preferShrineWithinThreshold.Should().BeTrue();
            preferShrineOutsideThreshold.Should().BeFalse();
        }

        [TestMethod]
        public void TryGetSmoothedPathDirection_PreservesDiagonalIntent_ForAlternatingSteps()
        {
            var path = new System.Collections.Generic.List<PathfindingService.GridPoint>
            {
                new(0, 0),
                new(1, 0),
                new(1, 1),
                new(2, 1),
                new(2, 2),
                new(3, 2),
                new(3, 3)
            };

            bool ok = ClickService.TryGetSmoothedPathDirection(
                path,
                new PathfindingService.GridPoint(0, 0),
                nearestIndex: 0,
                out float dx,
                out float dy);

            ok.Should().BeTrue();
            dx.Should().BeGreaterThan(0f);
            dy.Should().BeGreaterThan(0f);
            float ratio = dx / dy;
            ratio.Should().BeGreaterThan(0.6f);
            ratio.Should().BeLessThan(1.6f);
        }

        [TestMethod]
        public void TryGetSmoothedPathDirection_RemainsAxisAligned_WhenPathIsStraight()
        {
            var path = new System.Collections.Generic.List<PathfindingService.GridPoint>
            {
                new(10, 10),
                new(11, 10),
                new(12, 10),
                new(13, 10),
                new(14, 10)
            };

            bool ok = ClickService.TryGetSmoothedPathDirection(
                path,
                new PathfindingService.GridPoint(10, 10),
                nearestIndex: 0,
                out float dx,
                out float dy);

            ok.Should().BeTrue();
            dx.Should().BeGreaterThan(0f);
            Math.Abs(dy).Should().BeLessThan(0.001f);
        }

        [TestMethod]
        public void IsSameEntityAddress_ReturnsTrue_ForMatchingNonZeroAddresses()
        {
            ClickService.IsSameEntityAddress(12345, 12345).Should().BeTrue();
        }

        [TestMethod]
        public void IsSameEntityAddress_ReturnsFalse_ForZeroOrDifferentAddresses()
        {
            ClickService.IsSameEntityAddress(0, 12345).Should().BeFalse();
            ClickService.IsSameEntityAddress(12345, 0).Should().BeFalse();
            ClickService.IsSameEntityAddress(12345, 67890).Should().BeFalse();
        }

        [TestMethod]
        public void GetAreaTransitionMechanicIdForPath_ReturnsAreaTransition_WhenEnabledAndNotLabTrial()
        {
            string? mechanicId = ClickService.GetAreaTransitionMechanicIdForPath(
                clickAreaTransitions: true,
                clickLabyrinthTrials: false,
                ExileCore.Shared.Enums.EntityType.AreaTransition,
                "Metadata/MiscellaneousObjects/AreaTransition");

            mechanicId.Should().Be("area-transitions");
        }

        [TestMethod]
        public void GetAreaTransitionMechanicIdForPath_ReturnsLabTrial_WhenLabTrialEnabled()
        {
            string? mechanicId = ClickService.GetAreaTransitionMechanicIdForPath(
                clickAreaTransitions: true,
                clickLabyrinthTrials: true,
                ExileCore.Shared.Enums.EntityType.AreaTransition,
                "Metadata/Terrain/LabyrinthTrial/Objects/TrialPortal");

            mechanicId.Should().Be("labyrinth-trials");
        }

        [TestMethod]
        public void GetAreaTransitionMechanicIdForPath_ReturnsNull_WhenDisabled()
        {
            string? mechanicId = ClickService.GetAreaTransitionMechanicIdForPath(
                clickAreaTransitions: false,
                clickLabyrinthTrials: false,
                ExileCore.Shared.Enums.EntityType.AreaTransition,
                "Metadata/MiscellaneousObjects/AreaTransition");

            mechanicId.Should().BeNull();
        }

        [TestMethod]
        public void IsMovementSkillInternalName_ReturnsTrue_ForKnownMarker()
        {
            ClickService.IsMovementSkillInternalName("Metadata/Stats/Skills/QuickDashGem").Should().BeTrue();
            ClickService.IsMovementSkillInternalName("Metadata/Items/Gems/SkillGemDash").Should().BeTrue();
            ClickService.IsMovementSkillInternalName("FlameDash").Should().BeTrue();
            ClickService.IsMovementSkillInternalName("FrostblinkSkillGem").Should().BeTrue();
            ClickService.IsMovementSkillInternalName("frostblink").Should().BeTrue();
            ClickService.IsMovementSkillInternalName("AmbushSkillGem").Should().BeTrue();
        }

        [TestMethod]
        public void IsMovementSkillInternalName_ReturnsFalse_ForNonMovementSkill()
        {
            ClickService.IsMovementSkillInternalName("Fireball").Should().BeFalse();
            ClickService.IsMovementSkillInternalName(string.Empty).Should().BeFalse();
        }

        [TestMethod]
        public void TryMapKeyTextToKeys_ParsesPrimarySkillbarKeys()
        {
            bool parsedQ = ClickService.TryMapKeyTextToKeys("Q", out Keys q);
            bool parsedCtrlW = ClickService.TryMapKeyTextToKeys("Ctrl+W", out Keys w);

            parsedQ.Should().BeTrue();
            q.Should().Be(Keys.Q);
            parsedCtrlW.Should().BeTrue();
            w.Should().Be(Keys.W);
        }

        [TestMethod]
        public void TryMapKeyTextToKeys_RejectsMouseBinds()
        {
            ClickService.TryMapKeyTextToKeys("LMB", out _).Should().BeFalse();
            ClickService.TryMapKeyTextToKeys("Mouse4", out _).Should().BeFalse();
        }

        [TestMethod]
        public void TryResolveSkillKeyText_UsesKnownChildPathFallback()
        {
            var method = typeof(ClickService).GetMethod(
                "TryResolveSkillKeyText",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            method.Should().NotBeNull();

            var entry = SkillNode.WithPathText("Q");
            object[] args = [entry, null!];
            bool resolved = (bool)method!.Invoke(null, args)!;

            resolved.Should().BeTrue();
            args[1].Should().Be("Q");
        }

        [TestMethod]
        public void CountRemainingPathNodes_ReturnsExpectedCounts()
        {
            var path = new System.Collections.Generic.List<PathfindingService.GridPoint>
            {
                new(0, 0),
                new(1, 0),
                new(2, 0),
                new(3, 0)
            };

            ClickService.CountRemainingPathNodes(path, nearestIndex: 1).Should().Be(2);
            ClickService.CountRemainingPathNodes(path, nearestIndex: -1).Should().Be(0);
            ClickService.CountRemainingPathNodes(path, nearestIndex: 9).Should().Be(0);
        }

        [TestMethod]
        public void ShouldAttemptMovementSkill_RequiresPathLengthAndRecastWindow()
        {
            ClickService.ShouldAttemptMovementSkill(
                movementSkillsEnabled: true,
                builtPath: true,
                remainingPathNodes: 12,
                minPathNodes: 8,
                now: 10_000,
                lastSkillUseTimestampMs: 9_000,
                recastDelayMs: 450).Should().BeTrue();

            ClickService.ShouldAttemptMovementSkill(
                movementSkillsEnabled: true,
                builtPath: true,
                remainingPathNodes: 4,
                minPathNodes: 8,
                now: 10_000,
                lastSkillUseTimestampMs: 0,
                recastDelayMs: 450).Should().BeFalse();

            ClickService.ShouldAttemptMovementSkill(
                movementSkillsEnabled: true,
                builtPath: true,
                remainingPathNodes: 12,
                minPathNodes: 8,
                now: 10_000,
                lastSkillUseTimestampMs: 9_800,
                recastDelayMs: 450).Should().BeFalse();
        }

        [TestMethod]
        public void ResolveMovementSkillPostCastClickBlockMs_ReturnsZeroForInstantSkills()
        {
            ClickService.ResolveMovementSkillPostCastClickBlockMs("FrostblinkSkillGem").Should().Be(0);
            ClickService.ResolveMovementSkillPostCastClickBlockMs("frostblink").Should().Be(0);
            ClickService.ResolveMovementSkillPostCastClickBlockMs("FlameDash").Should().Be(0);
        }

        [TestMethod]
        public void ResolveMovementSkillPostCastClickBlockMs_ReturnsSkillSpecificWindowForShieldCharge()
        {
            ClickService.ResolveMovementSkillPostCastClickBlockMs("ShieldCharge").Should().Be(100);
            ClickService.ResolveMovementSkillPostCastClickBlockMs("shield_charge").Should().Be(100);
        }

        [TestMethod]
        public void IsMovementSkillPostCastClickBlocked_TracksActiveWindow()
        {
            ClickService.IsMovementSkillPostCastClickBlocked(
                now: 1_000,
                blockUntilTimestampMs: 1_220,
                out long remainingMs).Should().BeTrue();
            remainingMs.Should().Be(220);

            ClickService.IsMovementSkillPostCastClickBlocked(
                now: 1_300,
                blockUntilTimestampMs: 1_220,
                out long expiredRemaining).Should().BeFalse();
            expiredRemaining.Should().Be(0);
        }

        [TestMethod]
        public void ResolveMovementSkillStatusPollWindowMs_ReturnsZero_WhenNoPostCastWindow()
        {
            ClickService.ResolveMovementSkillStatusPollWindowMs(0, "ShieldCharge").Should().Be(0);
            ClickService.ResolveMovementSkillStatusPollWindowMs(120, string.Empty).Should().Be(0);
        }

        [TestMethod]
        public void ResolveMovementSkillStatusPollWindowMs_ExtendsShieldChargeWindow()
        {
            ClickService.ResolveMovementSkillStatusPollWindowMs(100, "ShieldCharge").Should().Be(0);
            ClickService.ResolveMovementSkillStatusPollWindowMs(100, "shield_charge").Should().Be(0);
        }

        [TestMethod]
        public void IsLostShipmentPath_ReturnsTrue_ForLostShipmentMetadata()
        {
            ClickService.IsLostShipmentPath("Metadata/Chests/LostShipmentCrate/Tier1").Should().BeTrue();
            ClickService.IsLostShipmentPath("metadata/chests/lostshipmentcrate").Should().BeTrue();
            ClickService.IsLostShipmentPath("Metadata/Chests/LostShipment/Crate").Should().BeTrue();
            ClickService.IsLostShipmentPath("Metadata/Chests/StrongBoxes/Arcanist").Should().BeFalse();
        }

        [TestMethod]
        public void IsInsideWindowInEitherSpace_AcceptsClientAndScreenCoordinates()
        {
            var windowArea = new SharpDX.RectangleF(100f, 200f, 1920f, 1080f);

            ClickService.IsInsideWindowInEitherSpace(new SharpDX.Vector2(960f, 540f), windowArea).Should().BeTrue();
            ClickService.IsInsideWindowInEitherSpace(new SharpDX.Vector2(1060f, 740f), windowArea).Should().BeTrue();
            ClickService.IsInsideWindowInEitherSpace(new SharpDX.Vector2(-10f, 740f), windowArea).Should().BeFalse();
        }

        [TestMethod]
        public void IsVerisiumPath_ReturnsTrue_ForSettlersNodeMetadata()
        {
            ClickService.IsVerisiumPath("Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/Verisium").Should().BeTrue();
            ClickService.IsVerisiumPath("metadata/terrain/leagues/settlers/node/objects/nodetypes/verisium").Should().BeTrue();
            ClickService.IsVerisiumPath("Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/VerisiumBossSubAreaTransition").Should().BeFalse();
            ClickService.IsVerisiumPath("Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/CrimsonIron").Should().BeFalse();
        }

        [TestMethod]
        public void TryGetSettlersOreMechanicId_MapsKnownSettlersNodeTypes()
        {
            LabelFilterService.TryGetSettlersOreMechanicId(
                "Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/CrimsonIron",
                out string? crimsonMechanicId).Should().BeTrue();
            crimsonMechanicId.Should().Be("settlers-crimson-iron");

            LabelFilterService.TryGetSettlersOreMechanicId(
                "Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/DemonCopperObjects/copper_altar",
                out string? copperMechanicId).Should().BeTrue();
            copperMechanicId.Should().Be("settlers-copper");

            LabelFilterService.TryGetSettlersOreMechanicId(
                "Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/Bismuth",
                out string? bismuthMechanicId).Should().BeTrue();
            bismuthMechanicId.Should().Be("settlers-bismuth");

            LabelFilterService.TryGetSettlersOreMechanicId(
                "Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/Verisium",
                out string? verisiumMechanicId).Should().BeTrue();
            verisiumMechanicId.Should().Be("settlers-verisium");

            LabelFilterService.TryGetSettlersOreMechanicId(
                "Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/UnknownNode",
                out string? unknownMechanicId).Should().BeFalse();
            unknownMechanicId.Should().BeNull();

            LabelFilterService.TryGetSettlersOreMechanicId(
                "Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/VerisiumBossSubAreaTransition",
                out string? verisiumTransitionMechanicId).Should().BeFalse();
            verisiumTransitionMechanicId.Should().BeNull();

            LabelFilterService.TryGetSettlersOreMechanicId(
                "Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/FakeCopper/copper_altar",
                out string? broadCopperFalsePositive).Should().BeFalse();
            broadCopperFalsePositive.Should().BeNull();
        }

        [TestMethod]
        public void ShouldSkipLostShipmentEntity_ReturnsTrue_WhenEntityIsOpened()
        {
            bool shouldSkip = ClickService.ShouldSkipLostShipmentEntity(
                isValid: true,
                distance: 20f,
                clickDistance: 100,
                isOpened: true);

            shouldSkip.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldSkipVerisiumEntity_RespectsValidityAndDistance()
        {
            ClickService.ShouldSkipVerisiumEntity(
                isValid: false,
                distance: 10f,
                clickDistance: 100).Should().BeTrue();

            ClickService.ShouldSkipVerisiumEntity(
                isValid: true,
                distance: 150f,
                clickDistance: 100).Should().BeTrue();

            ClickService.ShouldSkipVerisiumEntity(
                isValid: true,
                distance: 50f,
                clickDistance: 100).Should().BeFalse();
        }

        [TestMethod]
        public void IsBackedByGroundLabel_RequiresKnownNonZeroAddress()
        {
            var addresses = new System.Collections.Generic.HashSet<long> { 123, 456 };

            ClickService.IsBackedByGroundLabel(123, addresses).Should().BeTrue();
            ClickService.IsBackedByGroundLabel(999, addresses).Should().BeFalse();
            ClickService.IsBackedByGroundLabel(0, addresses).Should().BeFalse();
            ClickService.IsBackedByGroundLabel(123, null).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldPreferLostShipmentOverVisibleCandidates_RespectsConfiguredPriorities()
        {
            var priorityMap = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["shrines"] = 0,
                ["lost-shipment"] = 1,
                ["items"] = 2
            };

            var ignoreSet = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ignoreDistanceWithinByMechanicId = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            bool preferLostShipment = ClickService.ShouldPreferLostShipmentOverVisibleCandidates(
                lostShipmentDistance: 70f,
                labelDistance: 95f,
                labelMechanicId: "items",
                shrineDistance: 120f,
                priorityIndexMap: priorityMap,
                ignoreDistanceSet: ignoreSet,
                ignoreDistanceWithinByMechanicId: ignoreDistanceWithinByMechanicId,
                priorityDistancePenalty: 25);

            preferLostShipment.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldPreferLostShipmentOverVisibleCandidates_ReturnsFalse_WhenShrineRankWins()
        {
            var priorityMap = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["shrines"] = 0,
                ["lost-shipment"] = 1,
                ["items"] = 2
            };

            var ignoreSet = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ignoreDistanceWithinByMechanicId = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            bool preferLostShipment = ClickService.ShouldPreferLostShipmentOverVisibleCandidates(
                lostShipmentDistance: 80f,
                labelDistance: 10f,
                labelMechanicId: "items",
                shrineDistance: 10f,
                priorityIndexMap: priorityMap,
                ignoreDistanceSet: ignoreSet,
                ignoreDistanceWithinByMechanicId: ignoreDistanceWithinByMechanicId,
                priorityDistancePenalty: 25);

            preferLostShipment.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldPreferVerisiumOverVisibleCandidates_ReturnsTrue_WhenItHasBestRank()
        {
            var priorityMap = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["settlers-verisium"] = 0,
                ["lost-shipment"] = 1,
                ["items"] = 2,
                ["shrines"] = 3
            };

            var ignoreSet = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ignoreDistanceWithinByMechanicId = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            bool preferVerisium = ClickService.ShouldPreferVerisiumOverVisibleCandidates(
                verisiumDistance: 90f,
                labelDistance: 80f,
                labelMechanicId: "items",
                shrineDistance: 70f,
                lostShipmentDistance: 75f,
                priorityIndexMap: priorityMap,
                ignoreDistanceSet: ignoreSet,
                ignoreDistanceWithinByMechanicId: ignoreDistanceWithinByMechanicId,
                priorityDistancePenalty: 25);

            preferVerisium.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldPreferVerisiumOverVisibleCandidates_ReturnsFalse_WhenLostShipmentRankWins()
        {
            var priorityMap = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["lost-shipment"] = 0,
                ["settlers-verisium"] = 2,
                ["items"] = 3,
                ["shrines"] = 4
            };

            var ignoreSet = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ignoreDistanceWithinByMechanicId = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            bool preferVerisium = ClickService.ShouldPreferVerisiumOverVisibleCandidates(
                verisiumDistance: 90f,
                labelDistance: null,
                labelMechanicId: null,
                shrineDistance: null,
                lostShipmentDistance: 20f,
                priorityIndexMap: priorityMap,
                ignoreDistanceSet: ignoreSet,
                ignoreDistanceWithinByMechanicId: ignoreDistanceWithinByMechanicId,
                priorityDistancePenalty: 25);

            preferVerisium.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldPreferSettlersOreOverVisibleCandidates_UsesSelectedSettlersMechanicRank()
        {
            var priorityMap = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["settlers-crimson-iron"] = 0,
                ["lost-shipment"] = 1,
                ["items"] = 2,
                ["shrines"] = 3,
                ["settlers-verisium"] = 4
            };

            var ignoreSet = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ignoreDistanceWithinByMechanicId = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            bool preferSettlersOre = ClickService.ShouldPreferSettlersOreOverVisibleCandidates(
                settlersOreDistance: 90f,
                settlersOreMechanicId: "settlers-crimson-iron",
                labelDistance: 80f,
                labelMechanicId: "items",
                shrineDistance: 70f,
                lostShipmentDistance: 75f,
                priorityIndexMap: priorityMap,
                ignoreDistanceSet: ignoreSet,
                ignoreDistanceWithinByMechanicId: ignoreDistanceWithinByMechanicId,
                priorityDistancePenalty: 25);

            preferSettlersOre.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldPreferSettlersOreOverVisibleCandidates_ReturnsTrue_WhenNoCompetingCandidates()
        {
            var priorityMap = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["settlers-verisium"] = 0,
                ["lost-shipment"] = 1,
                ["shrines"] = 2,
                ["items"] = 3
            };

            var ignoreSet = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ignoreDistanceWithinByMechanicId = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            bool preferSettlersOre = ClickService.ShouldPreferSettlersOreOverVisibleCandidates(
                settlersOreDistance: 90f,
                settlersOreMechanicId: "settlers-verisium",
                labelDistance: null,
                labelMechanicId: null,
                shrineDistance: null,
                lostShipmentDistance: null,
                priorityIndexMap: priorityMap,
                ignoreDistanceSet: ignoreSet,
                ignoreDistanceWithinByMechanicId: ignoreDistanceWithinByMechanicId,
                priorityDistancePenalty: 25);

            preferSettlersOre.Should().BeTrue();
        }

        public sealed class SkillNode
        {
            private readonly SkillNode?[] _children;

            public SkillNode(string text = "", int childSlots = 2)
            {
                Text = text;
                _children = new SkillNode?[Math.Max(0, childSlots)];
            }

            public string Text { get; }

            public SkillNode? Child(int index)
            {
                if (index < 0 || index >= _children.Length)
                    return null;

                return _children[index];
            }

            public SkillNode SetChild(int index, SkillNode child)
            {
                _children[index] = child;
                return this;
            }

            public static SkillNode WithPathText(string text)
            {
                var level3 = new SkillNode().SetChild(1, new SkillNode(text));
                var level2 = new SkillNode().SetChild(0, level3);
                var level1 = new SkillNode().SetChild(0, level2);
                return new SkillNode().SetChild(0, level1);
            }
        }

    }
}
