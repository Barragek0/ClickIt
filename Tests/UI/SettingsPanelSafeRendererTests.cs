namespace ClickIt.Tests.UI
{
    [TestClass]
    public class SettingsPanelSafeRendererTests
    {
        [TestMethod]
        public void DrawPanel_WhenActionSucceeds_DoesNotRenderFallbackOrSetError()
        {
            var settings = new ClickItSettings();
            int separators = 0;
            int coloredTexts = 0;
            int wrappedTexts = 0;
            int buttons = 0;
            bool invoked = false;
            var renderer = new SettingsPanelSafeRenderer(
                settings,
                new SettingsPanelSafeRenderHooks(
                    Separator: () => separators++,
                    TextColored: (_, _) => coloredTexts++,
                    TextWrapped: _ => wrappedTexts++,
                    Button: _ =>
                    {
                        buttons++;
                        return false;
                    }));

            renderer.DrawPanel("Controls", () => invoked = true);

            invoked.Should().BeTrue();
            settings.UiState.LastSettingsUiError.Should().BeEmpty();
            separators.Should().Be(0);
            coloredTexts.Should().Be(0);
            wrappedTexts.Should().Be(0);
            buttons.Should().Be(0);
        }

        [TestMethod]
        public void DrawPanel_WhenActionThrows_RecordsErrorAndRendersFallback()
        {
            var settings = new ClickItSettings();
            int separators = 0;
            List<string> wrappedTexts = [];
            List<string> buttonLabels = [];
            var renderer = new SettingsPanelSafeRenderer(
                settings,
                new SettingsPanelSafeRenderHooks(
                    Separator: () => separators++,
                    TextColored: (_, _) => { },
                    TextWrapped: wrappedTexts.Add,
                    Button: label =>
                    {
                        buttonLabels.Add(label);
                        return false;
                    }));

            renderer.DrawPanel("Debug", static () => throw new InvalidOperationException("boom"));

            settings.UiState.LastSettingsUiError.Should().Be("Debug: InvalidOperationException: boom");
            separators.Should().Be(1);
            wrappedTexts.Should().ContainSingle().Which.Should().Be("Debug: InvalidOperationException: boom");
            buttonLabels.Should().ContainSingle().Which.Should().Be("Throw Last UI Error##Debug");
        }

        [TestMethod]
        public void DrawPanel_WhenFallbackButtonPressed_RethrowsWrappedError()
        {
            var settings = new ClickItSettings();
            var renderer = new SettingsPanelSafeRenderer(
                settings,
                new SettingsPanelSafeRenderHooks(
                    Separator: static () => { },
                    TextColored: static (_, _) => { },
                    TextWrapped: static _ => { },
                    Button: static _ => true));

            Action act = () => renderer.DrawPanel("Search", static () => throw new ArgumentException("bad filter"));

            InvalidOperationException exception = act.Should().Throw<InvalidOperationException>().Which;

            exception.Message.Should().Be("Search: ArgumentException: bad filter");
            exception.InnerException.Should().BeOfType<ArgumentException>();
            exception.InnerException!.Message.Should().Be("bad filter");
        }
    }
}