namespace ClickIt.Tests.Features.Labels
{
    [TestClass]
    public class LazyModeOverlayLogicTests
    {
        [TestMethod]
        public void ComposeLazyModeStatus_ReturnsRestrictionAndOverrideHint_WhenRestrictedAndHotkeyNotHeld()
        {
            var overlay = CreateOverlay();

            var result = InvokeComposeLazyModeStatus(
                overlay,
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
            var overlay = CreateOverlay();

            var result = InvokeComposeLazyModeStatus(
                overlay,
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
            var overlay = CreateOverlay(settings);

            var result = InvokeComposeLazyModeStatus(
                overlay,
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
                lazyModeDisableKeyBinding: settings.LazyModeDisableKeyBinding,
                isRitualActive: false,
                canActuallyClick: true);

            result.color.Should().Be(Color.Red);
            result.line1.Should().Be("Lazy mode disabled by hotkey.");
            result.line2.Should().Be("Press Y again to resume lazy clicking.");
            result.line3.Should().BeEmpty();
        }

        [TestMethod]
        public void ComposeLazyModeStatus_ReturnsReleaseHint_WhenDisableHotkeyIsHeldInNonToggleMode()
        {
            var overlay = CreateOverlay();

            var result = InvokeComposeLazyModeStatus(
                overlay,
                hasRestrictedItems: false,
                restrictionReason: string.Empty,
                hotkeyHeld: false,
                lazyModeDisableHeld: true,
                lazyModeDisableToggleMode: false,
                mouseButtonBlocks: false,
                leftClickBlocks: false,
                rightClickBlocks: false,
                gameController: null,
                clickLabelKey: Keys.T,
                isRitualActive: false,
                canActuallyClick: true);

            result.color.Should().Be(Color.Red);
            result.line1.Should().Be("Lazy mode disabled by hotkey.");
            result.line2.Should().Be("Release to resume lazy clicking.");
            result.line3.Should().BeEmpty();
        }

        [TestMethod]
        public void ComposeLazyModeStatus_ReturnsMouseBlockMessage_WhenMouseButtonsAreHeld()
        {
            var overlay = CreateOverlay();

            var result = InvokeComposeLazyModeStatus(
                overlay,
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
        public void ComposeLazyModeStatus_ReturnsCombinedMouseBlockMessage_WhenBothMouseButtonsAreHeld()
        {
            var overlay = CreateOverlay();

            var result = InvokeComposeLazyModeStatus(
                overlay,
                hasRestrictedItems: false,
                restrictionReason: string.Empty,
                hotkeyHeld: false,
                lazyModeDisableHeld: false,
                lazyModeDisableToggleMode: false,
                mouseButtonBlocks: true,
                leftClickBlocks: true,
                rightClickBlocks: true,
                gameController: null,
                clickLabelKey: Keys.T,
                isRitualActive: false,
                canActuallyClick: true);

            result.color.Should().Be(Color.Red);
            result.line1.Should().Be("both mouse buttons held.");
            result.line2.Should().Be("Release to resume lazy clicking.");
            result.line3.Should().BeEmpty();
        }

        [TestMethod]
        public void ComposeLazyModeStatus_ReturnsRitualBlockMessage_WhenRitualIsActiveWithoutOverride()
        {
            var overlay = CreateOverlay();

            var result = InvokeComposeLazyModeStatus(
                overlay,
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
        public void ComposeLazyModeStatus_ReturnsOverrideStatus_WhenRitualIsActiveAndHotkeyHeld()
        {
            var overlay = CreateOverlay();

            var result = InvokeComposeLazyModeStatus(
                overlay,
                hasRestrictedItems: false,
                restrictionReason: string.Empty,
                hotkeyHeld: true,
                lazyModeDisableHeld: false,
                lazyModeDisableToggleMode: false,
                mouseButtonBlocks: false,
                leftClickBlocks: false,
                rightClickBlocks: false,
                gameController: null,
                clickLabelKey: Keys.T,
                isRitualActive: true,
                canActuallyClick: true);

            result.color.Should().Be(Color.LawnGreen);
            result.line1.Should().Be("Blocking overridden by hotkey.");
            result.line2.Should().BeEmpty();
            result.line3.Should().BeEmpty();
        }

        [TestMethod]
        public void ComposeLazyModeStatus_ReturnsInputFailureReason_WhenClickingIsUnavailable()
        {
            var overlay = CreateOverlay();

            var result = InvokeComposeLazyModeStatus(
                overlay,
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
            var overlay = CreateOverlay();

            var result = InvokeComposeLazyModeStatus(
                overlay,
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
            var lines = LazyModeOverlay.WrapOverlayText("first line\n\nthis line should wrap into chunks", 12);

            lines.Should().NotBeEmpty();
            lines[0].Should().Be("first line");
            lines.Should().OnlyContain(x => x.Length <= 12);
        }

        [TestMethod]
        public void GetLazyModeRestrictionDisplayReason_ReturnsGenericForBlankAndTrimmedValueOtherwise()
        {
            LazyModeOverlay.GetLazyModeRestrictionDisplayReason(null).Should().Be("Lazy mode blocking condition detected.");
            LazyModeOverlay.GetLazyModeRestrictionDisplayReason("   ").Should().Be("Lazy mode blocking condition detected.");
            LazyModeOverlay.GetLazyModeRestrictionDisplayReason("  Rare monster nearby.  ").Should().Be("Rare monster nearby.");
        }

        [TestMethod]
        public void GetBlockingMouseButtonName_ReturnsExpectedLabels_ForRightAndBothButtons()
        {
            LazyModeOverlay.GetBlockingMouseButtonName(leftClickBlocks: false, rightClickBlocks: true)
                .Should().Be("Right mouse button");

            LazyModeOverlay.GetBlockingMouseButtonName(leftClickBlocks: true, rightClickBlocks: true)
                .Should().Be("both mouse buttons");
        }

        [TestMethod]
        public void RenderLazyModeText_EnqueuesTitleAndWrappedBodyLines()
        {
            var queue = new DeferredTextQueue();
            var overlay = CreateOverlay();

            overlay.RenderLazyModeText(
                queue,
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

        [TestMethod]
        public void HoldAndToggleHintHelpers_RefreshCachedHints_WhenKeysChange()
        {
            var overlay = CreateOverlay();

            InvokeGetHoldClickLabelHint(overlay, Keys.T).Should().Be("Hold T to override.");
            InvokeGetHoldClickLabelHint(overlay, Keys.Y).Should().Be("Hold Y to override.");

            InvokeGetToggleDisableHint(overlay, Keys.U).Should().Be("Press U again to resume lazy clicking.");
            InvokeGetToggleDisableHint(overlay, Keys.I).Should().Be("Press I again to resume lazy clicking.");
        }

        private static LazyModeOverlay CreateOverlay(
            ClickItSettings? settings = null,
            InputHandler? inputHandler = null,
            LazyModeBlockerService? lazyModeBlockerService = null)
        {
            settings ??= new ClickItSettings();
            inputHandler ??= new InputHandler(settings);

            return new LazyModeOverlay(inputHandler, lazyModeBlockerService);
        }

        private static (Color color, string line1, string line2, string line3) InvokeComposeLazyModeStatus(
            LazyModeOverlay overlay,
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
            Keys lazyModeDisableKeyBinding = Keys.None,
            bool isRitualActive = false,
            bool canActuallyClick = true)
        {
            MethodInfo method = typeof(LazyModeOverlay).GetMethod("ComposeLazyModeStatus", BindingFlags.Instance | BindingFlags.NonPublic)!;
            object result = method.Invoke(
                overlay,
                [hasRestrictedItems, restrictionReason, hotkeyHeld, lazyModeDisableHeld, lazyModeDisableToggleMode, mouseButtonBlocks, leftClickBlocks, rightClickBlocks, gameController, clickLabelKey, lazyModeDisableKeyBinding, isRitualActive, canActuallyClick])!;

            return ((Color color, string line1, string line2, string line3))result;
        }

        private static string InvokeGetHoldClickLabelHint(LazyModeOverlay overlay, Keys clickLabelKey)
        {
            MethodInfo method = typeof(LazyModeOverlay).GetMethod("GetHoldClickLabelHint", BindingFlags.Instance | BindingFlags.NonPublic)!;
            return (string)method.Invoke(overlay, [clickLabelKey])!;
        }

        private static string InvokeGetToggleDisableHint(LazyModeOverlay overlay, Keys disableKey)
        {
            MethodInfo method = typeof(LazyModeOverlay).GetMethod("GetToggleDisableHint", BindingFlags.Instance | BindingFlags.NonPublic)!;
            return (string)method.Invoke(overlay, [disableKey])!;
        }
    }
}
