using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClickIt.Services;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelFilterServiceDebugTests
    {
        [TestMethod]
        public void GetNextLabelToClick_PublishesNoLabelsDebugSnapshot_WhenInputIsNull()
        {
            var settings = new ClickItSettings();
            settings.DebugMode.Value = true;
            settings.DebugShowLabels.Value = true;
            var essenceService = new EssenceService(settings);
            var errorHandler = new global::ClickIt.Utils.ErrorHandler(settings, static (_, _) => { }, static (_, _) => { });
            var service = new LabelFilterService(settings, essenceService, errorHandler, null);

            var selected = service.GetNextLabelToClick(null, 0, 10);

            selected.Should().BeNull();
            var snapshot = service.GetLatestLabelDebug();
            snapshot.HasData.Should().BeTrue();
            snapshot.Stage.Should().Be("NoLabels");
            snapshot.TotalLabels.Should().Be(0);

            var trail = service.GetLatestLabelDebugTrail();
            trail.Should().NotBeEmpty();
            trail[^1].Should().Contain("NoLabels");
        }

        [TestMethod]
        public void GetNextLabelToClick_PublishesNoLabelsDebugSnapshot_WhenInputIsEmpty()
        {
            var settings = new ClickItSettings();
            settings.DebugMode.Value = true;
            settings.DebugShowLabels.Value = true;
            var essenceService = new EssenceService(settings);
            var errorHandler = new global::ClickIt.Utils.ErrorHandler(settings, static (_, _) => { }, static (_, _) => { });
            var service = new LabelFilterService(settings, essenceService, errorHandler, null);

            var selected = service.GetNextLabelToClick([], 0, 5);

            selected.Should().BeNull();
            var snapshot = service.GetLatestLabelDebug();
            snapshot.HasData.Should().BeTrue();
            snapshot.Stage.Should().Be("NoLabels");
            snapshot.TotalLabels.Should().Be(0);
        }
    }
}
