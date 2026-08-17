namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class LockedStrongboxLabelSuppressionTests
    {
        private const string StrongboxPath = "Metadata/Chests/StrongBoxes/Arcanist";

        private static LabelOnGround CreateStrongboxLabel(bool locked)
            => ClickPipelineScenarioFactory.CreateLabel(
                new RectangleF(100f, 100f, 60f, 20f),
                ClickPipelineScenarioFactory.CreateStrongbox(StrongboxPath, distance: 10f, address: 0x01, locked),
                address: 0x1001);

        [TestMethod]
        public void ShouldSuppress_ReturnsTrue_ForLockedStrongboxLabel()
        {
            LabelOnGround label = CreateStrongboxLabel(locked: true);

            LockedStrongboxLabelSuppression.ShouldSuppress(label).Should().BeTrue();
        }

        [TestMethod]
        public void ShouldSuppress_ReturnsFalse_ForOpenStrongboxLabel()
        {
            LabelOnGround label = CreateStrongboxLabel(locked: false);

            LockedStrongboxLabelSuppression.ShouldSuppress(label).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldSuppress_ReturnsFalse_ForNonStrongboxItem()
        {
            LabelOnGround label = ClickPipelineScenarioFactory.CreateLabel(
                new RectangleF(100f, 100f, 60f, 20f),
                ClickPipelineScenarioFactory.CreateWorldItem(distance: 10f, address: 0x02),
                address: 0x1002);

            LockedStrongboxLabelSuppression.ShouldSuppress(label).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldSuppress_ReturnsFalse_WhenLabelHasNoItem()
        {
            var label = (LabelProbe)RuntimeHelpers.GetUninitializedObject(typeof(LabelProbe));
            label.ItemOnGround = null;

            LockedStrongboxLabelSuppression.ShouldSuppress(label).Should().BeFalse();
        }
    }
}
