namespace ClickIt.Tests.UI
{
    [TestClass]
    public class LazyModeRendererLogicTests
    {
        [TestMethod]
        public void ComposeLazyModeStatus_ReturnsRestrictionAndOverrideHint_WhenRestrictedAndHotkeyNotHeld()
        {
            var renderer = CreateRenderer();

            var result = InvokeComposeLazyModeStatus(
                renderer,
                hasRestrictedItems: true,
                restrictionReason: "Rare monster nearby.",
                hotkeyHeld: false,
                lazyModeDisableHeld: false,
                lazyModeDisableToggleMode: false,
                mouseButtonBlocks: false,
                leftClickBlocks: false,
                rightClickBlocks: false,
                gameController: null,
                clickLabelKey: Keys.T,
                isRitualActive: false,
                canActuallyClick: true);

            result.color.Should().Be(Color.Red);
            result.line1.Should().Be("Rare monster nearby.");
            result.line2.Should().Be("Hold T to override.");
            result.line3.Should().BeEmpty();
        }

        [TestMethod]
        public void ComposeLazyModeStatus_ReturnsOverrideStatus_WhenRestrictedAndHotkeyHeld()
        {
            var renderer = CreateRenderer();

            var result = InvokeComposeLazyModeStatus(
                renderer,
                hasRestrictedItems: true,
                restrictionReason: "Rare monster nearby.",
                hotkeyHeld: true,
                lazyModeDisableHeld: false,
                lazyModeDisableToggleMode: false,
                mouseButtonBlocks: false,
                leftClickBlocks: false,
                rightClickBlocks: false,
                gameController: null,
                clickLabelKey: Keys.T,
                isRitualActive: false,
                canActuallyClick: true);

            result.color.Should().Be(Color.LawnGreen);
            result.line1.Should().Be("Blocking overridden by hotkey.");
            result.line2.Should().BeEmpty();
            result.line3.Should().BeEmpty();
        }

        [TestMethod]
        public void ComposeLazyModeStatus_ReturnsToggleResumeHint_WhenDisableHotkeyIsLatched()
        {
            var settings = new ClickItSettings();
            settings.LazyModeDisableKey = new HotkeyNodeV2(Keys.Y);
            var renderer = CreateRenderer(settings);

            var result = InvokeComposeLazyModeStatus(
                renderer,
                hasRestrictedItems: false,
                restrictionReason: string.Empty,
                hotkeyHeld: false,
                lazyModeDisableHeld: true,
                lazyModeDisableToggleMode: true,
                mouseButtonBlocks: false,
                leftClickBlocks: false,
                rightClickBlocks: false,
                gameController: null,
                clickLabelKey: Keys.T,
                isRitualActive: false,
                canActuallyClick: true);

            result.color.Should().Be(Color.Red);
            result.line1.Should().Be("Lazy mode disabled by hotkey.");
            result.line2.Should().Be("Press Y again to resume lazy clicking.");
            result.line3.Should().BeEmpty();
        }

        [TestMethod]
        public void ComposeLazyModeStatus_ReturnsMouseBlockMessage_WhenMouseButtonsAreHeld()
        {
            var renderer = CreateRenderer();

            var result = InvokeComposeLazyModeStatus(
                renderer,
                hasRestrictedItems: false,
                restrictionReason: string.Empty,
                hotkeyHeld: false,
                lazyModeDisableHeld: false,
                lazyModeDisableToggleMode: false,
                mouseButtonBlocks: true,
                leftClickBlocks: true,
                rightClickBlocks: false,
                gameController: null,
                clickLabelKey: Keys.T,
                isRitualActive: false,
                canActuallyClick: true);

            result.color.Should().Be(Color.Red);
            result.line1.Should().Be("Left mouse button held.");
            result.line2.Should().Be("Release to resume lazy clicking.");
            result.line3.Should().BeEmpty();
        }

        [TestMethod]
        public void ComposeLazyModeStatus_ReturnsRitualBlockMessage_WhenRitualIsActiveWithoutOverride()
        {
            var renderer = CreateRenderer();

            var result = InvokeComposeLazyModeStatus(
                renderer,
                hasRestrictedItems: false,
                restrictionReason: string.Empty,
                hotkeyHeld: false,
                lazyModeDisableHeld: false,
                lazyModeDisableToggleMode: false,
                mouseButtonBlocks: false,
                leftClickBlocks: false,
                rightClickBlocks: false,
                gameController: null,
                clickLabelKey: Keys.T,
                isRitualActive: true,
                canActuallyClick: true);

            result.color.Should().Be(Color.Red);
            result.line1.Should().Be("Ritual in progress.");
            result.line2.Should().Be("Complete it to resume lazy clicking.");
            result.line3.Should().BeEmpty();
        }

