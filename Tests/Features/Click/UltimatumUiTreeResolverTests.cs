namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class UltimatumUiTreeResolverTests
    {
        [TestMethod]
        public void ExtractUltimatumModifierNames_NormalizesStringAndObjectEntries()
        {
            IReadOnlyList<string> names = UltimatumUiTreeResolver.ExtractUltimatumModifierNames(
                new object?[]
                {
                    "  Ruin\r\nII  ",
                    new ModifierNameProbe("  Stalking Ruin\nIII  ")
                });

            names.Should().Equal("Ruin II", "Stalking Ruin III");
        }

        [TestMethod]
        public void ExtractUltimatumModifierNames_ConvertsNullEntries_ToEmptyModifierNames()
        {
            IReadOnlyList<string> names = UltimatumUiTreeResolver.ExtractUltimatumModifierNames(
                new object?[]
                {
                    null,
                    new ModifierNameProbe("  Razor Dance  ")
                });

            names.Should().Equal(string.Empty, "Razor Dance");
        }

        private sealed class ModifierNameProbe(string value)
        {
            public override string ToString() => value;
        }
    }
}