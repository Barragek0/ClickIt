using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace ClickIt.Tests.Core.Settings
{
    [TestClass]
    public class SettingsUiRenderHelpersTests
    {
        [TestMethod]
        public void MatchesSearch_ReturnsTrue_WhenFilterIsEmpty()
        {
            SettingsUiRenderHelpers.MatchesSearch(string.Empty, "alpha", "beta").Should().BeTrue();
        }

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

        [TestMethod]
        public void BuildExpandedRowKey_CombinesListAndRowId()
        {
            SettingsUiRenderHelpers.BuildExpandedRowKey("left", "row-1").Should().Be("left:row-1");
        }

        [TestMethod]
        public void ToggleExpandedRowKey_ClearsMatchingRow()
        {
            SettingsUiRenderHelpers.ToggleExpandedRowKey("left:row-1", "left", "row-1").Should().BeEmpty();
        }

        [TestMethod]
        public void ToggleExpandedRowKey_SetsDifferentRow()
        {
            SettingsUiRenderHelpers.ToggleExpandedRowKey("left:row-1", "right", "row-2").Should().Be("right:row-2");
        }
    }
}