namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class UltimatumUiTreeResolverTests
    {
        [TestMethod]
        public void GetUltimatumOptions_ReturnsEmptyAndLogs_WhenLabelIsNull()
        {
            List<string> diagnostics = [];

            List<(Element OptionElement, string ModifierName)> options = UltimatumUiTreeResolver.GetUltimatumOptions(null!, diagnostics);

            options.Should().BeEmpty();
            diagnostics.Should().ContainSingle().Which.Should().Be("Tree fail: label.Label is null.");
        }

        [TestMethod]
        public void GetUltimatumBeginButton_ReturnsNullAndLogs_WhenLabelIsNull()
        {
            List<string> diagnostics = [];

            Element? beginButton = UltimatumUiTreeResolver.GetUltimatumBeginButton(null!, diagnostics);

            beginButton.Should().BeNull();
            diagnostics.Should().ContainSingle().Which.Should().Be("Tree fail: label.Label is null.");
        }

        [TestMethod]
        public void TryExtractElement_ReturnsTrue_ForElementInstance()
        {
            Element element = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));

            bool ok = UltimatumUiTreeResolver.TryExtractElement(element, out Element? extracted);

            ok.Should().BeTrue();
            extracted.Should().BeSameAs(element);
        }

        [TestMethod]
        public void TryExtractElement_ReturnsFalse_ForNonElementObject()
        {
            bool ok = UltimatumUiTreeResolver.TryExtractElement(new object(), out Element? extracted);

            ok.Should().BeFalse();
            extracted.Should().BeNull();
        }

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

        [TestMethod]
        public void ExtractUltimatumModifierNames_ReturnsEmptyAndLogs_WhenModifiersMissing()
        {
            List<string> diagnostics = [];

            IReadOnlyList<string> names = UltimatumUiTreeResolver.ExtractUltimatumModifierNames(
                modifiersObj: null,
                diagnostics,
                missingModifiersMessage: "ChoicePanel: Modifiers missing.");

            names.Should().BeEmpty();
            diagnostics.Should().ContainSingle().Which.Should().Be("ChoicePanel: Modifiers missing.");
        }

        [TestMethod]
        public void ExtractUltimatumModifierNames_ReturnsEmpty_WhenModifierSequenceIsEmpty()
        {
            IReadOnlyList<string> names = UltimatumUiTreeResolver.ExtractUltimatumModifierNames(Array.Empty<object?>());

            names.Should().BeEmpty();
        }

        [TestMethod]
        public void ResolveUltimatumChoiceModifierName_PrefersPanelModifierName_WhenAvailable()
        {
            Element option = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));

            string modifierName = InvokeResolveUltimatumChoiceModifierName(option, 0, ["Ruin III"]);

            modifierName.Should().Be("Ruin III");
        }

        private static string InvokeResolveUltimatumChoiceModifierName(Element option, int seen, IReadOnlyList<string> modifierNamesByIndex)
        {
            MethodInfo method = typeof(UltimatumUiTreeResolver).GetMethod(
                "ResolveUltimatumChoiceModifierName",
                BindingFlags.NonPublic | BindingFlags.Static)!;

            method.Should().NotBeNull();

            return (string)method.Invoke(null, [option, seen, modifierNamesByIndex])!;
        }

        private sealed class ModifierNameProbe(string value)
        {
            public override string ToString() => value;
        }
    }
}