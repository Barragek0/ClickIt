namespace ClickIt.Tests.Features.Altars
{
    [TestClass]
    public class AltarMatcherBasicTests
    {
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
