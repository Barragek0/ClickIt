using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Numerics;

namespace ClickIt.Tests
{
    [TestClass]
    public class ScreenAreaCalculationTests
    {
        [TestMethod]
        public void PointInRectangle_ShouldReturnTrueForPointInside()
        {
            // Arrange
            var rectangle = new RectangleF(10, 10, 100, 100);
            var pointInside = new Vector2(50, 50);

            // Act
            bool result = PointInRectangle(pointInside, rectangle);

            // Assert
            result.Should().BeTrue("point (50,50) should be inside rectangle (10,10,100,100)");
        }

        [TestMethod]
        public void PointInRectangle_ShouldReturnFalseForPointOutside()
        {
            // Arrange
            var rectangle = new RectangleF(10, 10, 100, 100);
            var pointOutside = new Vector2(150, 150);

            // Act
            bool result = PointInRectangle(pointOutside, rectangle);

            // Assert
            result.Should().BeFalse("point (150,150) should be outside rectangle (10,10,100,100)");
        }

        [TestMethod]
        public void PointInRectangle_ShouldHandleBoundaryPoints()
        {
            // Arrange
            var rectangle = new RectangleF(0, 0, 100, 100);

            // Act & Assert
            PointInRectangle(new Vector2(0, 0), rectangle).Should().BeTrue("top-left corner should be inside");
            PointInRectangle(new Vector2(100, 100), rectangle).Should().BeTrue("bottom-right corner should be inside");
            PointInRectangle(new Vector2(50, 0), rectangle).Should().BeTrue("top edge should be inside");
            PointInRectangle(new Vector2(0, 50), rectangle).Should().BeTrue("left edge should be inside");
        }

        [TestMethod]
        public void PointInRectangle_ShouldHandleNegativeCoordinates()
        {
            // Arrange
            var rectangle = new RectangleF(-50, -50, 100, 100);

            // Act & Assert
            PointInRectangle(new Vector2(0, 0), rectangle).Should().BeTrue("origin should be inside centered rectangle");
            PointInRectangle(new Vector2(-25, -25), rectangle).Should().BeTrue("negative coordinates should work");
            PointInRectangle(new Vector2(-100, -100), rectangle).Should().BeFalse("point outside negative rectangle");
        }

        [TestMethod]
        public void ScreenAreaCalculation_ShouldCreateValidHealthFlaskArea()
        {
            // Arrange
            var windowRect = new RectangleF(0, 0, 1920, 1080);

            // Act
            var healthFlaskArea = CalculateHealthAndFlaskRectangle(windowRect);

            // Assert
            healthFlaskArea.X.Should().BeGreaterOrEqualTo(0, "health area should start at reasonable X position");
            healthFlaskArea.Y.Should().BeGreaterThan(windowRect.Height * 0.5f, "health area should be in bottom half");
            healthFlaskArea.Width.Should().BeGreaterThan(0, "health area should have positive width");
            healthFlaskArea.Height.Should().BeGreaterThan(0, "health area should have positive height");
        }

        [TestMethod]
        public void ScreenAreaCalculation_ShouldCreateValidManaSkillsArea()
        {
            // Arrange
            var windowRect = new RectangleF(0, 0, 1920, 1080);

            // Act
            var manaSkillsArea = CalculateManaAndSkillsRectangle(windowRect);

            // Assert
            manaSkillsArea.X.Should().BeGreaterThan(windowRect.Width * 0.5f, "mana area should be in right half");
            manaSkillsArea.Y.Should().BeGreaterThan(windowRect.Height * 0.5f, "mana area should be in bottom half");
            manaSkillsArea.Width.Should().BeGreaterThan(0, "mana area should have positive width");
            manaSkillsArea.Height.Should().BeGreaterThan(0, "mana area should have positive height");
        }

        [TestMethod]
        public void ScreenAreaCalculation_ShouldCreateValidBuffsDebuffsArea()
        {
            // Arrange
            var windowRect = new RectangleF(0, 0, 1920, 1080);

            // Act
            var buffsDebuffsArea = CalculateBuffsAndDebuffsRectangle(windowRect);

            // Assert
            buffsDebuffsArea.X.Should().Be(windowRect.X, "buffs area should start at window left edge");
            buffsDebuffsArea.Y.Should().Be(windowRect.Y, "buffs area should start at window top edge");
            buffsDebuffsArea.Width.Should().BeApproximately(windowRect.Width / 2, 50, "buffs area should cover left half");
            buffsDebuffsArea.Height.Should().Be(120, "buffs area should have fixed height");
        }

        [TestMethod]
        public void PointIsInClickableArea_ShouldAllowPointsInGameplayArea()
        {
            // Arrange
            var windowRect = new RectangleF(0, 0, 1920, 1080);
            var centerPoint = new Vector2(960, 540); // Center of screen

            // Act
            bool result = PointIsInClickableArea(centerPoint, windowRect);

            // Assert
            result.Should().BeTrue("center of screen should be in clickable area");
        }

        [TestMethod]
        public void PointIsInClickableArea_ShouldBlockHealthFlaskArea()
        {
            // Arrange
            var windowRect = new RectangleF(0, 0, 1920, 1080);
            var healthAreaPoint = new Vector2(800, 950); // Inside calculated health/flask area

            // Act
            bool result = PointIsInClickableArea(healthAreaPoint, windowRect);

            // Assert
            result.Should().BeFalse("points in health/flask area should not be clickable");
        }

