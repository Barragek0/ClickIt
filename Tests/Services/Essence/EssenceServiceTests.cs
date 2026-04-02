using ClickIt.Services;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Services.Essence
{
    [TestClass]
    public class EssenceServiceTests
    {
        [TestMethod]
        public void ShouldCorruptEssence_ReturnsFalse_WhenLabelIsNull()
        {
            var settings = new ClickItSettings();
            var service = new EssenceService(settings);

            bool shouldCorrupt = service.ShouldCorruptEssence(null);

            shouldCorrupt.Should().BeFalse();
        }
    }
}