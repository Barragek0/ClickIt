namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class SettlersOreTargetSelectorTests
    {
        [TestMethod]
        public void EntityProbeFactory_CreatesEntityReadableBySelectorPaths()
        {
            Entity entity = EntityProbeFactory.Create(
                path: MechanicIds.SettlersVerisiumMarker,
                renderName: "Verisium Node",
                isValid: true,
                isHidden: false,
                address: 1234,
                isTargetable: true,
                type: EntityType.Monster,
                distancePlayer: 15f,
                gridX: 6f,
                gridY: 9f);

            DynamicAccess.TryReadString(entity, static e => e.Path, out string path).Should().BeTrue();
            DynamicAccess.TryReadString(entity, static e => e.RenderName, out string renderName).Should().BeTrue();
            DynamicAccess.TryReadBool(entity, static e => e.IsValid, out bool isValid).Should().BeTrue();
            DynamicAccess.TryReadBool(entity, static e => e.IsHidden, out bool isHidden).Should().BeTrue();
            DynamicAccess.TryGetDynamicValue(entity, static e => e.Address, out object? address).Should().BeTrue();
            DynamicAccess.TryReadBool(entity, static e => e.IsTargetable, out bool isTargetable).Should().BeTrue();
            DynamicAccess.TryGetDynamicValue(entity, static e => e.Type, out object? type).Should().BeTrue();
            DynamicAccess.TryReadFloat(entity, static e => e.DistancePlayer, out float distancePlayer).Should().BeTrue();
            DynamicAccess.TryGetDynamicValue(entity, static e => e.GridPosNum, out object? gridPos).Should().BeTrue();

            path.Should().Be(MechanicIds.SettlersVerisiumMarker);
            renderName.Should().Be("Verisium Node");
            isValid.Should().BeTrue();
            isHidden.Should().BeFalse();
            address.Should().Be(1234L);
            isTargetable.Should().BeTrue();
            type.Should().Be(EntityType.Monster);
            distancePlayer.Should().Be(15f);
            gridPos.Should().Be(new System.Numerics.Vector2(6f, 9f));
        }

        [TestMethod]
        public void ResolveNextSettlersOreCandidate_ReturnsNull_WithoutCollectingLabels_WhenFeatureDisabled()
        {
            bool collectedAddresses = false;
            var selector = new SettlersOreTargetSelector(new SettlersOreTargetSelectorDependencies(
                Settings: new ClickItSettings
                {
                    ClickLostShipmentCrates = new ToggleNode(true),
                    ClickSettlersOre = new ToggleNode(false),
                    ClickDistance = new RangeNode<int>(600, 0, 1000)
                },
                GameController: null!,
                ClickDebugPublisher: ClickTestDebugPublisherFactory.Create(static () => false, static _ => { }),
                DebugLog: static _ => { },
                IsInsideWindowInEitherSpace: static _ => false,
                IsClickableInEitherSpace: static (_, _) => false,
                GroundLabelEntityAddresses: new GroundLabelEntityAddressProvider(() =>
                {
                    collectedAddresses = true;
                    return [];
                })));

            selector.ResolveNextSettlersOreCandidate().Should().BeNull();
            collectedAddresses.Should().BeFalse();
        }

        [TestMethod]
        public void ResolveNextSettlersOreCandidate_ReturnsNull_AndLogsFailure_WhenWindowReadThrows()
        {
            ClickItSettings settings = CreateSettings(clickDistance: 20, debugMode: false);
            List<string> logs = [];

            SettlersOreTargetSelector selector = CreateSelector(
                settings,
                gameController: null!,
                labelsProvider: static () => [],
                debugLog: logs.Add);

            SettlersOreCandidate? candidate = selector.ResolveNextSettlersOreCandidate();

            candidate.Should().BeNull();
            logs.Should().ContainSingle();
            logs[0].Should().Contain("[ResolveNextSettlersOreCandidate] Failed to scan entities:");
        }

        [TestMethod]
        public void ResolveNextSettlersOreCandidate_PublishesNoCandidateDiagnostics_WhenWindowExistsButNoEntitiesMatch()
        {
            ClickItSettings settings = CreateSettings(clickDistance: 20, debugMode: false);
            List<string> logs = [];

            SettlersOreTargetSelector selector = CreateSelector(
                settings,
                gameController: ExileCoreVisibleObjectBuilder.CreateGameControllerWithEntities(),
                labelsProvider: static () => [],
                debugLog: logs.Add,
                shouldCaptureClickDebug: static () => true);

            SettlersOreCandidate? candidate = selector.ResolveNextSettlersOreCandidate();

            candidate.Should().BeNull();
            logs.Should().ContainSingle();
            logs[0].StartsWith("[ResolveNextSettlersOreTargetSelector]", StringComparison.Ordinal).Should().BeFalse();
            logs[0].StartsWith("[ResolveNextSettlersOreCandidate]", StringComparison.Ordinal).Should().BeTrue();
        }

        [TestMethod]
        public void TryResolveSettlersMechanic_UsesSeededEntityMembers_FromEntityProbeFactory()
        {
            ClickItSettings settings = CreateSettings(clickDistance: 20, debugMode: false);
            SettlersOreTargetSelector selector = CreateSelector(
                settings,
                gameController: ExileCoreVisibleObjectBuilder.CreateGameControllerWithEntities(),
                labelsProvider: static () => []);
            Entity entity = EntityProbeFactory.Create(
                path: MechanicIds.SettlersVerisiumMarker,
                isValid: true,
                distancePlayer: 15f);

            object?[] args = [entity, null, null];

            bool resolved = (bool)typeof(SettlersOreTargetSelector)
                .GetMethod("TryResolveSettlersMechanic", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(selector, args)!;

            resolved.Should().BeTrue();
            args[1].Should().Be(MechanicIds.SettlersVerisium);
            args[2].Should().Be(MechanicIds.SettlersVerisiumMarker);
        }

        [TestMethod]
        public void TryResolveSettlersMechanic_RejectsEntityProbe_WhenSeededDistanceExceedsClickDistance()
        {
            ClickItSettings settings = CreateSettings(clickDistance: 20, debugMode: false);
            SettlersOreTargetSelector selector = CreateSelector(
                settings,
                gameController: ExileCoreVisibleObjectBuilder.CreateGameControllerWithEntities(),
                labelsProvider: static () => []);
            Entity entity = EntityProbeFactory.Create(
                path: MechanicIds.SettlersVerisiumMarker,
                isValid: true,
                distancePlayer: 25f);

            object?[] args = [entity, null, null];

            bool resolved = (bool)typeof(SettlersOreTargetSelector)
                .GetMethod("TryResolveSettlersMechanic", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(selector, args)!;

            resolved.Should().BeFalse();
            args[1].Should().Be(string.Empty);
            args[2].Should().Be(MechanicIds.SettlersVerisiumMarker);
        }

        private static SettlersOreTargetSelector CreateSelector(
            ClickItSettings settings,
            GameController gameController,
            Func<IList<LabelOnGround>?> labelsProvider,
            Action<string>? debugLog = null,
            Func<bool>? shouldCaptureClickDebug = null,
            Action<ClickDebugSnapshot>? setLatestClickDebug = null)
        {
            return new SettlersOreTargetSelector(new SettlersOreTargetSelectorDependencies(
                Settings: settings,
                GameController: gameController,
                ClickDebugPublisher: ClickTestDebugPublisherFactory.Create(
                    shouldCaptureClickDebug: shouldCaptureClickDebug,
                    setLatestClickDebug: setLatestClickDebug),
                DebugLog: debugLog ?? (static _ => { }),
                IsInsideWindowInEitherSpace: static _ => false,
                IsClickableInEitherSpace: static (_, _) => false,
                GroundLabelEntityAddresses: new GroundLabelEntityAddressProvider(labelsProvider)));
        }

        private static ClickItSettings CreateSettings(int clickDistance, bool debugMode)
        {
            return new ClickItSettings
            {
                ClickLostShipmentCrates = new ToggleNode(true),
                ClickSettlersOre = new ToggleNode(true),
                ClickSettlersCrimsonIron = new ToggleNode(true),
                ClickSettlersCopper = new ToggleNode(true),
                ClickSettlersPetrifiedWood = new ToggleNode(true),
                ClickSettlersBismuth = new ToggleNode(true),
                ClickSettlersVerisium = new ToggleNode(true),
                ClickDistance = new RangeNode<int>(clickDistance, 0, 1000),
                DebugMode = new ToggleNode(debugMode)
            };
        }
    }
}