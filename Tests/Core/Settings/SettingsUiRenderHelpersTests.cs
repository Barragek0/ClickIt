namespace ClickIt.Tests.Core.Settings
{
    [TestClass]
    public class SettingsUiRenderHelpersTests
    {
        [TestMethod]
        public void MatchesSearch_ReturnsTrue_WhenFilterIsEmpty()
        {
            SettingsUiRenderHelpers.MatchesSearch(string.Empty, "alpha", "beta").Should().BeTrue();
        }

        [TestMethod]
        public void MatchesSearch_ReturnsTrue_WhenAnyValueMatches()
        {
            SettingsUiRenderHelpers.MatchesSearch("beta", "alpha", "Beta value", null).Should().BeTrue();
        }

        [TestMethod]
        public void MatchesSearch_TrimsWhitespaceAroundFilter()
        {
            SettingsUiRenderHelpers.MatchesSearch("  beta  ", "alpha", "Beta value").Should().BeTrue();
        }

        [TestMethod]
        public void MatchesSearch_SupportsEnumerableInputs()
        {
            IEnumerable<string?> values = ["alpha", null, "Beta value"];

            SettingsUiRenderHelpers.MatchesSearch("beta", values).Should().BeTrue();
        }

        [TestMethod]
        public void MatchesSearch_ReturnsFalse_WhenNoValueMatches()
        {
            SettingsUiRenderHelpers.MatchesSearch("gamma", "alpha", "beta").Should().BeFalse();
        }

        [TestMethod]
        public void BuildExpandedRowKey_CombinesListAndRowId()
        {
            SettingsUiRenderHelpers.BuildExpandedRowKey("left", "row-1").Should().Be("left:row-1");
        }

        [TestMethod]
        public void ToggleExpandedRowKey_ClearsMatchingRow()
        {
            SettingsUiRenderHelpers.ToggleExpandedRowKey("left:row-1", "left", "row-1").Should().BeEmpty();
        }

        [TestMethod]
        public void ToggleExpandedRowKey_SetsDifferentRow()
        {
            SettingsUiRenderHelpers.ToggleExpandedRowKey("left:row-1", "right", "row-2").Should().Be("right:row-2");
        }

        [TestMethod]
        public void GetAltarUpsideSectionStyle_ReturnsExpectedStyle_ForBossType()
        {
            SettingsUiRenderHelpers.AltarModSectionStyle style = SettingsUiRenderHelpers.GetAltarUpsideSectionStyle(ClickItSettings.AltarTypeBoss);

            style.HeaderText.Should().Be("Boss Drops");
            style.HeaderTextColor.Should().BeNull();
            style.HeaderColor.Should().Be(new Vector4(0.6f, 0.2f, 0.2f, 0.3f));
            style.RowTextColor.Should().Be(new Vector4(0.8f, 0.4f, 0.4f, 1.0f));
        }

        [TestMethod]
        public void GetAltarDownsideSectionStyle_ReturnsExpectedStyle_ForBrickingWeight()
        {
            SettingsUiRenderHelpers.AltarModSectionStyle style = SettingsUiRenderHelpers.GetAltarDownsideSectionStyle(100);

            style.HeaderText.Should().Be("Build Bricking Modifiers");
            style.HeaderTextColor.Should().Be(new Vector4(1f, 1f, 1f, 1f));
            style.HeaderColor.Should().Be(new Vector4(1.0f, 0.0f, 0.0f, 0.6f));
            style.RowTextColor.Should().Be(new Vector4(1.0f, 0.2f, 0.2f, 1.0f));
        }

        [TestMethod]
        public void GetAltarDownsideSectionStyle_ReturnsExpectedStyle_ForLowWeight()
        {
            SettingsUiRenderHelpers.AltarModSectionStyle style = SettingsUiRenderHelpers.GetAltarDownsideSectionStyle(1);

            style.HeaderText.Should().Be("Free Modifiers");
            style.HeaderTextColor.Should().Be(new Vector4(1f, 1f, 1f, 1f));
            style.HeaderColor.Should().Be(new Vector4(0.0f, 0.7f, 0.0f, 0.3f));
            style.RowTextColor.Should().Be(new Vector4(0.5f, 1.0f, 0.5f, 1.0f));
        }

        [TestMethod]
        public void GetUpsideAltarModSectionDescriptor_ReturnsExpectedDescriptor()
        {
            SettingsUiRenderHelpers.AltarModSectionDescriptor descriptor = SettingsUiRenderHelpers.GetUpsideAltarModSectionDescriptor();

            descriptor.TreeLabel.Should().Be("Altar Weight Upsides");
            descriptor.ScaleHeading.Should().Be("Weight Scale (Higher = More Valuable):");
            descriptor.SearchId.Should().Be("##UpsideSearch");
            descriptor.ClearId.Should().Be("Clear##UpsideClear");
            descriptor.TableId.Should().Be("UpsideModsConfig");
            descriptor.BestAtHigh.Should().BeTrue();
            descriptor.ShowAlertColumn.Should().BeTrue();
        }

        [TestMethod]
        public void GetDownsideAltarModSectionDescriptor_ReturnsExpectedDescriptor()
        {
            SettingsUiRenderHelpers.AltarModSectionDescriptor descriptor = SettingsUiRenderHelpers.GetDownsideAltarModSectionDescriptor();

            descriptor.TreeLabel.Should().Be("Altar Weight Downsides");
            descriptor.ScaleHeading.Should().Be("Weight Scale (Higher = More Dangerous):");
            descriptor.SearchId.Should().Be("##DownsideSearch");
            descriptor.ClearId.Should().Be("Clear##DownsideClear");
            descriptor.TableId.Should().Be("DownsideModsConfig");
            descriptor.BestAtHigh.Should().BeFalse();
            descriptor.ShowAlertColumn.Should().BeFalse();
        }

        [TestMethod]
        public void TriggerButtonNode_InvokesOnPressed_WhenPresent()
        {
            bool invoked = false;
            var buttonNode = new ButtonNode
            {
                OnPressed = () => invoked = true
            };

            SettingsUiRenderHelpers.TriggerButtonNode(buttonNode);

            invoked.Should().BeTrue();
        }

        [TestMethod]
        public void TriggerButtonNode_SwallowsExceptions_FromOnPressed()
        {
            var buttonNode = new ButtonNode
            {
                OnPressed = static () => throw new InvalidOperationException("boom")
            };

            Action act = () => SettingsUiRenderHelpers.TriggerButtonNode(buttonNode);

            act.Should().NotThrow();
        }

        [TestMethod]
        public void TryInvokeHotkeyPicker_InvokesPublicDrawPickerButton_WhenPresent()
        {
            var hotkeyNode = new FakeHotkeyNode();

            bool invoked = SettingsUiRenderHelpers.TryInvokeHotkeyPicker(hotkeyNode, "Flare Hotkey##MechanicsDelveFlareHotkey");

            invoked.Should().BeTrue();
            hotkeyNode.CallCount.Should().Be(1);
            hotkeyNode.LastLabel.Should().Be("Flare Hotkey##MechanicsDelveFlareHotkey");
        }

        [TestMethod]
        public void TryInvokeHotkeyPicker_ReturnsFalse_WhenNodeIsNull()
        {
            SettingsUiRenderHelpers.TryInvokeHotkeyPicker(null, "ignored").Should().BeFalse();
        }

        [TestMethod]
        public void TryInvokeHotkeyPicker_SwallowsExceptions_FromDrawPickerButton()
        {
            var hotkeyNode = new ThrowingFakeHotkeyNode();

            bool invoked = SettingsUiRenderHelpers.TryInvokeHotkeyPicker(hotkeyNode, "bad-label");

            invoked.Should().BeFalse();
        }

        private sealed class FakeHotkeyNode
        {
            public int CallCount { get; private set; }
            public string LastLabel { get; private set; } = string.Empty;

            public void DrawPickerButton(string label)
            {
                CallCount++;
                LastLabel = label;
            }
        }

        private sealed class ThrowingFakeHotkeyNode
        {
            public void DrawPickerButton(string label)
                => throw new InvalidOperationException(label);
        }
    }
}