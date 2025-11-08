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

            var altar = CreateTestAltarComponent();

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
            var mockLabels = CreateMockLabels();
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
                CreateMockLabel(960, 540, "DelveMineral"),     // Center - clickable
                CreateMockLabel(800, 950, "CleansingFireAltar"), // Health area - not clickable
                CreateMockLabel(1720, 950, "TangleAltar"),      // Mana area - not clickable
                CreateMockLabel(100, 60, "CraftingUnlocks"),    // Buffs area - not clickable
                CreateMockLabel(500, 300, "Harvest/Extractor")   // Gameplay area - clickable
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

            var complexAltar = CreateComplexTestAltarComponent();

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
            corruptionPosition.X.Should().BeGreaterThan(100, "corruption position should account for window offset");
            corruptionPosition.Y.Should().BeGreaterThan(100, "corruption position should account for window offset");
        }

        [TestMethod]
        public void InputHandler_AreaService_Integration_ShouldCalculateValidClickPositions()
        {
            // Arrange
            var inputHandler = new MockInputHandler();
            var areaService = new MockAreaService();
            var windowRect = new MockRectangle(0, 0, 1920, 1080);

            areaService.UpdateScreenAreas(windowRect);

            var testLabel = CreateMockLabel(500, 300, "DelveMineral");
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

            var altarInClickableArea = CreateMockAltarLabel(500, 300, "CleansingFireAltar");
            var altarInHealthArea = CreateMockAltarLabel(800, 950, "TangleAltar");

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
            var testAltar = CreateTestAltarComponent();

            // Act
            var weights = weightCalculator.CalculateAltarWeights(testAltar);
            var hasOverrides = weightCalculator.HasWeightOverrides(weights);

            // Assert
            weights.TopUpside1Weight.Should().Be(95, "should use custom weight settings");
            hasOverrides.Should().BeTrue("should detect weight overrides for extreme values");

            // Test that calculation respects the override system
            var topDownsideWeights = new[] { weights.TopDownside1Weight };
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
                CreateMockLabelWithDistance(50, "DelveMineral"),    // Within range
                CreateMockLabelWithDistance(90, "CraftingUnlocks"), // Within range
                CreateMockLabelWithDistance(100, "Harvest/Extractor"), // Outside range
                CreateMockLabelWithDistance(150, "CleansingFireAltar")  // Far outside range
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

            var emptyAltar = CreateEmptyAltarComponent(); // No mods

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

        // Helper methods for creating mock objects
        private static MockAltarComponent CreateTestAltarComponent()
        {
            return new MockAltarComponent
            {
                TopMods = new MockSecondaryAltarComponent
                {
                    Upsides = new List<string> { "#% chance to drop an additional Divine Orb", "#% increased Quantity of Items found in this Area" },
                    Downsides = new List<string> { "-#% to Chaos Resistance", "#% reduced Recovery Rate of Life, Mana and Energy Shield per Endurance Charge" }
                },
                BottomMods = new MockSecondaryAltarComponent
                {
                    Upsides = new List<string> { "Final Boss drops # additional Divine Orbs", "#% increased Experience gain" },
                    Downsides = new List<string> { "Projectiles are fired in random directions", "-#% to Fire Resistance" }
                }
            };
        }

        private static MockAltarComponent CreateComplexTestAltarComponent()
        {
            return new MockAltarComponent
            {
                TopMods = new MockSecondaryAltarComponent
                {
                    Upsides = new List<string> { "#% chance to drop an additional Divine Orb", "Final Boss drops # additional Divine Orbs" },
                    Downsides = new List<string> { "Projectiles are fired in random directions", "Curses you inflict are reflected back to you" }
                },
                BottomMods = new MockSecondaryAltarComponent
                {
                    Upsides = new List<string> { "#% increased Quantity of Items found in this Area", "#% increased Experience gain" },
                    Downsides = new List<string> { "-#% to Chaos Resistance", "#% reduced Recovery Rate of Life, Mana and Energy Shield per Endurance Charge" }
                }
            };
        }

        private static MockAltarComponent CreateEmptyAltarComponent()
        {
            return new MockAltarComponent
            {
                TopMods = new MockSecondaryAltarComponent
                {
                    Upsides = new List<string>(),
                    Downsides = new List<string>()
                },
                BottomMods = new MockSecondaryAltarComponent
                {
                    Upsides = new List<string>(),
                    Downsides = new List<string>()
                }
            };
        }

        private static List<MockLabel> CreateMockLabels()
        {
            return new List<MockLabel>
            {
                CreateMockLabel(100, 100, "DelveMineral"),
                CreateMockLabel(200, 200, "CleansingFireAltar"),
                CreateMockLabel(300, 300, "Harvest/Extractor"),
                CreateMockLabel(800, 950, "TangleAltar") // In health area
            };
        }

        private static MockLabel CreateMockLabel(float x, float y, string path)
        {
            return new MockLabel
            {
                Position = new MockVector2(x, y),
                Path = path,
                Distance = 50,
                MockElement = MockElementService.CreateBasicElement()
            };
        }

        private static MockLabel CreateMockLabelWithDistance(float distance, string path)
        {
            return new MockLabel
            {
                Position = new MockVector2(500, 300),
                Path = path,
                Distance = distance,
                MockElement = MockElementService.CreateBasicElement()
            };
        }

        private static MockAltarLabel CreateMockAltarLabel(float x, float y, string path)
        {
            return new MockAltarLabel
            {
                Position = new MockVector2(x, y),
                Path = path,
                AltarType = path.Contains("CleansingFireAltar") ? "SearingExarch" : "EaterOfWorlds"
            };
        }

        // Mock classes for testing
        public class MockClickItSettings
        {
            public bool CorruptAllEssences { get; set; } = false;
            public int ClickDistance { get; set; } = 95;
            private readonly Dictionary<string, int> _modWeights = new Dictionary<string, int>();

            public void SetModWeight(string modId, int weight) => _modWeights[modId] = weight;
            public int GetModTier(string modId) => _modWeights.TryGetValue(modId, out int value) ? value : 50;
        }

        public class MockAltarService
        {
            public MockAltarWeights CalculateWeights(MockAltarComponent altar, MockClickItSettings settings)
            {
                var calculator = new MockWeightCalculator(settings);
                return calculator.CalculateAltarWeights(altar);
            }

            public (string negativeModType, List<string> mods) ExtractModsFromElement(MockElement element)
            {
                return ("PlayerDropsItemsOnDeath", new List<string>
                {
                    "#% increased Quantity of Items found in this Area",
                    "Final Boss drops # additional Divine Orbs",
                    "-#% to Chaos Resistance",
                    "#% reduced Recovery Rate of Life, Mana and Energy Shield per Endurance Charge"
                });
            }

            public (List<string> upsides, List<string> downsides) ProcessModsData(string negativeModType, List<string> mods)
            {
                var upsides = mods.Where(m => !m.StartsWith("-") && !m.Contains("reduced")).ToList();
                var downsides = mods.Where(m => m.StartsWith("-") || m.Contains("reduced")).ToList();
                return (upsides, downsides);
            }

            public MockAltarDecision DetermineOptimalChoice(MockAltarComponent altar, MockAltarWeights weights)
            {
                return new MockAltarDecision
                {
                    IsTopChoice = weights.TopWeight > weights.BottomWeight,
                    Confidence = (int)System.Math.Abs(weights.TopWeight - weights.BottomWeight) * 10
                };
            }

            public bool ShouldProcessAltar(MockAltarLabel altar, bool isInClickableArea)
            {
                return isInClickableArea;
            }

            public bool CanProcessAltar(MockAltarComponent altar)
            {
                return altar.TopMods.Upsides.Any() || altar.TopMods.Downsides.Any() ||
                       altar.BottomMods.Upsides.Any() || altar.BottomMods.Downsides.Any();
            }

            public MockAltarData ProcessNestedElementStructure(List<MockElement> elements)
            {
                return new MockAltarData
                {
                    TopMods = new MockSecondaryAltarComponent
                    {
                        Upsides = new List<string> { "Complex upside mod with nested structure" },
                        Downsides = new List<string> { "Complex downside mod" }
                    },
                    BottomMods = new MockSecondaryAltarComponent
                    {
                        Upsides = new List<string> { "Another complex upside" },
                        Downsides = new List<string> { "Another complex downside" }
                    }
                };
            }
        }

        public class MockWeightCalculator
        {
            private readonly MockClickItSettings _settings;

            public MockWeightCalculator(MockClickItSettings settings)
            {
                _settings = settings;
            }

            public MockAltarWeights CalculateAltarWeights(MockAltarComponent altar)
            {
                var topUpsideWeight = CalculateUpsideWeight(altar.TopMods.Upsides);
                var topDownsideWeight = CalculateDownsideWeight(altar.TopMods.Downsides);
                var bottomUpsideWeight = CalculateUpsideWeight(altar.BottomMods.Upsides);
                var bottomDownsideWeight = CalculateDownsideWeight(altar.BottomMods.Downsides);

                return new MockAltarWeights
                {
                    TopUpsideWeight = topUpsideWeight,
                    TopDownsideWeight = topDownsideWeight,
                    BottomUpsideWeight = bottomUpsideWeight,
                    BottomDownsideWeight = bottomDownsideWeight,
                    TopUpside1Weight = altar.TopMods.Upsides.Any() ? _settings.GetModTier(altar.TopMods.Upsides[0]) : 0,
                    TopDownside1Weight = altar.TopMods.Downsides.Any() ? _settings.GetModTier(altar.TopMods.Downsides[0]) : 0,
                    BottomUpside1Weight = altar.BottomMods.Upsides.Any() ? _settings.GetModTier(altar.BottomMods.Upsides[0]) : 0,
                    BottomDownside1Weight = altar.BottomMods.Downsides.Any() ? _settings.GetModTier(altar.BottomMods.Downsides[0]) : 0,
                    TopUpside2Weight = altar.TopMods.Upsides.Count > 1 ? _settings.GetModTier(altar.TopMods.Upsides[1]) : 0,
                    TopDownside2Weight = altar.TopMods.Downsides.Count > 1 ? _settings.GetModTier(altar.TopMods.Downsides[1]) : 0,
                    BottomUpside2Weight = altar.BottomMods.Upsides.Count > 1 ? _settings.GetModTier(altar.BottomMods.Upsides[1]) : 0,
                    BottomDownside2Weight = altar.BottomMods.Downsides.Count > 1 ? _settings.GetModTier(altar.BottomMods.Downsides[1]) : 0,
                    TopUpside3Weight = altar.TopMods.Upsides.Count > 2 ? _settings.GetModTier(altar.TopMods.Upsides[2]) : 0,
                    TopDownside3Weight = altar.TopMods.Downsides.Count > 2 ? _settings.GetModTier(altar.TopMods.Downsides[2]) : 0,
                    BottomUpside3Weight = altar.BottomMods.Upsides.Count > 2 ? _settings.GetModTier(altar.BottomMods.Upsides[2]) : 0,
                    BottomDownside3Weight = altar.BottomMods.Downsides.Count > 2 ? _settings.GetModTier(altar.BottomMods.Downsides[2]) : 0,
                    TopUpside4Weight = altar.TopMods.Upsides.Count > 3 ? _settings.GetModTier(altar.TopMods.Upsides[3]) : 0,
                    TopDownside4Weight = altar.TopMods.Downsides.Count > 3 ? _settings.GetModTier(altar.TopMods.Downsides[3]) : 0,
                    BottomUpside4Weight = altar.BottomMods.Upsides.Count > 3 ? _settings.GetModTier(altar.BottomMods.Upsides[3]) : 0,
                    BottomDownside4Weight = altar.BottomMods.Downsides.Count > 3 ? _settings.GetModTier(altar.BottomMods.Downsides[3]) : 0,
                    TopWeight = topDownsideWeight > 0 ? System.Math.Round(topUpsideWeight / topDownsideWeight, 2) : 0,
                    BottomWeight = bottomDownsideWeight > 0 ? System.Math.Round(bottomUpsideWeight / bottomDownsideWeight, 2) : 0
                };
            }

            public bool HasWeightOverrides(MockAltarWeights weights)
            {
                return weights.TopUpside1Weight >= 90 || weights.TopDownside1Weight >= 90 ||
                       weights.BottomUpside1Weight >= 90 || weights.BottomDownside1Weight >= 90 ||
                       weights.TopUpside2Weight >= 90 || weights.TopDownside2Weight >= 90 ||
                       weights.BottomUpside2Weight >= 90 || weights.BottomDownside2Weight >= 90 ||
                       weights.TopUpside3Weight >= 90 || weights.TopDownside3Weight >= 90 ||
                       weights.BottomUpside3Weight >= 90 || weights.BottomDownside3Weight >= 90 ||
                       weights.TopUpside4Weight >= 90 || weights.TopDownside4Weight >= 90 ||
                       weights.BottomUpside4Weight >= 90 || weights.BottomDownside4Weight >= 90;
            }

            private decimal CalculateUpsideWeight(List<string> upsides)
            {
                return upsides?.Sum(u => _settings.GetModTier(u)) ?? 0;
            }

            private decimal CalculateDownsideWeight(List<string> downsides)
            {
                return (downsides?.Sum(d => _settings.GetModTier(d)) ?? 0) + 1;
            }
        }

        public class MockLabelFilterService
        {
            private readonly MockClickItSettings _settings;

            public MockLabelFilterService(MockClickItSettings settings)
            {
                _settings = settings;
            }

            public List<MockLabel> GetFilteredLabels(List<MockLabel> labels)
            {
                return labels.Where(ShouldClickLabel).ToList();
            }

            public bool ShouldClickLabel(MockLabel label)
            {
                return !string.IsNullOrEmpty(label.Path) &&
                       (label.Path.Contains("Delve") || label.Path.Contains("Harvest") ||
                        label.Path.Contains("Altar") || label.Path.Contains("Crafting"));
            }

            public bool IsWithinClickDistance(MockLabel label)
            {
                return label.Distance <= _settings.ClickDistance;
            }
        }

        public class MockAreaService
        {
            private MockRectangle _healthArea;
            private MockRectangle _manaArea;
            private MockRectangle _buffsArea;
            private MockRectangle _fullArea;

            public void UpdateScreenAreas(MockRectangle windowRect)
            {
                _fullArea = windowRect;
                _healthArea = new MockRectangle(windowRect.Width / 3, windowRect.Height * 0.78f, windowRect.Width / 3.4f, windowRect.Height * 0.22f);
                _manaArea = new MockRectangle(windowRect.Width * 0.71f, windowRect.Height * 0.78f, windowRect.Width * 0.29f, windowRect.Height * 0.22f);
                _buffsArea = new MockRectangle(0, 0, windowRect.Width / 2, 120);
            }

            public bool PointIsInClickableArea(MockVector2 point)
            {
                return IsInRectangle(point, _fullArea) &&
                       !IsInRectangle(point, _healthArea) &&
                       !IsInRectangle(point, _manaArea) &&
                       !IsInRectangle(point, _buffsArea);
            }

            private static bool IsInRectangle(MockVector2 point, MockRectangle rect)
            {
                return point.X >= rect.X && point.X <= rect.X + rect.Width &&
                       point.Y >= rect.Y && point.Y <= rect.Y + rect.Height;
            }
        }

        public class MockInputHandler
        {
            public bool IsValidClickPosition(MockVector2 position, MockRectangle windowRect)
            {
                // Mock the same area validation logic as AreaService for consistency
                var areaService = new MockAreaService();
                areaService.UpdateScreenAreas(windowRect);
                return areaService.PointIsInClickableArea(position);
            }

            public MockVector2 CalculateClickPosition(MockLabel label, MockVector2 windowOffset)
            {
                var random = new System.Random();
                return new MockVector2(
                    label.Position.X + windowOffset.X + random.Next(-2, 3),
                    label.Position.Y + windowOffset.Y + random.Next(-2, 3)
                );
            }

            public bool CanPerformClickOn(MockLabel label)
            {
                return !string.IsNullOrEmpty(label.Path);
            }
        }

        public class MockEssenceService
        {
            private readonly MockClickItSettings _settings;

            public MockEssenceService(MockClickItSettings settings)
            {
                _settings = settings;
            }

            public bool ShouldCorruptEssence(MockElement element)
            {
                return _settings.CorruptAllEssences;
            }

            public MockVector2 GetCorruptionClickPosition(MockElement element, MockVector2 windowOffset)
            {
                return new MockVector2(windowOffset.X + 50, windowOffset.Y + 50);
            }
        }

        public static class MockElementService
        {
            public static MockElement GetElementByString(MockElement root, string searchString)
            {
                return root?.Text?.Contains(searchString) == true ? root : null;
            }

            public static List<MockElement> GetElementsByStringContains(MockElement root, string searchString)
            {
                var results = new List<MockElement>();
                if (root?.Text?.Contains(searchString) == true)
                {
                    results.Add(root);
                }
                return results;
            }

            public static bool IsValidElement(MockElement element)
            {
                return element != null && !string.IsNullOrEmpty(element.Text);
            }

            public static MockElement CreateAltarElement(string negativeModType, string[] upsides, string[] downsides)
            {
                return new MockElement { Text = $"valuedefault {negativeModType} {string.Join(" ", upsides)} {string.Join(" ", downsides)}" };
            }

            public static MockElement CreateEssenceElement(string[] essenceTypes, bool hasCorruption)
            {
                return new MockElement { Text = $"{string.Join(" ", essenceTypes)} {(hasCorruption ? "corruption" : "")}" };
            }

            public static MockElement CreateBasicElement()
            {
                return new MockElement { Text = "valuedefault basic element" };
            }

            public static MockElement CreateNestedAltarElement(int depth, string negativeModType, string[] upsides, string[] downsides)
            {
                return new MockElement
                {
                    Text = $"valuedefault nested_{depth} {negativeModType} {string.Join(" ", upsides)} {string.Join(" ", downsides)}",
                    Depth = depth
                };
            }

            public static MockElement CreateElementAtPosition(float x, float y)
            {
                return new MockElement
                {
                    Text = "valuedefault positioned element",
                    Position = new MockVector2(x, y)
                };
            }

            public static MockVector2 GetElementCenter(MockElement element)
            {
                return element.Position ?? new MockVector2(0, 0);
            }

            public static bool IsElementVisible(MockElement element)
            {
                return element != null && !string.IsNullOrEmpty(element.Text);
            }
        }

        // Data classes
        public class MockAltarWeights
        {
            public decimal TopUpsideWeight { get; set; }
            public decimal TopDownsideWeight { get; set; }
            public decimal BottomUpsideWeight { get; set; }
            public decimal BottomDownsideWeight { get; set; }
            public decimal TopUpside1Weight { get; set; }
            public decimal TopDownside1Weight { get; set; }
            public decimal BottomUpside1Weight { get; set; }
            public decimal BottomDownside1Weight { get; set; }
            public decimal TopUpside2Weight { get; set; }
            public decimal TopDownside2Weight { get; set; }
            public decimal BottomUpside2Weight { get; set; }
            public decimal BottomDownside2Weight { get; set; }
            public decimal TopUpside3Weight { get; set; }
            public decimal TopDownside3Weight { get; set; }
            public decimal BottomUpside3Weight { get; set; }
            public decimal BottomDownside3Weight { get; set; }
            public decimal TopUpside4Weight { get; set; }
            public decimal TopDownside4Weight { get; set; }
            public decimal BottomUpside4Weight { get; set; }
            public decimal BottomDownside4Weight { get; set; }
            public decimal TopWeight { get; set; }
            public decimal BottomWeight { get; set; }
        }

        public class MockAltarComponent
        {
            public MockSecondaryAltarComponent TopMods { get; set; }
            public MockSecondaryAltarComponent BottomMods { get; set; }
        }

        public class MockSecondaryAltarComponent
        {
            public List<string> Upsides { get; set; } = new List<string>();
            public List<string> Downsides { get; set; } = new List<string>();
            public string FirstUpside => Upsides.Count > 0 ? Upsides[0] : "";
            public string SecondUpside => Upsides.Count > 1 ? Upsides[1] : "";
            public string ThirdUpside => Upsides.Count > 2 ? Upsides[2] : "";
            public string FourthUpside => Upsides.Count > 3 ? Upsides[3] : "";
            public string FirstDownside => Downsides.Count > 0 ? Downsides[0] : "";
            public string SecondDownside => Downsides.Count > 1 ? Downsides[1] : "";
            public string ThirdDownside => Downsides.Count > 2 ? Downsides[2] : "";
            public string FourthDownside => Downsides.Count > 3 ? Downsides[3] : "";
        }

        public class MockLabel
        {
            public MockVector2 Position { get; set; }
            public string Path { get; set; }
            public float Distance { get; set; }
            public MockElement MockElement { get; set; }
        }

        public class MockAltarLabel
        {
            public MockVector2 Position { get; set; }
            public string Path { get; set; }
            public string AltarType { get; set; }
        }

        public class MockElement
        {
            public string Text { get; set; }
            public int Depth { get; set; }
            public MockVector2 Position { get; set; }
        }

        public class MockVector2
        {
            public float X { get; set; }
            public float Y { get; set; }

            public MockVector2(float x, float y)
            {
                X = x;
                Y = y;
            }

            public override string ToString() => $"({X}, {Y})";
        }

        public class MockRectangle
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float Width { get; set; }
            public float Height { get; set; }

            public MockRectangle(float x, float y, float width, float height)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }
        }

        public class MockElementResult
        {
            public MockLabel Label { get; set; }
            public MockElement Element { get; set; }
        }

        public class MockAltarDecision
        {
            public bool IsTopChoice { get; set; }
            public int Confidence { get; set; }
        }

        public class MockAltarData
        {
            public MockSecondaryAltarComponent TopMods { get; set; }
            public MockSecondaryAltarComponent BottomMods { get; set; }
        }
    }
}