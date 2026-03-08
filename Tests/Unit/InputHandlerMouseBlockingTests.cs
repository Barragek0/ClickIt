using ClickIt.Utils;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows.Forms;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class InputHandlerMouseBlockingTests
    {
        [DataTestMethod]
        [DataRow(false, false, false, false)]
        [DataRow(false, true, false, false)]
        [DataRow(true, false, false, false)]
        [DataRow(true, true, false, true)]
        [DataRow(false, false, true, false)]
        [DataRow(false, true, true, false)]
        [DataRow(true, false, true, false)]
        [DataRow(true, true, true, true)]
        public void GetMouseButtonBlockingState_LeftButtonTruthTable(bool leftSetting, bool leftPressed, bool rightSetting, bool expectedLeft)
        {
            var settings = new ClickItSettings();
            settings.DisableLazyModeLeftClickHeld.Value = leftSetting;
            settings.DisableLazyModeRightClickHeld.Value = rightSetting;

            bool KeyProvider(Keys key) => key switch
            {
                Keys.LButton => leftPressed,
                _ => false
            };

            var (left, _, _) = InputHandler.GetMouseButtonBlockingState(settings, KeyProvider);
            left.Should().Be(expectedLeft);
        }

        [DataTestMethod]
        [DataRow(false, false, false, false)]
        [DataRow(false, true, false, false)]
        [DataRow(true, false, false, false)]
        [DataRow(true, true, false, false)]
        [DataRow(false, false, true, false)]
        [DataRow(false, true, true, true)]
        [DataRow(true, false, true, false)]
        [DataRow(true, true, true, true)]
        public void GetMouseButtonBlockingState_RightButtonTruthTable(bool leftSetting, bool rightPressed, bool rightSetting, bool expectedRight)
        {
            var settings = new ClickItSettings();
            settings.DisableLazyModeLeftClickHeld.Value = leftSetting;
            settings.DisableLazyModeRightClickHeld.Value = rightSetting;

            bool KeyProvider(Keys key) => key switch
            {
                Keys.RButton => rightPressed,
                _ => false
            };

            var (_, right, _) = InputHandler.GetMouseButtonBlockingState(settings, KeyProvider);
            right.Should().Be(expectedRight);
        }

        [DataTestMethod]
        [DataRow(false, false, false, false, false)]
        [DataRow(true, false, false, false, false)]
        [DataRow(false, false, true, false, false)]
        [DataRow(true, true, false, false, true)]
        [DataRow(false, false, true, true, true)]
        [DataRow(true, true, true, true, true)]
        [DataRow(true, false, true, false, false)]
        public void GetMouseButtonBlockingState_AnyIsOrOfLeftAndRight(bool leftSetting, bool leftPressed, bool rightSetting, bool rightPressed, bool expectedAny)
        {
            var settings = new ClickItSettings();
            settings.DisableLazyModeLeftClickHeld.Value = leftSetting;
            settings.DisableLazyModeRightClickHeld.Value = rightSetting;

            bool KeyProvider(Keys key) => key switch
            {
                Keys.LButton => leftPressed,
                Keys.RButton => rightPressed,
                _ => false
            };

            var (left, right, any) = InputHandler.GetMouseButtonBlockingState(settings, KeyProvider);
            any.Should().Be(left || right);
            any.Should().Be(expectedAny);
        }

        [TestMethod]
        public void GetMouseButtonBlockingState_BlocksConfiguredPressedButtons()
        {
            var settings = new ClickItSettings();
            settings.DisableLazyModeLeftClickHeld.Value = true;
            settings.DisableLazyModeRightClickHeld.Value = true;

            bool KeyProvider(Keys key) => key == Keys.LButton;

            var (left, right, any) = InputHandler.GetMouseButtonBlockingState(settings, KeyProvider);

            left.Should().BeTrue();
            right.Should().BeFalse();
            any.Should().BeTrue();
        }

        [TestMethod]
        public void GetMouseButtonBlockingState_DoesNotBlockWhenSettingDisabled()
        {
            var settings = new ClickItSettings();
            settings.DisableLazyModeLeftClickHeld.Value = false;
            settings.DisableLazyModeRightClickHeld.Value = false;

            bool KeyProvider(Keys key) => key == Keys.LButton || key == Keys.RButton;

            var (left, right, any) = InputHandler.GetMouseButtonBlockingState(settings, KeyProvider);

            left.Should().BeFalse();
            right.Should().BeFalse();
            any.Should().BeFalse();
        }
    }
}
