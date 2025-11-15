using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Collections.Generic;
using System.Linq;
using ClickIt.Constants;

namespace ClickIt.Tests
{
    [TestClass]
    public class ServiceIntegrationTests
    {
        [TestMethod]
        public void AltarService_WeightCalculator_Integration_ShouldProduceSameResults()
        {
            // Arrange
            var mockSettings = new MockClickItSettings();
            var altarService = new MockAltarService();
            var weightCalculator = new MockWeightCalculator(mockSettings);

            var altar = TestFactories.CreateTestAltarComponent();

            // Act
            var serviceWeights = altarService.CalculateWeights(altar, mockSettings);
            var calculatorWeights = weightCalculator.CalculateAltarWeights(altar);

            // Assert
            serviceWeights.TopWeight.Should().Be(calculatorWeights.TopWeight, "service and calculator should produce identical weights");
            serviceWeights.BottomWeight.Should().Be(calculatorWeights.BottomWeight, "service and calculator should produce identical weights");
            serviceWeights.TopUpsideWeight.Should().Be(calculatorWeights.TopUpsideWeight);
            serviceWeights.TopDownsideWeight.Should().Be(calculatorWeights.TopDownsideWeight);
            serviceWeights.BottomUpsideWeight.Should().Be(calculatorWeights.BottomUpsideWeight);
            serviceWeights.BottomDownsideWeight.Should().Be(calculatorWeights.BottomDownsideWeight);
        }

        [TestMethod]
        public void ElementService_LabelFilterService_Integration_ShouldCoordinateFiltering()
        {
            // Arrange
            var mockLabels = TestFactories.CreateMockLabels();
            var mockSettings = new MockClickItSettings();
            var labelFilterService = new MockLabelFilterService(mockSettings);

            // Act
            var filteredLabels = labelFilterService.GetFilteredLabels(mockLabels);
            var elementResults = new List<MockElementResult>();

            foreach (var label in filteredLabels)
            {
                var elementFound = MockElementService.GetElementByString(label.MockElement, "valuedefault");
                if (elementFound != null)
                {
                    elementResults.Add(new MockElementResult { Label = label, Element = elementFound });
                }
            }

            // Assert
            elementResults.Should().NotBeEmpty("filtered labels should produce element results");
            elementResults.All(r => r.Element != null).Should().BeTrue("all results should have valid elements");
            elementResults.All(r => MockElementService.IsValidElement(r.Element)).Should().BeTrue("all elements should pass validation");
        }

        [TestMethod]
        public void AreaService_InputHandler_Integration_ShouldValidateCoordinates()
        {
            // Arrange
            var areaService = new MockAreaService();
            var inputHandler = new MockInputHandler();
            var windowRect = new MockRectangle(0, 0, 1920, 1080);

            areaService.UpdateScreenAreas(windowRect);

            var testPositions = new[]
            {
                new MockVector2(960, 540),   // Center - should be valid
                new MockVector2(800, 950),   // Health area - should be invalid
                new MockVector2(1720, 950),  // Mana area - should be invalid
                new MockVector2(100, 60),    // Buffs area - should be invalid
                new MockVector2(500, 300)    // Gameplay area - should be valid
            };

            // Act & Assert
            foreach (var position in testPositions)
            {
                var areaValid = areaService.PointIsInClickableArea(position);
                var inputValid = inputHandler.IsValidClickPosition(position, windowRect);

                if (areaValid)
                {
                    inputValid.Should().BeTrue($"position {position} should be valid for both area and input services");
                }
                else
                {
                    inputValid.Should().BeFalse($"position {position} should be invalid for both area and input services");
                }
            }
        }

        [TestMethod]
        public void AltarService_ElementService_Integration_ShouldParseAltarData()
        {
            // Arrange
            var altarService = new MockAltarService();
            var mockElement = MockElementService.CreateAltarElement(
                "PlayerDropsItemsOnDeath",
                new[] { "#% increased Quantity of Items found in this Area", "Final Boss drops # additional Divine Orbs" },
                new[] { "-#% to Chaos Resistance", "#% reduced Recovery Rate of Life, Mana and Energy Shield per Endurance Charge" }
            );

            // Act
            var extractedData = altarService.ExtractModsFromElement(mockElement);
            var processedMods = altarService.ProcessModsData(extractedData.negativeModType, extractedData.mods);

            // Assert
            extractedData.negativeModType.Should().Be("PlayerDropsItemsOnDeath");
            extractedData.mods.Should().HaveCount(4, "should extract all upside and downside mods");

            processedMods.upsides.Should().HaveCount(2, "should identify upside mods correctly");
            processedMods.downsides.Should().HaveCount(2, "should identify downside mods correctly");

            processedMods.upsides.Should().Contain("#% increased Quantity of Items found in this Area");
            processedMods.upsides.Should().Contain("Final Boss drops # additional Divine Orbs");
            processedMods.downsides.Should().Contain("-#% to Chaos Resistance");
            processedMods.downsides.Should().Contain("#% reduced Recovery Rate of Life, Mana and Energy Shield per Endurance Charge");
        }

