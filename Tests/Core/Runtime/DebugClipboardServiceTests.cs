namespace ClickIt.Tests.Core.Runtime
{
    [TestClass]
    public class DebugClipboardServiceTests
    {
        [TestMethod]
        public void RequestAdditionalDebugInfoCopy_SetsPendingFlag()
        {
            DebugClipboardService service = CreateService();

            service.RequestAdditionalDebugInfoCopy();

            service.HasPendingAdditionalDebugInfoCopyRequest.Should().BeTrue();
        }

        [TestMethod]
        public void CompleteAdditionalDebugInfoCopy_ClearsPendingFlag_WhenDebugLinesAreEmpty()
        {
            DebugClipboardService service = CreateService();
            service.RequestAdditionalDebugInfoCopy();

            service.CompleteAdditionalDebugInfoCopy([]);

            service.HasPendingAdditionalDebugInfoCopyRequest.Should().BeFalse();
        }

        [TestMethod]
        public void CompleteAdditionalDebugInfoCopy_ClearsPendingFlag_WhenDebugLinesAreNull()
        {
            DebugClipboardService service = CreateService();
            service.RequestAdditionalDebugInfoCopy();

            service.CompleteAdditionalDebugInfoCopy(null!);

            service.HasPendingAdditionalDebugInfoCopyRequest.Should().BeFalse();
        }

        [TestMethod]
        public void CompleteAdditionalDebugInfoCopy_EmptyLines_DoesNotInvokeRecorder()
        {
            DebugClipboardService service = CreateService();
            bool recorderInvoked = false;

            service.CompleteAdditionalDebugInfoCopy([], (_, _) => recorderInvoked = true);

            recorderInvoked.Should().BeFalse("empty lines skip the background copy entirely");
            service.HasPendingAdditionalDebugInfoCopyRequest.Should().BeFalse();
        }

        [TestMethod]
        public void GameStateDump_IsExposed()
        {
            DebugClipboardService service = CreateService();

            service.GameStateDump.Should().NotBeNull();
            service.GameStateDump.GetStatusMessage().Should().BeEmpty("nothing has been dumped yet");

            GameStateDumpSnapshot progress = service.GameStateDump.GetProgress();
            progress.InProgress.Should().BeFalse();
            progress.ProgressPercent.Should().Be(0);
            progress.Errors.Should().BeEmpty();
            progress.Steps.Should().BeEmpty();
        }

        [TestMethod]
        public void CancelDump_WhenNotRunning_DoesNothing()
        {
            DebugClipboardService service = CreateService();

            Action act = () => service.GameStateDump.CancelDump();

            act.Should().NotThrow();
            service.GameStateDump.GetProgress().InProgress.Should().BeFalse();
        }

        private static DebugClipboardService CreateService(
            PluginContext? state = null,
            ClickIt? owner = null,
            Func<GameController?>? getGameController = null)
        {
            return new DebugClipboardService(new DebugClipboardServiceDependencies(
                state ?? new PluginContext(),
                owner ?? new ClickIt(),
                getGameController ?? (() => null)));
        }

        private static void InvokePrivateVoid(object instance, string methodName, params object?[] args)
        {
            MethodInfo? method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method.Should().NotBeNull($"Expected private method {methodName} to exist.");
            method!.Invoke(instance, args);
        }
    }
}