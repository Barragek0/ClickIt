using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using FluentAssertions;
using ClickIt.Services;
using ClickIt.Definitions;
using ExileCore.PoEMemory.Elements;
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
        public void GetGroundLabelSearchLimit_ReturnsFullVisibleLabelCount()
        {
            ClickService.GetGroundLabelSearchLimit(0).Should().Be(0);
            ClickService.GetGroundLabelSearchLimit(1).Should().Be(1);
            ClickService.GetGroundLabelSearchLimit(37).Should().Be(37);
        }

        [TestMethod]
        public void ClearThreadLocalStorageForCurrentThread_ClearsThreadStaticBuffers()
        {
            var addressesField = typeof(ClickService).GetField(
                "_threadGroundLabelEntityAddresses",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var skillEntriesField = typeof(ClickService).GetField(
                "_threadSkillBarEntriesBuffer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            addressesField.Should().NotBeNull();
            skillEntriesField.Should().NotBeNull();

            addressesField!.SetValue(null, new System.Collections.Generic.HashSet<long> { 42L });
            skillEntriesField!.SetValue(null, new System.Collections.Generic.List<object?> { new object() });

            ClickService.ClearThreadLocalStorageForCurrentThread();

            addressesField.GetValue(null).Should().BeNull();
            skillEntriesField.GetValue(null).Should().BeNull();
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
        public void IsPointInsideRectInEitherSpace_ReturnsTrue_ForAbsoluteCursorPoint()
        {
            var rect = new SharpDX.RectangleF(100f, 100f, 30f, 20f);
            var cursorAbsolute = new SharpDX.Vector2(110f, 110f);
            var windowTopLeft = new SharpDX.Vector2(800f, 600f);

            bool inside = ClickService.IsPointInsideRectInEitherSpace(rect, cursorAbsolute, windowTopLeft);

            inside.Should().BeTrue();
        }

        [TestMethod]
        public void IsPointInsideRectInEitherSpace_ReturnsTrue_ForClientSpaceCursorPoint()
        {
            var rect = new SharpDX.RectangleF(10f, 10f, 20f, 20f);
            var cursorAbsolute = new SharpDX.Vector2(814f, 614f);
            var windowTopLeft = new SharpDX.Vector2(800f, 600f);

            bool inside = ClickService.IsPointInsideRectInEitherSpace(rect, cursorAbsolute, windowTopLeft);

            inside.Should().BeTrue();
        }

        [TestMethod]
        public void IsWithinManualCursorMatchDistanceInEitherSpace_ReturnsExpectedValue()
        {
            var cursorAbsolute = new SharpDX.Vector2(814f, 614f);
            var candidateClient = new SharpDX.Vector2(12f, 12f);
            var windowTopLeft = new SharpDX.Vector2(800f, 600f);

            ClickService.IsWithinManualCursorMatchDistanceInEitherSpace(
                cursorAbsolute,
                candidateClient,
                windowTopLeft,
                maxDistancePx: 8f).Should().BeTrue();

            ClickService.IsWithinManualCursorMatchDistanceInEitherSpace(
                cursorAbsolute,
                candidateClient,
                windowTopLeft,
                maxDistancePx: 1f).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldAttemptManualCursorAltarClick_ReturnsTrue_OnlyForAltarLabelWithClickableAltars()
        {
            ClickService.ShouldAttemptManualCursorAltarClick(isAltarLabel: true, hasClickableAltars: true).Should().BeTrue();
            ClickService.ShouldAttemptManualCursorAltarClick(isAltarLabel: true, hasClickableAltars: false).Should().BeFalse();
            ClickService.ShouldAttemptManualCursorAltarClick(isAltarLabel: false, hasClickableAltars: true).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldTreatManualCursorAsHoveringCandidate_ReturnsTrue_WhenEitherSignalMatches()
        {
            ClickService.ShouldTreatManualCursorAsHoveringCandidate(cursorInsideLabelRect: true, cursorNearGroundProjection: false).Should().BeTrue();
            ClickService.ShouldTreatManualCursorAsHoveringCandidate(cursorInsideLabelRect: false, cursorNearGroundProjection: true).Should().BeTrue();
            ClickService.ShouldTreatManualCursorAsHoveringCandidate(cursorInsideLabelRect: true, cursorNearGroundProjection: true).Should().BeTrue();
            ClickService.ShouldTreatManualCursorAsHoveringCandidate(cursorInsideLabelRect: false, cursorNearGroundProjection: false).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldUseManualGroundProjectionForCandidate_DisablesProjection_ForWorldItems()
        {
            ClickService.ShouldUseManualGroundProjectionForCandidate(hasBackingEntity: true, isWorldItem: true).Should().BeFalse();
            ClickService.ShouldUseManualGroundProjectionForCandidate(hasBackingEntity: true, isWorldItem: false).Should().BeTrue();
            ClickService.ShouldUseManualGroundProjectionForCandidate(hasBackingEntity: false, isWorldItem: false).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldReuseTimedLabelCountCache_ReturnsTrue_WhenFreshAndLabelCountMatches()
        {
            bool reuse = ClickService.ShouldReuseTimedLabelCountCache(
                now: 10_000,
                cachedAtMs: 9_900,
                cachedLabelCount: 873,
                currentLabelCount: 873,
                cacheWindowMs: 150);

            reuse.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldReuseTimedLabelCountCache_ReturnsFalse_WhenStaleOrCountChanged()
        {
            ClickService.ShouldReuseTimedLabelCountCache(
                now: 10_000,
                cachedAtMs: 9_700,
                cachedLabelCount: 873,
                currentLabelCount: 873,
                cacheWindowMs: 150).Should().BeFalse();

            ClickService.ShouldReuseTimedLabelCountCache(
                now: 10_000,
                cachedAtMs: 9_900,
                cachedLabelCount: 873,
                currentLabelCount: 872,
                cacheWindowMs: 150).Should().BeFalse();
        }

        [TestMethod]
        public void ResolveVisibleLabelsWithoutForcedCopy_ReturnsSameListReference_ForReadOnlyListSource()
        {
            var label = (LabelOnGround)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(LabelOnGround));
            var labels = new System.Collections.Generic.List<LabelOnGround> { label };

            var resolved = ClickService.ResolveVisibleLabelsWithoutForcedCopy(labels);

            resolved.Should().BeSameAs(labels);
            resolved.Should().HaveCount(1);
        }

        [TestMethod]
        public void ResolveVisibleLabelsWithoutForcedCopy_MaterializesEnumerable_WhenSourceIsNotReadOnlyList()
        {
            var label = (LabelOnGround)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(LabelOnGround));
            System.Collections.Generic.IEnumerable<LabelOnGround> labels = YieldSingleLabel(label);

            var resolved = ClickService.ResolveVisibleLabelsWithoutForcedCopy(labels);

            resolved.Should().NotBeNull();
            resolved.Should().HaveCount(1);
            resolved.Should().NotBeSameAs(labels);
        }

        [TestMethod]
        public void ResolveVisibleLabelsWithoutForcedCopy_ReturnsNull_ForNullOrEmptySources()
        {
            ClickService.ResolveVisibleLabelsWithoutForcedCopy(null).Should().BeNull();

            var emptyList = new System.Collections.Generic.List<LabelOnGround>();
            ClickService.ResolveVisibleLabelsWithoutForcedCopy(emptyList).Should().BeNull();

            System.Collections.Generic.IEnumerable<LabelOnGround> emptyEnumerable = YieldNoLabels();
            ClickService.ResolveVisibleLabelsWithoutForcedCopy(emptyEnumerable).Should().BeNull();
        }

        private static System.Collections.Generic.IEnumerable<LabelOnGround> YieldSingleLabel(LabelOnGround label)
        {
            yield return label;
        }

        private static System.Collections.Generic.IEnumerable<LabelOnGround> YieldNoLabels()
        {
            yield break;
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
        public void ShouldContinuePathfindingWhenLabelActionable_ReturnsFalse_OnlyWhenFullyActionable()
        {
            ClickService.ShouldContinuePathfindingWhenLabelActionable(
                labelInWindow: true,
                labelClickable: true,
                clickPointResolvable: true).Should().BeFalse();

            ClickService.ShouldContinuePathfindingWhenLabelActionable(
                labelInWindow: true,
                labelClickable: true,
                clickPointResolvable: false).Should().BeTrue();

            ClickService.ShouldContinuePathfindingWhenLabelActionable(
                labelInWindow: false,
                labelClickable: true,
                clickPointResolvable: true).Should().BeTrue();
        }

        [TestMethod]
        public void ShouldPathfindToEntityAfterClickPointResolveFailure_EnabledForEssencesAndItems()
        {
            ClickService.ShouldPathfindToEntityAfterClickPointResolveFailure(
                walkTowardOffscreenLabelsEnabled: true,
                hasEntity: true,
                isEntityHidden: false,
                mechanicId: "essences").Should().BeTrue();

            ClickService.ShouldPathfindToEntityAfterClickPointResolveFailure(
                walkTowardOffscreenLabelsEnabled: true,
                hasEntity: true,
                isEntityHidden: false,
                mechanicId: "items").Should().BeTrue();
        }

        [TestMethod]
        public void ShouldPathfindToEntityAfterClickPointResolveFailure_RequiresPathingAndEntity()
        {
            ClickService.ShouldPathfindToEntityAfterClickPointResolveFailure(
                walkTowardOffscreenLabelsEnabled: false,
                hasEntity: true,
                isEntityHidden: false,
                mechanicId: "essences").Should().BeFalse();

            ClickService.ShouldPathfindToEntityAfterClickPointResolveFailure(
                walkTowardOffscreenLabelsEnabled: true,
                hasEntity: false,
                isEntityHidden: false,
                mechanicId: "essences").Should().BeFalse();

            ClickService.ShouldPathfindToEntityAfterClickPointResolveFailure(
                walkTowardOffscreenLabelsEnabled: true,
                hasEntity: true,
                isEntityHidden: true,
                mechanicId: "essences").Should().BeFalse();

            ClickService.ShouldPathfindToEntityAfterClickPointResolveFailure(
                walkTowardOffscreenLabelsEnabled: true,
                hasEntity: true,
                isEntityHidden: false,
                mechanicId: null).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldContinuePathingForSpecialAltarLabel_ReturnsTrue_OnlyWhenPathingEnabledWithEntityAndNoClickableAltars()
        {
            ClickService.ShouldContinuePathingForSpecialAltarLabel(
                walkTowardOffscreenLabelsEnabled: true,
                hasBackingEntity: true,
                isBackingEntityHidden: false,
                hasClickableAltars: false).Should().BeTrue();

            ClickService.ShouldContinuePathingForSpecialAltarLabel(
                walkTowardOffscreenLabelsEnabled: false,
                hasBackingEntity: true,
                isBackingEntityHidden: false,
                hasClickableAltars: false).Should().BeFalse();

            ClickService.ShouldContinuePathingForSpecialAltarLabel(
                walkTowardOffscreenLabelsEnabled: true,
                hasBackingEntity: false,
                isBackingEntityHidden: false,
                hasClickableAltars: false).Should().BeFalse();

            ClickService.ShouldContinuePathingForSpecialAltarLabel(
                walkTowardOffscreenLabelsEnabled: true,
                hasBackingEntity: true,
                isBackingEntityHidden: true,
                hasClickableAltars: false).Should().BeFalse();

            ClickService.ShouldContinuePathingForSpecialAltarLabel(
                walkTowardOffscreenLabelsEnabled: true,
                hasBackingEntity: true,
                isBackingEntityHidden: false,
                hasClickableAltars: true).Should().BeFalse();
        }

        [TestMethod]
        public void ResolveLabelMechanicIdForVisibleCandidateComparison_FallsBackToItems_ForWorldItemLabel()
        {
            ClickService.ResolveLabelMechanicIdForVisibleCandidateComparison(
                resolvedMechanicId: null,
                hasLabel: true,
                isWorldItemLabel: true,
                clickItemsEnabled: true).Should().Be("items");
        }

        [TestMethod]
        public void ResolveLabelMechanicIdForVisibleCandidateComparison_PreservesResolvedMechanic_AndSkipsFallbackWhenDisabled()
        {
            ClickService.ResolveLabelMechanicIdForVisibleCandidateComparison(
                resolvedMechanicId: "essences",
                hasLabel: true,
                isWorldItemLabel: true,
                clickItemsEnabled: true).Should().Be("essences");

            ClickService.ResolveLabelMechanicIdForVisibleCandidateComparison(
                resolvedMechanicId: null,
                hasLabel: true,
                isWorldItemLabel: true,
                clickItemsEnabled: false).Should().BeNull();
        }

        [TestMethod]
        public void ShouldFallbackToSettlersEntityClickAfterLabelResolveFailure_ReturnsTrue_ForMatchingSettlersMechanic()
        {
            ClickService.ShouldFallbackToSettlersEntityClickAfterLabelResolveFailure(
                labelMechanicId: "settlers-verisium",
                settlersCandidateMechanicId: "settlers-verisium").Should().BeTrue();
        }

        [TestMethod]
        public void ShouldFallbackToSettlersEntityClickAfterLabelResolveFailure_ReturnsFalse_ForMismatchedOrNonSettlersMechanic()
        {
            ClickService.ShouldFallbackToSettlersEntityClickAfterLabelResolveFailure(
                labelMechanicId: "settlers-verisium",
                settlersCandidateMechanicId: "settlers-sulphite").Should().BeFalse();

            ClickService.ShouldFallbackToSettlersEntityClickAfterLabelResolveFailure(
                labelMechanicId: "items",
                settlersCandidateMechanicId: "settlers-verisium").Should().BeFalse();

            ClickService.ShouldFallbackToSettlersEntityClickAfterLabelResolveFailure(
                labelMechanicId: "settlers-verisium",
                settlersCandidateMechanicId: null).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldPrioritizeOnscreenMechanicsOverOffscreenPathing_ReturnsFalse_WhenSettingDisabled()
        {
            ClickService.ShouldPrioritizeOnscreenMechanicsOverOffscreenPathing(
                prioritizeOnscreenClickableMechanics: false,
                hasClickableAltar: true,
                hasClickableShrine: true,
                hasClickableLostShipment: true,
                hasClickableSettlersOre: true).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldPrioritizeOnscreenMechanicsOverOffscreenPathing_ReturnsTrue_WhenAnyOnscreenMechanicIsClickable()
        {
            ClickService.ShouldPrioritizeOnscreenMechanicsOverOffscreenPathing(
                prioritizeOnscreenClickableMechanics: true,
                hasClickableAltar: false,
                hasClickableShrine: true,
                hasClickableLostShipment: false,
                hasClickableSettlersOre: false).Should().BeTrue();

            ClickService.ShouldPrioritizeOnscreenMechanicsOverOffscreenPathing(
                prioritizeOnscreenClickableMechanics: true,
                hasClickableAltar: false,
                hasClickableShrine: false,
                hasClickableLostShipment: false,
                hasClickableSettlersOre: false).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldEvaluateOnscreenMechanicChecks_ReturnsFalse_WhenPrioritizationDisabled()
        {
            ClickService.ShouldEvaluateOnscreenMechanicChecks(
                prioritizeOnscreenClickableMechanics: false,
                clickShrinesEnabled: true,
                clickLostShipmentEnabled: true,
                clickSettlersOreEnabled: true,
                clickEaterAltarsEnabled: true,
                clickExarchAltarsEnabled: true).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldEvaluateOnscreenMechanicChecks_ReturnsFalse_WhenNoMechanicFeaturesEnabled()
        {
            ClickService.ShouldEvaluateOnscreenMechanicChecks(
                prioritizeOnscreenClickableMechanics: true,
                clickShrinesEnabled: false,
                clickLostShipmentEnabled: false,
                clickSettlersOreEnabled: false,
                clickEaterAltarsEnabled: false,
                clickExarchAltarsEnabled: false).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldEvaluateOnscreenMechanicChecks_ReturnsTrue_WhenAnyMechanicFeatureEnabled()
        {
            ClickService.ShouldEvaluateOnscreenMechanicChecks(
                prioritizeOnscreenClickableMechanics: true,
                clickShrinesEnabled: false,
                clickLostShipmentEnabled: true,
                clickSettlersOreEnabled: false,
                clickEaterAltarsEnabled: false,
                clickExarchAltarsEnabled: false).Should().BeTrue();
        }

        [TestMethod]
        public void ShouldEvaluateAltarScan_ReturnsTrue_WhenAnyAltarToggleEnabled()
        {
            ClickService.ShouldEvaluateAltarScan(clickEaterEnabled: true, clickExarchEnabled: false).Should().BeTrue();
            ClickService.ShouldEvaluateAltarScan(clickEaterEnabled: false, clickExarchEnabled: true).Should().BeTrue();
        }

        [TestMethod]
        public void ShouldEvaluateAltarScan_ReturnsFalse_WhenBothAltarTogglesDisabled()
        {
            ClickService.ShouldEvaluateAltarScan(clickEaterEnabled: false, clickExarchEnabled: false).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldScanSettlersGroundLabelAddresses_ReturnsTrue_WhenCapturingClickDebug()
        {
            ClickService.ShouldScanSettlersGroundLabelAddresses(captureClickDebug: true).Should().BeTrue();
        }

        [TestMethod]
        public void ShouldScanSettlersGroundLabelAddresses_ReturnsFalse_WhenNotCapturingClickDebug()
        {
            ClickService.ShouldScanSettlersGroundLabelAddresses(captureClickDebug: false).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldSkipOffscreenPathfindingForRitual_ReturnsTrue_WhenRitualIsActive()
        {
            ClickService.ShouldSkipOffscreenPathfindingForRitual(ritualActive: true).Should().BeTrue();
        }

        [TestMethod]
        public void ShouldSkipOffscreenPathfindingForRitual_ReturnsFalse_WhenRitualIsNotActive()
        {
            ClickService.ShouldSkipOffscreenPathfindingForRitual(ritualActive: false).Should().BeFalse();
        }

        [TestMethod]
        public void AreBothAltarOptionsActionable_ReturnsTrue_OnlyWhenTopAndBottomAreActionable()
        {
            ClickService.AreBothAltarOptionsActionable(
                topVisibleAndClickable: true,
                bottomVisibleAndClickable: true).Should().BeTrue();

            ClickService.AreBothAltarOptionsActionable(
                topVisibleAndClickable: true,
                bottomVisibleAndClickable: false).Should().BeFalse();

            ClickService.AreBothAltarOptionsActionable(
                topVisibleAndClickable: false,
                bottomVisibleAndClickable: true).Should().BeFalse();

            ClickService.AreBothAltarOptionsActionable(
                topVisibleAndClickable: false,
                bottomVisibleAndClickable: false).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldRetryLabelClickPointWithoutClickableArea_ReturnsTrue_ForSettlersMechanics()
        {
            ClickService.ShouldRetryLabelClickPointWithoutClickableArea("settlers-verisium").Should().BeTrue();
            ClickService.ShouldRetryLabelClickPointWithoutClickableArea("settlers-copper").Should().BeTrue();
        }

        [TestMethod]
        public void ShouldRetryLabelClickPointWithoutClickableArea_ReturnsFalse_ForNonSettlers()
        {
            ClickService.ShouldRetryLabelClickPointWithoutClickableArea("items").Should().BeFalse();
            ClickService.ShouldRetryLabelClickPointWithoutClickableArea("shrines").Should().BeFalse();
            ClickService.ShouldRetryLabelClickPointWithoutClickableArea(null).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldAllowSettlersRelaxedClickPointFallback_AllowsOnlyWhenBackingEntityIsOffscreen()
        {
            ClickService.ShouldAllowSettlersRelaxedClickPointFallback(
                hasBackingEntity: true,
                worldProjectionInWindow: false).Should().BeTrue();

            ClickService.ShouldAllowSettlersRelaxedClickPointFallback(
                hasBackingEntity: true,
                worldProjectionInWindow: true).Should().BeFalse();

            ClickService.ShouldAllowSettlersRelaxedClickPointFallback(
                hasBackingEntity: false,
                worldProjectionInWindow: false).Should().BeFalse();
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
        public void GetEldritchAltarMechanicIdForPath_ReturnsSearingExarch_WhenPathMatchesAndEnabled()
        {
            string? mechanicId = ClickService.GetEldritchAltarMechanicIdForPath(
                clickExarchAltars: true,
                clickEaterAltars: false,
                path: "Metadata/MiscellaneousObjects/PrimordialBosses/CleansingFireAltar");

            mechanicId.Should().Be(MechanicIds.AltarsSearingExarch);
        }

        [TestMethod]
        public void GetEldritchAltarMechanicIdForPath_ReturnsEaterOfWorlds_WhenPathMatchesAndEnabled()
        {
            string? mechanicId = ClickService.GetEldritchAltarMechanicIdForPath(
                clickExarchAltars: false,
                clickEaterAltars: true,
                path: "Metadata/MiscellaneousObjects/PrimordialBosses/TangleAltar");

            mechanicId.Should().Be(MechanicIds.AltarsEaterOfWorlds);
        }

        [TestMethod]
        public void GetEldritchAltarMechanicIdForPath_ReturnsNull_WhenDisabledOrUnrecognized()
        {
            ClickService.GetEldritchAltarMechanicIdForPath(
                clickExarchAltars: false,
                clickEaterAltars: false,
                path: "Metadata/MiscellaneousObjects/PrimordialBosses/CleansingFireAltar").Should().BeNull();

            ClickService.GetEldritchAltarMechanicIdForPath(
                clickExarchAltars: true,
                clickEaterAltars: true,
                path: "Metadata/MiscellaneousObjects/PrimordialBosses/SomeOtherAltar").Should().BeNull();
        }

        [TestMethod]
        public void ShouldDropStickyTargetForUntargetableEldritchAltar_DropsOnlyUntargetableAltars()
        {
            ClickService.ShouldDropStickyTargetForUntargetableEldritchAltar(
                isEldritchAltar: true,
                isTargetable: false).Should().BeTrue();

            ClickService.ShouldDropStickyTargetForUntargetableEldritchAltar(
                isEldritchAltar: true,
                isTargetable: true).Should().BeFalse();

            ClickService.ShouldDropStickyTargetForUntargetableEldritchAltar(
                isEldritchAltar: false,
                isTargetable: false).Should().BeFalse();
        }

        [TestMethod]
        public void IsEldritchAltarPath_ReturnsTrueForKnownAltarPaths()
        {
            ClickService.IsEldritchAltarPath("Metadata/MiscellaneousObjects/PrimordialBosses/CleansingFireAltar").Should().BeTrue();
            ClickService.IsEldritchAltarPath("Metadata/MiscellaneousObjects/PrimordialBosses/TangleAltar").Should().BeTrue();
            ClickService.IsEldritchAltarPath("Metadata/MiscellaneousObjects/PrimordialBosses/OtherObject").Should().BeFalse();
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
        public void ShouldWaitForChestLootSettlement_ReturnsTrueForEnabledChestMechanics()
        {
            ClickService.ShouldWaitForChestLootSettlement(
                mechanicId: "basic-chests",
                waitAfterOpeningBasicChests: true,
                waitAfterOpeningLeagueChests: false).Should().BeTrue();

            ClickService.ShouldWaitForChestLootSettlement(
                mechanicId: "league-chests",
                waitAfterOpeningBasicChests: false,
                waitAfterOpeningLeagueChests: true).Should().BeTrue();
        }

        [TestMethod]
        public void ShouldWaitForChestLootSettlement_ReturnsFalseWhenDisabledOrNonChestMechanic()
        {
            ClickService.ShouldWaitForChestLootSettlement(
                mechanicId: "basic-chests",
                waitAfterOpeningBasicChests: false,
                waitAfterOpeningLeagueChests: true).Should().BeFalse();

            ClickService.ShouldWaitForChestLootSettlement(
                mechanicId: "items",
                waitAfterOpeningBasicChests: true,
                waitAfterOpeningLeagueChests: true).Should().BeFalse();
        }

        [TestMethod]
        public void ResolvePostChestLootSettlementTimingSettings_UsesBasicChestTiming()
        {
            ClickService.ResolvePostChestLootSettlementTimingSettings(
                mechanicId: "basic-chests",
                basicInitialDelayMs: 650,
                basicPollIntervalMs: 120,
                basicQuietWindowMs: 900,
                leagueInitialDelayMs: 500,
                leaguePollIntervalMs: 100,
                leagueQuietWindowMs: 500,
                out int initialDelayMs,
                out int pollIntervalMs,
                out int quietWindowMs);

            initialDelayMs.Should().Be(650);
            pollIntervalMs.Should().Be(120);
            quietWindowMs.Should().Be(900);
        }

        [TestMethod]
        public void ResolvePostChestLootSettlementTimingSettings_UsesLeagueChestTiming()
        {
            ClickService.ResolvePostChestLootSettlementTimingSettings(
                mechanicId: "league-chests",
                basicInitialDelayMs: 500,
                basicPollIntervalMs: 100,
                basicQuietWindowMs: 500,
                leagueInitialDelayMs: 750,
                leaguePollIntervalMs: 140,
                leagueQuietWindowMs: 1100,
                out int initialDelayMs,
                out int pollIntervalMs,
                out int quietWindowMs);

            initialDelayMs.Should().Be(750);
            pollIntervalMs.Should().Be(140);
            quietWindowMs.Should().Be(1100);
        }

        [TestMethod]
        public void ResolvePostChestLootSettlementTimingSettings_ClampsInvalidValues()
        {
            ClickService.ResolvePostChestLootSettlementTimingSettings(
                mechanicId: "basic-chests",
                basicInitialDelayMs: -10,
                basicPollIntervalMs: 0,
                basicQuietWindowMs: -50,
                leagueInitialDelayMs: 500,
                leaguePollIntervalMs: 100,
                leagueQuietWindowMs: 500,
                out int initialDelayMs,
                out int pollIntervalMs,
                out int quietWindowMs);

            initialDelayMs.Should().Be(0);
            pollIntervalMs.Should().Be(1);
            quietWindowMs.Should().Be(0);
        }

        [TestMethod]
        public void ShouldContinueChestOpenRetries_ReturnsTrue_OnlyWhenPendingAndVisible()
        {
            ClickService.ShouldContinueChestOpenRetries(
                pendingChestOpenConfirmationActive: true,
                chestLabelVisible: true).Should().BeTrue();

            ClickService.ShouldContinueChestOpenRetries(
                pendingChestOpenConfirmationActive: true,
                chestLabelVisible: false).Should().BeFalse();

            ClickService.ShouldContinueChestOpenRetries(
                pendingChestOpenConfirmationActive: false,
                chestLabelVisible: true).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldStartChestLootSettlementAfterClick_ReturnsTrue_OnlyWhenPendingAndLabelGone()
        {
            ClickService.ShouldStartChestLootSettlementAfterClick(
                pendingChestOpenConfirmationActive: true,
                chestLabelVisible: false).Should().BeTrue();

            ClickService.ShouldStartChestLootSettlementAfterClick(
                pendingChestOpenConfirmationActive: true,
                chestLabelVisible: true).Should().BeFalse();

            ClickService.ShouldStartChestLootSettlementAfterClick(
                pendingChestOpenConfirmationActive: false,
                chestLabelVisible: false).Should().BeFalse();
        }

        [TestMethod]
        public void IsChestLootSettlementQuietPeriodElapsed_TracksRemainingWindow()
        {
            ClickService.IsChestLootSettlementQuietPeriodElapsed(
                now: 5_000,
                lastNewGroundItemTimestampMs: 4_700,
                quietWindowMs: 500,
                out long remainingMs).Should().BeFalse();
            remainingMs.Should().Be(200);

            ClickService.IsChestLootSettlementQuietPeriodElapsed(
                now: 5_300,
                lastNewGroundItemTimestampMs: 4_700,
                quietWindowMs: 500,
                out long settledRemainingMs).Should().BeTrue();
            settledRemainingMs.Should().Be(0);

            ClickService.IsChestLootSettlementQuietPeriodElapsed(
                now: 5_300,
                lastNewGroundItemTimestampMs: 0,
                quietWindowMs: 500,
                out long unknownRemainingMs).Should().BeFalse();
            unknownRemainingMs.Should().Be(500);

            ClickService.IsChestLootSettlementQuietPeriodElapsed(
                now: 5_300,
                lastNewGroundItemTimestampMs: 5_200,
                quietWindowMs: 0,
                out long zeroWindowRemainingMs).Should().BeTrue();
            zeroWindowRemainingMs.Should().Be(0);
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
        public void ShouldUseHoldClickForSettlersMechanic_ReturnsTrue_OnlyForVerisium()
        {
            ClickService.ShouldUseHoldClickForSettlersMechanic("settlers-verisium").Should().BeTrue();
            ClickService.ShouldUseHoldClickForSettlersMechanic("settlers-copper").Should().BeFalse();
            ClickService.ShouldUseHoldClickForSettlersMechanic("settlers-bismuth").Should().BeFalse();
            ClickService.ShouldUseHoldClickForSettlersMechanic("settlers-crimson-iron").Should().BeFalse();
            ClickService.ShouldUseHoldClickForSettlersMechanic("settlers-petrified-wood").Should().BeFalse();
            ClickService.ShouldUseHoldClickForSettlersMechanic(null).Should().BeFalse();
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
        public void ShouldSkipSettlersEntityBeforeMechanicResolution_RespectsValidityAndDistance()
        {
            ClickService.ShouldSkipSettlersEntityBeforeMechanicResolution(
                isValid: true,
                isHidden: true,
                distance: 10f,
            clickDistance: 100).Should().BeFalse();

            ClickService.ShouldSkipSettlersEntityBeforeMechanicResolution(
                isValid: false,
                isHidden: false,
                distance: 10f,
                clickDistance: 100).Should().BeTrue();

            ClickService.ShouldSkipSettlersEntityBeforeMechanicResolution(
                isValid: true,
                isHidden: false,
                distance: 150f,
                clickDistance: 100).Should().BeTrue();

            ClickService.ShouldSkipSettlersEntityBeforeMechanicResolution(
                isValid: true,
                isHidden: false,
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

        [TestMethod]
        public void ShouldPreferSettlersOreOverVisibleCandidates_RespectsPriorityAgainstAllNonSettlersLabelMechanics()
        {
            var settings = new ClickItSettings();
            var allMechanics = settings.GetMechanicPriorityOrder();

            string[] settlersMechanics =
            [
                MechanicIds.SettlersCrimsonIron,
                MechanicIds.SettlersCopper,
                MechanicIds.SettlersPetrifiedWood,
                MechanicIds.SettlersBismuth,
                MechanicIds.SettlersVerisium
            ];

            var nonSettlersMechanics = allMechanics
                .Where(x => !settlersMechanics.Contains(x, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var ignoreSet = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ignoreDistanceWithinByMechanicId = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (string settlersMechanic in settlersMechanics)
            {
                foreach (string nonSettlersMechanic in nonSettlersMechanics)
                {
                    var priorityMap = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                    {
                        [nonSettlersMechanic] = 0,
                        [settlersMechanic] = 1
                    };

                    bool preferSettlersOre = ClickService.ShouldPreferSettlersOreOverVisibleCandidates(
                        settlersOreDistance: 80f,
                        settlersOreMechanicId: settlersMechanic,
                        labelDistance: 80f,
                        labelMechanicId: nonSettlersMechanic,
                        shrineDistance: null,
                        lostShipmentDistance: null,
                        priorityIndexMap: priorityMap,
                        ignoreDistanceSet: ignoreSet,
                        ignoreDistanceWithinByMechanicId: ignoreDistanceWithinByMechanicId,
                        priorityDistancePenalty: 100);

                    preferSettlersOre.Should().BeFalse($"{nonSettlersMechanic} should beat {settlersMechanic} when ranked higher");
                }
            }
        }

        [TestMethod]
        public void ShouldPreferSettlersOreOverVisibleCandidates_RespectsPriorityAgainstShrineAndLostShipmentChannels()
        {
            string settlersMechanic = MechanicIds.SettlersVerisium;
            var ignoreSet = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ignoreDistanceWithinByMechanicId = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            var higherShrineMap = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [MechanicIds.Shrines] = 0,
                [settlersMechanic] = 1,
                [MechanicIds.LostShipment] = 2
            };

            bool preferSettlersWithHigherShrine = ClickService.ShouldPreferSettlersOreOverVisibleCandidates(
                settlersOreDistance: 80f,
                settlersOreMechanicId: settlersMechanic,
                labelDistance: null,
                labelMechanicId: null,
                shrineDistance: 80f,
                lostShipmentDistance: null,
                priorityIndexMap: higherShrineMap,
                ignoreDistanceSet: ignoreSet,
                ignoreDistanceWithinByMechanicId: ignoreDistanceWithinByMechanicId,
                priorityDistancePenalty: 100);

            preferSettlersWithHigherShrine.Should().BeFalse();

            var higherLostShipmentMap = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [MechanicIds.LostShipment] = 0,
                [settlersMechanic] = 1,
                [MechanicIds.Shrines] = 2
            };

            bool preferSettlersWithHigherLost = ClickService.ShouldPreferSettlersOreOverVisibleCandidates(
                settlersOreDistance: 80f,
                settlersOreMechanicId: settlersMechanic,
                labelDistance: null,
                labelMechanicId: null,
                shrineDistance: null,
                lostShipmentDistance: 80f,
                priorityIndexMap: higherLostShipmentMap,
                ignoreDistanceSet: ignoreSet,
                ignoreDistanceWithinByMechanicId: ignoreDistanceWithinByMechanicId,
                priorityDistancePenalty: 100);

            preferSettlersWithHigherLost.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldPreferSettlersOreOverVisibleCandidates_PenaltyZero_UsesClosestDistance()
        {
            var priorityMap = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [MechanicIds.SettlersVerisium] = 4,
                [MechanicIds.Shrines] = 0,
                [MechanicIds.LostShipment] = 1,
                ["items"] = 2
            };

            var ignoreSet = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ignoreDistanceWithinByMechanicId = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            bool preferSettlersWhenClosest = ClickService.ShouldPreferSettlersOreOverVisibleCandidates(
                settlersOreDistance: 40f,
                settlersOreMechanicId: MechanicIds.SettlersVerisium,
                labelDistance: 60f,
                labelMechanicId: "items",
                shrineDistance: 50f,
                lostShipmentDistance: 55f,
                priorityIndexMap: priorityMap,
                ignoreDistanceSet: ignoreSet,
                ignoreDistanceWithinByMechanicId: ignoreDistanceWithinByMechanicId,
                priorityDistancePenalty: 0);

            preferSettlersWhenClosest.Should().BeTrue();

            bool preferSettlersWhenFarther = ClickService.ShouldPreferSettlersOreOverVisibleCandidates(
                settlersOreDistance: 70f,
                settlersOreMechanicId: MechanicIds.SettlersVerisium,
                labelDistance: 60f,
                labelMechanicId: "items",
                shrineDistance: 50f,
                lostShipmentDistance: 55f,
                priorityIndexMap: priorityMap,
                ignoreDistanceSet: ignoreSet,
                ignoreDistanceWithinByMechanicId: ignoreDistanceWithinByMechanicId,
                priorityDistancePenalty: 0);

            preferSettlersWhenFarther.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldPreferLostShipmentOverVisibleCandidates_PenaltyZero_UsesClosestDistance()
        {
            var priorityMap = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [MechanicIds.Shrines] = 0,
                [MechanicIds.LostShipment] = 3,
                ["items"] = 1
            };

            var ignoreSet = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ignoreDistanceWithinByMechanicId = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            bool preferLostWhenClosest = ClickService.ShouldPreferLostShipmentOverVisibleCandidates(
                lostShipmentDistance: 25f,
                labelDistance: 35f,
                labelMechanicId: "items",
                shrineDistance: 40f,
                priorityIndexMap: priorityMap,
                ignoreDistanceSet: ignoreSet,
                ignoreDistanceWithinByMechanicId: ignoreDistanceWithinByMechanicId,
                priorityDistancePenalty: 0);

            preferLostWhenClosest.Should().BeTrue();

            bool preferLostWhenFarther = ClickService.ShouldPreferLostShipmentOverVisibleCandidates(
                lostShipmentDistance: 45f,
                labelDistance: 35f,
                labelMechanicId: "items",
                shrineDistance: 40f,
                priorityIndexMap: priorityMap,
                ignoreDistanceSet: ignoreSet,
                ignoreDistanceWithinByMechanicId: ignoreDistanceWithinByMechanicId,
                priorityDistancePenalty: 0);

            preferLostWhenFarther.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldPreferSettlersOreOverVisibleCandidates_PenaltyZero_IgnoreDistanceMechanicStillWorks()
        {
            var priorityMap = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [MechanicIds.SettlersVerisium] = 1,
                ["items"] = 2,
                [MechanicIds.Shrines] = 3,
                [MechanicIds.LostShipment] = 4
            };

            var ignoreSet = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                MechanicIds.SettlersVerisium
            };

            var ignoreDistanceWithinByMechanicId = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [MechanicIds.SettlersVerisium] = 120
            };

            bool preferIgnoredWithinRange = ClickService.ShouldPreferSettlersOreOverVisibleCandidates(
                settlersOreDistance: 100f,
                settlersOreMechanicId: MechanicIds.SettlersVerisium,
                labelDistance: 80f,
                labelMechanicId: "items",
                shrineDistance: null,
                lostShipmentDistance: null,
                priorityIndexMap: priorityMap,
                ignoreDistanceSet: ignoreSet,
                ignoreDistanceWithinByMechanicId: ignoreDistanceWithinByMechanicId,
                priorityDistancePenalty: 0);

            preferIgnoredWithinRange.Should().BeTrue();

            bool preferIgnoredOutOfRange = ClickService.ShouldPreferSettlersOreOverVisibleCandidates(
                settlersOreDistance: 130f,
                settlersOreMechanicId: MechanicIds.SettlersVerisium,
                labelDistance: 80f,
                labelMechanicId: "items",
                shrineDistance: null,
                lostShipmentDistance: null,
                priorityIndexMap: priorityMap,
                ignoreDistanceSet: ignoreSet,
                ignoreDistanceWithinByMechanicId: ignoreDistanceWithinByMechanicId,
                priorityDistancePenalty: 0);

            preferIgnoredOutOfRange.Should().BeFalse();
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
