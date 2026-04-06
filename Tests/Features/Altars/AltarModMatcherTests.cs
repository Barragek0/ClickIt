namespace ClickIt.Tests.Features.Altars
{
    [TestClass]
    public class AltarModMatcherTests
    {
        [TestMethod]
        public void TryMatchMod_ReturnsFalseForUnknown()
        {
            bool isUp;
            string matched;
            var ok = AltarModMatcher.TryMatchMod("nonexistentmod", "", out isUp, out matched);
            ok.Should().BeFalse();
            matched.Should().BeEmpty();
        }
    }
}
