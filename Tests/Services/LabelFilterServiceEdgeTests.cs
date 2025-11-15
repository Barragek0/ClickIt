using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;

namespace ClickIt.Tests.Services
{
    [TestClass]
    public class LabelFilterServiceEdgeTests
    {
        [TestMethod]
        public void ShouldClickLabel_ReturnsFalseForNullOrEmptyPath()
        {
            var settings = new MockClickItSettings();
            var svc = new MockLabelFilterService(settings);

            var labelEmpty = TestFactories.CreateMockLabel(10, 10, "");
            svc.ShouldClickLabel(labelEmpty).Should().BeFalse();

            var labelNull = TestFactories.CreateMockLabel(20, 20, null);
            // CreateMockLabel sets Path; mimic a null path manually
            labelNull.Path = null;
            svc.ShouldClickLabel(labelNull).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickLabel_IsCaseSensitive_CurrentBehavior()
        {
            var settings = new MockClickItSettings();
            var svc = new MockLabelFilterService(settings);

            var label = TestFactories.CreateMockLabel(0, 0, "delvemineral");
            // current implementation uses Contains with exact casing, so lower-case won't match
            svc.ShouldClickLabel(label).Should().BeFalse();

            var labelProper = TestFactories.CreateMockLabel(0, 0, "DelveMineral");
            svc.ShouldClickLabel(labelProper).Should().BeTrue();
        }

        [TestMethod]
        public void IsWithinClickDistance_ReturnsTrueAtExactThreshold()
        {
            var settings = new MockClickItSettings { ClickDistance = 50 };
            var svc = new MockLabelFilterService(settings);

            var labelAt = TestFactories.CreateMockLabelWithDistance(50, "DelveMineral");
            svc.IsWithinClickDistance(labelAt).Should().BeTrue();

            var labelOver = TestFactories.CreateMockLabelWithDistance(51, "DelveMineral");
            svc.IsWithinClickDistance(labelOver).Should().BeFalse();
        }
    }
}
