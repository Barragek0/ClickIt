namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class UltimatumPanelButtonResolverTests
    {
        [TestMethod]
        public void TryResolveTakeRewardsButton_ReturnsFalse_WhenElementIsMissing()
        {
            List<string> logs = [];

            bool ok = UltimatumPanelButtonResolver.TryResolveTakeRewardsButton(
                takeRewardsElement: null,
                logs.Add,
                out Element resolved);

            ok.Should().BeFalse();
            resolved.Should().BeNull();
            logs.Should().ContainSingle(log => log.Contains("Take Rewards button missing", StringComparison.Ordinal));
        }

        [TestMethod]
        public void TryResolveConfirmButton_ReturnsFalse_WhenConfirmObjectIsMissing()
        {
            List<string> logs = [];

            bool ok = UltimatumPanelButtonResolver.TryResolveConfirmButton(
                confirmObj: null,
                logs.Add,
                out Element resolved);

            ok.Should().BeFalse();
            resolved.Should().BeNull();
            logs.Should().ContainSingle().Which.Should().Be("[TryClickUltimatumPanelConfirm] ConfirmButton missing.");
        }

        [TestMethod]
        public void TryResolveConfirmButton_ReturnsFalse_WhenConfirmObjectIsNotAnElement()
        {
            List<string> logs = [];

            bool ok = UltimatumPanelButtonResolver.TryResolveConfirmButton(
                confirmObj: new UltimatumUiTreeResolverTests.ReflectionFriendlyVisibilitylessPanel(),
                logs.Add,
                out Element resolved);

            ok.Should().BeFalse();
            resolved.Should().BeNull();
            logs.Should().ContainSingle().Which.Should().Be("[TryClickUltimatumPanelConfirm] ConfirmButton is not an Element.");
        }

        [TestMethod]
        public void TryResolveConfirmButton_ReturnsFalse_WhenExtractedElementIsNotVisible()
        {
            List<string> logs = [];
            Element confirmElement = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));

            bool ok = UltimatumPanelButtonResolver.TryResolveConfirmButton(
                confirmObj: new UltimatumUiTreeResolverTests.ReflectionFriendlyChoiceOption { OptionElement = confirmElement },
                logs.Add,
                out Element resolved);

            ok.Should().BeFalse();
            resolved.Should().BeNull();
            logs.Should().ContainSingle(log => log.Contains("ConfirmButton ignored", StringComparison.Ordinal));
        }

        [TestMethod]
        public void TryResolveTakeRewardsButton_ReturnsFalse_WhenElementIsNotVisible()
        {
            List<string> logs = [];
            Element takeRewardsElement = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));

            bool ok = UltimatumPanelButtonResolver.TryResolveTakeRewardsButton(
                takeRewardsElement,
                logs.Add,
                out Element resolved);

            ok.Should().BeFalse();
            resolved.Should().BeNull();
            logs.Should().ContainSingle(log => log.Contains("Take Rewards button ignored", StringComparison.Ordinal));
        }
    }
}