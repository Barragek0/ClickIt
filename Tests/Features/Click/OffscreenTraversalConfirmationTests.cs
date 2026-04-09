namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class OffscreenTraversalConfirmationTests
    {
        [TestMethod]
        public void EvaluateOffscreenTraversalTargetConfirmation_Delays_FirstSighting()
        {
            var result = OffscreenPathingMath.EvaluateOffscreenTraversalTargetConfirmation(
                targetAddress: 42,
                targetPath: "Metadata/Chests/Chest9",
                pendingAddress: 0,
                pendingPath: string.Empty,
                pendingFirstSeenTimestampMs: 0,
                now: 1000,
                confirmationWindowMs: 120);

            result.ShouldDelay.Should().BeTrue();
            result.NextAddress.Should().Be(42);
            result.NextPath.Should().Be("Metadata/Chests/Chest9");
            result.NextFirstSeenTimestampMs.Should().Be(1000);
            result.RemainingDelayMs.Should().Be(120);
        }

        [TestMethod]
        public void EvaluateOffscreenTraversalTargetConfirmation_Allows_AfterWindowElapsed()
        {
            var result = OffscreenPathingMath.EvaluateOffscreenTraversalTargetConfirmation(
                targetAddress: 42,
                targetPath: "Metadata/Chests/Chest9",
                pendingAddress: 42,
                pendingPath: "Metadata/Chests/Chest9",
                pendingFirstSeenTimestampMs: 1000,
                now: 1125,
                confirmationWindowMs: 120);

            result.ShouldDelay.Should().BeFalse();
            result.RemainingDelayMs.Should().Be(0);
        }

        [TestMethod]
        public void EvaluateOffscreenTraversalTargetConfirmation_AllowsImmediately_WhenWindowIsDisabled()
        {
            var result = OffscreenPathingMath.EvaluateOffscreenTraversalTargetConfirmation(
                targetAddress: 42,
                targetPath: "Metadata/Chests/Chest9",
                pendingAddress: 99,
                pendingPath: "Metadata/Chests/OldChest",
                pendingFirstSeenTimestampMs: 900,
                now: 1000,
                confirmationWindowMs: 0);

            result.ShouldDelay.Should().BeFalse();
            result.NextAddress.Should().Be(42);
            result.NextPath.Should().Be("Metadata/Chests/Chest9");
            result.NextFirstSeenTimestampMs.Should().Be(1000);
            result.RemainingDelayMs.Should().Be(0);
        }

        [TestMethod]
        public void EvaluateOffscreenTraversalTargetConfirmation_RestartsDelay_WhenTargetChanges()
        {
            var result = OffscreenPathingMath.EvaluateOffscreenTraversalTargetConfirmation(
                targetAddress: 0,
                targetPath: "Metadata/Chests/NewChest",
                pendingAddress: 0,
                pendingPath: "Metadata/Chests/OldChest",
                pendingFirstSeenTimestampMs: 950,
                now: 1000,
                confirmationWindowMs: 120);

            result.ShouldDelay.Should().BeTrue();
            result.NextAddress.Should().Be(0);
            result.NextPath.Should().Be("Metadata/Chests/NewChest");
            result.NextFirstSeenTimestampMs.Should().Be(1000);
            result.RemainingDelayMs.Should().Be(120);
        }

        [TestMethod]
        public void EvaluateOffscreenTraversalTargetConfirmation_UsesCaseInsensitivePathMatch_WhenAddressesAreMissing()
        {
            var result = OffscreenPathingMath.EvaluateOffscreenTraversalTargetConfirmation(
                targetAddress: 0,
                targetPath: "metadata/chests/chest9",
                pendingAddress: 0,
                pendingPath: "Metadata/Chests/Chest9",
                pendingFirstSeenTimestampMs: 1000,
                now: 1050,
                confirmationWindowMs: 120);

            result.ShouldDelay.Should().BeTrue();
            result.NextFirstSeenTimestampMs.Should().Be(1000);
            result.RemainingDelayMs.Should().Be(70);
        }

        [TestMethod]
        public void EvaluateOffscreenTraversalTargetConfirmation_UsesCurrentTimestamp_WhenFirstSeenIsMissing()
        {
            var result = OffscreenPathingMath.EvaluateOffscreenTraversalTargetConfirmation(
                targetAddress: 0,
                targetPath: "Metadata/Chests/Chest9",
                pendingAddress: 0,
                pendingPath: "Metadata/Chests/Chest9",
                pendingFirstSeenTimestampMs: 0,
                now: 1050,
                confirmationWindowMs: 120);

            result.ShouldDelay.Should().BeTrue();
            result.NextFirstSeenTimestampMs.Should().Be(1050);
            result.RemainingDelayMs.Should().Be(120);
        }

        [DataTestMethod]
        [DataRow(42L, "Metadata/A", 42L, "Metadata/B", true)]
        [DataRow(0L, "metadata/chests/chest9", 0L, "Metadata/Chests/Chest9", true)]
        [DataRow(0L, "Metadata/Chests/A", 0L, "Metadata/Chests/B", false)]
        public void IsSameOffscreenTraversalTarget_UsesAddressOrCaseInsensitivePathMatching(
            long leftAddress,
            string leftPath,
            long rightAddress,
            string rightPath,
            bool expected)
        {
            OffscreenPathingMath.IsSameOffscreenTraversalTarget(leftAddress, leftPath, rightAddress, rightPath)
                .Should()
                .Be(expected);
        }

        [DataTestMethod]
        [DataRow(true, false, true)]
        [DataRow(true, true, false)]
        [DataRow(false, false, false)]
        public void ShouldDropStickyTargetForUntargetableEldritchAltar_ReturnsExpected(
            bool isEldritchAltar,
            bool isTargetable,
            bool expected)
        {
            OffscreenPathingMath.ShouldDropStickyTargetForUntargetableEldritchAltar(isEldritchAltar, isTargetable)
                .Should()
                .Be(expected);
        }

        [DataTestMethod]
        [DataRow(42L, 42L, true)]
        [DataRow(42L, 99L, false)]
        [DataRow(0L, 42L, false)]
        public void IsSameEntityAddress_RequiresNonZeroMatchingAddresses(long leftAddress, long rightAddress, bool expected)
        {
            OffscreenPathingMath.IsSameEntityAddress(leftAddress, rightAddress)
                .Should()
                .Be(expected);
        }

        [DataTestMethod]
        [DataRow(true, false, false, false, false, false)]
        [DataRow(true, false, true, false, false, true)]
        [DataRow(false, true, true, true, true, false)]
        public void ShouldPrioritizeOnscreenMechanicsOverOffscreenPathing_ReturnsExpected(
            bool prioritizeOnscreenClickableMechanics,
            bool hasClickableAltar,
            bool hasClickableShrine,
            bool hasClickableLostShipment,
            bool hasClickableSettlersOre,
            bool expected)
        {
            OffscreenPathingMath.ShouldPrioritizeOnscreenMechanicsOverOffscreenPathing(
                    prioritizeOnscreenClickableMechanics,
                    hasClickableAltar,
                    hasClickableShrine,
                    hasClickableLostShipment,
                    hasClickableSettlersOre)
                .Should()
                .Be(expected);
        }

        [DataTestMethod]
        [DataRow(false, true, true, true, true, true, false)]
        [DataRow(true, false, false, false, false, false, false)]
        [DataRow(true, false, false, true, false, false, true)]
        public void ShouldEvaluateOnscreenMechanicChecks_ReturnsExpected(
            bool prioritizeOnscreenClickableMechanics,
            bool clickShrinesEnabled,
            bool clickLostShipmentEnabled,
            bool clickSettlersOreEnabled,
            bool clickEaterAltarsEnabled,
            bool clickExarchAltarsEnabled,
            bool expected)
        {
            OffscreenPathingMath.ShouldEvaluateOnscreenMechanicChecks(
                    prioritizeOnscreenClickableMechanics,
                    clickShrinesEnabled,
                    clickLostShipmentEnabled,
                    clickSettlersOreEnabled,
                    clickEaterAltarsEnabled,
                    clickExarchAltarsEnabled)
                .Should()
                .Be(expected);
        }

        [TestMethod]
        public void IsBackedByGroundLabel_RequiresNonZeroAddressAndContainingSet()
        {
            IReadOnlySet<long> addresses = new HashSet<long> { 42, 99 };

            OffscreenPathingMath.IsBackedByGroundLabel(42, addresses).Should().BeTrue();
            OffscreenPathingMath.IsBackedByGroundLabel(100, addresses).Should().BeFalse();
            OffscreenPathingMath.IsBackedByGroundLabel(42, null).Should().BeFalse();
            OffscreenPathingMath.IsBackedByGroundLabel(0, addresses).Should().BeFalse();
        }

        [DataTestMethod]
        [DataRow(true, true, true, false)]
        [DataRow(true, true, false, true)]
        [DataRow(true, false, true, true)]
        [DataRow(false, true, true, true)]
        public void ShouldContinuePathfindingWhenLabelActionable_ReturnsExpected(
            bool labelInWindow,
            bool labelClickable,
            bool clickPointResolvable,
            bool expected)
        {
            OffscreenPathingMath.ShouldContinuePathfindingWhenLabelActionable(labelInWindow, labelClickable, clickPointResolvable)
                .Should()
                .Be(expected);
        }

        [DataTestMethod]
        [DataRow(true, true, "settlers-verisium", true)]
        [DataRow(false, true, "settlers-verisium", false)]
        [DataRow(true, false, "settlers-verisium", false)]
        [DataRow(true, true, "", false)]
        public void ShouldPathfindToEntityAfterClickPointResolveFailure_ReturnsExpected(
            bool walkTowardOffscreenLabelsEnabled,
            bool hasEntity,
            string mechanicId,
            bool expected)
        {
            OffscreenPathingMath.ShouldPathfindToEntityAfterClickPointResolveFailure(
                    walkTowardOffscreenLabelsEnabled,
                    hasEntity,
                    mechanicId)
                .Should()
                .Be(expected);
        }

        [TestMethod]
        public void ResolveLabelMechanicIdForVisibleCandidateComparison_PrefersResolvedMechanicOrWorldItemFallback()
        {
            OffscreenPathingMath.ResolveLabelMechanicIdForVisibleCandidateComparison(
                    resolvedMechanicId: "essence",
                    hasLabel: true,
                    isWorldItemLabel: true,
                    clickItemsEnabled: true)
                .Should()
                .Be("essence");

            OffscreenPathingMath.ResolveLabelMechanicIdForVisibleCandidateComparison(
                    resolvedMechanicId: null,
                    hasLabel: true,
                    isWorldItemLabel: true,
                    clickItemsEnabled: true)
                .Should()
                .Be(MechanicIds.Items);

            OffscreenPathingMath.ResolveLabelMechanicIdForVisibleCandidateComparison(
                    resolvedMechanicId: null,
                    hasLabel: true,
                    isWorldItemLabel: false,
                    clickItemsEnabled: true)
                .Should()
                .BeNull();
        }

        [TestMethod]
        public void ShouldFallbackToSettlersEntityClickAfterLabelResolveFailure_RequiresMatchingSettlersMechanics()
        {
            OffscreenPathingMath.ShouldFallbackToSettlersEntityClickAfterLabelResolveFailure("settlers-verisium", "settlers-verisium")
                .Should()
                .BeTrue();

            OffscreenPathingMath.ShouldFallbackToSettlersEntityClickAfterLabelResolveFailure("settlers-verisium", "settlers-copper")
                .Should()
                .BeFalse();

            OffscreenPathingMath.ShouldFallbackToSettlersEntityClickAfterLabelResolveFailure("items", "settlers-verisium")
                .Should()
                .BeFalse();
        }

        [TestMethod]
        public void GetEldritchAltarMechanicIdForPath_RespectsEnabledInfluence()
        {
            OffscreenPathingMath.GetEldritchAltarMechanicIdForPath(
                    clickExarchAltars: true,
                    clickEaterAltars: false,
                    path: "Metadata/Terrain/CleansingFireAltar")
                .Should()
                .Be(MechanicIds.AltarsSearingExarch);

            OffscreenPathingMath.GetEldritchAltarMechanicIdForPath(
                    clickExarchAltars: false,
                    clickEaterAltars: true,
                    path: "Metadata/Terrain/TangleAltar")
                .Should()
                .Be(MechanicIds.AltarsEaterOfWorlds);

            OffscreenPathingMath.GetEldritchAltarMechanicIdForPath(
                    clickExarchAltars: false,
                    clickEaterAltars: false,
                    path: "Metadata/Terrain/CleansingFireAltar")
                .Should()
                .BeNull();
        }

        [TestMethod]
        public void ShouldBlockOffscreenTraversalAfterPathBuildFailure_BlocksOnlyNoRouteFailure()
        {
            OffscreenPathingMath.ShouldBlockOffscreenTraversalAfterPathBuildFailure(PathfindingService.AStarNoRouteFailureReason)
                .Should()
                .BeTrue();

            OffscreenPathingMath.ShouldBlockOffscreenTraversalAfterPathBuildFailure("Terrain/pathfinding data unavailable.")
                .Should()
                .BeFalse();

            OffscreenPathingMath.ShouldBlockOffscreenTraversalAfterPathBuildFailure(string.Empty)
                .Should()
                .BeFalse();
        }

        [TestMethod]
        public void FindVisibleLabelForEntity_ReturnsNull_WhenLabelsCannotExposeGroundItems()
        {
            Entity entity = CreateEntityWithAddress(77);
            LabelOnGround opaqueLabel = ExileCoreOpaqueFactory.CreateOpaqueLabel();

            LabelOnGround? result = OffscreenPathingMath.FindVisibleLabelForEntity(entity, [opaqueLabel]);

            result.Should().BeNull();
        }

        [TestMethod]
        public void FindVisibleLabelForEntity_FailsClosed_WhenOpaqueLabelCannotExposeGroundItem()
        {
            Entity entity = CreateEntityWithAddress(77);
            LabelOnGround opaqueLabel = ExileCoreOpaqueFactory.CreateOpaqueLabel();

            LabelOnGround? result = OffscreenPathingMath.FindVisibleLabelForEntity(entity, [opaqueLabel, null!]);

            result.Should().BeNull();
        }

        [TestMethod]
        public void FindVisibleLabelForEntity_ReturnsNull_WhenEntityAddressCannotBeRead()
        {
            Entity opaqueEntity = ExileCoreOpaqueFactory.CreateOpaqueEntity();
            LabelOnGround opaqueLabel = ExileCoreOpaqueFactory.CreateOpaqueLabel();

            LabelOnGround? result = OffscreenPathingMath.FindVisibleLabelForEntity(opaqueEntity, [opaqueLabel]);

            result.Should().BeNull();
        }

        [TestMethod]
        public void FindVisibleLabelForEntity_ReturnsMatchingLabel_WhenGraphShaperProvidesGroundItem()
        {
            Entity entity = OffscreenStickyTargetGraphShaper.CreateActiveStickyEntity(address: 77);
            LabelOnGround label = OffscreenStickyTargetGraphShaper.CreateVisibleLabel(entity);

            LabelOnGround? result = OffscreenPathingMath.FindVisibleLabelForEntity(entity, [label]);

            result.Should().BeSameAs(label);
        }

        [TestMethod]
        public void ShouldForceUiHoverVerificationForLabel_FailsClosed_WhenLabelCannotExposeGroundItem()
        {
            LabelOnGround opaqueLabel = ExileCoreOpaqueFactory.CreateOpaqueLabel();

            OffscreenPathingMath.ShouldForceUiHoverVerificationForLabel(opaqueLabel)
                .Should()
                .BeFalse();
        }

        private static Entity CreateEntityWithAddress(long address)
        {
            Entity entity = ExileCoreOpaqueFactory.CreateOpaqueEntity();
            SetMember(entity, "Address", address);
            return entity;
        }

        private static void SetMember(object instance, string memberName, object value)
        {
            Type? currentType = instance.GetType();
            while (currentType != null)
            {
                FieldInfo? backingField = currentType.GetField($"<{memberName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    ?? currentType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    ?? currentType.GetField($"_{char.ToLowerInvariant(memberName[0])}{memberName[1..]}", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (backingField != null)
                {
                    backingField.SetValue(instance, value);
                    return;
                }

                PropertyInfo? property = currentType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                MethodInfo? setMethod = property?.GetSetMethod(nonPublic: true);
                if (setMethod != null)
                {
                    setMethod.Invoke(instance, [value]);
                    return;
                }

                currentType = currentType.BaseType;
            }

            throw new InvalidOperationException($"Unable to set member '{memberName}' on {instance.GetType().FullName}.");
        }

    }
}