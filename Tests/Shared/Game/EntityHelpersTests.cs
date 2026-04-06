namespace ClickIt.Tests.Shared.Game
{
    [TestClass]
    public class EntityHelpersTests
    {
        [TestMethod]
        public void IsRitualActive_IsCaseSensitive()
        {
            var lowerOnly = new System.Collections.Generic.List<string?> { "ritualblocker" };
            EntityHelpers.IsRitualActive((System.Collections.Generic.IEnumerable<string?>)lowerOnly).Should().BeFalse();

            var proper = new System.Collections.Generic.List<string?> { "Some/Prefix/RitualBlocker/Node" };
            EntityHelpers.IsRitualActive((System.Collections.Generic.IEnumerable<string?>)proper).Should().BeTrue();
        }
    }
}
