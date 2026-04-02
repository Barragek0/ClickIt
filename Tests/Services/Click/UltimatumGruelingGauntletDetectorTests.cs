using ClickIt.Services.Click.Runtime;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Services.Click
{
    [TestClass]
    public class UltimatumGruelingGauntletDetectorTests
    {
        public sealed class AtlasServerData
        {
            public object[] AtlasPassiveSkillIds { get; set; } = [];
        }

        public sealed class AtlasDataRoot
        {
            public AtlasServerData? ServerData { get; set; }
        }

        [TestMethod]
        public void IsActive_ReturnsTrue_WhenAtlasPassiveSkillIdIsPresent()
        {
            var detector = new UltimatumGruelingGauntletDetector();
            var data = new AtlasDataRoot
            {
                ServerData = new AtlasServerData
                {
                    AtlasPassiveSkillIds = [42, 9882]
                }
            };

            bool result = detector.IsActive(data);

            result.Should().BeTrue();
        }

        [TestMethod]
        public void IsActive_UsesCachedValueWithinCacheWindow()
        {
            long now = 1000;
            var detector = new UltimatumGruelingGauntletDetector(cacheWindowMs: 100, tickProvider: () => now);
            var activeData = new AtlasDataRoot
            {
                ServerData = new AtlasServerData
                {
                    AtlasPassiveSkillIds = [9882]
                }
            };
            var inactiveData = new AtlasDataRoot
            {
                ServerData = new AtlasServerData
                {
                    AtlasPassiveSkillIds = [7]
                }
            };

            detector.IsActive(activeData).Should().BeTrue();

            now += 50;
            detector.IsActive(inactiveData).Should().BeTrue();

            now += 101;
            detector.IsActive(inactiveData).Should().BeFalse();
        }

        [TestMethod]
        public void IsActive_ReturnsFalse_WhenServerDataIsMissing()
        {
            var detector = new UltimatumGruelingGauntletDetector();

            bool result = detector.IsActive(new AtlasDataRoot { ServerData = null });

            result.Should().BeFalse();
        }
    }
}