using ClickIt.Utils;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class InputHandlerLazyDisableHotkeyModeTests
    {
        [TestMethod]
        public void ResolveLazyModeDisableActive_HoldMode_TracksRawKeyState()
        {
            bool toggled = true;
            bool wasPressed = false;

            bool activeWhenDown = InputHandler.ResolveLazyModeDisableActive(toggleModeEnabled: false, disableKeyPressed: true, ref toggled, ref wasPressed);
            bool activeWhenUp = InputHandler.ResolveLazyModeDisableActive(toggleModeEnabled: false, disableKeyPressed: false, ref toggled, ref wasPressed);

            activeWhenDown.Should().BeTrue();
            activeWhenUp.Should().BeFalse();
        }

        [TestMethod]
        public void ResolveLazyModeDisableActive_ToggleMode_TogglesOnlyOnKeyDownEdge()
        {
            bool toggled = false;
            bool wasPressed = false;

            bool step1 = InputHandler.ResolveLazyModeDisableActive(toggleModeEnabled: true, disableKeyPressed: true, ref toggled, ref wasPressed);
            bool step2 = InputHandler.ResolveLazyModeDisableActive(toggleModeEnabled: true, disableKeyPressed: true, ref toggled, ref wasPressed);
            bool step3 = InputHandler.ResolveLazyModeDisableActive(toggleModeEnabled: true, disableKeyPressed: false, ref toggled, ref wasPressed);
            bool step4 = InputHandler.ResolveLazyModeDisableActive(toggleModeEnabled: true, disableKeyPressed: true, ref toggled, ref wasPressed);

            step1.Should().BeTrue("first key press should toggle on");
            step2.Should().BeTrue("holding key should not toggle repeatedly");
            step3.Should().BeTrue("release keeps the latched state");
            step4.Should().BeFalse("next key press toggles off");
        }

        [TestMethod]
        public void ResolveClickHotkeyActive_HoldMode_TracksRawKeyState()
        {
            bool toggled = true;
            bool wasPressed = false;

            bool activeWhenDown = InputHandler.ResolveClickHotkeyActive(toggleModeEnabled: false, hotkeyPressed: true, ref toggled, ref wasPressed);
            bool activeWhenUp = InputHandler.ResolveClickHotkeyActive(toggleModeEnabled: false, hotkeyPressed: false, ref toggled, ref wasPressed);

            activeWhenDown.Should().BeTrue();
            activeWhenUp.Should().BeFalse();
            toggled.Should().BeFalse("hold mode should not preserve latched toggle state");
        }

        [TestMethod]
        public void ResolveClickHotkeyActive_ToggleMode_TogglesOnlyOnKeyDownEdge()
        {
            bool toggled = false;
            bool wasPressed = false;

            bool step1 = InputHandler.ResolveClickHotkeyActive(toggleModeEnabled: true, hotkeyPressed: true, ref toggled, ref wasPressed);
            bool step2 = InputHandler.ResolveClickHotkeyActive(toggleModeEnabled: true, hotkeyPressed: true, ref toggled, ref wasPressed);
            bool step3 = InputHandler.ResolveClickHotkeyActive(toggleModeEnabled: true, hotkeyPressed: false, ref toggled, ref wasPressed);
            bool step4 = InputHandler.ResolveClickHotkeyActive(toggleModeEnabled: true, hotkeyPressed: true, ref toggled, ref wasPressed);

            step1.Should().BeTrue("first key press should toggle on");
            step2.Should().BeTrue("holding key should not toggle repeatedly");
            step3.Should().BeTrue("release keeps the latched state");
            step4.Should().BeFalse("next key press toggles off");
        }

        [TestMethod]
        public void ResolveClickHotkeyActive_HoldMode_ClearsLatchedState()
        {
            bool toggled = true;
            bool wasPressed = true;

            bool active = InputHandler.ResolveClickHotkeyActive(toggleModeEnabled: false, hotkeyPressed: false, ref toggled, ref wasPressed);

            active.Should().BeFalse();
            toggled.Should().BeFalse();
            wasPressed.Should().BeFalse();
        }
    }
}
