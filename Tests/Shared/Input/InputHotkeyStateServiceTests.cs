namespace ClickIt.Tests.Shared.Input
{
    [TestClass]
    public class InputHotkeyStateServiceTests
    {
        [TestMethod]
        public void ResolveToggleHotkeyActive_HoldMode_ReturnsRawKeyState_AndResetsToggle()
        {
            bool toggled = true;
            bool wasDown = false;

            InputHotkeyStateService.ResolveToggleHotkeyActive(toggleModeEnabled: false, hotkeyPressed: true, ref toggled, ref wasDown).Should().BeTrue();
            InputHotkeyStateService.ResolveToggleHotkeyActive(toggleModeEnabled: false, hotkeyPressed: false, ref toggled, ref wasDown).Should().BeFalse();
            toggled.Should().BeFalse();
        }

        [TestMethod]
        public void ResolveToggleHotkeyActive_ToggleMode_PressEdgeFlipsState()
        {
            bool toggled = false;
            bool wasDown = false;

            // Fresh press edge -> toggled on.
            InputHotkeyStateService.ResolveToggleHotkeyActive(toggleModeEnabled: true, hotkeyPressed: true, ref toggled, ref wasDown).Should().BeTrue();
            // Held (no edge) -> stays on.
            InputHotkeyStateService.ResolveToggleHotkeyActive(toggleModeEnabled: true, hotkeyPressed: true, ref toggled, ref wasDown).Should().BeTrue();
            // Release.
            InputHotkeyStateService.ResolveToggleHotkeyActive(toggleModeEnabled: true, hotkeyPressed: false, ref toggled, ref wasDown).Should().BeTrue();
            // Second press edge -> toggled off.
            InputHotkeyStateService.ResolveToggleHotkeyActive(toggleModeEnabled: true, hotkeyPressed: true, ref toggled, ref wasDown).Should().BeFalse();
        }

        [TestMethod]
        public void IsClickHotkeyActive_ReturnsFalse_WhenNoBindingConfigured()
        {
            var settings = new ClickItSettings();
            settings.ClickLabelKey = new HotkeyNodeV2(Keys.None);
            var service = new InputHotkeyStateService(settings);

            service.IsClickHotkeyActive(static _ => true).Should().BeFalse();
        }

        [TestMethod]
        public void IsClickHotkeyActive_HoldMode_ReflectsKeyState()
        {
            var settings = new ClickItSettings();
            settings.ClickLabelKey = new HotkeyNodeV2(Keys.F5);
            settings.ClickHotkeyToggleMode.Value = false;
            var service = new InputHotkeyStateService(settings);

            service.IsClickHotkeyActive(static _ => true).Should().BeTrue();
            service.IsClickHotkeyActive(static _ => false).Should().BeFalse();
        }

        [TestMethod]
        public void IsLazyModeDisableActive_ToggleMode_TogglesOnFreshPressEdge()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = true;
            settings.LazyModeDisableKeyToggleMode.Value = true;
            var service = new InputHotkeyStateService(settings);

            bool keyDown = true;
            bool first = service.IsLazyModeDisableActive(_ => keyDown);
            bool held = service.IsLazyModeDisableActive(_ => keyDown);
            keyDown = false;
            service.IsLazyModeDisableActive(_ => keyDown);
            keyDown = true;
            bool second = service.IsLazyModeDisableActive(_ => keyDown);

            first.Should().BeTrue();
            held.Should().BeTrue();
            second.Should().BeFalse();
        }
    }
}
