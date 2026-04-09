namespace ClickIt.Tests.Features.Labels.Classification.Policies
{
    [TestClass]
    public class SettlersMechanicPolicyTests
    {
        [DataTestMethod]
        [DataRow(null, false)]
        [DataRow("", false)]
        [DataRow("ritual-initiate", false)]
        [DataRow("settlers-copper", true)]
        [DataRow("SETTLERS-bismuth", true)]
        public void IsSettlersMechanicId_ReturnsExpected(string? mechanicId, bool expected)
        {
            bool result = SettlersMechanicPolicy.IsSettlersMechanicId(mechanicId);

            result.Should().Be(expected);
        }

        [DataTestMethod]
        [DataRow(null, false)]
        [DataRow("settlers-copper", false)]
        [DataRow("SETTLERS-VERISIUM", true)]
        public void RequiresHoldClick_ReturnsExpected(string? mechanicId, bool expected)
        {
            bool result = SettlersMechanicPolicy.RequiresHoldClick(mechanicId);

            result.Should().Be(expected);
        }

        [TestMethod]
        public void IsEnabled_ReturnsFalse_WhenSettlersOreIsDisabled_ForClickSettings()
        {
            ClickSettings settings = CreateClickSettings();
            settings.ClickSettlersOre = false;

            bool result = SettlersMechanicPolicy.IsEnabled(settings, MechanicIds.SettlersCopper);

            result.Should().BeFalse();
        }

        [TestMethod]
        public void IsEnabled_ReturnsFalse_WhenMechanicIdIsUnknown_ForClickSettings()
        {
            ClickSettings settings = CreateClickSettings();

            bool result = SettlersMechanicPolicy.IsEnabled(settings, "settlers-unknown");

            result.Should().BeFalse();
        }

        [DataTestMethod]
        [DataRow(MechanicIds.SettlersCrimsonIron, nameof(ClickSettings.ClickSettlersCrimsonIron))]
        [DataRow(MechanicIds.SettlersCopper, nameof(ClickSettings.ClickSettlersCopper))]
        [DataRow(MechanicIds.SettlersPetrifiedWood, nameof(ClickSettings.ClickSettlersPetrifiedWood))]
        [DataRow(MechanicIds.SettlersBismuth, nameof(ClickSettings.ClickSettlersBismuth))]
        [DataRow(MechanicIds.SettlersVerisium, nameof(ClickSettings.ClickSettlersVerisium))]
        public void IsEnabled_UsesPerMechanicToggle_ForClickSettings(string mechanicId, string enabledPropertyName)
        {
            ClickSettings enabledSettings = CreateClickSettings();
            SetBooleanProperty(ref enabledSettings, enabledPropertyName, true);

            ClickSettings disabledSettings = CreateClickSettings();
            SetBooleanProperty(ref disabledSettings, enabledPropertyName, false);

            SettlersMechanicPolicy.IsEnabled(enabledSettings, mechanicId).Should().BeTrue();
            SettlersMechanicPolicy.IsEnabled(disabledSettings, mechanicId).Should().BeFalse();
        }

        [TestMethod]
        public void IsEnabled_UsesGlobalOreToggle_ForHourglass_ForClickSettings()
        {
            ClickSettings settings = CreateClickSettings();

            bool enabled = SettlersMechanicPolicy.IsEnabled(settings, MechanicIds.SettlersHourglass);

            settings.ClickSettlersOre = false;
            bool disabled = SettlersMechanicPolicy.IsEnabled(settings, MechanicIds.SettlersHourglass);

            enabled.Should().BeTrue();
            disabled.Should().BeFalse();
        }

        [TestMethod]
        public void IsEnabled_ReturnsFalse_WhenSettlersOreIsDisabled_ForClickItSettings()
        {
            ClickItSettings settings = CreateRootSettings();
            settings.ClickSettlersOre.Value = false;

            bool result = SettlersMechanicPolicy.IsEnabled(settings, MechanicIds.SettlersCopper);

            result.Should().BeFalse();
        }

        [TestMethod]
        public void IsEnabled_ReturnsFalse_WhenMechanicIdIsUnknown_ForClickItSettings()
        {
            ClickItSettings settings = CreateRootSettings();

            bool result = SettlersMechanicPolicy.IsEnabled(settings, "settlers-unknown");

            result.Should().BeFalse();
        }

        [DataTestMethod]
        [DataRow(MechanicIds.SettlersCrimsonIron, nameof(ClickItSettings.ClickSettlersCrimsonIron))]
        [DataRow(MechanicIds.SettlersCopper, nameof(ClickItSettings.ClickSettlersCopper))]
        [DataRow(MechanicIds.SettlersPetrifiedWood, nameof(ClickItSettings.ClickSettlersPetrifiedWood))]
        [DataRow(MechanicIds.SettlersBismuth, nameof(ClickItSettings.ClickSettlersBismuth))]
        [DataRow(MechanicIds.SettlersVerisium, nameof(ClickItSettings.ClickSettlersVerisium))]
        public void IsEnabled_UsesPerMechanicToggle_ForClickItSettings(string mechanicId, string enabledPropertyName)
        {
            ClickItSettings enabledSettings = CreateRootSettings();
            SetToggleNodeValue(enabledSettings, enabledPropertyName, true);

            ClickItSettings disabledSettings = CreateRootSettings();
            SetToggleNodeValue(disabledSettings, enabledPropertyName, false);

            SettlersMechanicPolicy.IsEnabled(enabledSettings, mechanicId).Should().BeTrue();
            SettlersMechanicPolicy.IsEnabled(disabledSettings, mechanicId).Should().BeFalse();
        }

        [TestMethod]
        public void IsEnabled_UsesGlobalOreToggle_ForHourglass_ForClickItSettings()
        {
            ClickItSettings settings = CreateRootSettings();

            bool enabled = SettlersMechanicPolicy.IsEnabled(settings, MechanicIds.SettlersHourglass);

            settings.ClickSettlersOre.Value = false;
            bool disabled = SettlersMechanicPolicy.IsEnabled(settings, MechanicIds.SettlersHourglass);

            enabled.Should().BeTrue();
            disabled.Should().BeFalse();
        }

        private static ClickSettings CreateClickSettings()
        {
            return new ClickSettings
            {
                ClickSettlersOre = true,
                ClickSettlersCrimsonIron = false,
                ClickSettlersCopper = false,
                ClickSettlersPetrifiedWood = false,
                ClickSettlersBismuth = false,
                ClickSettlersVerisium = false
            };
        }

        private static ClickItSettings CreateRootSettings()
        {
            var settings = new ClickItSettings();
            settings.ClickSettlersOre.Value = true;
            settings.ClickSettlersCrimsonIron.Value = false;
            settings.ClickSettlersCopper.Value = false;
            settings.ClickSettlersPetrifiedWood.Value = false;
            settings.ClickSettlersBismuth.Value = false;
            settings.ClickSettlersVerisium.Value = false;
            return settings;
        }

        private static void SetBooleanProperty(ref ClickSettings settings, string propertyName, bool value)
        {
            object boxed = settings;
            typeof(ClickSettings).GetProperty(propertyName)!.SetValue(boxed, value);
            settings = (ClickSettings)boxed;
        }

        private static void SetToggleNodeValue(ClickItSettings settings, string propertyName, bool value)
        {
            ToggleNode toggle = (ToggleNode)typeof(ClickItSettings).GetProperty(propertyName)!.GetValue(settings)!;
            toggle.Value = value;
        }
    }
}