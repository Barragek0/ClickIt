namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class OffscreenStickyTargetHandlerTests
    {
        [TestMethod]
        public void TryResolveStickyOffscreenTarget_ReturnsFalse_WhenNoStickyTargetAddressIsSet()
        {
            var runtimeState = new ClickRuntimeState();
            var settings = new ClickItSettings();
            var pathfindingLabelSuppression = new PathfindingLabelSuppressionEvaluator(new PathfindingLabelSuppressionEvaluatorDependencies(
                settings,
                runtimeState));
            var handler = new OffscreenStickyTargetHandler(new OffscreenStickyTargetHandlerDependencies(
                GameController: null!,
                ShrineService: null!,
                RuntimeState: runtimeState,
                LabelInteraction: null!,
                ChestLootSettlement: null!,
                IsClickableInEitherSpace: static (_, _) => false,
                PathfindingLabelSuppression: pathfindingLabelSuppression,
                LabelInteractionPort: null!,
                HoldDebugTelemetryAfterSuccess: static _ => { }));

            bool resolved = handler.TryResolveStickyOffscreenTarget(out var target);

            resolved.Should().BeFalse();
            target.Should().BeNull();
            runtimeState.StickyOffscreenTargetAddress.Should().Be(0);
        }

        [TestMethod]
        public void SetStickyOffscreenTarget_StoresEntityAddress()
        {
            var runtimeState = new ClickRuntimeState();
            Entity target = CreateEntityWithAddress(77);
            var handler = CreateHandler(runtimeState, ExileCoreVisibleObjectBuilder.CreateGameControllerWithEntities(target));

            handler.SetStickyOffscreenTarget(target);

            runtimeState.StickyOffscreenTargetAddress.Should().Be(77);
        }

        [TestMethod]
        public void ClearStickyOffscreenTarget_ResetsStickyAddress()
        {
            var runtimeState = new ClickRuntimeState
            {
                StickyOffscreenTargetAddress = 77
            };
            var handler = CreateHandler(runtimeState, ExileCoreVisibleObjectBuilder.CreateGameControllerWithEntities());

            handler.ClearStickyOffscreenTarget();

            runtimeState.StickyOffscreenTargetAddress.Should().Be(0);
        }

        [TestMethod]
        public void IsStickyTarget_ReturnsTrue_WhenEntityAddressMatchesStickyAddress()
        {
            var runtimeState = new ClickRuntimeState
            {
                StickyOffscreenTargetAddress = 77
            };
            Entity target = CreateEntityWithAddress(77);
            var handler = CreateHandler(runtimeState, ExileCoreVisibleObjectBuilder.CreateGameControllerWithEntities(target));

            bool sticky = handler.IsStickyTarget(target);

            sticky.Should().BeTrue();
        }

        [TestMethod]
        public void IsStickyTarget_ReturnsFalse_WhenEntityIsNullOrAddressDiffers()
        {
            var runtimeState = new ClickRuntimeState
            {
                StickyOffscreenTargetAddress = 77
            };
            Entity other = CreateEntityWithAddress(88);
            var handler = CreateHandler(runtimeState, ExileCoreVisibleObjectBuilder.CreateGameControllerWithEntities(other));

            handler.IsStickyTarget(null).Should().BeFalse();
            handler.IsStickyTarget(other).Should().BeFalse();
        }

        [TestMethod]
        public void TryResolveStickyOffscreenTarget_ClearsStickyAddress_WhenEntityCannotBeResolved()
        {
            var runtimeState = new ClickRuntimeState
            {
                StickyOffscreenTargetAddress = 42
            };
            var settings = new ClickItSettings();
            var pathfindingLabelSuppression = new PathfindingLabelSuppressionEvaluator(new PathfindingLabelSuppressionEvaluatorDependencies(
                settings,
                runtimeState));
            var handler = new OffscreenStickyTargetHandler(new OffscreenStickyTargetHandlerDependencies(
                GameController: null!,
                ShrineService: null!,
                RuntimeState: runtimeState,
                LabelInteraction: null!,
                ChestLootSettlement: null!,
                IsClickableInEitherSpace: static (_, _) => false,
                PathfindingLabelSuppression: pathfindingLabelSuppression,
                LabelInteractionPort: null!,
                HoldDebugTelemetryAfterSuccess: static _ => { }));

            bool resolved = handler.TryResolveStickyOffscreenTarget(out var target);

            resolved.Should().BeFalse();
            target.Should().BeNull();
            runtimeState.StickyOffscreenTargetAddress.Should().Be(0);
        }

        [TestMethod]
        public void TryResolveStickyOffscreenTarget_ClearsStickyAddress_WhenEntityStateCannotBeRead()
        {
            var runtimeState = new ClickRuntimeState
            {
                StickyOffscreenTargetAddress = 42
            };
            Entity target = CreateEntityWithAddress(42);
            var handler = CreateHandler(runtimeState, ExileCoreVisibleObjectBuilder.CreateGameControllerWithEntities(target));

            bool resolved = handler.TryResolveStickyOffscreenTarget(out var stickyTarget);

            resolved.Should().BeFalse();
            stickyTarget.Should().BeNull();
            runtimeState.StickyOffscreenTargetAddress.Should().Be(0);
        }

        [TestMethod]
        public void TryResolveStickyOffscreenTarget_ReturnsStickyEntity_WhenGraphShaperProvidesActiveVisibleTarget()
        {
            var runtimeState = new ClickRuntimeState
            {
                StickyOffscreenTargetAddress = 42
            };
            Entity target = OffscreenStickyTargetGraphShaper.CreateActiveStickyEntity(
                address: 42,
                path: "Metadata/Monsters/TestSticky",
                isValid: true,
                isHidden: false,
                isTargetable: true);
            var handler = CreateHandler(runtimeState, ExileCoreVisibleObjectBuilder.CreateGameControllerWithEntities(target));

            bool resolved = handler.TryResolveStickyOffscreenTarget(out var stickyTarget);

            resolved.Should().BeTrue();
            stickyTarget.Should().BeSameAs(target);
            runtimeState.StickyOffscreenTargetAddress.Should().Be(42);
        }

        [TestMethod]
        public void TryClickStickyTargetIfPossible_ReturnsFalse_WhenVisibleLabelCannotBeResolved()
        {
            var runtimeState = new ClickRuntimeState
            {
                StickyOffscreenTargetAddress = 42
            };
            Entity stickyTarget = CreateEntityWithAddress(42);
            var handler = CreateHandler(
                runtimeState,
                ExileCoreVisibleObjectBuilder.CreateGameControllerWithEntities(stickyTarget),
                labelInteraction: ClickTestServiceFactory.CreateLabelInteractionService(),
                labelInteractionPort: new StubLabelInteractionPort());

            bool clicked = handler.TryClickStickyTargetIfPossible(stickyTarget, new Vector2(100f, 200f), allLabels: null);

            clicked.Should().BeFalse();
            runtimeState.StickyOffscreenTargetAddress.Should().Be(42);
        }


        private static OffscreenStickyTargetHandler CreateHandler(
            ClickRuntimeState runtimeState,
            GameController gameController,
            ClickItSettings? settings = null,
            ClickLabelInteractionService? labelInteraction = null,
            ILabelInteractionPort? labelInteractionPort = null)
        {
            settings ??= new ClickItSettings();
            var pathfindingLabelSuppression = new PathfindingLabelSuppressionEvaluator(new PathfindingLabelSuppressionEvaluatorDependencies(
                settings,
                runtimeState));
            var chestLootSettlement = CreateChestLootSettlementTracker(settings, new ChestLootSettlementState());

            return new OffscreenStickyTargetHandler(new OffscreenStickyTargetHandlerDependencies(
                GameController: gameController,
                ShrineService: null!,
                RuntimeState: runtimeState,
                LabelInteraction: labelInteraction ?? ClickTestServiceFactory.CreateLabelInteractionService(gameController: gameController, labelInteractionPort: labelInteractionPort),
                ChestLootSettlement: chestLootSettlement,
                IsClickableInEitherSpace: static (_, _) => false,
                PathfindingLabelSuppression: pathfindingLabelSuppression,
                LabelInteractionPort: labelInteractionPort ?? new StubLabelInteractionPort(),
                HoldDebugTelemetryAfterSuccess: static _ => { }));
        }

        private static ChestLootSettlementTracker CreateChestLootSettlementTracker(
            ClickItSettings settings,
            ChestLootSettlementState state)
        {
            return new ChestLootSettlementTracker(new ChestLootSettlementTrackerDependencies(
                Settings: settings,
                State: state,
                GroundLabelEntityAddresses: new GroundLabelEntityAddressProvider(static () => null),
                ClickDebugPublisher: ClickTestDebugPublisherFactory.Create(),
                LabelInteraction: ClickTestServiceFactory.CreateLabelInteractionService()));
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

        private sealed class StubLabelInteractionPort(string? mechanicId = null) : ILabelInteractionPort
        {
            public SelectionDebugSummary GetSelectionDebugSummary(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
                => default;

            public void LogSelectionDiagnostics(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
            {
            }

            public string? GetMechanicIdForLabel(LabelOnGround? label)
                => mechanicId;

            public LabelOnGround? GetNextLabelToClick(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
                => null;

            public bool ShouldCorruptEssence(LabelOnGround label)
                => false;
        }
    }
}