using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClickIt.Services;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class AltarMatcherBasicTests
    {
        [TestMethod]
        public void CleanAltarModsText_ReturnsEmpty_ForNullInput()
        {
            var matcher = new AltarMatcher();
            string result = matcher.CleanAltarModsText(null);
            result.Should().Be(string.Empty);
        }

        [TestMethod]
        public void TryMatchModCached_ReturnsFalse_ForUnknown()
        {
            var matcher = new AltarMatcher();
            bool isUp;
            string matched;
            bool ok = matcher.TryMatchModCached("__not_a_real_mod__", "", out isUp, out matched);
            ok.Should().BeFalse();
            matched.Should().BeEmpty();
        }
    }
}
