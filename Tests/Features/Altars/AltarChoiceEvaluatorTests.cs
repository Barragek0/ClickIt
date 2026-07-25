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
        public void EvaluateChoice_ReturnsInvalidRectangles_WhenAnyRectangleIsInvalid()
        {
            var settings = new ClickItSettings();
            var evaluator = new AltarChoiceEvaluator(settings);
            var altar = TestBuilders.BuildPrimary();

            AltarChoiceEvaluation evaluation = evaluator.EvaluateChoice(
                altar,
                TestBuilders.BuildAltarWeights(),
                new RectangleF(10f, 20f, 0f, 50f),
                ValidRect);

            evaluation.Outcome.Should().Be(AltarChoiceOutcome.InvalidRectangles);
            evaluation.ChosenElement.Should().BeNull();
        }

        [TestMethod]
        public void EvaluateChoice_ReturnsUnrecognizedTopUpside_WithUnrecognizedMods()
        {
            var settings = new ClickItSettings();
            var evaluator = new AltarChoiceEvaluator(settings);
            var altar = TestBuilders.BuildPrimary(
                TestBuilders.BuildSecondary(upsides: ["Gain a dubious upside"], downsides: ["Minor downside"]),
                TestBuilders.BuildSecondary());
            var weights = TestBuilders.BuildAltarWeights(
                topDown: [5m],
                bottomDown: [5m],
                topUp: [0m],
                bottomUp: [5m],
                topWeight: 5,
                bottomWeight: 4);

            AltarChoiceEvaluation evaluation = evaluator.EvaluateChoice(altar, weights, ValidRect, ValidRect);

            evaluation.Outcome.Should().Be(AltarChoiceOutcome.UnrecognizedTopUpside);
            evaluation.UnrecognizedWeightType.Should().Be("Top upside");
            evaluation.UnrecognizedMods.Should().NotBeNull();
            evaluation.UnrecognizedMods![0].Should().Be("Gain a dubious upside");
            evaluation.UnrecognizedMods.Where(static mod => !string.IsNullOrWhiteSpace(mod)).Should().Equal("Gain a dubious upside");
        }

        [TestMethod]
        public void EvaluateChoice_ReturnsUnrecognizedTopDownside_WithUnrecognizedMods()
        {
            var settings = new ClickItSettings();
            var evaluator = new AltarChoiceEvaluator(settings);
            var altar = TestBuilders.BuildPrimary(
                TestBuilders.BuildSecondary(upsides: ["Known top upside"], downsides: ["Unmapped top downside"]),
                TestBuilders.BuildSecondary(upsides: ["Known bottom upside"], downsides: ["Known bottom downside"]));
            var weights = TestBuilders.BuildAltarWeights(
                topDown: [0m],
                bottomDown: [5m],
                topUp: [5m],
                bottomUp: [5m],
                topWeight: 5,
                bottomWeight: 4);

            AltarChoiceEvaluation evaluation = evaluator.EvaluateChoice(altar, weights, ValidRect, ValidRect);

            evaluation.Outcome.Should().Be(AltarChoiceOutcome.UnrecognizedTopDownside);
            evaluation.UnrecognizedWeightType.Should().Be("Top downside");
            evaluation.UnrecognizedMods.Should().NotBeNull();
            evaluation.UnrecognizedMods!.Where(static mod => !string.IsNullOrWhiteSpace(mod)).Should().Equal("Unmapped top downside");
        }

        [TestMethod]
        public void EvaluateChoice_ReturnsUnrecognizedBottomUpside_WithUnrecognizedMods()
        {
            var settings = new ClickItSettings();
            var evaluator = new AltarChoiceEvaluator(settings);
            var altar = TestBuilders.BuildPrimary(
                TestBuilders.BuildSecondary(upsides: ["Known top upside"], downsides: ["Known top downside"]),
                TestBuilders.BuildSecondary(upsides: ["Unmapped bottom upside"], downsides: ["Known bottom downside"]));
            var weights = TestBuilders.BuildAltarWeights(
                topDown: [5m],
                bottomDown: [5m],
                topUp: [5m],
                bottomUp: [0m],
                topWeight: 5,
                bottomWeight: 4);

            AltarChoiceEvaluation evaluation = evaluator.EvaluateChoice(altar, weights, ValidRect, ValidRect);

            evaluation.Outcome.Should().Be(AltarChoiceOutcome.UnrecognizedBottomUpside);
            evaluation.UnrecognizedWeightType.Should().Be("Bottom upside");
            evaluation.UnrecognizedMods.Should().NotBeNull();
            evaluation.UnrecognizedMods!.Where(static mod => !string.IsNullOrWhiteSpace(mod)).Should().Equal("Unmapped bottom upside");
        }

        [TestMethod]
        public void EvaluateChoice_ReturnsUnrecognizedBottomDownside_WithUnrecognizedMods()
        {
            var settings = new ClickItSettings();
            var evaluator = new AltarChoiceEvaluator(settings);
            var altar = TestBuilders.BuildPrimary(
                TestBuilders.BuildSecondary(upsides: ["Known top upside"], downsides: ["Known top downside"]),
                TestBuilders.BuildSecondary(upsides: ["Known bottom upside"], downsides: ["Unmapped bottom downside"]));
            var weights = TestBuilders.BuildAltarWeights(
                topDown: [5m],
                bottomDown: [0m],
                topUp: [5m],
                bottomUp: [5m],
                topWeight: 5,
                bottomWeight: 4);

            AltarChoiceEvaluation evaluation = evaluator.EvaluateChoice(altar, weights, ValidRect, ValidRect);

            evaluation.Outcome.Should().Be(AltarChoiceOutcome.UnrecognizedBottomDownside);
            evaluation.UnrecognizedWeightType.Should().Be("Bottom downside");
            evaluation.UnrecognizedMods.Should().NotBeNull();
            evaluation.UnrecognizedMods!.Where(static mod => !string.IsNullOrWhiteSpace(mod)).Should().Equal("Unmapped bottom downside");
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
        public void EvaluateChoice_ReturnsBothDangerousManual_WhenBothDownsidesExceedThreshold()
        {
            var settings = CreateWeightOnlySettings();
            settings.DangerousDownside.Value = true;
            var evaluator = new AltarChoiceEvaluator(settings);
            var altar = TestBuilders.BuildPrimary();
            var weights = TestBuilders.BuildAltarWeights(
                topDown: [95m],
                bottomDown: [91m],
                topUp: [10m],
                bottomUp: [10m],
                topWeight: 40,
                bottomWeight: 30);

            AltarChoiceEvaluation evaluation = evaluator.EvaluateChoice(altar, weights, ValidRect, ValidRect);

            evaluation.Outcome.Should().Be(AltarChoiceOutcome.BothDangerousManual);
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
        public void EvaluateChoice_ReturnsBothLowValueManual_WhenBothUpsidesAreAtOrBelowThreshold()
        {
            var settings = CreateWeightOnlySettings();
            settings.UnvaluableUpside.Value = true;
            settings.UnvaluableUpsideThreshold.Value = 5;
            var evaluator = new AltarChoiceEvaluator(settings);
            var altar = TestBuilders.BuildPrimary();
            var weights = TestBuilders.BuildAltarWeights(
                topDown: [10m],
                bottomDown: [10m],
                topUp: [5m],
                bottomUp: [3m],
                topWeight: 40,
                bottomWeight: 30);

            AltarChoiceEvaluation evaluation = evaluator.EvaluateChoice(altar, weights, ValidRect, ValidRect);

            evaluation.Outcome.Should().Be(AltarChoiceOutcome.BothLowValueManual);
            evaluation.Threshold.Should().Be(5);
        }

        [TestMethod]
        public void EvaluateChoice_ReturnsBothBelowMinimumManual_WhenMinimumThresholdBlocksBothChoices()
        {
            var settings = CreateWeightOnlySettings();
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
        public void EvaluateChoice_ReturnsTopBelowMinimumChooseBottom_WhenOnlyTopIsBelowMinimum()
        {
            var settings = CreateWeightOnlySettings();
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
                bottomWeight: 40);

            AltarChoiceEvaluation evaluation = evaluator.EvaluateChoice(altar, weights, ValidRect, ValidRect);

            evaluation.Outcome.Should().Be(AltarChoiceOutcome.TopBelowMinimumChooseBottom);
            evaluation.Threshold.Should().Be(25);
            evaluation.TopWeight.Should().Be(10);
            evaluation.BottomWeight.Should().Be(40);
        }

        [TestMethod]
        public void EvaluateChoice_ReturnsBottomBelowMinimumChooseTop_WhenOnlyBottomIsBelowMinimum()
        {
            var settings = CreateWeightOnlySettings();
            settings.MinWeightThresholdEnabled.Value = true;
            settings.MinWeightThreshold.Value = 25;
            var evaluator = new AltarChoiceEvaluator(settings);
            var altar = TestBuilders.BuildPrimary();
            var weights = TestBuilders.BuildAltarWeights(
                topDown: [10m],
                bottomDown: [10m],
                topUp: [10m],
                bottomUp: [10m],
                topWeight: 40,
                bottomWeight: 10);

            AltarChoiceEvaluation evaluation = evaluator.EvaluateChoice(altar, weights, ValidRect, ValidRect);

            evaluation.Outcome.Should().Be(AltarChoiceOutcome.BottomBelowMinimumChooseTop);
            evaluation.Threshold.Should().Be(25);
            evaluation.TopWeight.Should().Be(40);
            evaluation.BottomWeight.Should().Be(10);
        }

        [TestMethod]
        public void EvaluateChoice_ReturnsTopWeightHigher_WhenNoOverridesApply()
        {
            var settings = CreateWeightOnlySettings();
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
        public void EvaluateChoice_ReturnsBottomWeightHigher_WhenNoOverridesApply()
        {
            var settings = CreateWeightOnlySettings();
            var evaluator = new AltarChoiceEvaluator(settings);
            var altar = TestBuilders.BuildPrimary();
            var weights = TestBuilders.BuildAltarWeights(
                topDown: [10m],
                bottomDown: [10m],
                topUp: [10m],
                bottomUp: [10m],
                topWeight: 20,
                bottomWeight: 60);

            AltarChoiceEvaluation evaluation = evaluator.EvaluateChoice(altar, weights, ValidRect, ValidRect);

            evaluation.Outcome.Should().Be(AltarChoiceOutcome.BottomWeightHigher);
            evaluation.TopWeight.Should().Be(20);
            evaluation.BottomWeight.Should().Be(60);
        }

        [TestMethod]
        public void EvaluateChoice_ReturnsEqualWeightsManual_WhenWeightsTie()
        {
            var settings = CreateWeightOnlySettings();
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

        [TestMethod]
        public void EvaluateChoice_LogsCriticalAndReturnsNullChosenElement_WhenPreferredButtonElementIsMissing()
        {
            var settings = CreateWeightOnlySettings();
            List<string> messages = [];
            var evaluator = new AltarChoiceEvaluator(settings, (message, _) => messages.Add(message));
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
            evaluation.ChosenElement.Should().BeNull();
            messages.Should().ContainSingle(message => message.Contains("TopButton.Element is null", StringComparison.Ordinal));
        }

        [TestMethod]
        public void EvaluateChoice_LogsCriticalAndReturnsNullChosenElement_WhenPreferredButtonIsNull()
        {
            var settings = CreateWeightOnlySettings();
            List<string> messages = [];
            var evaluator = new AltarChoiceEvaluator(settings, (message, _) => messages.Add(message));
            var altar = new PrimaryAltarComponent(
                AltarType.Unknown,
                TestBuilders.BuildSecondary(),
                null!,
                TestBuilders.BuildSecondary(),
                new AltarButton(null));
            var weights = TestBuilders.BuildAltarWeights(
                topDown: [10m],
                bottomDown: [10m],
                topUp: [10m],
                bottomUp: [10m],
                topWeight: 80,
                bottomWeight: 30);

            AltarChoiceEvaluation evaluation = evaluator.EvaluateChoice(altar, weights, ValidRect, ValidRect);

            evaluation.Outcome.Should().Be(AltarChoiceOutcome.TopWeightHigher);
            evaluation.ChosenElement.Should().BeNull();
            messages.Should().ContainSingle(message => message.Contains("TopButton is null", StringComparison.Ordinal));
        }

        private static ClickItSettings CreateWeightOnlySettings()
        {
            var settings = new ClickItSettings();
            settings.DangerousDownside.Value = false;
            settings.ValuableUpside.Value = false;
            settings.UnvaluableUpside.Value = false;
            settings.MinWeightThresholdEnabled.Value = false;
            return settings;
        }
    }
}