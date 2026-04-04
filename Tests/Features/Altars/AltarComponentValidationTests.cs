namespace ClickIt.Tests.Features.Altars
{
    [TestClass]
    public class AltarComponentValidationTests
    {
        [TestMethod]
        public void IsComponentComplete_ReturnsTrue_WhenAllPartsExist()
        {
            AltarComponentValidation.IsComponentComplete(TestBuilders.BuildPrimary()).Should().BeTrue();
        }

        [TestMethod]
        public void IsComponentComplete_ReturnsFalse_WhenAnyPartIsMissing()
        {
            var complete = TestBuilders.BuildPrimary();
            var incomplete = new PrimaryAltarComponent(AltarType.Unknown, complete.TopMods, complete.TopButton, complete.BottomMods, complete.BottomButton)
            {
                BottomMods = null!
            };

            AltarComponentValidation.IsComponentComplete(incomplete).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldRemoveInvalidCachedComponent_ReturnsTrue_WhenElementIsMissing()
        {
            var component = TestBuilders.BuildPrimary();

            AltarComponentValidation.ShouldRemoveInvalidCachedComponent(component).Should().BeTrue();
        }
    }
}