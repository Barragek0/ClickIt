namespace ClickIt.Tests.Features.Labels.Classification
{
    [TestClass]
    public class LabelInteractionRuleServiceTests
    {
        [TestMethod]
        public void ShouldClickEssence_ReturnsFalse_WhenEssenceClickingDisabled()
        {
            LabelOnGround label = ExileCoreOpaqueFactory.CreateOpaqueLabel();

            bool result = LabelInteractionRuleService.ShouldClickEssence(clickEssences: false, label);

            result.Should().BeFalse();
        }

        [TestMethod]
        public void GetRitualMechanicId_ReturnsNull_WhenPathIsNotRitual()
        {
            LabelOnGround label = ExileCoreOpaqueFactory.CreateOpaqueLabel();

            string? mechanicId = LabelInteractionRuleService.GetRitualMechanicId(
                clickRitualInitiate: true,
                clickRitualCompleted: true,
                path: "Metadata/Terrain/Chests/SomeOtherChest",
                label);

            mechanicId.Should().BeNull();
        }

        [TestMethod]
        public void ShouldClickStrongbox_ReturnsFalse_WhenPathIsMissing()
        {
            var settings = new ClickSettings
            {
                StrongboxClickMetadata = ["special:strongbox-unique"]
            };

            bool result = LabelInteractionRuleService.ShouldClickStrongbox(settings, string.Empty, label: null!);

            result.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickStrongbox_ReturnsFalse_WhenLabelHasNoItem()
        {
            var settings = new ClickSettings
            {
                StrongboxClickMetadata = ["special:strongbox-unique"]
            };

            bool result = LabelInteractionRuleService.ShouldClickStrongbox(settings, "Metadata/Chests/StrongBoxes/Arcanist", label: null!);

            result.Should().BeFalse();
        }
    }
}