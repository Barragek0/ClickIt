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

            DynamicAccess.TryReadString(entity, DynamicAccessProfiles.Path, out string path).Should().BeTrue();
            DynamicAccess.TryReadString(entity, DynamicAccessProfiles.RenderName, out string renderName).Should().BeTrue();
            DynamicAccess.TryReadBool(entity, DynamicAccessProfiles.IsValid, out bool isValid).Should().BeTrue();
            DynamicAccess.TryReadBool(entity, DynamicAccessProfiles.IsHidden, out bool isHidden).Should().BeTrue();
            DynamicAccess.TryGetDynamicValue(entity, DynamicAccessProfiles.Address, out object? address).Should().BeTrue();
            DynamicAccess.TryReadBool(entity, DynamicAccessProfiles.IsTargetable, out bool isTargetable).Should().BeTrue();
            DynamicAccess.TryGetDynamicValue(entity, DynamicAccessProfiles.Type, out object? type).Should().BeTrue();
            DynamicAccess.TryReadFloat(entity, DynamicAccessProfiles.DistancePlayer, out float distancePlayer).Should().BeTrue();
            DynamicAccess.TryGetDynamicValue(entity, DynamicAccessProfiles.GridPosNum, out object? gridPos).Should().BeTrue();

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
        public void TryBuildSettlersCandidate_ReturnsFalse_WhenSettlersEntityLacksGroundLabel()
        {
            SettlersOreTargetSelector selector = CreateSelector(
                CreateSettings(clickDistance: 20, debugMode: false),
                gameController: ExileCoreVisibleObjectBuilder.CreateGameControllerWithEntities(),
                labelsProvider: static () => []);
            Entity entity = EntityProbeFactory.Create(
                path: MechanicIds.SettlersVerisiumMarker,
                isValid: true,
                address: 1234,
                distancePlayer: 12f,
                posX: 5f,
                posY: 6f,
                posZ: 7f);

            TryBuildSettlersCandidateResult result = InvokeTryBuildSettlersCandidate(
                selector,
                entity,
                new HashSet<long>(),
                captureClickDebug: false);

            result.Resolved.Should().BeFalse();
            result.Candidate.Should().BeNull();
            result.HasGroundLabel.Should().BeFalse();
            result.MatchedMechanic.Should().BeTrue();
            result.AttemptedProbe.Should().BeFalse();
        }

        [TestMethod]
        public void TryBuildSettlersCandidate_ReturnsFalse_AndPublishesProbeFailedDebug_WhenLabelBackedEntityIsNotClickable()
        {
            ClickDebugSnapshot? latestSnapshot = null;
            Entity entity = EntityProbeFactory.Create(
                path: MechanicIds.SettlersVerisiumMarker,
                isValid: true,
                address: 1234,
                distancePlayer: 12f,
                posX: 5f,
                posY: 6f,
                posZ: 7f);

            SettlersOreTargetSelector selector = CreateSelector(
                CreateSettings(clickDistance: 20, debugMode: false),
                gameController: ExileCoreVisibleObjectBuilder.CreateGameControllerWithWindowAndGame(
                    new RectangleF(100f, 200f, 1280f, 720f)),
                labelsProvider: static () => [],
                shouldCaptureClickDebug: static () => true,
                setLatestClickDebug: snapshot => latestSnapshot = snapshot,
                isInsideWindowInEitherSpace: static _ => true,
                isClickableInEitherSpace: static (_, _) => false);

            TryBuildSettlersCandidateResult result = InvokeTryBuildSettlersCandidate(
                selector,
                entity,
                new HashSet<long> { 1234 },
                captureClickDebug: true);

            result.Resolved.Should().BeFalse();
            result.Candidate.Should().BeNull();
            result.HasGroundLabel.Should().BeTrue();
            result.MatchedMechanic.Should().BeTrue();
            result.AttemptedProbe.Should().BeTrue();
            latestSnapshot.Should().NotBeNull();
            latestSnapshot!.Stage.Should().Be("ProbeFailed");
            latestSnapshot.MechanicId.Should().Be(MechanicIds.SettlersVerisium);
            latestSnapshot.EntityPath.Should().Be(MechanicIds.SettlersVerisiumMarker);
            latestSnapshot.Resolved.Should().BeFalse();
            latestSnapshot.Notes.Should().Be("No nearby clickable point resolved");
        }

        [TestMethod]
        public void TryBuildSettlersCandidate_ReturnsFalse_WhenSettlersMechanicIsDisabled()
        {
            ClickItSettings settings = CreateSettings(clickDistance: 20, debugMode: false);
            settings.ClickSettlersVerisium = new ToggleNode(false);
            Entity entity = EntityProbeFactory.Create(
                path: MechanicIds.SettlersVerisiumMarker,
                isValid: true,
                address: 1234,
                distancePlayer: 12f,
                posX: 5f,
                posY: 6f,
                posZ: 7f);

            SettlersOreTargetSelector selector = CreateSelector(
                settings,
                gameController: ExileCoreVisibleObjectBuilder.CreateGameControllerWithEntities(),
                labelsProvider: static () => [],
                isInsideWindowInEitherSpace: static _ => true,
                isClickableInEitherSpace: static (_, _) => true);

            TryBuildSettlersCandidateResult result = InvokeTryBuildSettlersCandidate(
                selector,
                entity,
                new HashSet<long> { 1234 },
                captureClickDebug: false);

            result.Resolved.Should().BeFalse();
            result.Candidate.Should().BeNull();
            result.HasGroundLabel.Should().BeFalse();
            result.MatchedMechanic.Should().BeFalse();
            result.AttemptedProbe.Should().BeFalse();
        }

        [TestMethod]
        public void TryBuildSettlersCandidate_ReturnsFalse_WhenEntityPathIsNotSettlersOre()
        {
            Entity entity = EntityProbeFactory.Create(
                path: "Metadata/Monsters/Test/NotSettlers",
                isValid: true,
                address: 1234,
                distancePlayer: 9f,
                posX: 9f,
                posY: 8f,
                posZ: 7f);

            SettlersOreTargetSelector selector = CreateSelector(
                CreateSettings(clickDistance: 20, debugMode: false),
                gameController: ExileCoreVisibleObjectBuilder.CreateGameControllerWithWindowAndGame(
                    new RectangleF(100f, 200f, 1280f, 720f)),
                labelsProvider: static () => [],
                isInsideWindowInEitherSpace: static _ => true,
                isClickableInEitherSpace: static (_, _) => true);

            TryBuildSettlersCandidateResult result = InvokeTryBuildSettlersCandidate(
                selector,
                entity,
                new HashSet<long> { 1234 },
                captureClickDebug: false);

            result.Resolved.Should().BeFalse();
            result.Candidate.Should().BeNull();
            result.HasGroundLabel.Should().BeFalse();
            result.MatchedMechanic.Should().BeFalse();
            result.AttemptedProbe.Should().BeFalse();
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
            Action<ClickDebugSnapshot>? setLatestClickDebug = null,
            Func<Vector2, bool>? isInsideWindowInEitherSpace = null,
            Func<Vector2, string, bool>? isClickableInEitherSpace = null)
        {
            return new SettlersOreTargetSelector(new SettlersOreTargetSelectorDependencies(
                Settings: settings,
                GameController: gameController,
                ClickDebugPublisher: ClickTestDebugPublisherFactory.Create(
                    shouldCaptureClickDebug: shouldCaptureClickDebug,
                    setLatestClickDebug: setLatestClickDebug,
                    isClickableInEitherSpace: isClickableInEitherSpace,
                    isInsideWindowInEitherSpace: isInsideWindowInEitherSpace),
                DebugLog: debugLog ?? (static _ => { }),
                IsInsideWindowInEitherSpace: isInsideWindowInEitherSpace ?? (static _ => false),
                IsClickableInEitherSpace: isClickableInEitherSpace ?? (static (_, _) => false),
                GroundLabelEntityAddresses: new GroundLabelEntityAddressProvider(labelsProvider)));
        }

        private static TryBuildSettlersCandidateResult InvokeTryBuildSettlersCandidate(
            SettlersOreTargetSelector selector,
            Entity entity,
            IReadOnlySet<long> labelEntityAddresses,
            bool captureClickDebug)
        {
            object?[] args =
            [
                entity,
                new RectangleF(100f, 200f, 1280f, 720f),
                labelEntityAddresses,
                captureClickDebug,
                null,
                null,
                null,
                null
            ];

            bool resolved = (bool)typeof(SettlersOreTargetSelector)
                .GetMethod("TryBuildSettlersCandidate", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(selector, args)!;

            return new TryBuildSettlersCandidateResult(
                Resolved: resolved,
                Candidate: resolved && args[4] is SettlersOreCandidate candidate ? candidate : null,
                HasGroundLabel: args[5] is true,
                MatchedMechanic: args[6] is true,
                AttemptedProbe: args[7] is true);
        }

        private readonly record struct TryBuildSettlersCandidateResult(
            bool Resolved,
            SettlersOreCandidate? Candidate,
            bool HasGroundLabel,
            bool MatchedMechanic,
            bool AttemptedProbe);

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