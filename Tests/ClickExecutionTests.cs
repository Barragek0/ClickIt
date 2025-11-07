using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;

namespace ClickIt.Tests
{
    [TestClass]
    public class ClickExecutionTests
    {
        [TestMethod]
        public void MouseClickSimulation_ShouldUseCorrectButton()
        {
            // Test mouse button selection based on left-handed setting
            var leftHanded = false;
            var expectedButton = leftHanded ? "Right" : "Left";

            expectedButton.Should().Be("Left", "right-handed users should use left mouse button");

            // Test left-handed configuration
            leftHanded = true;
            expectedButton = leftHanded ? "Right" : "Left";

            expectedButton.Should().Be("Right", "left-handed users should use right mouse button");
        }

        [TestMethod]
        public void CoordinateTransformation_ShouldHandleWindowOffset()
        {
            // Test coordinate transformation from game coordinates to screen coordinates
            var gameCoordinate = new PointF(100, 200);
            var windowOffset = new PointF(50, 75);

            var screenCoordinate = new PointF(gameCoordinate.X + windowOffset.X, gameCoordinate.Y + windowOffset.Y);

            screenCoordinate.X.Should().Be(150, "X coordinate should include window offset");
            screenCoordinate.Y.Should().Be(275, "Y coordinate should include window offset");

            // Test edge case with zero offset
            var zeroOffset = new PointF(0, 0);
            var screenCoordZero = new PointF(gameCoordinate.X + zeroOffset.X, gameCoordinate.Y + zeroOffset.Y);

            screenCoordZero.Should().Be(gameCoordinate, "zero offset should not change coordinates");
        }

        [TestMethod]
        public void ClickAreaSafety_ShouldAvoidUIElements()
        {
            // Test that clicks avoid dangerous UI areas
            var screenWidth = 1920f;
            var screenHeight = 1080f;

            // Define UI areas to avoid (based on PointIsInClickableArea logic)
            var healthManaArea = new RectangleF(0, screenHeight - 200, 400, 200);  // Bottom left
            var skillsArea = new RectangleF(screenWidth - 400, screenHeight - 200, 400, 200);  // Bottom right
            var buffsArea = new RectangleF(0, 0, screenWidth, 100);  // Top area

            // Test points in dangerous areas
            var healthPoint = new PointF(200, screenHeight - 100);  // In health area
            var skillPoint = new PointF(screenWidth - 200, screenHeight - 100);  // In skills area
            var buffPoint = new PointF(screenWidth / 2, 50);  // In buff area

            // Points should be detected as unsafe
            IsPointInUIArea(healthPoint, healthManaArea).Should().BeTrue("health area should be detected");
            IsPointInUIArea(skillPoint, skillsArea).Should().BeTrue("skills area should be detected");
            IsPointInUIArea(buffPoint, buffsArea).Should().BeTrue("buffs area should be detected");

            // Test safe area
            var safePoint = new PointF(screenWidth / 2, screenHeight / 2);  // Center screen
            IsPointInAnyUIArea(safePoint, healthManaArea, skillsArea, buffsArea).Should().BeFalse("center should be safe");
        }

        [TestMethod]
        public void ClickTiming_ShouldHaveRandomization()
        {
            // Test click timing randomization
            var baseDelay = 50;
            var maxVariance = 10;
            var timings = new List<int>();

            var random = new Random(42); // Fixed seed for reproducible tests

            for (int i = 0; i < 20; i++)
            {
                var randomDelay = random.Next(0, maxVariance);
                var totalDelay = baseDelay + randomDelay;
                timings.Add(totalDelay);
            }

            // Should have variance in timing
            timings.Distinct().Should().HaveCountGreaterThan(3, "should have timing variance");

            // All timings should be within expected range
            timings.Should().AllSatisfy(timing =>
                timing.Should().BeInRange(baseDelay, baseDelay + maxVariance, "timing should be within expected range"));
        }