        [TestMethod]
        public void LabelFilterService_AreaService_Integration_ShouldRespectClickableAreas()
        {
            // Arrange
            var areaService = new MockAreaService();
            var labelFilterService = new MockLabelFilterService(new MockClickItSettings());
            var windowRect = new MockRectangle(0, 0, 1920, 1080);

            areaService.UpdateScreenAreas(windowRect);

            var labelsInVariousAreas = new[]
            {
                TestFactories.CreateMockLabel(960, 540, "DelveMineral"),     // Center - clickable
                TestFactories.CreateMockLabel(800, 950, "CleansingFireAltar"), // Health area - not clickable
                TestFactories.CreateMockLabel(1720, 950, "TangleAltar"),      // Mana area - not clickable
                TestFactories.CreateMockLabel(100, 60, "CraftingUnlocks"),    // Buffs area - not clickable
                TestFactories.CreateMockLabel(500, 300, "Harvest/Extractor")   // Gameplay area - clickable
            };

            // Act
            var filteredLabels = new List<MockLabel>();
            foreach (var label in labelsInVariousAreas)
            {
                if (areaService.PointIsInClickableArea(label.Position) && labelFilterService.ShouldClickLabel(label))
                {
                    filteredLabels.Add(label);
                }
            }

            // Assert
            filteredLabels.Should().HaveCount(2, "only labels in clickable areas should be included");
            filteredLabels.Should().Contain(l => l.Path == "DelveMineral");
            filteredLabels.Should().Contain(l => l.Path == "Harvest/Extractor");
            filteredLabels.Should().NotContain(l => l.Path == "CleansingFireAltar");
            filteredLabels.Should().NotContain(l => l.Path == "TangleAltar");
            filteredLabels.Should().NotContain(l => l.Path == "CraftingUnlocks");
        }

        [TestMethod]
        public void WeightCalculator_AltarService_Integration_ShouldHandleComplexModCombinations()
        {
            // Arrange
            var settings = new MockClickItSettings();
            var weightCalculator = new MockWeightCalculator(settings);
            var altarService = new MockAltarService();

            // Set up complex mod combination with high-value upsides and dangerous downsides
            settings.SetModWeight("#% chance to drop an additional Divine Orb", 100);
            settings.SetModWeight("Final Boss drops # additional Divine Orbs", 100);
            settings.SetModWeight("Projectiles are fired in random directions", 100);
            settings.SetModWeight("-#% to Chaos Resistance", 75);

            var complexAltar = TestFactories.CreateComplexTestAltarComponent();

            // Act
            var weights = weightCalculator.CalculateAltarWeights(complexAltar);
            var serviceDecision = altarService.DetermineOptimalChoice(complexAltar, weights);

            // Assert
            weights.TopWeight.Should().BeGreaterThan(0, "complex calculations should produce valid weights");
            weights.BottomWeight.Should().BeGreaterThan(0, "complex calculations should produce valid weights");

            serviceDecision.Should().NotBeNull("service should make a decision even with complex mods");
            serviceDecision.IsTopChoice.Should().Be(weights.TopWeight > weights.BottomWeight);
            serviceDecision.Confidence.Should().BeInRange(0, 100, "confidence should be in valid range");
        }

        [TestMethod]
        public void EssenceService_ElementService_Integration_ShouldDetectCorruptionOptions()
        {
            // Arrange
            var essenceService = new MockEssenceService(new MockClickItSettings { CorruptAllEssences = true });
            var essenceElement = MockElementService.CreateEssenceElement(
                new[] { "Screaming Essence of Misery", "Deafening Essence of Rage" },
                hasCorruption: true
            );

            // Act
            var shouldCorrupt = essenceService.ShouldCorruptEssence(essenceElement);
            var corruptionPosition = essenceService.GetCorruptionClickPosition(essenceElement, new MockVector2(100, 100));

            // Assert
            shouldCorrupt.Should().BeTrue("essence service should detect corruption opportunity");
            corruptionPosition.Should().NotBeNull("should provide valid corruption click position");
            // Ensure corruption click position accounts for provided window offset (allow equality)
            corruptionPosition.X.Should().BeGreaterOrEqualTo(100, "corruption position should account for window offset");
            corruptionPosition.Y.Should().BeGreaterOrEqualTo(100, "corruption position should account for window offset");
        }

