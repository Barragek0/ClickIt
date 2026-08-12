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
                new ClickSettings { ClickDistance = 999 },
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
        public void TryBuildCandidate_ReturnsFalse_WhenChestIsLocked()
        {
            // A locked chest (the strongbox overlay's red frame) must be rejected even when a
            // mechanic would otherwise match, so it never enters the clickable label scope.
            Entity item = EntityProbeFactory.Create(path: "Metadata/Chests/StrongBoxes/Arcanist");
            EntityProbeFactory.WithComponent<Chest>(item, new LockedChestProbe { IsLocked = true });
            LabelOnGround label = new LabelProbe { ItemOnGround = item };

            int mechanicResolverCallCount = 0;
            bool result = LabelEligibilityEngine.TryBuildCandidate(
                label,
                new ClickSettings { ClickDistance = 999 },
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
            // The locked-chest eligibility rule targets strongboxes; other locked chests are left
            // to their own mechanic and lazy-mode restrictions.
            Entity item = EntityProbeFactory.Create(path: "Metadata/Chests/Standard");
            EntityProbeFactory.WithComponent<Chest>(item, new LockedChestProbe { IsLocked = true });
            LabelOnGround label = new LabelProbe { ItemOnGround = item };

            bool result = LabelEligibilityEngine.TryBuildCandidate(
                label,
                new ClickSettings { ClickDistance = 999 },
                (_, _) => true,
                (_, _, _) => MechanicIds.BasicChests,
                out _,
                out _,
                out LabelCandidateRejectReason rejectReason);

            result.Should().BeTrue();
            rejectReason.Should().Be(LabelCandidateRejectReason.None);
        }
    }
}