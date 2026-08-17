namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class PathfindingLabelSuppressionEvaluatorTests
    {
        private const string LeverPath = "Metadata/Terrain/Levers/Switch_Once";

        private static PathfindingLabelSuppressionEvaluator CreateEvaluator(ClickItSettings settings, ClickRuntimeState runtimeState)
            => new(new PathfindingLabelSuppressionEvaluatorDependencies(settings, runtimeState));

        private static LabelOnGround CreateLeverLabel(long address)
            => ClickPipelineScenarioFactory.CreateLabel(
                new RectangleF(100f, 100f, 40f, 20f),
                EntityProbeFactory.Create(path: LeverPath, address: address),
                address: address + 0x1000);

        [TestMethod]
        public void ShouldSuppressLeverClick_ReturnsFalse_WhenLazyModeDisabled()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = false;
            PathfindingLabelSuppressionEvaluator evaluator = CreateEvaluator(settings, new ClickRuntimeState());

            evaluator.ShouldSuppressLeverClick(CreateLeverLabel(42)).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldSuppressLeverClick_ReturnsFalse_WhenLabelIsNotLever()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = true;
            PathfindingLabelSuppressionEvaluator evaluator = CreateEvaluator(settings, new ClickRuntimeState());

            LabelOnGround nonLever = ClickPipelineScenarioFactory.CreateLabel(
                new RectangleF(100f, 100f, 40f, 20f),
                EntityProbeFactory.Create(path: "Metadata/MiscellaneousObjects/WorldItem", address: 42),
                address: 0x2000);

            evaluator.ShouldSuppressLeverClick(nonLever).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldSuppressLeverClick_ReturnsTrue_WithinCooldown_AfterRecordLeverClick()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = true;
            ClickRuntimeState runtimeState = new();
            PathfindingLabelSuppressionEvaluator evaluator = CreateEvaluator(settings, runtimeState);
            LabelOnGround lever = CreateLeverLabel(42);

            evaluator.RecordLeverClick(lever);
            runtimeState.LastLeverKey.Should().Be(42);

            evaluator.ShouldSuppressLeverClick(lever).Should().BeTrue();
        }

        [TestMethod]
        public void ShouldSuppressLeverClick_ReturnsFalse_ForDifferentLeverIdentity()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = true;
            PathfindingLabelSuppressionEvaluator evaluator = CreateEvaluator(settings, new ClickRuntimeState());
            LabelOnGround leverA = CreateLeverLabel(42);

            evaluator.RecordLeverClick(leverA);

            evaluator.ShouldSuppressLeverClick(CreateLeverLabel(43)).Should().BeFalse();
        }

        [TestMethod]
        public void RecordLeverClick_RecordsNothing_WhenLazyModeDisabled()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = false;
            ClickRuntimeState runtimeState = new();
            PathfindingLabelSuppressionEvaluator evaluator = CreateEvaluator(settings, runtimeState);
            LabelOnGround lever = CreateLeverLabel(42);

            evaluator.RecordLeverClick(lever);

            runtimeState.LastLeverKey.Should().Be(0);
            evaluator.ShouldSuppressLeverClick(lever).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldSuppressPathfindingLabel_ReturnsTrue_WhenLeverClickIsSuppressed()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = true;
            PathfindingLabelSuppressionEvaluator evaluator = CreateEvaluator(settings, new ClickRuntimeState());
            LabelOnGround lever = CreateLeverLabel(42);

            evaluator.RecordLeverClick(lever);

            evaluator.ShouldSuppressPathfindingLabel(lever).Should().BeTrue();
        }

        [TestMethod]
        public void ShouldSuppressPathfindingLabel_ReturnsFalse_WhenNothingIsSuppressed()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = true;
            PathfindingLabelSuppressionEvaluator evaluator = CreateEvaluator(settings, new ClickRuntimeState());

            evaluator.ShouldSuppressPathfindingLabel(CreateLeverLabel(42)).Should().BeFalse();
        }
    }
}
