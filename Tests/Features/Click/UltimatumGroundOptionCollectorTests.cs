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
    }
}