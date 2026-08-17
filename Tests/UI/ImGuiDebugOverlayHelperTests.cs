namespace ClickIt.Tests.UI;

[TestClass]
public class ImGuiDebugOverlayHelperTests
{
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
