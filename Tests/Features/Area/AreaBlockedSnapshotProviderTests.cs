namespace ClickIt.Tests.Features.Area
{
    [TestClass]
    public class AreaBlockedSnapshotProviderTests
    {
        [TestMethod]
        public void ApplySnapshot_ReplacesExistingBlockedRectangleCollections()
        {
            var provider = new AreaBlockedSnapshotProvider();

            provider.ApplySnapshot(new AreaBlockedSnapshot
            {
                BuffsAndDebuffsRectangles = [new RectangleF(1, 1, 10, 10)],
                QuestTrackerBlockedRectangles = [new RectangleF(2, 2, 10, 10)]
            });

            provider.ApplySnapshot(new AreaBlockedSnapshot());
            AreaBlockedSnapshot current = provider.CurrentSnapshot;

            current.BuffsAndDebuffsRectangles.Should().BeEmpty();
            current.QuestTrackerBlockedRectangles.Should().BeEmpty();
        }

        [TestMethod]
        public void ApplySnapshot_PublishesCopiedCollections()
        {
            var provider = new AreaBlockedSnapshotProvider();
            var buffs = new List<RectangleF> { new(10, 10, 20, 20) };
            var quest = new List<RectangleF> { new(30, 30, 40, 40) };

            provider.ApplySnapshot(new AreaBlockedSnapshot
            {
                BuffsAndDebuffsRectangles = buffs,
                QuestTrackerBlockedRectangles = quest
            });

            buffs.Clear();
            quest.Clear();
            AreaBlockedSnapshot current = provider.CurrentSnapshot;

            current.BuffsAndDebuffsRectangles.Should().ContainSingle();
            current.QuestTrackerBlockedRectangles.Should().ContainSingle();
        }

        [TestMethod]
        public void ApplyQuestTrackerRectangles_RetainsExistingRectangles_OnEmptyReadWithinHoldWindow()
        {
            var blockedState = new AreaBlockedState
            {
                LastQuestTrackerRectanglesSuccessTimestampMs = 1_000
            };
            blockedState.QuestTrackerBlockedRectangles.Add(new RectangleF(10, 10, 20, 20));

            AreaBlockedSnapshotProvider.ApplyQuestTrackerRectangles(blockedState, [], now: 1_500);

            blockedState.QuestTrackerBlockedRectangles.Should().ContainSingle();
            blockedState.LastQuestTrackerRectanglesSuccessTimestampMs.Should().Be(1_000);
        }

        [TestMethod]
        public void ApplyQuestTrackerRectangles_ClearsExistingRectangles_OnEmptyReadAfterHoldWindow()
        {
            var blockedState = new AreaBlockedState
            {
                LastQuestTrackerRectanglesSuccessTimestampMs = 1_000
            };
            blockedState.QuestTrackerBlockedRectangles.Add(new RectangleF(10, 10, 20, 20));

            AreaBlockedSnapshotProvider.ApplyQuestTrackerRectangles(blockedState, [], now: 2_500);

            blockedState.QuestTrackerBlockedRectangles.Should().BeEmpty();
            blockedState.LastQuestTrackerRectanglesSuccessTimestampMs.Should().Be(1_000);
        }

        [TestMethod]
        public void SplitBottomAnchoredRectangleFromLeft_ReturnsExpectedPrimaryAndSecondaryRectangles()
        {
            (RectangleF primary, RectangleF secondary) = AreaBlockedSnapshotProvider.SplitBottomAnchoredRectangleFromLeft(
                new RectangleF(0, 0, 100, 40),
                secondaryHeightRatio: 0.625f);

            AssertRectangle(primary, 0, 0, 40, 40);
            AssertRectangle(secondary, 40, 15, 73.3f, 40);
        }

        [TestMethod]
        public void SplitBottomAnchoredRectangleFromRight_ReturnsExpectedPrimaryAndSecondaryRectangles()
        {
            (RectangleF primary, RectangleF secondary) = AreaBlockedSnapshotProvider.SplitBottomAnchoredRectangleFromRight(
                new RectangleF(0, 0, 100, 40),
                secondaryHeightRatio: 0.625f);

            AssertRectangle(primary, 60, 0, 100, 40);
            AssertRectangle(secondary, 26.7f, 15, 60, 40);
        }

        [TestMethod]
        public void SplitBottomAnchoredThreeRectanglesFromLeft_ReturnsExpectedLinkedTertiaryRectangle()
        {
            (RectangleF primary, RectangleF secondary, RectangleF tertiary) = AreaBlockedSnapshotProvider.SplitBottomAnchoredThreeRectanglesFromLeft(
                new RectangleF(0, 0, 100, 40),
                secondaryHeightRatio: 0.625f,
                tertiaryHeightRatio: 0.85f,
                tertiaryWidthRatio: 0.79f);

            AssertRectangle(primary, 0, 0, 40, 40);
            AssertRectangle(secondary, 40, 15, 73.3f, 40);
            AssertRectangle(tertiary, 73.3f, 18.75f, 99.607f, 40);
        }

        [TestMethod]
        public void SplitBottomAnchoredThreeRectanglesFromRight_ReturnsExpectedLinkedTertiaryRectangle()
        {
            (RectangleF primary, RectangleF secondary, RectangleF tertiary) = AreaBlockedSnapshotProvider.SplitBottomAnchoredThreeRectanglesFromRight(
                new RectangleF(0, 0, 100, 40),
                secondaryHeightRatio: 0.625f,
                tertiaryHeightRatio: 0.85f,
                tertiaryWidthRatio: 0.79f);

            AssertRectangle(primary, 60, 0, 100, 40);
            AssertRectangle(secondary, 26.7f, 15, 60, 40);
            AssertRectangle(tertiary, 0.3930006f, 18.75f, 26.7f, 40);
        }

        [TestMethod]
        public void SplitBottomAnchoredRectangleHelpers_ReturnEmpty_WhenSourceHasNoArea()
        {
            AreaBlockedSnapshotProvider.SplitBottomAnchoredRectangleFromLeft(RectangleF.Empty, 0.5f)
                .Should().Be((RectangleF.Empty, RectangleF.Empty));

            AreaBlockedSnapshotProvider.SplitBottomAnchoredRectangleFromRight(RectangleF.Empty, 0.5f)
                .Should().Be((RectangleF.Empty, RectangleF.Empty));

            AreaBlockedSnapshotProvider.SplitBottomAnchoredThreeRectanglesFromLeft(RectangleF.Empty, 0.5f, 0.5f, 0.5f)
                .Should().Be((RectangleF.Empty, RectangleF.Empty, RectangleF.Empty));

            AreaBlockedSnapshotProvider.SplitBottomAnchoredThreeRectanglesFromRight(RectangleF.Empty, 0.5f, 0.5f, 0.5f)
                .Should().Be((RectangleF.Empty, RectangleF.Empty, RectangleF.Empty));
        }

        private static void AssertRectangle(RectangleF actual, float x, float y, float width, float height)
        {
            actual.X.Should().BeApproximately(x, 0.0001f);
            actual.Y.Should().BeApproximately(y, 0.0001f);
            actual.Width.Should().BeApproximately(width, 0.0001f);
            actual.Height.Should().BeApproximately(height, 0.0001f);
        }
    }
}