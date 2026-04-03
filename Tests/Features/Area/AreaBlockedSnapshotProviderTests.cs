using ClickIt.Features.Area;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpDX;
using System.Collections.Generic;

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
    }
}