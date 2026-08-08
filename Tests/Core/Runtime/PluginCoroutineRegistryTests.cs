namespace ClickIt.Tests.Core.Runtime
{
    [TestClass]
    [DoNotParallelize]
    public class PluginCoroutineRegistryTests
    {
        [TestMethod]
        public void FindActiveCoroutine_ReturnsFirstMatchingActiveCoroutine()
        {
            Coroutine expected = CoroutineTestHarness.CreateCoroutine("Click", isDone: false);

            using var scope = CoroutineTestHarness.ReplaceParallelRunnerCoroutines(
            [
                CoroutineTestHarness.CreateCoroutine("Click", isDone: true),
                expected,
                CoroutineTestHarness.CreateCoroutine("Manual UI Hover", isDone: false),
            ]);

            PluginCoroutineRegistry.FindActiveCoroutine("Click").Should().BeSameAs(expected);
        }

        [TestMethod]
        public void FindActiveCoroutine_ReturnsNull_WhenOnlyDoneOrMismatchedCoroutinesExist()
        {
            using var scope = CoroutineTestHarness.ReplaceParallelRunnerCoroutines(
            [
                CoroutineTestHarness.CreateCoroutine("Click", isDone: true),
                CoroutineTestHarness.CreateCoroutine("Other.Coroutine", isDone: false),
            ]);

            PluginCoroutineRegistry.FindActiveCoroutine("Manual UI Hover").Should().BeNull();
        }

        [TestMethod]
        public void ConvenienceMethods_UseExpectedCoroutineNames()
        {
            Coroutine clickLogic = CoroutineTestHarness.CreateCoroutine("Click", isDone: false);
            Coroutine manualUiHover = CoroutineTestHarness.CreateCoroutine("Manual UI Hover", isDone: false);

            using var scope = CoroutineTestHarness.ReplaceParallelRunnerCoroutines([clickLogic, manualUiHover]);

            PluginCoroutineRegistry.FindClickLogicCoroutine().Should().BeSameAs(clickLogic);
            PluginCoroutineRegistry.FindManualUiHoverCoroutine().Should().BeSameAs(manualUiHover);
        }
    }
}