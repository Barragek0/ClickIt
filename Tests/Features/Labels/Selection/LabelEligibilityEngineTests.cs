namespace ClickIt.Tests.Features.Labels.Selection
{
    [TestClass]
    public class LabelEligibilityEngineTests
    {
        [TestMethod]
        public void TryBuildCandidate_ReturnsFalse_WhenOpaqueLabelCannotResolveRuntimeItem()
        {
            LabelOnGround label = ExileCoreOpaqueFactory.CreateOpaqueLabel();

            int targetableCallCount = 0;
            int mechanicResolverCallCount = 0;

            bool result = LabelEligibilityEngine.TryBuildCandidate(
                label,
                new ClickSettings(),
                (_, _) =>
                {
                    targetableCallCount++;
                    return true;
                },
                (_, _, _) =>
                {
                    mechanicResolverCallCount++;
                    return MechanicIds.HeistSecureRepository;
                },
                out _,
                out _,
                out LabelCandidateRejectReason rejectReason);

            result.Should().BeFalse();
            rejectReason.Should().Be(LabelCandidateRejectReason.NullItem);
            targetableCallCount.Should().Be(0);
            mechanicResolverCallCount.Should().Be(0);
        }

        [TestMethod]
        public void TryBuildCandidate_DoesNotApplyWorldDistanceLimit_ToScreenEligibility()
        {
            Entity item = EntityProbeFactory.Create(path: "Metadata/Items/Currency/Currency", type: EntityType.WorldItem, distancePlayer: 500f);
            LabelOnGround label = new LabelProbe { ItemOnGround = item };

            bool result = LabelEligibilityEngine.TryBuildCandidate(
                label,
                new ClickSettings(),
                (_, _) => true,
                (_, _, _) => MechanicIds.Items,
                out _,
                out string? mechanicId,
                out LabelCandidateRejectReason rejectReason);

            result.Should().BeTrue("world distance no longer limits viewport-based clicking");
            mechanicId.Should().Be(MechanicIds.Items);
            rejectReason.Should().Be(LabelCandidateRejectReason.None);
        }

        [TestMethod]
        public void TryBuildCandidate_ReturnsFalse_WhenChestIsLocked()
        {
            // A locked chest (the strongbox overlay's red frame) must be rejected even when a mechanic would otherwise match, so it never enters the clickable label scope.
            Entity item = EntityProbeFactory.Create(path: "Metadata/Chests/StrongBoxes/Arcanist", type: EntityType.Chest);
            EntityProbeFactory.WithComponent<Chest>(item, new LockedChestProbe { IsLocked = true });
            LabelOnGround label = new LabelProbe { ItemOnGround = item };

            int mechanicResolverCallCount = 0;
            bool result = LabelEligibilityEngine.TryBuildCandidate(
                label,
                new ClickSettings(),
                (_, _) => true,
                (_, _, _) =>
                {
                    mechanicResolverCallCount++;
                    return MechanicIds.Strongboxes;
                },
                out _,
                out _,
                out LabelCandidateRejectReason rejectReason);

            result.Should().BeFalse();
            rejectReason.Should().Be(LabelCandidateRejectReason.LockedChest);
            mechanicResolverCallCount.Should().Be(0, "the locked-chest rejection fires before mechanic resolution");
        }

        [TestMethod]
        public void TryBuildCandidate_DoesNotRejectLockedNonStrongboxChest()
        {
            // The locked-chest eligibility rule targets strongboxes; other locked chests are left to their own mechanic and lazy-mode restrictions.
            Entity item = EntityProbeFactory.Create(path: "Metadata/Chests/Standard", type: EntityType.Chest);
            EntityProbeFactory.WithComponent<Chest>(item, new LockedChestProbe { IsLocked = true });
            LabelOnGround label = new LabelProbe { ItemOnGround = item };

            bool result = LabelEligibilityEngine.TryBuildCandidate(
                label,
                new ClickSettings(),
                (_, _) => true,
                (_, _, _) => MechanicIds.BasicChests,
                out _,
                out _,
                out LabelCandidateRejectReason rejectReason);

            result.Should().BeTrue();
            rejectReason.Should().Be(LabelCandidateRejectReason.None);
        }

        [TestMethod]
        public void TryBuildCandidate_RejectsLockedDelveChestEvenWhenEntityTypeIsNotChest()
        {
            Entity item = EntityProbeFactory.Create(path: "Metadata/Terrain/Leagues/Delve/Objects/Encounter/Chest", type: EntityType.Monster);
            EntityProbeFactory.WithComponent<Chest>(item, new LockedChestProbe { IsLocked = true });
            LabelOnGround label = new LabelProbe { ItemOnGround = item };

            bool result = LabelEligibilityEngine.TryBuildCandidate(
                label,
                new ClickSettings(),
                (_, _) => true,
                (_, _, _) => MechanicIds.DelveEncounterInitiators,
                out _, out _, out LabelCandidateRejectReason rejectReason);

            result.Should().BeFalse();
            rejectReason.Should().Be(LabelCandidateRejectReason.LockedChest);
        }

        [DataTestMethod]
        [TestCategory("Regression")]
        [DataRow("Metadata/Chests/DelveChests/TangledFossilChest", true)]
        [DataRow("metadata/chests/delvechests/tangledfossilchest", true)]
        [DataRow("Metadata/Chests/DelveChests/OtherFossilChest", false)]
        public void DelveEncounterInitiator_IsNotTreatedAsLockedRewardChest(string path, bool expected)
        {
            Entity item = EntityProbeFactory.Create(path: path, type: EntityType.Chest);
            EntityProbeFactory.WithComponent<Chest>(item, new LockedChestProbe { IsLocked = true });
            LabelOnGround label = new LabelProbe { ItemOnGround = item };

            MechanicClassifier.IsLockedDelveChest(item).Should().Be(!expected);
            bool accepted = LabelEligibilityEngine.TryBuildCandidate(label, new ClickSettings { DelveEncounterComplete = false },
                (_, _) => true, (_, _, _) => MechanicIds.DelveEncounterInitiators, out _, out _, out _);
            accepted.Should().Be(expected);
        }

        [DataTestMethod]
        [TestCategory("Regression")]
        [DataRow("DelveNode", true, 1, true)]
        [DataRow("DelveNodeNoEncounter", true, 1, true)]
        [DataRow("DelveNode", true, 0, false)]
        [DataRow("DelveNode", true, 2, false)]
        [DataRow("DelveNode", false, 1, false)]
        public void DelveChestCompletion_RequiresVisibleNodeAndCompletedState(string node, bool visible, int value, bool expected)
        {
            Entity crawler = EntityProbeFactory.Create(path: "Metadata/Terrain/Leagues/Delve/Objects/" + node);
            var stateMachine = (DelveStateMachineProbe)RuntimeHelpers.GetUninitializedObject(typeof(DelveStateMachineProbe));
            stateMachine.States = [new DelveStateProbe { Name = "encounter_complete", Value = value }];
            EntityProbeFactory.WithComponent<StateMachine>(crawler, stateMachine);
            LabelOnGround crawlerLabel = new LabelProbe { ItemOnGround = crawler, Label = new DelveElementProbe { IsVisible = visible } };
            Entity chest = EntityProbeFactory.Create(path: "Metadata/Chests/DelveChests/DelveChestArmourMovementSpeed", type: EntityType.Chest);
            EntityProbeFactory.WithComponent<Chest>(chest, new LockedChestProbe { IsLocked = true });
            LabelOnGround chestLabel = new LabelProbe { ItemOnGround = chest };

            bool complete = MechanicClassifier.HasVisibleDelveCompletionLabel([crawlerLabel]);
            complete.Should().Be(expected);
            bool accepted = LabelEligibilityEngine.TryBuildCandidate(chestLabel, new ClickSettings { DelveEncounterComplete = complete },
                (_, _) => true, (_, _, _) => MechanicIds.LeagueChests, out _, out _, out _);
            accepted.Should().Be(expected);
        }

        [TestMethod]
        [TestCategory("Boundary")]
        public void DelveChestCompletion_MissingStateDoesNotConfirmCompletion()
        {
            Entity crawler = EntityProbeFactory.Create(path: "Metadata/Terrain/Leagues/Delve/Objects/DelveNode");
            LabelOnGround label = new LabelProbe { ItemOnGround = crawler, Label = new DelveElementProbe { IsVisible = true } };
            MechanicClassifier.HasVisibleDelveCompletionLabel([label]).Should().BeFalse();

            var stateMachine = (DelveStateMachineProbe)RuntimeHelpers.GetUninitializedObject(typeof(DelveStateMachineProbe));
            stateMachine.States = [new DelveStateProbe { Name = "other", Value = 1 }];
            EntityProbeFactory.WithComponent<StateMachine>(crawler, stateMachine);
            MechanicClassifier.HasVisibleDelveCompletionLabel([label]).Should().BeFalse();
        }

        public sealed class DelveElementProbe { public bool IsVisible { get; set; } }
        public sealed class DelveStateProbe { public string Name { get; set; } = string.Empty; public int Value { get; set; } }
        public sealed class DelveStateMachineProbe : StateMachine { public new object[] States { get; set; } = []; }
    }
}
