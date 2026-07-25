namespace ClickIt.Tests.Core.Settings
{
    [TestClass]
    public class SettingsUiRenderHelpersTests
    {
        [TestMethod]
        public void MatchesSearch_ReturnsTrue_WhenAnyValueMatches()
        {
            SettingsUiRenderHelpers.MatchesSearch("beta", "alpha", "Beta value", null).Should().BeTrue();
        }

        [TestMethod]
        public void MatchesSearch_TrimsWhitespaceAroundFilter()
        {
            SettingsUiRenderHelpers.MatchesSearch("  beta  ", "alpha", "Beta value").Should().BeTrue();
        }

        [TestMethod]
        public void MatchesSearch_SupportsEnumerableInputs()
        {
            IEnumerable<string?> values = ["alpha", null, "Beta value"];

            SettingsUiRenderHelpers.MatchesSearch("beta", values).Should().BeTrue();
        }

        [TestMethod]
        public void MatchesSearch_ReturnsFalse_WhenNoValueMatches()
        {
            SettingsUiRenderHelpers.MatchesSearch("gamma", "alpha", "beta").Should().BeFalse();
        }

        private sealed class FakeHotkeyNode
        {
            public int CallCount { get; private set; }
            public string LastLabel { get; private set; } = string.Empty;

            public void DrawPickerButton(string label)
            {
                CallCount++;
                LastLabel = label;
            }
        }

    }
}