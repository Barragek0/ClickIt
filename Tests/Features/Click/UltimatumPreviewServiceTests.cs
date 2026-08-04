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

        [TestMethod]
        public void TryGetOptionPreview_WithWindowArea_SkipsUltimatumLabelFullyOffScreen()
        {
            RectangleF window = new(100f, 100f, 1280f, 720f);
            List<LabelOnGround> labels =
            [
                CreateUltimatumLabelWithOptions(
                    new RectangleF(9000f, 9000f, 300f, 40f),
                    [(new PreviewOptionElement(new RectangleF(150f, 200f, 300f, 40f)), "Test Modifier")])
            ];

            UltimatumPreviewService service = CreateService(cachedLabels: new TimeCache<List<LabelOnGround>>(() => labels, 50));

            bool result = service.TryGetOptionPreview(window, out List<UltimatumPanelOptionPreview> previews);

            result.Should().BeFalse();
            previews.Should().BeEmpty();
        }

        [TestMethod]
        public void TryGetOptionPreview_WithWindowArea_ProcessesUltimatumLabelOnScreen()
        {
            RectangleF window = new(100f, 100f, 1280f, 720f);
            List<LabelOnGround> labels =
            [
                CreateUltimatumLabelWithOptions(
                    new RectangleF(50f, 60f, 300f, 40f),
                    [(new PreviewOptionElement(new RectangleF(150f, 200f, 300f, 40f)), "Test Modifier")])
            ];

            UltimatumPreviewService service = CreateService(cachedLabels: new TimeCache<List<LabelOnGround>>(() => labels, 50));

            bool result = service.TryGetOptionPreview(window, out List<UltimatumPanelOptionPreview> previews);

            result.Should().BeTrue();
            previews.Should().HaveCount(1);
            previews[0].ModifierName.Should().Be("Test Modifier");
            previews[0].Rect.Should().Be(new RectangleF(150f, 200f, 300f, 40f));
        }

        [TestMethod]
        public void TryGetOptionPreview_ReusesGroundLabelResult_WhenLabelListReferenceUnchanged()
        {
            RectangleF window = new(100f, 100f, 1280f, 720f);
            List<LabelOnGround> labels =
            [
                CreateUltimatumLabelWithOptions(
                    new RectangleF(50f, 60f, 300f, 40f),
                    [(new PreviewOptionElement(new RectangleF(150f, 200f, 300f, 40f)), "Test Modifier")])
            ];
            var cached = new TimeCache<List<LabelOnGround>>(() => labels, 50);
            UltimatumPreviewService service = CreateService(cachedLabels: cached);

            service.TryGetOptionPreview(window, out List<UltimatumPanelOptionPreview> first).Should().BeTrue();
            first.Should().HaveCount(1);

            service.TryGetOptionPreview(window, out List<UltimatumPanelOptionPreview> second).Should().BeTrue();
            second.Should().HaveCount(1);
            second[0].ModifierName.Should().Be("Test Modifier");
        }

        [TestMethod]
        public void TryGetOptionPreview_Rescans_WhenLabelListReferenceChanges()
        {
            RectangleF window = new(100f, 100f, 1280f, 720f);
            List<LabelOnGround> currentLabels =
            [
                CreateUltimatumLabelWithOptions(
                    new RectangleF(50f, 60f, 300f, 40f),
                    [(new PreviewOptionElement(new RectangleF(150f, 200f, 300f, 40f)), "Test Modifier")])
            ];
            var cached = new TimeCache<List<LabelOnGround>>(() => currentLabels, 50);
            UltimatumPreviewService service = CreateService(cachedLabels: cached);

            service.TryGetOptionPreview(window, out _).Should().BeTrue();

            currentLabels = [];
            Thread.Sleep(60);

            service.TryGetOptionPreview(window, out List<UltimatumPanelOptionPreview> previews).Should().BeFalse();
            previews.Should().BeEmpty();
        }

        private static LabelOnGround CreateUltimatumLabelWithOptions(
            RectangleF labelRect,
            IReadOnlyList<(Element OptionElement, string ModifierName)> options)
        {
            Entity item = EntityProbeFactory.Create(path: "Metadata/Leagues/Ultimatum/Objects/UltimatumChallengeInteractable");
            PreviewTreeElement root = CreateUltimatumRoot(options);
            root.ClientRect = labelRect;

            PreviewLabelProbe label = (PreviewLabelProbe)RuntimeHelpers.GetUninitializedObject(typeof(PreviewLabelProbe));
            label.ItemOnGround = item;
            label.Label = root;
            return label;
        }

        private static PreviewTreeElement CreateUltimatumRoot(IReadOnlyList<(Element OptionElement, string ModifierName)> options)
        {
            UltimatumUiTreeResolverTests.ReflectionFriendlyNode optionContainer = new()
            {
                Children = options
                    .Select(static option => (object?)new UltimatumUiTreeResolverTests.ReflectionFriendlyChoiceOption
                    {
                        OptionElement = option.OptionElement,
                        Text = option.ModifierName
                    })
                    .ToArray()
            };

            PreviewTreeElement branch = new()
            {
                Children = new object?[]
                {
                    null,
                    null,
                    new UltimatumUiTreeResolverTests.ReflectionFriendlyNode { Children = new object?[] { optionContainer } },
                    null,
                    new PreviewTreeElement { Children = Array.Empty<object?>() }
                }
            };

            return new PreviewTreeElement
            {
                Children = new object?[] { new PreviewTreeElement { IsVisible = true, Children = new object?[] { branch } } }
            };
        }

        public sealed class PreviewLabelProbe : LabelOnGround
        {
            public new Entity? ItemOnGround { get; set; }

            public new Element? Label { get; set; }
        }

        public sealed class PreviewTreeElement : Element
        {
            public new bool IsVisible { get; set; } = true;

            public new IList? Children { get; set; }

            public RectangleF ClientRect { get; set; } = new(0f, 0f, 10f, 10f);

            public new object? GetChildAtIndex(int index)
                => Children != null && index >= 0 && index < Children.Count ? Children[index] : null;

            public override RectangleF GetClientRect() => ClientRect;
        }

        public sealed class PreviewOptionElement(RectangleF clientRect) : Element
        {
            public new bool IsValid { get; set; } = true;

            public new bool IsVisible { get; set; } = true;

            public override RectangleF GetClientRect() => clientRect;
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