        [TestMethod]
        public void PointIsInClickableArea_ShouldBlockManaSkillsArea()
        {
            // Arrange
            var windowRect = new RectangleF(0, 0, 1920, 1080);
            var manaAreaPoint = new Vector2(1720, 950); // Likely in mana/skills area

            // Act
            bool result = PointIsInClickableArea(manaAreaPoint, windowRect);

            // Assert
            result.Should().BeFalse("points in mana/skills area should not be clickable");
        }

        [TestMethod]
        public void PointIsInClickableArea_ShouldBlockBuffsDebuffsArea()
        {
            // Arrange
            var windowRect = new RectangleF(0, 0, 1920, 1080);
            var buffsAreaPoint = new Vector2(100, 60); // Likely in buffs/debuffs area

            // Act
            bool result = PointIsInClickableArea(buffsAreaPoint, windowRect);

            // Assert
            result.Should().BeFalse("points in buffs/debuffs area should not be clickable");
        }

        [TestMethod]
        public void PointIsInClickableArea_ShouldRejectPointsOutsideWindow()
        {
            // Arrange
            var windowRect = new RectangleF(0, 0, 1920, 1080);
            var outsidePoint = new Vector2(2000, 1200); // Outside window

            // Act
            bool result = PointIsInClickableArea(outsidePoint, windowRect);

            // Assert
            result.Should().BeFalse("points outside window should not be clickable");
        }

        [TestMethod]
        public void ScreenAreaCalculation_ShouldHandleDifferentResolutions()
        {
            // Test common resolutions
            var resolutions = new[]
            {
                new RectangleF(0, 0, 1920, 1080), // 1080p
                new RectangleF(0, 0, 2560, 1440), // 1440p  
                new RectangleF(0, 0, 3840, 2160), // 4K
                new RectangleF(0, 0, 1366, 768),  // Common laptop
                new RectangleF(0, 0, 1280, 720)   // 720p
            };

            foreach (var resolution in resolutions)
            {
                // Act
                var healthArea = CalculateHealthAndFlaskRectangle(resolution);
                var manaArea = CalculateManaAndSkillsRectangle(resolution);
                var buffsArea = CalculateBuffsAndDebuffsRectangle(resolution);

                // Assert
                healthArea.Width.Should().BeGreaterThan(0, $"Health area should be valid for resolution {resolution.Width}x{resolution.Height}");
                manaArea.Width.Should().BeGreaterThan(0, $"Mana area should be valid for resolution {resolution.Width}x{resolution.Height}");
                buffsArea.Width.Should().BeGreaterThan(0, $"Buffs area should be valid for resolution {resolution.Width}x{resolution.Height}");
            }
        }

        [TestMethod]
        public void ScreenAreaCalculation_ShouldHandleWindowOffset()
        {
            // Arrange - Window not at origin
            var offsetWindow = new RectangleF(100, 50, 1920, 1080);

            // Act
            var healthArea = CalculateHealthAndFlaskRectangle(offsetWindow);
            var manaArea = CalculateManaAndSkillsRectangle(offsetWindow);
            var buffsArea = CalculateBuffsAndDebuffsRectangle(offsetWindow);

            // Assert
            healthArea.X.Should().BeGreaterOrEqualTo(offsetWindow.X, "health area should respect window offset");
            manaArea.X.Should().BeGreaterOrEqualTo(offsetWindow.X, "mana area should respect window offset");
            buffsArea.X.Should().Be(offsetWindow.X, "buffs area should start at window X offset");
            buffsArea.Y.Should().Be(offsetWindow.Y, "buffs area should start at window Y offset");
        }

        // Helper methods that simulate the area calculation logic
        private static bool PointInRectangle(Vector2 point, RectangleF rectangle)
        {
            return point.X >= rectangle.X && point.X <= rectangle.X + rectangle.Width &&
                   point.Y >= rectangle.Y && point.Y <= rectangle.Y + rectangle.Height;
        }

        private static RectangleF CalculateHealthAndFlaskRectangle(RectangleF windowRect)
        {
            return new RectangleF(
                windowRect.X + (windowRect.Width / 3),
                windowRect.Y + (windowRect.Height / 5 * 3.92f),
                windowRect.Width / 3.4f,
                windowRect.Height - (windowRect.Height / 5 * 3.92f)
            );
        }

        private static RectangleF CalculateManaAndSkillsRectangle(RectangleF windowRect)
        {
            return new RectangleF(
                windowRect.X + (windowRect.Width / 3 * 2.12f),
                windowRect.Y + (windowRect.Height / 5 * 3.92f),
                windowRect.Width - (windowRect.Width / 3 * 2.12f),
                windowRect.Height - (windowRect.Height / 5 * 3.92f)
            );
        }

        private static RectangleF CalculateBuffsAndDebuffsRectangle(RectangleF windowRect)
        {
            return new RectangleF(
                windowRect.X,
                windowRect.Y,
                windowRect.Width / 2,
                120
            );
        }

        private static bool PointIsInClickableArea(Vector2 point, RectangleF windowRect)
        {
            if (!PointInRectangle(point, windowRect))
                return false;

            var healthArea = CalculateHealthAndFlaskRectangle(windowRect);
            var manaArea = CalculateManaAndSkillsRectangle(windowRect);
            var buffsArea = CalculateBuffsAndDebuffsRectangle(windowRect);

            return !PointInRectangle(point, healthArea) &&
                   !PointInRectangle(point, manaArea) &&
                   !PointInRectangle(point, buffsArea);
        }

        // Simple RectangleF struct for testing
        private struct RectangleF
        {
            public float X { get; }
            public float Y { get; }
            public float Width { get; }
            public float Height { get; }

            public RectangleF(float x, float y, float width, float height)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }
        }
    }
}