        [TestMethod]
        public void InputHandler_AreaService_Integration_ShouldCalculateValidClickPositions()
        {
            // Arrange
            var inputHandler = new MockInputHandler();
            var areaService = new MockAreaService();
            var windowRect = new MockRectangle(0, 0, 1920, 1080);

            areaService.UpdateScreenAreas(windowRect);

            var testLabel = TestFactories.CreateMockLabel(500, 300, "DelveMineral");
            var windowOffset = new MockVector2(100, 50);

            // Act
            var clickPosition = inputHandler.CalculateClickPosition(testLabel, windowOffset);
            var isClickable = areaService.PointIsInClickableArea(new MockVector2(clickPosition.X - windowOffset.X, clickPosition.Y - windowOffset.Y));

            // Assert
            isClickable.Should().BeTrue("calculated click position should be in clickable area");
            clickPosition.X.Should().BeGreaterThan(testLabel.Position.X + windowOffset.X - 5, "should include randomization within bounds");
            clickPosition.X.Should().BeLessThan(testLabel.Position.X + windowOffset.X + 5, "should include randomization within bounds");
            clickPosition.Y.Should().BeGreaterThan(testLabel.Position.Y + windowOffset.Y - 5, "should include randomization within bounds");
            clickPosition.Y.Should().BeLessThan(testLabel.Position.Y + windowOffset.Y + 5, "should include randomization within bounds");
        }

        [TestMethod]
        public void AltarService_AreaService_Integration_ShouldValidateAltarVisibility()
        {
            // Arrange
            var altarService = new MockAltarService();
            var areaService = new MockAreaService();
            var windowRect = new MockRectangle(0, 0, 1920, 1080);

            areaService.UpdateScreenAreas(windowRect);

            var altarInClickableArea = TestFactories.CreateMockAltarLabel(500, 300, "CleansingFireAltar");
            var altarInHealthArea = TestFactories.CreateMockAltarLabel(800, 950, "TangleAltar");

            // Act
            var clickableAltarValid = areaService.PointIsInClickableArea(altarInClickableArea.Position);
            var healthAreaAltarValid = areaService.PointIsInClickableArea(altarInHealthArea.Position);

            var clickableAltarShouldProcess = altarService.ShouldProcessAltar(altarInClickableArea, clickableAltarValid);
            var healthAreaAltarShouldProcess = altarService.ShouldProcessAltar(altarInHealthArea, healthAreaAltarValid);

            // Assert
            clickableAltarValid.Should().BeTrue("altar in gameplay area should be in clickable area");
            healthAreaAltarValid.Should().BeFalse("altar in health area should not be in clickable area");

            clickableAltarShouldProcess.Should().BeTrue("altar in clickable area should be processed");
            healthAreaAltarShouldProcess.Should().BeFalse("altar in non-clickable area should not be processed");
        }

        [TestMethod]
        public void WeightCalculator_Settings_Integration_ShouldRespectUserPreferences()
        {
            // Arrange
            var customSettings = new MockClickItSettings();
            customSettings.SetModWeight("#% chance to drop an additional Divine Orb", 95); // Override weight
            customSettings.SetModWeight("Projectiles are fired in random directions", 100); // Build-breaking

            var weightCalculator = new MockWeightCalculator(customSettings);
            var testAltar = TestFactories.CreateTestAltarComponent();

            // Act
            var weights = weightCalculator.CalculateAltarWeights(testAltar);
            var hasOverrides = weightCalculator.HasWeightOverrides(weights);

            // Assert
            weights.GetTopUpsideWeights()[0].Should().Be(95, "should use custom weight settings");
            hasOverrides.Should().BeTrue("should detect weight overrides for extreme values");

            // Test that calculation respects the override system
            var topDownsideWeights = new[] { weights.GetTopDownsideWeights()[0] };
            if (topDownsideWeights.Any(w => w >= 90))
            {
                weights.TopWeight.Should().BeLessThan(weights.BottomWeight, "build-breaking downsides should heavily penalize option");
            }
        }

