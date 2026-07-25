namespace ClickIt.Tests.Core.Runtime
{
    [TestClass]
    [DoNotParallelize]
    public class PluginCoroutineRegistryTests
    {
        [TestMethod]
        public void FindActiveCoroutine_ReturnsFirstMatchingActiveCoroutine()
        {
            Coroutine expected = CoroutineTestHarness.CreateCoroutine("ClickIt.ClickLogic", isDone: false);

            using var scope = CoroutineTestHarness.ReplaceParallelRunnerCoroutines(
            [
                CoroutineTestHarness.CreateCoroutine("ClickIt.ClickLogic", isDone: true),
                expected,
                CoroutineTestHarness.CreateCoroutine("ClickIt.ManualUiHoverLogic", isDone: false),
            ]);

            PluginCoroutineRegistry.FindActiveCoroutine("ClickIt.ClickLogic").Should().BeSameAs(expected);
        }

        [TestMethod]
        public void FindActiveCoroutine_ReturnsNull_WhenOnlyDoneOrMismatchedCoroutinesExist()
        {
            using var scope = CoroutineTestHarness.ReplaceParallelRunnerCoroutines(
            [
                CoroutineTestHarness.CreateCoroutine("ClickIt.ClickLogic", isDone: true),
                CoroutineTestHarness.CreateCoroutine("Other.Coroutine", isDone: false),
            ]);

            PluginCoroutineRegistry.FindActiveCoroutine("ClickIt.ManualUiHoverLogic").Should().BeNull();
        }

        [TestMethod]
        public void ConvenienceMethods_UseExpectedCoroutineNames()
        {
            Coroutine clickLogic = CoroutineTestHarness.CreateCoroutine("ClickIt.ClickLogic", isDone: false);
            Coroutine manualUiHover = CoroutineTestHarness.CreateCoroutine("ClickIt.ManualUiHoverLogic", isDone: false);

            using var scope = CoroutineTestHarness.ReplaceParallelRunnerCoroutines([clickLogic, manualUiHover]);

            PluginCoroutineRegistry.FindClickLogicCoroutine().Should().BeSameAs(clickLogic);
            PluginCoroutineRegistry.FindManualUiHoverCoroutine().Should().BeSameAs(manualUiHover);
        }
    }
}