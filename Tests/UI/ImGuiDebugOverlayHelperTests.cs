namespace ClickIt.Tests.UI;

[TestClass]
public class ImGuiDebugOverlayHelperTests
{
    [TestMethod]
    [TestCategory("Parity")]
    public void CopyAllUltimatum_PreservesExactTextAndStageOrder()
    {
        var settings = new ClickItSettings();
        var store = new ClickTelemetryStore(settings);
        store.PublishUltimatumEvent(new UltimatumDebugEvent("Check", "PanelUi", true, false));
        store.PublishUltimatumEvent(new UltimatumDebugEvent("Choose", "PanelUi", true, true)
        {
            Action = "Select",
            CandidateCount = 2,
            ClickedChoice = true,
            Notes = "Ruin II",
        });
        var overlay = new ImGuiDebugOverlay(settings);
        RuntimeMemberAccessor.SetRequiredMember(overlay, "_lastSnapshot", DebugTelemetrySnapshot.Empty with
        {
            Click = ClickTelemetrySnapshot.Empty with
            {
                Ultimatum = store.GetLatestUltimatumDebug(),
                UltimatumTrail = store.GetLatestUltimatumDebugTrail(),
            },
        });
        var buffer = new System.Text.StringBuilder();
        typeof(ImGuiDebugOverlay).GetMethod("AppendUltimatum", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(overlay, [buffer]);

        string[] expectedLines =
        [
            "--- Ultimatum ---",
            "  Enabled Initial/Other: False/False",
            "  Stage: Choose Seq: 2 Source: PanelUi",
            "  Panel Visible: True GG Active: True",
            "  Saturated: False Modifier: ",
            "  TakeReward: False Action: Select",
            "  Candidates: 2 Saturated: 0",
            "  Best:  (priority=2147483647)",
            "  Clicked Choice/Confirm/Reward: True/False/False",
            "  Note: Ruin II",
            "  Recent Stages:",
            "  00001 Check [PanelUi] GG=False Action= Sat=False/ Reward=False Cands=0 Clicks C/Q/R=False/False/False | ",
            "  00002 Choose [PanelUi] GG=True Action=Select Sat=False/ Reward=False Cands=2 Clicks C/Q/R=True/False/False | Ruin II",
            "",
            "",
        ];
        buffer.ToString().Should().Be(string.Join(Environment.NewLine, expectedLines));
    }

    [TestMethod]
    public void SanitizeText_ReplacesSpecialGlyphs_WithAscii()
    {
        ImGuiDebugOverlay.SanitizeText("\u2192 arrow \u2713 check").Should().Be("> arrow + check");
    }

    [TestMethod]
    public void SanitizeText_ReturnsSameReference_WhenNoSpecialGlyphs()
    {
        string input = "plain ascii";
        ImGuiDebugOverlay.SanitizeText(input).Should().BeSameAs(input);
    }

    [TestMethod]
    public void SanitizeText_HandlesLongStrings_WithHeapFallback()
    {
        string input = new('\u2192', 5000);
        ImGuiDebugOverlay.SanitizeText(input).Should().Be(new string('>', 5000));
    }

    [TestMethod]
    public void FormatMemoryMb_FormatsGbAndMb()
    {
        ImGuiDebugOverlay.FormatMemoryMb(1024).Should().Be("1.0 GB");
        ImGuiDebugOverlay.FormatMemoryMb(512).Should().Be("512 MB");
    }

    [TestMethod]
    public void FormatBytes_FormatsMbKbAndBytes()
    {
        ImGuiDebugOverlay.FormatBytes(2 * 1024 * 1024).Should().Be("2 MB");
        ImGuiDebugOverlay.FormatBytes(1024).Should().Be("1 KB");
        ImGuiDebugOverlay.FormatBytes(512).Should().Be("512 B");
    }

    [TestMethod]
    public void FormatAllocRate_AppendsPerSecond()
    {
        ImGuiDebugOverlay.FormatAllocRate(1024).Should().Be("1 KB/s");
    }

    [TestMethod]
    public void BoolStr_FormatsYesAndNo()
    {
        ImGuiDebugOverlay.BoolStr(true).Should().Be("Yes");
        ImGuiDebugOverlay.BoolStr(false).Should().Be("No");
    }

    [TestMethod]
    public void TrimPath_ReturnsNone_ForNullOrWhitespace()
    {
        ImGuiDebugOverlay.TrimPath(null).Should().Be("<none>");
        ImGuiDebugOverlay.TrimPath("  ").Should().Be("<none>");
    }

    [TestMethod]
    public void TrimPath_ReturnsShortPathUnchanged()
    {
        ImGuiDebugOverlay.TrimPath("short").Should().Be("short");
    }

    [TestMethod]
    public void TrimPath_TruncatesLongPath_WithEllipsis()
    {
        string path = new('a', 81);
        ImGuiDebugOverlay.TrimPath(path).Should().Be(new string('a', 77) + "...");
    }

    [TestMethod]
    [DataRow(1f, 0f, "E")]
    [DataRow(1f, 1f, "NE")]
    [DataRow(0f, 1f, "N")]
    [DataRow(-1f, 1f, "NW")]
    [DataRow(-1f, 0f, "W")]
    [DataRow(-1f, -1f, "SW")]
    [DataRow(0f, -1f, "S")]
    [DataRow(1f, -1f, "SE")]
    public void ToCompass_ReturnsExpectedDirection(float dx, float dy, string expected)
    {
        ImGuiDebugOverlay.ToCompass(new Vector2(dx, dy)).Should().StartWith(expected);
    }
}
