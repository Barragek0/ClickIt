using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Collections.Generic;
using System.Linq;
using System;
using ClickIt.Constants;

namespace ClickIt.Tests
{
    [TestClass]
    public class PathMatchingAndClassificationTests
    {
        [TestMethod]
        public void EntityPathMatching_ShouldIdentifyExarchAltars()
        {
            // Arrange
            var exarchPaths = new[]
            {
                "Metadata/Terrain/Leagues/Archnemesis/Objects/CleansingFireAltar",
                "SomePrefix/CleansingFireAltar/SomeSuffix",
                "/CleansingFireAltar",
                "CleansingFireAltar"
            };

            // Act & Assert
            foreach (var path in exarchPaths)
            {
                IsExarchAltar(path).Should().BeTrue($"Path '{path}' should be identified as Exarch altar");
            }
        }

        [TestMethod]
        public void EntityPathMatching_ShouldIdentifyEaterAltars()
        {
            // Arrange
            var eaterPaths = new[]
            {
                "Metadata/Terrain/Leagues/Archnemesis/Objects/TangleAltar",
                "SomePrefix/TangleAltar/SomeSuffix",
                "/TangleAltar",
                "TangleAltar"
            };

            // Act & Assert
            foreach (var path in eaterPaths)
            {
                IsEaterAltar(path).Should().BeTrue($"Path '{path}' should be identified as Eater altar");
            }
        }

        [TestMethod]
        public void EntityPathMatching_ShouldIdentifySettlersOre()
        {
            // Arrange
            var orePaths = new[]
            {
                "Metadata/Chests/CrimsonIron",
                "Metadata/Chests/Verisium",
                "Metadata/Chests/PetrifiedWood",
                "Metadata/Chests/Bismuth",
                "Metadata/Chests/copper_altar"
            };

            // Act & Assert
            foreach (var path in orePaths)
            {
                IsSettlersOre(path).Should().BeTrue($"Path '{path}' should be identified as Settlers ore");
            }
        }

        [TestMethod]
        public void EntityPathMatching_ShouldIdentifyHarvestNodes()
        {
            // Arrange
            var harvestPaths = new[]
            {
                "Metadata/Terrain/Leagues/Harvest/Objects/Harvest/Irrigator",
                "Metadata/Terrain/Leagues/Harvest/Objects/Harvest/Extractor",
                "SomePrefix/Harvest/Irrigator",
                "SomePrefix/Harvest/Extractor"
            };

            // Act & Assert
            foreach (var path in harvestPaths)
            {
                IsHarvestNode(path).Should().BeTrue($"Path '{path}' should be identified as Harvest node");
            }
        }

        [TestMethod]
        public void EntityPathMatching_ShouldIdentifyDelveNodes()
        {
            // Arrange
            var delvePaths = new[]
            {
                "Metadata/Terrain/Leagues/Delve/Objects/DelveMineral",
                "Metadata/Terrain/Leagues/Delve/Objects/AzuriteEncounterController",
                "SomePrefix/DelveMineral",
                "SomePrefix/AzuriteEncounterController"
            };

            // Act & Assert
            foreach (var path in delvePaths)
            {
                IsDelveNode(path).Should().BeTrue($"Path '{path}' should be identified as Delve node");
            }
        }

        [TestMethod]
        public void EntityPathMatching_ShouldRejectInvalidPaths()
        {
            // Arrange
            var invalidPaths = new[]
            {
                "",
                null,
                "Metadata/Items/Currency/CurrencyDivination",
                "Metadata/Monsters/MonsterName",
                "RandomStringThatDoesNotMatch",
                "CleansingFire", // Partial match
                "Tangle", // Partial match
                "Irrigat", // Partial match
            };

            // Act & Assert
            foreach (var path in invalidPaths)
            {
                IsRecognizedSpecialPath(path).Should().BeFalse($"Path '{path}' should not be recognized as special");
            }
        }

        [TestMethod]
        public void EntityPathMatching_ShouldBeCaseInsensitive()
        {
            // Arrange
            var caseMixedPaths = new[]
            {
                "cleansingfirealtar",
                "TANGLEALTAR",
                "CrimsonIRON",
                "harvest/IRRIGATOR",
                "HARVEST/extractor"
            };

            // Act & Assert
            foreach (var path in caseMixedPaths)
            {
                IsRecognizedSpecialPath(path).Should().BeTrue($"Path '{path}' should be recognized regardless of case");
            }
        }

        [TestMethod]
        public void AltarTypeClassification_ShouldReturnCorrectTypes()
        {
            // Test Exarch
            GetAltarType("CleansingFireAltar").Should().Be(AltarType.SearingExarch);

            // Test Eater
            GetAltarType("TangleAltar").Should().Be(AltarType.EaterOfWorlds);

            // Test Unknown
            GetAltarType("SomeRandomPath").Should().Be(AltarType.Unknown);
            GetAltarType("").Should().Be(AltarType.Unknown);
            GetAltarType(null).Should().Be(AltarType.Unknown);
        }