        [TestMethod]
        public void ElementService_AltarService_Integration_ShouldHandleNestedElementStructures()
        {
            // Arrange
            var altarService = new MockAltarService();
            var complexElement = MockElementService.CreateNestedAltarElement(
                depth: 3,
                "PlayerDropsItemsOnDeath",
                new[] { "Complex upside mod with nested structure", "Another complex upside" },
                new[] { "Complex downside mod", "Another complex downside" }
            );

            // Act
            var elementsFound = MockElementService.GetElementsByStringContains(complexElement, "valuedefault");
            var altarData = altarService.ProcessNestedElementStructure(elementsFound);

            // Assert
            elementsFound.Should().NotBeEmpty("should find elements in nested structure");
            altarData.Should().NotBeNull("should successfully process nested elements");
            altarData.TopMods.Should().NotBeNull("should extract top mods from nested structure");
            altarData.BottomMods.Should().NotBeNull("should extract bottom mods from nested structure");
            altarData.TopMods.Upsides.Should().HaveCountGreaterThan(0, "should extract upside mods");
            altarData.TopMods.Downsides.Should().HaveCountGreaterThan(0, "should extract downside mods");
        }

        [TestMethod]
        public void LabelFilterService_InputHandler_Integration_ShouldCoordinateDistanceValidation()
        {
            // Arrange
            var settings = new MockClickItSettings { ClickDistance = 95 };
            var labelFilterService = new MockLabelFilterService(settings);
            var inputHandler = new MockInputHandler();

            var labelsAtVariousDistances = new[]
            {
                TestFactories.CreateMockLabelWithDistance(50, "DelveMineral"),    // Within range
                TestFactories.CreateMockLabelWithDistance(90, "CraftingUnlocks"), // Within range
                TestFactories.CreateMockLabelWithDistance(100, "Harvest/Extractor"), // Outside range
                TestFactories.CreateMockLabelWithDistance(150, "CleansingFireAltar")  // Far outside range
            };

            // Act
            var filteredLabels = new List<MockLabel>();
            foreach (var label in labelsAtVariousDistances)
            {
                if (labelFilterService.IsWithinClickDistance(label) && inputHandler.CanPerformClickOn(label))
                {
                    filteredLabels.Add(label);
                }
            }

            // Assert
            filteredLabels.Should().HaveCount(2, "only labels within distance should be processed");
            filteredLabels.All(l => l.Distance <= 95).Should().BeTrue("all filtered labels should respect distance limit");
            filteredLabels.Should().Contain(l => l.Path == "DelveMineral");
            filteredLabels.Should().Contain(l => l.Path == "CraftingUnlocks");
        }

        [TestMethod]
        public void AreaService_ElementService_Integration_ShouldValidateElementPositions()
        {
            // Arrange
            var areaService = new MockAreaService();
            var windowRect = new MockRectangle(0, 0, 1920, 1080);
            areaService.UpdateScreenAreas(windowRect);

            var elementsInVariousAreas = new[]
            {
                MockElementService.CreateElementAtPosition(960, 540),   // Center - valid
                MockElementService.CreateElementAtPosition(800, 950),   // Health area - invalid
                MockElementService.CreateElementAtPosition(1720, 950),  // Mana area - invalid
                MockElementService.CreateElementAtPosition(100, 60),    // Buffs area - invalid
                MockElementService.CreateElementAtPosition(500, 300)    // Gameplay area - valid
            };

            // Act
            var validElements = new List<MockElement>();
            foreach (var element in elementsInVariousAreas)
            {
                var elementCenter = MockElementService.GetElementCenter(element);
                if (areaService.PointIsInClickableArea(elementCenter) && MockElementService.IsElementVisible(element))
                {
                    validElements.Add(element);
                }
            }

            // Assert
            validElements.Should().HaveCount(2, "only elements in clickable areas should be valid");
            validElements.All(e => MockElementService.IsElementVisible(e)).Should().BeTrue("all valid elements should be visible");
        }

        [TestMethod]
        public void AltarService_WeightCalculator_Integration_ShouldHandleEmptyModLists()
        {
            // Arrange
            var settings = new MockClickItSettings();
            var altarService = new MockAltarService();
            var weightCalculator = new MockWeightCalculator(settings);

            var emptyAltar = TestFactories.CreateEmptyAltarComponent(); // No mods

            // Act
            var weights = weightCalculator.CalculateAltarWeights(emptyAltar);
            var serviceCanProcess = altarService.CanProcessAltar(emptyAltar);

            // Assert
            weights.TopUpsideWeight.Should().Be(0, "empty upside list should have zero weight");
            weights.TopDownsideWeight.Should().Be(1, "empty downside list should have minimum weight of 1");
            weights.BottomUpsideWeight.Should().Be(0, "empty upside list should have zero weight");
            weights.BottomDownsideWeight.Should().Be(1, "empty downside list should have minimum weight of 1");

            serviceCanProcess.Should().BeFalse("service should not process altar with no mods");
        }


    }
}
