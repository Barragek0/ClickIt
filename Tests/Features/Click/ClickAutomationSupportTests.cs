namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class ClickAutomationSupportTests
    {
        [TestMethod]
        public void PublishClickSnapshot_DoesNothing_WhenClickDebugCaptureDisabled()
        {
            var settings = new ClickItSettings();
            settings.DebugMode.Value = false;

            var support = CreateSupport(settings);

            support.PublishClickSnapshot(new ClickDebugSnapshot(
                HasData: true,
                Stage: "stage",
                MechanicId: string.Empty,
                EntityPath: string.Empty,
                Distance: 0f,
                WorldScreenRaw: default,
                WorldScreenAbsolute: default,
                ResolvedClickPoint: default,
                Resolved: false,
                CenterInWindow: false,
                CenterClickable: false,
                ResolvedInWindow: false,
                ResolvedClickable: false,
                Notes: "notes",
                Sequence: 0,
                TimestampMs: 1));

            support.GetLatestClickDebug().Should().Be(ClickDebugSnapshot.Empty);
        }

        [TestMethod]
        public void DebugLog_PublishesRuntimeLog_AndForwardsMessage_WhenLoggingEnabled()
        {
            var settings = new ClickItSettings();
            settings.DebugMode.Value = true;
            settings.LogMessages.Value = true;
            string? loggedMessage = null;

            var support = CreateSupport(settings, logMessage: message => loggedMessage = message);

            support.DebugLog("hello world");

            support.GetLatestRuntimeDebugLog().Message.Should().Be("hello world");
            loggedMessage.Should().Be("hello world");
        }

        [TestMethod]
        public void EnsureCursorInsideGameWindowForClick_ReturnsFalse_AndLogs_WhenVerificationFails()
        {
            var settings = new ClickItSettings();
            settings.DebugMode.Value = true;
            settings.VerifyCursorInGameWindowBeforeClick.Value = true;
            string? loggedMessage = null;

            var support = CreateSupport(
                settings,
                getWindowRectangle: static () => new RectangleF(100f, 200f, 50f, 50f),
                getCursorPosition: static () => new Vector2(10f, 20f),
                logMessage: message => loggedMessage = message);

            bool allowed = support.EnsureCursorInsideGameWindowForClick("cursor outside");

            allowed.Should().BeFalse();
            support.GetLatestRuntimeDebugLog().Message.Should().Be("cursor outside");
            loggedMessage.Should().BeNull();
        }

        [TestMethod]
        public void HoldDebugTelemetryAfterSuccessfulInteraction_InvokesFreeze_WhenDebugRenderingEnabled()
        {
            var settings = new ClickItSettings();
            settings.DebugMode.Value = true;
            settings.RenderDebug.Value = true;
            settings.DebugFreezeSuccessfulInteractionMs.Value = 321;
            string? frozenReason = null;
            int frozenDuration = 0;

            var support = CreateSupport(
                settings,
                freezeDebugTelemetrySnapshot: (reason, duration) =>
                {
                    frozenReason = reason;
                    frozenDuration = duration;
                });

            support.HoldDebugTelemetryAfterSuccessfulInteraction("clicked mechanic");

            frozenReason.Should().Be("clicked mechanic");
            frozenDuration.Should().Be(321);
        }

        [TestMethod]
        public void IsClickableInEitherSpace_UsesWindowTopLeftOffset_WhenEvaluatingPoint()
        {
            var settings = new ClickItSettings();
            var probedPoints = new List<Vector2>();
            var probedPaths = new List<string>();

            var support = CreateSupport(
                settings,
                getWindowRectangle: static () => new RectangleF(100f, 200f, 400f, 300f),
                pointIsInClickableArea: (point, path) =>
                {
                    probedPoints.Add(point);
                    probedPaths.Add(path);
                    return point == new Vector2(210f, 415f) && path == "Metadata/Test";
                });

            bool clickable = support.IsClickableInEitherSpace(new Vector2(110f, 215f), "Metadata/Test");

            clickable.Should().BeTrue();
            probedPoints.Should().ContainInOrder(new Vector2(110f, 215f), new Vector2(210f, 415f));
            probedPaths.Should().OnlyContain(path => path == "Metadata/Test");
        }

        private static ClickAutomationSupport CreateSupport(
            ClickItSettings settings,
            Func<RectangleF>? getWindowRectangle = null,
            Func<Vector2>? getCursorPosition = null,
            Func<Vector2, string, bool>? pointIsInClickableArea = null,
            Action<string>? logMessage = null,
            Action<string, int>? freezeDebugTelemetrySnapshot = null)
        {
            return new ClickAutomationSupport(new ClickAutomationSupportDependencies(
                Settings: settings,
                TelemetryStore: new ClickTelemetryStore(settings),
                GetWindowRectangle: getWindowRectangle ?? (static () => new RectangleF(0f, 0f, 100f, 100f)),
                GetCursorPosition: getCursorPosition ?? (static () => new Vector2(0f, 0f)),
                PointIsInClickableArea: pointIsInClickableArea ?? (static (_, _) => false),
                LogMessage: logMessage ?? (static _ => { }),
                FreezeDebugTelemetrySnapshot: freezeDebugTelemetrySnapshot));
        }
    }
}