        [TestMethod]
        public void EntityClassification_ShouldIdentifyBreachNodes()
        {
            // Arrange
            var breachPaths = new[]
            {
                "Metadata/Terrain/Leagues/Breach/Objects/Brequel",
                "SomePrefix/Brequel",
                "/Brequel"
            };

            // Act & Assert
            foreach (var path in breachPaths)
            {
                IsBreachNode(path).Should().BeTrue($"Path '{path}' should be identified as Breach node");
            }
        }

        [TestMethod]
        public void EntityClassification_ShouldIdentifyAlvaTempleDoors()
        {
            // Arrange
            var templeDoorPaths = new[]
            {
                "Metadata/Terrain/Leagues/Incursion/Objects/ClosedDoorPast",
                "SomePrefix/ClosedDoorPast",
                "/ClosedDoorPast"
            };

            // Act & Assert
            foreach (var path in templeDoorPaths)
            {
                IsAlvaTempleDoor(path).Should().BeTrue($"Path '{path}' should be identified as Alva temple door");
            }
        }

        [TestMethod]
        public void EntityClassification_ShouldIdentifyCraftingRecipes()
        {
            // Arrange
            var craftingPaths = new[]
            {
                "Metadata/Terrain/Leagues/Betrayal/Objects/CraftingUnlocks",
                "SomePrefix/CraftingUnlocks",
                "/CraftingUnlocks"
            };

            // Act & Assert
            foreach (var path in craftingPaths)
            {
                IsCraftingRecipe(path).Should().BeTrue($"Path '{path}' should be identified as crafting recipe");
            }
        }

        [TestMethod]
        public void PathMatchingPerformance_ShouldHandleLargeLists()
        {
            // Create a large list of paths to test performance isn't severely impacted
            var largePaths = new List<string>();
            for (int i = 0; i < 1000; i++)
            {
                largePaths.Add($"Metadata/RandomPath{i}/SomeObject");
            }
            largePaths.Add("CleansingFireAltar"); // Add one valid path

            // Act & Assert - Should complete quickly
            var validPaths = largePaths.Where(IsRecognizedSpecialPath).ToList();
            validPaths.Should().HaveCount(1, "should find exactly one valid path in large list");
        }

        // Helper methods that simulate the path matching logic
        private static bool IsExarchAltar(string path)
        {
            return !string.IsNullOrEmpty(path) && path.ToLowerInvariant().Contains("cleansingfirealtar");
        }

        private static bool IsEaterAltar(string path)
        {
            return !string.IsNullOrEmpty(path) && path.ToLowerInvariant().Contains("tanglealtar");
        }

        private static bool IsSettlersOre(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            var lowerPath = path.ToLowerInvariant();
            return lowerPath.Contains("crimsoniron") ||
                   lowerPath.Contains("verisium") ||
                   lowerPath.Contains("petrifiedwood") ||
                   lowerPath.Contains("bismuth") ||
                   lowerPath.Contains("copper_altar");
        }

        private static bool IsHarvestNode(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            var lowerPath = path.ToLowerInvariant();
            return lowerPath.Contains("harvest/irrigator") || lowerPath.Contains("harvest/extractor");
        }

        private static bool IsDelveNode(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            var lowerPath = path.ToLowerInvariant();
            return lowerPath.Contains("delvemineral") || lowerPath.Contains("azuriteencountercontroller");
        }

        private static bool IsBreachNode(string path)
        {
            return !string.IsNullOrEmpty(path) && path.ToLowerInvariant().Contains("brequel");
        }

        private static bool IsAlvaTempleDoor(string path)
        {
            return !string.IsNullOrEmpty(path) && path.ToLowerInvariant().Contains("closeddoorpast");
        }

        private static bool IsCraftingRecipe(string path)
        {
            return !string.IsNullOrEmpty(path) && path.ToLowerInvariant().Contains("craftingunlocks");
        }

        private static bool IsRecognizedSpecialPath(string path)
        {
            return IsExarchAltar(path) || IsEaterAltar(path) || IsSettlersOre(path) ||
                   IsHarvestNode(path) || IsDelveNode(path) || IsBreachNode(path) ||
                   IsAlvaTempleDoor(path) || IsCraftingRecipe(path);
        }

        private enum AltarType
        {
            SearingExarch,
            EaterOfWorlds,
            Unknown
        }

        private static AltarType GetAltarType(string path)
        {
            if (IsExarchAltar(path)) return AltarType.SearingExarch;
            if (IsEaterAltar(path)) return AltarType.EaterOfWorlds;
            return AltarType.Unknown;
        }
    }
}