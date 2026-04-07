namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class UltimatumGruelingGauntletDetectionStoreTests
    {
        [TestInitialize]
        public void ResetStore()
        {
            typeof(UltimatumGruelingGauntletDetectionStore)
                .GetField("_isActive", BindingFlags.NonPublic | BindingFlags.Static)!
                .SetValue(null, false);
            typeof(UltimatumGruelingGauntletDetectionStore)
                .GetField("_hasValue", BindingFlags.NonPublic | BindingFlags.Static)!
                .SetValue(null, false);
        }

        [TestMethod]
        public void TryGet_ReturnsFalseUntilAnyDetectionWasPublished()
        {
            bool hasValue = UltimatumGruelingGauntletDetectionStore.TryGet(out bool isActive);

            hasValue.Should().BeFalse();
            isActive.Should().BeFalse();
        }

        [TestMethod]
        public void Publish_SetsDetectionValueVisibleToSettingsConsumers()
        {
            UltimatumGruelingGauntletDetectionStore.Publish(true);
            UltimatumGruelingGauntletDetectionStore.TryGet(out bool activeAfterTrue).Should().BeTrue();
            activeAfterTrue.Should().BeTrue();

            UltimatumGruelingGauntletDetectionStore.Publish(false);
            UltimatumGruelingGauntletDetectionStore.TryGet(out bool activeAfterFalse).Should().BeTrue();
            activeAfterFalse.Should().BeFalse();
        }
    }
}