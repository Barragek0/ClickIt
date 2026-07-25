namespace ClickIt.Tests.Features.Click.Interaction
{
    [TestClass]
    public class InteractionExecutorTests
    {
        [TestMethod]
        public void PerformClick_ReturnsWithoutIncrementingSequence_WhenHotkeyInactiveAndNotLazy()
        {
            var settings = new ClickItSettings();
            var executor = new InteractionExecutor(settings, new PerformanceMonitor(settings), () => false);

            executor.PerformClick(new Vector2(10f, 20f));

            executor.GetSuccessfulClickSequence().Should().Be(0);
        }

        [TestMethod]
        public void PerformClick_LogsAndReturns_WhenLazyModeLimiterIsActive()
        {
            var settings = new ClickItSettings();
            settings.DebugMode.Value = true;
            settings.LogMessages.Value = true;
            settings.LazyMode.Value = true;
            settings.LazyModeClickLimiting.Value = 500;
            var messages = new List<string>();
            var executor = new InteractionExecutor(
                settings,
                new PerformanceMonitor(settings),
                () => true,
                new ErrorHandler(settings, static (_, _) => { }, (message, _) => messages.Add(message)));

            RuntimeMemberAccessor.SetRequiredMember(executor, "_lastClickTimestampMs", Environment.TickCount64);

            executor.PerformClick(new Vector2(10f, 20f));

            executor.GetSuccessfulClickSequence().Should().Be(0);
            messages.Should().ContainSingle(message => message.Contains("LazyMode limiter", StringComparison.Ordinal));
        }

        [TestMethod]
        public void PerformClick_LogsAndReturns_WhenPointOutsideVirtualScreen()
        {
            var settings = new ClickItSettings();
            settings.DebugMode.Value = true;
            settings.LogMessages.Value = true;
            var messages = new List<string>();
            var executor = new InteractionExecutor(
                settings,
                new PerformanceMonitor(settings),
                () => true,
                new ErrorHandler(settings, static (_, _) => { }, (message, _) => messages.Add(message)));

            executor.PerformClick(new Vector2(-5000f, -5000f));

            executor.GetSuccessfulClickSequence().Should().Be(0);
            messages.Should().ContainSingle(message => message.Contains("Skipping click at", StringComparison.Ordinal)
                && message.Contains("outside virtual screen bounds", StringComparison.Ordinal));
        }

        [TestMethod]
        public void PerformClickAndHold_ReturnsWithoutIncrementingSequence_WhenKeyBindingIsNone()
        {
            var settings = new ClickItSettings
            {
                ClickLabelKey = new HotkeyNodeV2(Keys.None)
            };
            var executor = new InteractionExecutor(settings, new PerformanceMonitor(settings), () => true);

            executor.PerformClickAndHold(new Vector2(10f, 20f), holdDurationMs: 100);

            executor.GetSuccessfulClickSequence().Should().Be(0);
        }

        [TestMethod]
        public void PerformClickAndHold_ReturnsWithoutIncrementingSequence_WhenHotkeyInactiveAndNotLazy()
        {
            var settings = new ClickItSettings
            {
                ClickLabelKey = new HotkeyNodeV2(Keys.F)
            };
            var executor = new InteractionExecutor(settings, new PerformanceMonitor(settings), () => false);

            executor.PerformClickAndHold(new Vector2(10f, 20f), holdDurationMs: 100);

            executor.GetSuccessfulClickSequence().Should().Be(0);
        }

        [TestMethod]
        public void PerformClickAndHold_LogsAndReturns_WhenLazyModeLimiterIsActive()
        {
            var settings = new ClickItSettings
            {
                ClickLabelKey = new HotkeyNodeV2(Keys.F)
            };
            settings.DebugMode.Value = true;
            settings.LogMessages.Value = true;
            settings.LazyMode.Value = true;
            settings.LazyModeClickLimiting.Value = 500;
            var messages = new List<string>();
            var executor = new InteractionExecutor(
                settings,
                new PerformanceMonitor(settings),
                () => true,
                new ErrorHandler(settings, static (_, _) => { }, (message, _) => messages.Add(message)));

            RuntimeMemberAccessor.SetRequiredMember(executor, "_lastClickTimestampMs", Environment.TickCount64);

            executor.PerformClickAndHold(new Vector2(10f, 20f), holdDurationMs: 100);

            executor.GetSuccessfulClickSequence().Should().Be(0);
            messages.Should().ContainSingle(message => message.Contains("LazyMode limiter", StringComparison.Ordinal));
        }

        [TestMethod]
        public void PerformClickAndHold_LogsAndReturns_WhenPointOutsideVirtualScreen()
        {
            var settings = new ClickItSettings
            {
                ClickLabelKey = new HotkeyNodeV2(Keys.F)
            };
            settings.DebugMode.Value = true;
            settings.LogMessages.Value = true;
            var messages = new List<string>();
            var executor = new InteractionExecutor(
                settings,
                new PerformanceMonitor(settings),
                () => true,
                new ErrorHandler(settings, static (_, _) => { }, (message, _) => messages.Add(message)));

            executor.PerformClickAndHold(new Vector2(-5000f, -5000f), holdDurationMs: 100);

            executor.GetSuccessfulClickSequence().Should().Be(0);
            messages.Should().ContainSingle(message => message.Contains("Skipping hold click at", StringComparison.Ordinal)
                && message.Contains("outside virtual screen bounds", StringComparison.Ordinal));
        }

        [TestMethod]
        public void PerformClick_LogsInvalidPoint_WhenHotkeyInactiveButAllowed()
        {
            var settings = new ClickItSettings();
            settings.DebugMode.Value = true;
            settings.LogMessages.Value = true;
            var messages = new List<string>();
            var executor = new InteractionExecutor(
                settings,
                new PerformanceMonitor(settings),
                () => false,
                new ErrorHandler(settings, static (_, _) => { }, (message, _) => messages.Add(message)));

            executor.PerformClick(new Vector2(-5000f, -5000f), allowWhenHotkeyInactive: true);

            executor.GetSuccessfulClickSequence().Should().Be(0);
            messages.Should().ContainSingle(message => message.Contains("Skipping click at", StringComparison.Ordinal)
                && message.Contains("outside virtual screen bounds", StringComparison.Ordinal));
        }

        [TestMethod]
        public void PerformClickAndHold_LogsInvalidPoint_WhenHotkeyInactiveButAllowed()
        {
            var settings = new ClickItSettings
            {
                ClickLabelKey = new HotkeyNodeV2(Keys.F)
            };
            settings.DebugMode.Value = true;
            settings.LogMessages.Value = true;
            var messages = new List<string>();
            var executor = new InteractionExecutor(
                settings,
                new PerformanceMonitor(settings),
                () => false,
                new ErrorHandler(settings, static (_, _) => { }, (message, _) => messages.Add(message)));

            executor.PerformClickAndHold(new Vector2(-5000f, -5000f), holdDurationMs: 100, allowWhenHotkeyInactive: true);

            executor.GetSuccessfulClickSequence().Should().Be(0);
            messages.Should().ContainSingle(message => message.Contains("Skipping hold click at", StringComparison.Ordinal)
                && message.Contains("outside virtual screen bounds", StringComparison.Ordinal));
        }

        [TestMethod]
        public void HoverAndGetUIHover_ReturnsNull_WhenPointIsOutsideVirtualScreen()
        {
            var settings = new ClickItSettings();
            var executor = new InteractionExecutor(settings, new PerformanceMonitor(settings), () => true);
            GameController gameController = ExileCoreVisibleObjectBuilder.CreateGameControllerWithWindow(new RectangleF(100f, 200f, 1280f, 720f));

            Element? hover = executor.HoverAndGetUIHover(new Vector2(-5000f, -5000f), gameController);

            hover.Should().BeNull();
        }

        [TestMethod]
        public void HoverAndGetUIHover_ReturnsNull_WhenGameControllerIsNull()
        {
            var settings = new ClickItSettings();
            var executor = new InteractionExecutor(settings, new PerformanceMonitor(settings), () => true);

            Element? hover = executor.HoverAndGetUIHover(new Vector2(100f, 200f), null);

            hover.Should().BeNull();
        }

        [DataTestMethod]
        [DataRow(false, false, false, true)]
        [DataRow(false, true, false, false)]
        [DataRow(true, false, false, false)]
        [DataRow(true, true, false, false)]
        [DataRow(false, false, true, false)]
        [DataRow(false, true, true, false)]
        [DataRow(true, false, true, false)]
        [DataRow(true, true, true, false)]
        public void ShouldSkipClickWhenNotLazyAndHotkeyInactive_ReturnsExpected(bool lazyModeEnabled, bool clickHotkeyActive, bool allowWhenHotkeyInactive, bool expected)
        {
            bool shouldSkip = InteractionExecutor.ShouldSkipClickWhenNotLazyAndHotkeyInactive(lazyModeEnabled, clickHotkeyActive, allowWhenHotkeyInactive);

            shouldSkip.Should().Be(expected);
        }

        [DataTestMethod]
        [DataRow(false, false, 0UL, 0UL, false, false)]
        [DataRow(false, true, 0UL, 999UL, false, false)]
        [DataRow(false, false, 123UL, 999UL, true, true)]
        [DataRow(true, false, 123UL, 123UL, false, false)]
        [DataRow(true, false, 123UL, 0UL, false, true)]
        [DataRow(false, false, 123UL, 999UL, false, false)]
        [DataRow(false, true, 123UL, 999UL, false, true)]
        [DataRow(false, true, 123UL, 123UL, false, false)]
        [DataRow(false, false, 123UL, 0UL, true, true)]
        [DataRow(false, false, 0UL, 999UL, true, false)]
        [DataRow(true, true, 123UL, 999UL, false, true)]
        [DataRow(true, true, 123UL, 123UL, true, false)]
        public void ShouldSkipClickDueToHoverMismatch_ReturnsExpected(
            bool lazyModeEnabled,
            bool verifyUiHoverWhenNotLazy,
            ulong expectedAddress,
            ulong hoverAddress,
            bool forceUiHoverVerification,
            bool expected)
        {
            bool shouldSkip = InteractionExecutor.ShouldSkipClickDueToHoverMismatch(
                lazyModeEnabled,
                verifyUiHoverWhenNotLazy,
                expectedAddress,
                hoverAddress,
                forceUiHoverVerification);

            shouldSkip.Should().Be(expected);
        }

        [TestMethod]
        public void ResolveClickExecutionPosition_ReturnsRequestedPosition_WhenCursorMoveIsAllowed()
        {
            Vector2 requested = new(321f, 654f);

            Vector2 resolved = InteractionExecutor.ResolveClickExecutionPosition(requested, avoidCursorMove: false);

            resolved.Should().Be(requested);
        }

        [TestMethod]
        public void ResolveClickExecutionPosition_ReturnsCurrentCursorPosition_WhenCursorMoveIsAvoided()
        {
            var cursor = Mouse.GetCursorPosition();

            Vector2 resolved = InteractionExecutor.ResolveClickExecutionPosition(new Vector2(321f, 654f), avoidCursorMove: true);

            resolved.Should().Be(new Vector2(cursor.X, cursor.Y));
        }

        [TestMethod]
        public void TryPrepareClickExecution_ReturnsTrue_AndLogsExpectedElementMissing_WhenExecutionCanProceed()
        {
            var settings = new ClickItSettings();
            settings.DebugMode.Value = true;
            settings.LogMessages.Value = true;
            var messages = new List<string>();
            var executor = new InteractionExecutor(
                settings,
                new PerformanceMonitor(settings),
                () => true,
                new ErrorHandler(settings, static (_, _) => { }, (message, _) => messages.Add(message)));

            bool ok = InvokeTryPrepareClickExecution(
                executor,
                position: new Vector2(50f, 50f),
                expectedElement: null,
                gameController: null,
                forceUiHoverVerification: false,
                allowWhenHotkeyInactive: false,
                avoidCursorMove: true,
                clickKind: "click",
                hoverMismatchMessage: "hover mismatch",
                logExpectedElementMissing: true,
                out _,
                out _);

            ok.Should().BeTrue();
            messages.Should().ContainSingle(message => message.Contains("expectedElement is null", StringComparison.Ordinal));
        }

        [TestMethod]
        public void TryPrepareClickExecution_ReturnsFalse_AndLogsHoverMismatch_WhenForcedVerificationFails()
        {
            var settings = new ClickItSettings();
            settings.DebugMode.Value = true;
            settings.LogMessages.Value = true;
            var messages = new List<string>();
            var executor = new InteractionExecutor(
                settings,
                new PerformanceMonitor(settings),
                () => true,
                new ErrorHandler(settings, static (_, _) => { }, (message, _) => messages.Add(message)));
            Element expectedElement = CreateElementWithAddress(123);

            bool ok = InvokeTryPrepareClickExecution(
                executor,
                position: new Vector2(50f, 50f),
                expectedElement: expectedElement,
                gameController: null,
                forceUiHoverVerification: true,
                allowWhenHotkeyInactive: false,
                avoidCursorMove: true,
                clickKind: "click",
                hoverMismatchMessage: "hover mismatch",
                logExpectedElementMissing: true,
                out _,
                out _);

            ok.Should().BeFalse();
            messages.Should().ContainSingle(message => message.Contains("hover mismatch", StringComparison.Ordinal));
        }

        [TestMethod]
        public void MarkLazyModeClickCompleted_UpdatesTimestamp_WhenLazyModeEnabled()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = true;
            var executor = new InteractionExecutor(settings, new PerformanceMonitor(settings), () => true);

            InvokePrivateVoid(executor, "MarkLazyModeClickCompleted");

            ReadPrivateLong(executor, "_lastClickTimestampMs").Should().BeGreaterThan(0);
        }

        [TestMethod]
        public void MarkLazyModeClickCompleted_DoesNotUpdateTimestamp_WhenLazyModeDisabled()
        {
            var settings = new ClickItSettings();
            var executor = new InteractionExecutor(settings, new PerformanceMonitor(settings), () => true);
            RuntimeMemberAccessor.SetRequiredMember(executor, "_lastClickTimestampMs", 77L);

            InvokePrivateVoid(executor, "MarkLazyModeClickCompleted");

            ReadPrivateLong(executor, "_lastClickTimestampMs").Should().Be(77);
        }

        [TestMethod]
        public void TryConsumeLazyModeLimiter_ReturnsTrue_WhenNoPreviousLazyClickWasRecorded()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = true;
            settings.LazyModeClickLimiting.Value = 500;
            var executor = new InteractionExecutor(settings, new PerformanceMonitor(settings), () => true);

            InvokePrivateBool(executor, "TryConsumeLazyModeLimiter").Should().BeTrue();
        }

        [TestMethod]
        public void RestoreCursorIfLazyMode_DoesNothing_WhenLazyModeIsDisabled()
        {
            var settings = new ClickItSettings();
            settings.DebugMode.Value = true;
            settings.LogMessages.Value = true;
            var messages = new List<string>();
            var executor = new InteractionExecutor(
                settings,
                new PerformanceMonitor(settings),
                () => true,
                new ErrorHandler(settings, static (_, _) => { }, (message, _) => messages.Add(message)));

            InvokePrivateVoid(executor, "RestoreCursorIfLazyMode", [new System.Drawing.Point(10, 20), null]);

            messages.Should().BeEmpty();
        }

        [TestMethod]
        public void RestoreCursorIfLazyMode_DoesNothing_WhenRestoreCursorSettingIsDisabled()
        {
            var settings = new ClickItSettings();
            settings.DebugMode.Value = true;
            settings.LogMessages.Value = true;
            settings.LazyMode.Value = true;
            settings.RestoreCursorInLazyMode.Value = false;
            var messages = new List<string>();
            var executor = new InteractionExecutor(
                settings,
                new PerformanceMonitor(settings),
                () => true,
                new ErrorHandler(settings, static (_, _) => { }, (message, _) => messages.Add(message)));

            InvokePrivateVoid(executor, "RestoreCursorIfLazyMode", [new System.Drawing.Point(10, 20), null]);

            messages.Should().BeEmpty();
        }

        [TestMethod]
        public void RestoreCursorIfLazyMode_ReturnsWithoutLogging_WhenPreviousCursorPointIsUnsafe()
        {
            var settings = new ClickItSettings();
            settings.DebugMode.Value = true;
            settings.LogMessages.Value = true;
            settings.LazyMode.Value = true;
            settings.RestoreCursorInLazyMode.Value = true;
            var messages = new List<string>();
            var executor = new InteractionExecutor(
                settings,
                new PerformanceMonitor(settings),
                () => true,
                new ErrorHandler(settings, static (_, _) => { }, (message, _) => messages.Add(message)));

            InvokePrivateVoid(executor, "RestoreCursorIfLazyMode", [new System.Drawing.Point(-5000, -5000), null]);

            messages.Should().BeEmpty();
        }

        private static bool InvokeTryPrepareClickExecution(
            InteractionExecutor executor,
            Vector2 position,
            Element? expectedElement,
            GameController? gameController,
            bool forceUiHoverVerification,
            bool allowWhenHotkeyInactive,
            bool avoidCursorMove,
            string clickKind,
            string hoverMismatchMessage,
            bool logExpectedElementMissing,
            out Stopwatch stopwatch,
            out System.Drawing.Point before)
        {
            object?[] args =
            [
                position,
                expectedElement,
                gameController,
                forceUiHoverVerification,
                allowWhenHotkeyInactive,
                avoidCursorMove,
                clickKind,
                hoverMismatchMessage,
                logExpectedElementMissing,
                null,
                null
            ];

            bool result = InvokePrivate<bool>(executor, "TryPrepareClickExecution", args);
            stopwatch = (Stopwatch)args[9]!;
            before = (System.Drawing.Point)args[10]!;
            return result;
        }

        private static bool InvokePrivateBool(object instance, string methodName)
            => InvokePrivate<bool>(instance, methodName, []);

        private static void InvokePrivateVoid(object instance, string methodName)
            => InvokePrivate<object?>(instance, methodName, []);

        private static void InvokePrivateVoid(object instance, string methodName, object?[] args)
            => InvokePrivate<object?>(instance, methodName, args);

        private static T InvokePrivate<T>(object instance, string methodName, object?[] args)
        {
            MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Unable to find method '{methodName}'.");

            object? result = method.Invoke(instance, args);
            return result is T value ? value : (T)result!;
        }

        private static long ReadPrivateLong(object instance, string memberName)
            => (long)RuntimeMemberAccessor.GetRequiredMemberValue(instance, memberName)!;

        private static Element CreateElementWithAddress(long address)
        {
            Element element = ExileCoreOpaqueFactory.CreateOpaque<Element>();
            RuntimeMemberAccessor.SetRequiredMember(element, "Address", address);
            return element;
        }

    }
}