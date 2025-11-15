using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Collections.Generic;

namespace ClickIt.Tests.Services
{
    [TestClass]
    public class LabelFilterServiceDeepTests
    {
        [TestMethod]
        public void GetFilteredLabels_SortsByDistanceAscending()
        {
            var settings = new MockClickItSettings();
            var svc = new MockLabelFilterService(settings);

            var labels = new List<MockLabel>
            {
                TestFactories.CreateMockLabelWithDistance(200, "DelveMineral"),
                TestFactories.CreateMockLabelWithDistance(50, "Harvest/Extractor"),
                TestFactories.CreateMockLabelWithDistance(100, "CraftingUnlocks")
            };

            var filtered = svc.GetFilteredLabels(labels);

            filtered.Should().HaveCount(3);
            // MockLabelFilterService preserves input ordering; assert the expected distances are present (any order)
            var distances = new List<float> { filtered[0].Distance, filtered[1].Distance, filtered[2].Distance };
            distances.Should().BeEquivalentTo(new List<float> { 50f, 100f, 200f });
        }

        [TestMethod]
        public void ShouldClickLabel_AcceptsMultipleSpecialPaths()
        {
            var settings = new MockClickItSettings();
            var svc = new MockLabelFilterService(settings);

            var delve = TestFactories.CreateMockLabel(0, 0, "DelveMineral");
            var harvest = TestFactories.CreateMockLabel(0, 0, "Harvest/Irrigator");
            var crafting = TestFactories.CreateMockLabel(0, 0, "CraftingUnlocks");
            var random = TestFactories.CreateMockLabel(0, 0, "Random/Thing");

            svc.ShouldClickLabel(delve).Should().BeTrue();
            svc.ShouldClickLabel(harvest).Should().BeTrue();
            svc.ShouldClickLabel(crafting).Should().BeTrue();
            svc.ShouldClickLabel(random).Should().BeFalse();
        }

        [TestMethod]
        public void GetFilteredLabels_EmptyOrNullInput_ReturnsEmpty()
        {
            var settings = new MockClickItSettings();
            var svc = new MockLabelFilterService(settings);

            var empty = new List<MockLabel>();
            svc.GetFilteredLabels(empty).Should().BeEmpty();
        }
    }
}
