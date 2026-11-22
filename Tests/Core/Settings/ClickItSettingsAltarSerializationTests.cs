namespace ClickIt.Tests.Core.Settings
{
    [TestClass]
    public class AltarSettingsSerializationTests
    {
        [TestMethod]
        [TestCategory("Settings")]
        public void EmbeddedSettings_UseLegacySettingsTreeVisibilityGate()
        {
            string[] embeddedProperties =
            [
                nameof(ClickItSettings.EssenceCorruptionSkipIfDeafening),
                nameof(ClickItSettings.EssenceCorruptionSkipIfSpecialEssence),
                nameof(ClickItSettings.ChooseHigherUpsideWhenAltarWeightsTie)
            ];

            foreach (string propertyName in embeddedProperties)
            {
                PropertyInfo property = typeof(ClickItSettings).GetProperty(propertyName)!;
                property.GetCustomAttributes().OfType<ConditionalDisplayAttribute>()
                    .Should().ContainSingle(attribute => attribute.ConditionMethodName == nameof(ClickItSettings.ShowLegacySettingsTreeNodes));
            }
        }

        [TestMethod]
        public void AltarWeightingAndAlerts_RoundTrip_PreservesCustomValues()
        {
            var settings = new ClickItSettings();

            const string tierKey = "Player|custom-upside";
            const string alertKey = "Boss|custom-upside-alert";

            settings.ModTiers[tierKey] = 77;
            settings.ModAlerts[alertKey] = true;

            string json = JsonConvert.SerializeObject(settings);
            var restored = JsonConvert.DeserializeObject<ClickItSettings>(json);

            restored.Should().NotBeNull();
            restored!.ModTiers.Should().ContainKey(tierKey);
            restored.ModTiers[tierKey].Should().Be(77);
            restored.ModAlerts.Should().ContainKey(alertKey);
            restored.ModAlerts[alertKey].Should().BeTrue();
        }
    }
}
