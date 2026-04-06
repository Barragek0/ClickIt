namespace ClickIt.Tests.Core.Runtime
{
    [TestClass]
    public class PluginClickRuntimeStateEvaluatorTests
    {
        [TestMethod]
        public void BuildSnapshot_UsesRitualAndRestrictedItems_ForLazyTiming()
        {
            PluginClickRuntimeStateSnapshot blockedByRitual = PluginClickRuntimeStateEvaluator.BuildSnapshot(
                lazyModeEnabled: true,
                lazyModeDisableActive: false,
                hasLazyModeRestrictedItems: false,
                isRitualActive: true,
                poeForeground: true);

            PluginClickRuntimeStateSnapshot blockedByRestrictedItems = PluginClickRuntimeStateEvaluator.BuildSnapshot(
                lazyModeEnabled: true,
                lazyModeDisableActive: false,
                hasLazyModeRestrictedItems: true,
                isRitualActive: false,
                poeForeground: true);

            blockedByRitual.UseLazyModeTiming.Should().BeFalse();
            blockedByRitual.ShowLazyModeTarget.Should().BeFalse();
            blockedByRestrictedItems.UseLazyModeTiming.Should().BeFalse();
            blockedByRestrictedItems.ShowLazyModeTarget.Should().BeFalse();
        }

        [TestMethod]
        public void BuildSnapshot_HidesLazyTarget_WhenDisableHeldOrGameInactive()
        {
            PluginClickRuntimeStateSnapshot disabledByHotkey = PluginClickRuntimeStateEvaluator.BuildSnapshot(
                lazyModeEnabled: true,
                lazyModeDisableActive: true,
                hasLazyModeRestrictedItems: false,
                isRitualActive: false,
                poeForeground: true);

            PluginClickRuntimeStateSnapshot hiddenOutOfFocus = PluginClickRuntimeStateEvaluator.BuildSnapshot(
                lazyModeEnabled: true,
                lazyModeDisableActive: false,
                hasLazyModeRestrictedItems: false,
                isRitualActive: false,
                poeForeground: false);

            disabledByHotkey.UseLazyModeTiming.Should().BeTrue();
            disabledByHotkey.ShowLazyModeTarget.Should().BeFalse();
            hiddenOutOfFocus.UseLazyModeTiming.Should().BeTrue();
            hiddenOutOfFocus.ShowLazyModeTarget.Should().BeFalse();
        }

    }
}