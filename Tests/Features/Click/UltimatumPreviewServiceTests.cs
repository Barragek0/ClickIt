namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class UltimatumPreviewServiceTests
    {
        [TestMethod]
        public void TryGetOptionPreview_ReturnsFalse_WhenPanelIsMissing_AndCachedLabelsAreEmpty()
        {
            int gruelingChecks = 0;
            UltimatumPreviewService service = CreateService(
                useNullGameController: true,
                cachedLabels: new TimeCache<List<LabelOnGround>>(() => [], 50),
                isGruelingGauntletPassiveActive: () =>
                {
                    gruelingChecks++;
                    return false;
                });

            bool result = service.TryGetOptionPreview(out List<UltimatumPanelOptionPreview> previews);

            result.Should().BeFalse();
            previews.Should().BeEmpty();
            gruelingChecks.Should().Be(0);
        }

        [TestMethod]
        public void TryGetOptionPreview_ReturnsFalse_WhenCachedLabelsServiceIsNull()
        {
            UltimatumPreviewService service = CreateService(
                useNullGameController: true,
                cachedLabels: null);

            bool result = service.TryGetOptionPreview(out List<UltimatumPanelOptionPreview> previews);

            result.Should().BeFalse();
            previews.Should().BeEmpty();
        }

        [TestMethod]
        public void TryGetOptionPreview_ReturnsFalse_WhenCachedLabelsValueIsNull()
        {
            UltimatumPreviewService service = CreateService(
                useNullGameController: true,
                cachedLabels: new TimeCache<List<LabelOnGround>>(() => null!, 50));

            bool result = service.TryGetOptionPreview(out List<UltimatumPanelOptionPreview> previews);

            result.Should().BeFalse();
            previews.Should().BeEmpty();
        }

        [TestMethod]
        public void TryGetOptionPreview_ReturnsFalse_WhenCachedLabelsContainOnlyNullEntries()
        {
            UltimatumPreviewService service = CreateService(
                useNullGameController: true,
                cachedLabels: new TimeCache<List<LabelOnGround>>(() => [null!], 50));

            bool result = service.TryGetOptionPreview(out List<UltimatumPanelOptionPreview> previews);

            result.Should().BeFalse();
            previews.Should().BeEmpty();
        }

        private static UltimatumPreviewService CreateService(
            ClickItSettings? settings = null,
            GameController? gameController = null,
            TimeCache<List<LabelOnGround>>? cachedLabels = null,
            bool useNullGameController = false,
            Func<bool>? isGruelingGauntletPassiveActive = null)
        {
            settings ??= new ClickItSettings();
            if (!useNullGameController)
                gameController ??= (GameController)RuntimeHelpers.GetUninitializedObject(typeof(GameController));
            cachedLabels ??= new TimeCache<List<LabelOnGround>>(() => [], 50);
            isGruelingGauntletPassiveActive ??= static () => false;

            var automation = new UltimatumAutomationServiceDependencies(
                settings,
                gameController!,
                cachedLabels!,
                _ => true,
                (_, _) => true,
                _ => { },
                (_, _) => { },
                () => { },
                () => false,
                _ => { });

            return new UltimatumPreviewService(new UltimatumPreviewServiceDependencies(
                automation,
                isGruelingGauntletPassiveActive));
        }
    }
}