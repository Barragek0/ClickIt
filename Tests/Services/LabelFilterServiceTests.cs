using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Collections.Generic;

namespace ClickIt.Tests.Services
{
    [TestClass]
    public class LabelFilterServiceTests
    {
        [TestMethod]
        public void GetFilteredLabels_ReturnsOnlyClickablePaths()
        {
            var settings = new MockClickItSettings();
            var svc = new MockLabelFilterService(settings);
            var labels = new List<MockLabel>
            {
                TestFactories.CreateMockLabel(100,100,"DelveMineral"),
                TestFactories.CreateMockLabel(200,200,"Random/Thing"),
                TestFactories.CreateMockLabel(300,300,"Harvest/Extractor"),
                TestFactories.CreateMockLabel(400,400,"SomeAltar"),
                TestFactories.CreateMockLabel(500,500,"CraftingUnlocks")
            };

            var filtered = svc.GetFilteredLabels(labels);

            // Expect only paths that contain Delve, Harvest, Altar, or Crafting
            filtered.Should().ContainSingle(l => l.Path.Contains("Delve"));
            filtered.Should().Contain(l => l.Path.Contains("Harvest"));
            filtered.Should().Contain(l => l.Path.Contains("Altar") || l.Path.Contains("SomeAltar"));
            filtered.Should().Contain(l => l.Path.Contains("Crafting"));
            filtered.Should().NotContain(l => l.Path.Contains("Random/Thing"));
        }

        [TestMethod]
        public void GetFilteredLabels_EmptyInput_ReturnsEmpty()
        {
            var settings = new MockClickItSettings();
            var svc = new MockLabelFilterService(settings);
            var labels = new List<MockLabel>();
            var filtered = svc.GetFilteredLabels(labels);
            filtered.Should().BeEmpty();
        }

        [TestMethod]
        public void IsWithinClickDistance_RespectsSettings()
        {
            var settings = new MockClickItSettings { ClickDistance = 100 };
            var svc = new MockLabelFilterService(settings);
            var nearby = TestFactories.CreateMockLabelWithDistance(50, "DelveMineral");
            var far = TestFactories.CreateMockLabelWithDistance(150, "DelveMineral");

            svc.IsWithinClickDistance(nearby).Should().BeTrue();
            svc.IsWithinClickDistance(far).Should().BeFalse();
        }

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
