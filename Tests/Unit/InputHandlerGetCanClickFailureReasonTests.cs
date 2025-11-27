using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
// test uses seam helper; no deep reflection required
using ClickIt.Utils;
using ClickIt;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class InputHandlerGetCanClickFailureReasonTests
    {
        // Avoid constructing ExileCore.GameController in tests; use test seam helpers instead.

        [TestMethod]
        public void Returns_InTownHideout_When_CurrentArea_IsTown()
        {
            var settings = new ClickItSettings();
            var perf = new PerformanceMonitor(settings);
            var handler = new InputHandler(settings, perf);

            // Call the test seam helper with a simulated area (avoid creating a real GameController)
            var res = InputHandler.GetCanClickFailureReasonForTests(
                windowIsForeground: true,
                isTown: true
            );
            res.Should().Be("In town/hideout.");
        }

        [TestMethod]
        public void Returns_PanelOpen_When_OpenLeftPanel_AddressNonZero_And_BlockSettingEnabled()
        {
            var settings = new ClickItSettings();
            settings.BlockOnOpenLeftRightPanel.Value = true;
            var perf = new PerformanceMonitor(settings);
            var handler = new InputHandler(settings, perf);

            var res = InputHandler.GetCanClickFailureReasonForTests(
                windowIsForeground: true,
                blockOnOpenLeftRightPanel: true,
                openLeftPanelAddress: 123L
            );
            res.Should().Be("Panel is open.");
        }

        [TestMethod]
        public void Returns_ChatOpen_When_ChatTitlePanel_IsVisible()
        {
            var settings = new ClickItSettings();
            var perf = new PerformanceMonitor(settings);
            var handler = new InputHandler(settings, perf);

            var res = InputHandler.GetCanClickFailureReasonForTests(
                windowIsForeground: true,
                chatTitlePanelIsVisible: true
            );
            res.Should().Be("Chat is open.");
        }

        [TestMethod]
        public void Returns_EscapeMenu_When_Game_IsEscapeState_True()
        {
            var settings = new ClickItSettings();
            var perf = new PerformanceMonitor(settings);
            var handler = new InputHandler(settings, perf);

            var res = InputHandler.GetCanClickFailureReasonForTests(
                windowIsForeground: true,
                escapeState: true
            );
            res.Should().Be("Escape menu is open.");
        }
    }
}