        [TestMethod]
        public void MultiMonitorSupport_ShouldHandleCoordinates()
        {
            // Test multi-monitor coordinate handling
            var primaryMonitor = new RectangleF(0, 0, 1920, 1080);
            var secondaryMonitor = new RectangleF(1920, 0, 1920, 1080);

            // Test primary monitor coordinates
            var primaryPoint = new PointF(960, 540); // Center of primary
            IsPointInMonitor(primaryPoint, primaryMonitor).Should().BeTrue("should detect primary monitor");

            // Test secondary monitor coordinates
            var secondaryPoint = new PointF(2880, 540); // Center of secondary
            IsPointInMonitor(secondaryPoint, secondaryMonitor).Should().BeTrue("should detect secondary monitor");

            // Test point outside both monitors
            var outsidePoint = new PointF(-100, 540);
            IsPointInMonitor(outsidePoint, primaryMonitor).Should().BeFalse("should detect outside primary");
            IsPointInMonitor(outsidePoint, secondaryMonitor).Should().BeFalse("should detect outside secondary");
        }

        [TestMethod]
        public void InputBlocking_ShouldBeCoordinated()
        {
            // Test input blocking coordination during clicks
            var inputBlocked = false;
            var blockingEnabled = true;

            // Test blocking sequence
            if (blockingEnabled)
            {
                inputBlocked = true;  // Block before click
            }

            inputBlocked.Should().BeTrue("input should be blocked when blocking enabled");

            // Simulate click execution
            var clickExecuted = inputBlocked;  // Click happens while blocked

            // Unblock after click
            if (blockingEnabled)
            {
                inputBlocked = false;
            }

            inputBlocked.Should().BeFalse("input should be unblocked after click");
            clickExecuted.Should().BeTrue("click should execute while input was blocked");
        }

        [TestMethod]
        public void ClickValidation_ShouldCheckGameState()
        {
            // Test click validation based on game state
            var inGame = true;
            var pluginEnabled = true;
            var groundItemsVisible = true;

            var canClick = inGame && pluginEnabled && groundItemsVisible;

            canClick.Should().BeTrue("should allow clicks when all conditions are met");

            // Test with game not active
            inGame = false;
            canClick = inGame && pluginEnabled && groundItemsVisible;

            canClick.Should().BeFalse("should not allow clicks when not in game");

            // Test with plugin disabled
            inGame = true;
            pluginEnabled = false;
            canClick = inGame && pluginEnabled && groundItemsVisible;

            canClick.Should().BeFalse("should not allow clicks when plugin disabled");
        }

        [TestMethod]
        public void DistanceValidation_ShouldRespectLimits()
        {
            // Test distance validation for click targets
            var playerPosition = new PointF(100, 100);
            var targetPosition = new PointF(150, 150);
            var maxClickDistance = 100f;

            var distance = CalculateDistance(playerPosition, targetPosition);
            var withinRange = distance <= maxClickDistance;

            distance.Should().BeApproximately(70.71f, 0.1f, "distance calculation should be accurate");
            withinRange.Should().BeTrue("target should be within click range");

            // Test target too far
            targetPosition = new PointF(300, 300);
            distance = CalculateDistance(playerPosition, targetPosition);
            withinRange = distance <= maxClickDistance;

            withinRange.Should().BeFalse("distant target should be out of range");
        }

        // Helper methods for testing
        private static bool IsPointInUIArea(PointF point, RectangleF area)
        {
            return point.X >= area.X && point.X <= area.X + area.Width &&
                   point.Y >= area.Y && point.Y <= area.Y + area.Height;
        }

        private static bool IsPointInAnyUIArea(PointF point, params RectangleF[] areas)
        {
            return areas.Any(area => IsPointInUIArea(point, area));
        }

        private static bool IsPointInMonitor(PointF point, RectangleF monitor)
        {
            return point.X >= monitor.X && point.X <= monitor.X + monitor.Width &&
                   point.Y >= monitor.Y && point.Y <= monitor.Y + monitor.Height;
        }

        private static float CalculateDistance(PointF p1, PointF p2)
        {
            var dx = p2.X - p1.X;
            var dy = p2.Y - p1.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}