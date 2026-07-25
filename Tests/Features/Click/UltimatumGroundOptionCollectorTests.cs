namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class UltimatumGroundOptionCollectorTests
    {
        [TestMethod]
        public void TryCollectCandidates_ReturnsFalse_WhenOptionListIsEmpty()
        {
            List<string> logs = [];

            bool ok = UltimatumGroundOptionCollector.TryCollectCandidates(
                options: [],
                priorities: [],
                includeSaturation: false,
                logFailures: true,
                logs.Add,
                out List<UltimatumGroundOptionCandidate> candidates);

            ok.Should().BeFalse();
            candidates.Should().BeEmpty();
            logs.Should().BeEmpty();
        }

        [TestMethod]
        public void TryCollectCandidates_ReturnsFalse_AndLogs_WhenOnlyNullOptionsAreProvided()
        {
            List<string> logs = [];

            bool ok = UltimatumGroundOptionCollector.TryCollectCandidates(
                options:
                [
                    (OptionElement: null!, ModifierName: "Ruin")
                ],
                priorities: ["Ruin"],
                includeSaturation: false,
                logFailures: true,
                logs.Add,
                out List<UltimatumGroundOptionCandidate> candidates);

            ok.Should().BeFalse();
            candidates.Should().BeEmpty();
            logs.Should().ContainSingle().Which.Should().Be("[TryClickPreferredUltimatumModifier] Option[0] ignored - valid=False");
        }

        [TestMethod]
        public void TryCollectCandidates_CollectsValidOptions_UsingProvidedModifierNames_WhenFailureLoggingIsDisabled()
        {
            Element option = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));
            List<string> logs = [];

            bool ok = UltimatumGroundOptionCollector.TryCollectCandidates(
                options:
                [
                    (OptionElement: option, ModifierName: "Ruin III")
                ],
                priorities: ["Ruin III", "Stalking Ruin III"],
                includeSaturation: false,
                logFailures: false,
                logs.Add,
                out List<UltimatumGroundOptionCandidate> candidates);

            ok.Should().BeTrue();
            logs.Should().BeEmpty();
            candidates.Should().ContainSingle();
            candidates[0].OptionElement.Should().BeSameAs(option);
            candidates[0].ModifierName.Should().Be("Ruin III");
            candidates[0].PriorityIndex.Should().Be(0);
            candidates[0].IsSaturated.Should().BeFalse();
        }

        [TestMethod]
        public void TryCollectCandidates_SkipsNullOptions_AndKeepsCollectingValidOptions()
        {
            Element option = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));
            List<string> logs = [];

            bool ok = UltimatumGroundOptionCollector.TryCollectCandidates(
                options:
                [
                    (OptionElement: null!, ModifierName: "Ignored"),
                    (OptionElement: option, ModifierName: "Unknown Modifier")
                ],
                priorities: ["Ruin III"],
                includeSaturation: false,
                logFailures: false,
                logs.Add,
                out List<UltimatumGroundOptionCandidate> candidates);

            ok.Should().BeTrue();
            candidates.Should().ContainSingle();
            candidates[0].OptionElement.Should().BeSameAs(option);
            candidates[0].ModifierName.Should().Be("Unknown Modifier");
            candidates[0].PriorityIndex.Should().Be(int.MaxValue);
            logs.Should().BeEmpty();
        }
    }
}