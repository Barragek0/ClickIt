using ClickIt.Services.Click.Runtime;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace ClickIt.Tests.Services.Click
{
    [TestClass]
    public class UltimatumPanelUiQueryTests
    {
        [TestMethod]
        public void TryGetVisiblePanel_ReturnsFalse_AndLogs_WhenPanelIsMissingAndLoggingEnabled()
        {
            List<string> logs = [];

            bool ok = UltimatumPanelUiQuery.TryGetVisiblePanel(
                gameController: null,
                logFailures: true,
                logs.Add,
                out var panel);

            ok.Should().BeFalse();
            panel.Should().BeNull();
            logs.Should().ContainSingle();
            logs[0].Should().Contain("UltimatumPanel not available");
        }

        [TestMethod]
        public void TryGetVisiblePanel_ReturnsFalse_WithoutLogs_WhenPanelIsMissingAndLoggingDisabled()
        {
            List<string> logs = [];

            bool ok = UltimatumPanelUiQuery.TryGetVisiblePanel(
                gameController: null,
                logFailures: false,
                logs.Add,
                out var panel);

            ok.Should().BeFalse();
            panel.Should().BeNull();
            logs.Should().BeEmpty();
        }
    }
}