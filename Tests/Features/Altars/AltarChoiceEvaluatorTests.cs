namespace ClickIt.Tests.Features.Altars
{
    [TestClass]
    public class AltarChoiceEvaluatorTests
    {
        private static readonly RectangleF ValidRect = new(10f, 20f, 50f, 50f);

        [TestMethod]
        public void EvaluateChoice_ReturnsUnmatchedMods_WhenEitherSideHasUnmatchedMods()
        {
            var settings = new ClickItSettings();
            var evaluator = new AltarChoiceEvaluator(settings);
            var altar = TestBuilders.BuildPrimary(
                TestBuilders.BuildSecondary(hasUnmatched: true),
                TestBuilders.BuildSecondary());

            AltarChoiceEvaluation evaluation = evaluator.EvaluateChoice(altar, TestBuilders.BuildAltarWeights(), ValidRect, ValidRect);

            evaluation.Outcome.Should().Be(AltarChoiceOutcome.UnmatchedMods);
            evaluation.ChosenElement.Should().BeNull();
        }

        [TestMethod]
        public void EvaluateChoice_ReturnsDangerousTopChooseBottom_WhenTopDownsideExceedsThreshold()
        {
            var settings = new ClickItSettings();
            var evaluator = new AltarChoiceEvaluator(settings);
            var altar = TestBuilders.BuildPrimary();
            var weights = TestBuilders.BuildAltarWeights(
                topDown: [95m],
                bottomDown: [10m],
                topUp: [10m],
                bottomUp: [10m],
                topWeight: 40,
                bottomWeight: 30);

            AltarChoiceEvaluation evaluation = evaluator.EvaluateChoice(altar, weights, ValidRect, ValidRect);

            evaluation.Outcome.Should().Be(AltarChoiceOutcome.DangerousTopChooseBottom);
            evaluation.Threshold.Should().Be(settings.DangerousDownsideThreshold.Value);
        }

        [TestMethod]
        public void EvaluateChoice_ReturnsHighValueBottomChosen_WhenBottomUpsideExceedsThreshold()
        {
            var settings = new ClickItSettings();
            var evaluator = new AltarChoiceEvaluator(settings);
            var altar = TestBuilders.BuildPrimary();
            var weights = TestBuilders.BuildAltarWeights(
                topDown: [10m],
                bottomDown: [10m],
                topUp: [20m],
                bottomUp: [95m],
                topWeight: 40,
                bottomWeight: 30);

            AltarChoiceEvaluation evaluation = evaluator.EvaluateChoice(altar, weights, ValidRect, ValidRect);

            evaluation.Outcome.Should().Be(AltarChoiceOutcome.HighValueBottomChosen);
            evaluation.Threshold.Should().Be(settings.ValuableUpsideThreshold.Value);
        }

        [TestMethod]
        public void EvaluateChoice_ReturnsBothBelowMinimumManual_WhenMinimumThresholdBlocksBothChoices()
        {
            var settings = new ClickItSettings();
            settings.MinWeightThresholdEnabled.Value = true;
            settings.MinWeightThreshold.Value = 25;
            var evaluator = new AltarChoiceEvaluator(settings);
            var altar = TestBuilders.BuildPrimary();
            var weights = TestBuilders.BuildAltarWeights(
                topDown: [10m],
                bottomDown: [10m],
                topUp: [10m],
                bottomUp: [10m],
                topWeight: 10,
                bottomWeight: 20);

            AltarChoiceEvaluation evaluation = evaluator.EvaluateChoice(altar, weights, ValidRect, ValidRect);

            evaluation.Outcome.Should().Be(AltarChoiceOutcome.BothBelowMinimumManual);
            evaluation.Threshold.Should().Be(25);
            evaluation.TopWeight.Should().Be(10);
            evaluation.BottomWeight.Should().Be(20);
        }

        [TestMethod]
        public void EvaluateChoice_ReturnsTopWeightHigher_WhenNoOverridesApply()
        {
            var settings = new ClickItSettings();
            settings.DangerousDownside.Value = false;
            settings.ValuableUpside.Value = false;
            settings.UnvaluableUpside.Value = false;
            settings.MinWeightThresholdEnabled.Value = false;
            var evaluator = new AltarChoiceEvaluator(settings);
            var altar = TestBuilders.BuildPrimary();
            var weights = TestBuilders.BuildAltarWeights(
                topDown: [10m],
                bottomDown: [10m],
                topUp: [10m],
                bottomUp: [10m],
                topWeight: 80,
                bottomWeight: 30);

            AltarChoiceEvaluation evaluation = evaluator.EvaluateChoice(altar, weights, ValidRect, ValidRect);

            evaluation.Outcome.Should().Be(AltarChoiceOutcome.TopWeightHigher);
            evaluation.TopWeight.Should().Be(80);
            evaluation.BottomWeight.Should().Be(30);
        }

        [TestMethod]
        public void EvaluateChoice_ReturnsEqualWeightsManual_WhenWeightsTie()
        {
            var settings = new ClickItSettings();
            settings.DangerousDownside.Value = false;
            settings.ValuableUpside.Value = false;
            settings.UnvaluableUpside.Value = false;
            settings.MinWeightThresholdEnabled.Value = false;
            var evaluator = new AltarChoiceEvaluator(settings);
            var altar = TestBuilders.BuildPrimary();
            var weights = TestBuilders.BuildAltarWeights(
                topDown: [10m],
                bottomDown: [10m],
                topUp: [10m],
                bottomUp: [10m],
                topWeight: 42,
                bottomWeight: 42);

            AltarChoiceEvaluation evaluation = evaluator.EvaluateChoice(altar, weights, ValidRect, ValidRect);

            evaluation.Outcome.Should().Be(AltarChoiceOutcome.EqualWeightsManual);
        }
    }
}