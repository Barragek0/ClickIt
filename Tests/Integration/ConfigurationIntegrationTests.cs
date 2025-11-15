using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Collections.Generic;
using System.Linq;

namespace ClickIt.Tests
{
    [TestClass]
    public class ConfigurationIntegrationTests
    {
        [TestMethod]
        public void Settings_ShouldValidateConfigurationIntegrity()
        {
            // Arrange
            var settingsValidator = new MockSettingsValidator();
            var settings = new MockClickItSettings();

            // Configure with valid values
            settings.ClickLabelKey = MockKeys.F1;
            settings.ClickDistance = 95;
            settings.DebugMode = true;
            settings.CorruptAllEssences = false;

            // Act
            var validationResult = settingsValidator.ValidateSettings(settings);

            // Assert
            validationResult.IsValid.Should().BeTrue("valid configuration should pass validation");
            validationResult.Errors.Should().BeEmpty("valid configuration should have no errors");
            validationResult.Warnings.Should().BeEmpty("optimal configuration should have no warnings");
        }

        [TestMethod]
        public void Settings_ShouldDetectInvalidConfigurations()
        {
            // Arrange
            var settingsValidator = new MockSettingsValidator();
            var invalidSettings = new MockClickItSettings();

            // Configure with invalid values
            invalidSettings.ClickDistance = -10; // Invalid negative distance
            invalidSettings.ClickLabelKey = MockKeys.None; // Invalid key
            invalidSettings.ModWeights["InvalidMod"] = 150; // Weight out of range

            // Act
            var validationResult = settingsValidator.ValidateSettings(invalidSettings);

            // Assert
            validationResult.IsValid.Should().BeFalse("invalid configuration should fail validation");
            validationResult.Errors.Should().NotBeEmpty("invalid configuration should have errors");
            validationResult.Errors.Should().Contain(e => e.Contains("ClickDistance"), "should detect invalid click distance");
            validationResult.Errors.Should().Contain(e => e.Contains("ClickLabelKey"), "should detect invalid key");
            validationResult.Errors.Should().Contain(e => e.Contains("weight"), "should detect invalid mod weights");
        }

        [TestMethod]
        public void Settings_ShouldHandleRealTimeUpdates()
        {
            // Arrange
            var settings = new MockClickItSettings();
            var updateHandler = new MockSettingsUpdateHandler();
            settings.AttachUpdateHandler(updateHandler);

            var initialClickDistance = settings.ClickDistance;

            // Act
            settings.SetClickDistance(120);
            settings.SetDebugMode(!settings.DebugMode);
            settings.SetModWeight("#% chance to drop an additional Divine Orb", 95);

            // Assert
            updateHandler.UpdatesReceived.Should().HaveCount(3, "should receive update for each setting change");
            updateHandler.UpdatesReceived.Should().Contain(u => u.PropertyName == "ClickDistance");
            updateHandler.UpdatesReceived.Should().Contain(u => u.PropertyName == "DebugMode");
            updateHandler.UpdatesReceived.Should().Contain(u => u.PropertyName == "ModWeights");

            // Should provide old and new values
            var clickDistanceUpdate = updateHandler.UpdatesReceived.First(u => u.PropertyName == "ClickDistance");
            clickDistanceUpdate.OldValue.Should().Be(initialClickDistance);
            clickDistanceUpdate.NewValue.Should().Be(120);
        }

        [TestMethod]
        public void Settings_ShouldPersistConfigurationChanges()
        {
            // Arrange
            var settingsPersistence = new MockSettingsPersistence();
            var settings = new MockClickItSettings();
            settings.AttachPersistence(settingsPersistence);

            // Act
            settings.ClickDistance = 110;
            settings.CorruptAllEssences = true;
            settings.ModWeights["Final Boss drops # additional Divine Orbs"] = 90;

            // Trigger save
            settings.SaveSettings();

            // Create new instance and load
            var loadedSettings = new MockClickItSettings();
            loadedSettings.AttachPersistence(settingsPersistence);
            loadedSettings.LoadSettings();

            // Assert
            loadedSettings.ClickDistance.Should().Be(110, "click distance should persist");
            loadedSettings.CorruptAllEssences.Should().BeTrue("essence corruption setting should persist");
            loadedSettings.ModWeights["Final Boss drops # additional Divine Orbs"].Should().Be(90, "mod weights should persist");
        }

        // (Remaining helper mocks and implementation preserved in original file)
    }
}