        [TestMethod]
        public void ComposeLazyModeStatus_ReturnsInputFailureReason_WhenClickingIsUnavailable()
        {
            var renderer = CreateRenderer();

            var result = InvokeComposeLazyModeStatus(
                renderer,
                hasRestrictedItems: false,
                restrictionReason: string.Empty,
                hotkeyHeld: false,
                lazyModeDisableHeld: false,
                lazyModeDisableToggleMode: false,
                mouseButtonBlocks: false,
                leftClickBlocks: false,
                rightClickBlocks: false,
                gameController: null,
                clickLabelKey: Keys.T,
                isRitualActive: false,
                canActuallyClick: false);

            result.color.Should().Be(Color.Red);
            result.line1.Should().Be("PoE not in focus.");
            result.line2.Should().BeEmpty();
            result.line3.Should().BeEmpty();
        }

        [TestMethod]
        public void ComposeLazyModeStatus_ReturnsReadyState_WhenNoBlockersApply()
        {
            var renderer = CreateRenderer();

            var result = InvokeComposeLazyModeStatus(
                renderer,
                hasRestrictedItems: false,
                restrictionReason: string.Empty,
                hotkeyHeld: false,
                lazyModeDisableHeld: false,
                lazyModeDisableToggleMode: false,
                mouseButtonBlocks: false,
                leftClickBlocks: false,
                rightClickBlocks: false,
                gameController: null,
                clickLabelKey: Keys.T,
                isRitualActive: false,
                canActuallyClick: true);

            result.color.Should().Be(Color.LawnGreen);
            result.line1.Should().BeEmpty();
            result.line2.Should().BeEmpty();
            result.line3.Should().BeEmpty();
        }

        [TestMethod]
        public void WrapOverlayText_WrapsLongTextAndSkipsBlankLines()
        {
            var lines = LazyModeRenderer.WrapOverlayText("first line\n\nthis line should wrap into chunks", 12);

            lines.Should().NotBeEmpty();
            lines[0].Should().Be("first line");
            lines.Should().OnlyContain(x => x.Length <= 12);
        }

        [TestMethod]
        public void RenderLazyModeText_EnqueuesTitleAndWrappedBodyLines()
        {
            var queue = new DeferredTextQueue();
            var renderer = CreateRenderer(deferredTextQueue: queue);

            InvokeRenderLazyModeText(
                renderer,
                centerX: 300f,
                topY: 60f,
                color: Color.Red,
                line1: "This is a deliberately long first line that must wrap.",
                line2: "Second line.",
                line3: string.Empty);

            string[] snapshot = queue.GetPendingTextSnapshot();
            snapshot.Should().NotBeEmpty();
            snapshot[0].Should().Be("Lazy Mode");
            snapshot.Should().Contain("Second line.");
            snapshot.Should().Contain(line => line.StartsWith("This is a deliberately long"));
        }

        private static LazyModeRenderer CreateRenderer(
            ClickItSettings? settings = null,
            DeferredTextQueue? deferredTextQueue = null,
            InputHandler? inputHandler = null,
            LazyModeBlockerService? lazyModeBlockerService = null)
        {
            settings ??= new ClickItSettings();
            inputHandler ??= new InputHandler(settings);
            deferredTextQueue ??= new DeferredTextQueue();

            return new LazyModeRenderer(settings, deferredTextQueue, inputHandler, lazyModeBlockerService);
        }

        private static (Color color, string line1, string line2, string line3) InvokeComposeLazyModeStatus(
            LazyModeRenderer renderer,
            bool hasRestrictedItems,
            string restrictionReason,
            bool hotkeyHeld,
            bool lazyModeDisableHeld,
            bool lazyModeDisableToggleMode,
            bool mouseButtonBlocks,
            bool leftClickBlocks,
            bool rightClickBlocks,
            GameController? gameController,
            Keys clickLabelKey,
            bool isRitualActive,
            bool canActuallyClick)
        {
            MethodInfo method = typeof(LazyModeRenderer).GetMethod("ComposeLazyModeStatus", BindingFlags.Instance | BindingFlags.NonPublic)!;
            object result = method.Invoke(
                renderer,
                [
                    hasRestrictedItems,
                    restrictionReason,
                    hotkeyHeld,
                    lazyModeDisableHeld,
                    lazyModeDisableToggleMode,
                    mouseButtonBlocks,
                    leftClickBlocks,
                    rightClickBlocks,
                    gameController,
                    clickLabelKey,
                    isRitualActive,
                    canActuallyClick
                ])!;

            Type resultType = result.GetType();
            return (
                (Color)resultType.GetField("Item1")!.GetValue(result)!,
                (string)resultType.GetField("Item2")!.GetValue(result)!,
                (string)resultType.GetField("Item3")!.GetValue(result)!,
                (string)resultType.GetField("Item4")!.GetValue(result)!);
        }

        private static void InvokeRenderLazyModeText(
            LazyModeRenderer renderer,
            float centerX,
            float topY,
            Color color,
            string line1,
            string line2,
            string line3)
        {
            MethodInfo method = typeof(LazyModeRenderer).GetMethod("RenderLazyModeText", BindingFlags.Instance | BindingFlags.NonPublic)!;
            method.Invoke(renderer, [centerX, topY, color, line1, line2, line3]);
        }
    }
}