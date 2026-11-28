namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class OffscreenTraversalTargetResolverTests
    {
        [DataTestMethod]
        [TestCategory("Regression")]
        [DataRow(0, true)]
        [DataRow(1, false)]
        [DataRow(-1, false)]
        [DataRow(null, false)]
        public void DelveAzuriteShard_RequiresStationaryMagneticComponent(int? force, bool expected)
        {
            const string path = "Metadata/Terrain/Leagues/Delve/Objects/DelveAzuriteShard";
            Entity shard = EntityProbeFactory.Create(path: path, isTargetable: true);
            if (force.HasValue)
                EntityProbeFactory.WithComponent<Magnetic>(shard, new MagneticProbe { Force = force.Value });

            MechanicClassifier.CanClickGroundAzurite(shard).Should().Be(expected);
            OffscreenPathingMath.IsEligibleDelveTraversalTarget(shard, path).Should().Be(expected);
        }

        [DataTestMethod]
        [TestCategory("Regression")]
        [DataRow(true, true)]
        [DataRow(false, false)]
        public void DelveAzuriteTraversal_RequiresTargetableEntity(bool targetable, bool expected)
        {
            const string path = "Metadata/Terrain/Leagues/Delve/Objects/EncounterControlObjects/AzuriteEncounterController";
            Entity target = EntityProbeFactory.Create(path: path, address: 42, isTargetable: targetable);
            OffscreenPathingMath.IsEligibleDelveTraversalTarget(target, path).Should().Be(expected);
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void OtherTraversalTargets_DoNotGainDelveTargetabilityRequirement()
        {
            Entity target = EntityProbeFactory.Create(path: "Metadata/AreaTransition/Test", isTargetable: false);
            OffscreenPathingMath.IsEligibleDelveTraversalTarget(target, "Metadata/AreaTransition/Test").Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void UnlabelledDelveChest_CanBeApproachedOnScreenOnlyWhileUnopenedAndTargetable()
        {
            const string path = "Metadata/Chests/DelveChests/FossilTest";
            Entity target = EntityProbeFactory.Create(path: path, address: 42, type: EntityType.Chest);
            var chest = new TraversalChestProbe();
            EntityProbeFactory.WithComponent<Chest>(target, chest);

            OffscreenTraversalTargetResolver.CanApproachUnlabelledDelveTarget(target, path, []).Should().BeTrue();
            OffscreenTraversalTargetResolver.CanApproachUnlabelledDelveTarget(target, path, null).Should().BeFalse();
            OffscreenTraversalTargetResolver.CanApproachUnlabelledDelveTarget(target, "Metadata/Terrain/Leagues/Delve/Objects/EncounterSpawners/WaterSpawnerEffect", []).Should().BeFalse();
            chest.IsOpened = true;
            MechanicClassifier.IsUnopenedTargetableDelveChest(target).Should().BeFalse();
            OffscreenTraversalTargetResolver.CanApproachUnlabelledDelveTarget(target, path, []).Should().BeFalse();
            chest.IsOpened = false;
            ((EntityProbe)target).IsTargetable = false;
            OffscreenTraversalTargetResolver.CanApproachUnlabelledDelveTarget(target, path, []).Should().BeFalse();
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void UnlabelledDelveChest_WithVisibleLabel_IsNotApproachedAsUnlabelled()
        {
            const string path = "Metadata/Chests/DelveChests/FossilTest";
            Entity target = EntityProbeFactory.Create(path: path, address: 42, type: EntityType.Chest);
            EntityProbeFactory.WithComponent<Chest>(target, new TraversalChestProbe());
            LabelOnGround label = OffscreenStickyTargetGraphShaper.CreateVisibleLabel(target);

            OffscreenTraversalTargetResolver.CanApproachUnlabelledDelveTarget(target, path, [label]).Should().BeFalse();
        }

        public sealed class TraversalChestProbe : Chest
        {
            public new bool IsOpened { get; set; }
        }

        [TestMethod]
        [TestCategory("Parity")]
        [DataRow("Chests/DelveChests/TangledFossil", true, true)]
        [DataRow("Chests/DelveChests/TangledFossil", false, false)]
        [DataRow("Terrain/Leagues/Delve/Objects/DelveAzuriteShard", true, false)]
        [DataRow("Terrain/Leagues/Delve/Objects/EncounterSpawners/WaterSpawnerEffect", true, false)]
        [DataRow("Terrain/Leagues/Delve/Objects/DelveNode", true, false)]
        public void UnlabelledOnscreenApproach_UsesTheExactDelveAllowlist(string metadata, bool targetable, bool expected)
        {
            string path = "Metadata/" + metadata;
            Entity entity = EntityProbeFactory.Create(path: path, address: 42, isTargetable: targetable);

            OffscreenTraversalTargetResolver.CanApproachUnlabelledDelveTarget(entity, path, []).Should().Be(expected);
        }

        [TestMethod]
        public void ResolveNearestOffscreenWalkTarget_ReturnsNull_AndDoesNotRefreshPriorities_WhenNoCandidateSourcesExist()
        {
            var settings = new ClickItSettings();
            var snapshotProvider = new CountingMechanicPrioritySnapshotProvider();
            var resolver = CreateResolver(settings, snapshotProvider);

            Entity? result = resolver.ResolveNearestOffscreenWalkTarget();

            result.Should().BeNull();
            snapshotProvider.RefreshCalls.Should().Be(0);
        }

        [TestMethod]
        public void ResolveNearestOffscreenWalkTarget_ReturnsNull_WhenShrinesEnabledButEntityGraphIsUnavailable()
        {
            var settings = new ClickItSettings();
            settings.ClickShrines.Value = true;
            var snapshotProvider = new CountingMechanicPrioritySnapshotProvider();
            var resolver = CreateResolver(settings, snapshotProvider);

            Entity? result = resolver.ResolveNearestOffscreenWalkTarget();

            result.Should().BeNull();
            snapshotProvider.RefreshCalls.Should().Be(0);
        }

        [TestMethod]
        public void ResolveNearestOffscreenWalkTarget_ReturnsNull_WhenAreaTransitionsEnabledButEntityGraphIsUnavailable()
        {
            var settings = new ClickItSettings();
            settings.ClickAreaTransitions.Value = true;
            var snapshotProvider = new CountingMechanicPrioritySnapshotProvider();
            var resolver = CreateResolver(settings, snapshotProvider);

            Entity? result = resolver.ResolveNearestOffscreenWalkTarget();

            result.Should().BeNull();
            snapshotProvider.RefreshCalls.Should().Be(0);
        }

        [TestMethod]
        public void ResolveNearestOffscreenWalkTarget_ReturnsNull_WhenEldritchAltarsEnabledButEntityGraphIsUnavailable()
        {
            var settings = new ClickItSettings();
            settings.ClickExarchAltars.Value = true;
            var snapshotProvider = new CountingMechanicPrioritySnapshotProvider();
            var resolver = CreateResolver(settings, snapshotProvider);

            Entity? result = resolver.ResolveNearestOffscreenWalkTarget();

            result.Should().BeNull();
            snapshotProvider.RefreshCalls.Should().Be(0);
        }

        private static OffscreenTraversalTargetResolver CreateResolver(ClickItSettings settings, CountingMechanicPrioritySnapshotProvider snapshotProvider)
        {
            GameController gameController = ExileCoreVisibleObjectBuilder.CreateGameControllerWithWindow(new RectangleF(100f, 200f, 1280f, 720f));
            var runtimeState = new ClickRuntimeState();
            var pathfindingLabelSuppression = new PathfindingLabelSuppressionEvaluator(new PathfindingLabelSuppressionEvaluatorDependencies(settings, runtimeState));

            return new OffscreenTraversalTargetResolver(new OffscreenTraversalTargetResolverDependencies(
                Settings: settings,
                GameController: gameController,
                MechanicPriorityContextProvider: new MechanicPriorityContextProvider(settings, snapshotProvider),
                LabelInteraction: ClickTestServiceFactory.CreateLabelInteractionService(gameController: gameController),
                LabelInteractionPort: ClickTestServiceFactory.CreateNoOpLabelInteractionPort(),
                VisibleLabelSnapshots: new VisibleLabelSnapshotProvider(new TimeCache<List<LabelOnGround>>(() => [], 50)),
                IsClickableInEitherSpace: static (_, _) => false,
                PathfindingLabelSuppression: pathfindingLabelSuppression));
        }

        [TestMethod]
        public void IsOffscreenWalkableStructure_True_ForEldritchAltarPaths()
        {
            OffscreenTraversalTargetResolver.IsOffscreenWalkableStructure(EntityProbeFactory.Create(path: "Metadata/Terrain/CleansingFireAltar"))
                .Should().BeTrue();
            OffscreenTraversalTargetResolver.IsOffscreenWalkableStructure(EntityProbeFactory.Create(path: "Metadata/Terrain/TangleAltar"))
                .Should().BeTrue();
        }

        [TestMethod]
        public void IsOffscreenWalkableStructure_True_ForAreaTransitionPathsAndType()
        {
            OffscreenTraversalTargetResolver.IsOffscreenWalkableStructure(EntityProbeFactory.Create(path: "Metadata/AreaTransition/Zone"))
                .Should().BeTrue();
            OffscreenTraversalTargetResolver.IsOffscreenWalkableStructure(EntityProbeFactory.Create(path: "Metadata/LabyrinthTrial/Portal"))
                .Should().BeTrue();
            OffscreenTraversalTargetResolver.IsOffscreenWalkableStructure(EntityProbeFactory.Create(path: "Metadata/TrialPortal", type: EntityType.AreaTransition))
                .Should().BeTrue();
        }

        [TestMethod]
        public void IsOffscreenWalkableStructure_ExcludesDelveSpawnerEffects()
        {
            OffscreenTraversalTargetResolver.IsOffscreenWalkableStructure(
                EntityProbeFactory.Create(path: "Metadata/Terrain/Leagues/Delve/Objects/EncounterSpawners/WaterSpawnerEffect"))
                .Should().BeFalse();
        }

        [TestMethod]
        public void IsOffscreenWalkableStructure_True_ForShrinePaths()
        {
            OffscreenTraversalTargetResolver.IsOffscreenWalkableStructure(EntityProbeFactory.Create(path: "Metadata/DarkShrine/Shrine"))
                .Should().BeTrue();
        }

        [TestMethod]
        public void IsOffscreenWalkableStructure_True_ForComponentShrineWithoutDarkShrinePath()
        {
            Entity shrine = EntityProbeFactory.Create(path: "Metadata/MiscellaneousObjects/ShroudedShrine");
            EntityProbeFactory.WithComponent<Shrine>(shrine, (Shrine)RuntimeHelpers.GetUninitializedObject(typeof(Shrine)));
            OffscreenTraversalTargetResolver.IsOffscreenWalkableStructure(shrine).Should().BeTrue();
        }

        [TestMethod]
        public void IsOffscreenWalkableStructure_False_ForUnrelatedEntities()
        {
            OffscreenTraversalTargetResolver.IsOffscreenWalkableStructure(EntityProbeFactory.Create(path: "Metadata/Monsters/LeagueBlight/BlightTowerFlameRank3@83"))
                .Should().BeFalse();
            OffscreenTraversalTargetResolver.IsOffscreenWalkableStructure(EntityProbeFactory.Create(path: ""))
                .Should().BeFalse();
            OffscreenTraversalTargetResolver.IsOffscreenWalkableStructure(EntityProbeFactory.Create(path: "Metadata/Monsters/Test", type: EntityType.Monster))
                .Should().BeFalse();
        }

        private sealed class CountingMechanicPrioritySnapshotProvider : IMechanicPrioritySnapshotProvider
        {
            public int RefreshCalls { get; private set; }

            public MechanicPrioritySnapshot Snapshot { get; private set; } = new(
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));

            public MechanicPrioritySnapshot Refresh(
                IReadOnlyList<string> mechanicPriorities,
                IReadOnlyCollection<string> ignoreDistanceIds,
                IReadOnlyDictionary<string, int> ignoreDistanceWithinByMechanicId)
            {
                RefreshCalls++;
                Snapshot = new MechanicPrioritySnapshot(
                    mechanicPriorities.Select((mechanicId, index) => new KeyValuePair<string, int>(mechanicId, index))
                        .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase),
                    new HashSet<string>(ignoreDistanceIds, StringComparer.OrdinalIgnoreCase),
                    new Dictionary<string, int>(ignoreDistanceWithinByMechanicId, StringComparer.OrdinalIgnoreCase));
                return Snapshot;
            }
        }
    }
}
