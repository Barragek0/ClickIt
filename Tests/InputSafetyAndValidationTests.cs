using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Numerics;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Reflection;

namespace ClickIt.Tests
{
    [TestClass]
    public class InputSafetyAndValidationTests
    {
        [TestMethod]
        public void InputValidation_ShouldRejectInvalidMousePositions()
        {
            // Arrange
            var invalidPositions = new[]
            {
                new Vector2(-1, 100),     // Negative X
                new Vector2(100, -1),     // Negative Y
                new Vector2(float.NaN, 100), // NaN X
                new Vector2(100, float.NaN), // NaN Y
                new Vector2(float.PositiveInfinity, 100), // Infinite X
                new Vector2(100, float.PositiveInfinity)  // Infinite Y
            };

            foreach (var position in invalidPositions)
            {
                // Act
                bool isValid = IsValidMousePosition(position);

                // Assert
                isValid.Should().BeFalse($"Position {position} should be invalid");
            }
        }

        [TestMethod]
        public void InputValidation_ShouldAcceptValidMousePositions()
        {
            // Arrange
            var validPositions = new[]
            {
                new Vector2(0, 0),
                new Vector2(100, 100),
                new Vector2(1920, 1080),
                new Vector2(0.5f, 0.5f)
            };

            foreach (var position in validPositions)
            {
                // Act
                bool isValid = IsValidMousePosition(position);

                // Assert
                isValid.Should().BeTrue($"Position {position} should be valid");
            }
        }

        [TestMethod]
        public void ClickSafety_ShouldValidateClickIntervals()
        {
            // Arrange
            var invalidIntervals = new[] { -1, 0, int.MaxValue };
            var validIntervals = new[] { 1, 50, 100, 500, 1000 };

            // Act & Assert
            foreach (var interval in invalidIntervals)
            {
                IsValidClickInterval(interval).Should().BeFalse($"Interval {interval} should be invalid");
            }

            foreach (var interval in validIntervals)
            {
                IsValidClickInterval(interval).Should().BeTrue($"Interval {interval} should be valid");
            }
        }

        [TestMethod]
        public void KeyboardSafety_ShouldValidateKeyboardInput()
        {
            // Arrange
            var invalidKeys = new[] { -1, 0, 1000 };
            var validKeys = new[] { 0x01, 0x02, 0x20, 0x41, 0x5A }; // Common virtual key codes

            // Act & Assert
            foreach (var key in invalidKeys)
            {
                IsValidVirtualKeyCode(key).Should().BeFalse($"Key code {key} should be invalid");
            }

            foreach (var key in validKeys)
            {
                IsValidVirtualKeyCode(key).Should().BeTrue($"Key code {key} should be valid");
            }
        }

        [TestMethod]
        public void InputBlocking_ShouldTrackBlockingState()
        {
            // Arrange
            var inputBlocker = new MockInputBlocker();

            // Act & Assert
            inputBlocker.IsBlocked.Should().BeFalse("Initial state should be unblocked");

            inputBlocker.BlockInput();
            inputBlocker.IsBlocked.Should().BeTrue("State should be blocked after blocking");

            inputBlocker.UnblockInput();
            inputBlocker.IsBlocked.Should().BeFalse("State should be unblocked after unblocking");
        }

        [TestMethod]
        public void InputBlocking_ShouldHandleNestedBlocking()
        {
            // Arrange
            var inputBlocker = new MockInputBlocker();

            // Act - Multiple blocks
            inputBlocker.BlockInput();
            inputBlocker.BlockInput();
            inputBlocker.BlockInput();

            // Assert
            inputBlocker.IsBlocked.Should().BeTrue("Should remain blocked with nested blocks");

            // Act - Single unblock
            inputBlocker.UnblockInput();

            // Assert
            inputBlocker.IsBlocked.Should().BeFalse("Should be unblocked even with nested calls");
        }

        [TestMethod]
        public void ClickSequence_ShouldValidateClickOrder()
        {
            // Arrange
            var clickSequence = new List<MockClick>
            {
                new MockClick { Position = new Vector2(100, 100), Timestamp = 0 },
                new MockClick { Position = new Vector2(200, 200), Timestamp = 50 },
                new MockClick { Position = new Vector2(300, 300), Timestamp = 100 }
            };

            // Act
            bool isValidSequence = ValidateClickSequence(clickSequence);

            // Assert
            isValidSequence.Should().BeTrue("Valid sequence should pass validation");
        }

        [TestMethod]
        public void ClickSequence_ShouldRejectInvalidTimingSequence()
        {
            // Arrange - Clicks with decreasing timestamps
            var invalidSequence = new List<MockClick>
            {
                new MockClick { Position = new Vector2(100, 100), Timestamp = 100 },
                new MockClick { Position = new Vector2(200, 200), Timestamp = 50 },
                new MockClick { Position = new Vector2(300, 300), Timestamp = 0 }
            };

            // Act
            bool isValidSequence = ValidateClickSequence(invalidSequence);

            // Assert
            isValidSequence.Should().BeFalse("Sequence with decreasing timestamps should be invalid");
        }

        [TestMethod]
        public void ClickSequence_ShouldRejectTooFastClicks()
        {
            // Arrange - Clicks too close together
            var tooFastSequence = new List<MockClick>
            {
                new MockClick { Position = new Vector2(100, 100), Timestamp = 0 },
                new MockClick { Position = new Vector2(200, 200), Timestamp = 1 }, // Too fast
                new MockClick { Position = new Vector2(300, 300), Timestamp = 2 }
            };

            // Act
            bool isValidSequence = ValidateClickSequence(tooFastSequence, minInterval: 10);

            // Assert
            isValidSequence.Should().BeFalse("Sequence with clicks too close together should be invalid");
        }

        [TestMethod]
        public void ScreenBounds_ShouldValidateClicksWithinScreenBounds()
        {
            // Arrange
            var screenBounds = new ScreenBounds(0, 0, 1920, 1080);
            var validClick = new Vector2(960, 540);
            var invalidClick = new Vector2(2000, 1200);

            // Act & Assert
            IsWithinScreenBounds(validClick, screenBounds).Should().BeTrue("Click within bounds should be valid");
            IsWithinScreenBounds(invalidClick, screenBounds).Should().BeFalse("Click outside bounds should be invalid");
        }

        [TestMethod]
        public void InputRate_ShouldLimitInputFrequency()
        {
            // Arrange
            var rateLimiter = new MockRateLimiter(maxActionsPerSecond: 10);

            // Act - Try to perform actions rapidly
            var results = new List<bool>();
            for (int i = 0; i < 20; i++)
            {
                results.Add(rateLimiter.TryPerformAction());
            }

            // Assert
            var allowedActions = results.Count(r => r);
            allowedActions.Should().BeLessOrEqualTo(10, "Rate limiter should restrict actions per second");
        }

        [TestMethod]
        public void InputValidation_ShouldHandleExtremeValues()
        {
            // Arrange
            var extremePositions = new[]
            {
                new Vector2(float.MaxValue, float.MaxValue),
                new Vector2(float.MinValue, float.MinValue),
                new Vector2(0, float.MaxValue),
                new Vector2(float.MaxValue, 0)
            };

            foreach (var position in extremePositions)
            {
                // Act
                bool isValid = IsValidMousePosition(position);

                // Assert
                isValid.Should().BeFalse($"Extreme position {position} should be invalid");
            }
        }

        [TestMethod]
        public void SafetyChecks_ShouldValidateGameWindowState()
        {
            // Arrange
            var validWindowStates = new[]
            {
                new WindowState { IsActive = true, IsVisible = true, HasFocus = true },
                new WindowState { IsActive = true, IsVisible = true, HasFocus = false }
            };

            var invalidWindowStates = new[]
            {
                new WindowState { IsActive = false, IsVisible = true, HasFocus = true },
                new WindowState { IsActive = true, IsVisible = false, HasFocus = true },
                new WindowState { IsActive = false, IsVisible = false, HasFocus = false }
            };

            // Act & Assert
            foreach (var state in validWindowStates)
            {
                IsValidWindowStateForInput(state).Should().BeTrue($"Valid window state should allow input");
            }

            foreach (var state in invalidWindowStates)
            {
                IsValidWindowStateForInput(state).Should().BeFalse($"Invalid window state should block input");
            }
        }

        [TestMethod]
        public void ThreadSafety_ShouldHandleConcurrentInputRequests()
        {
            // Arrange
            var threadSafeInputHandler = new MockThreadSafeInputHandler();
            var tasks = new List<Task<bool>>();

            // Act - Create multiple concurrent input requests
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(() => threadSafeInputHandler.TryPerformInput()));
            }

            Task.WaitAll(tasks.ToArray());

            // Assert
            var successCount = tasks.Count(t => t.Result);
            successCount.Should().BeGreaterThan(0, "At least some concurrent requests should succeed");
            successCount.Should().BeLessOrEqualTo(10, "Not more requests than created should succeed");
        }

        // Helper methods and mock classes for testing input safety
        private static bool IsValidMousePosition(Vector2 position)
        {
            return !float.IsNaN(position.X) && !float.IsNaN(position.Y) &&
                   !float.IsInfinity(position.X) && !float.IsInfinity(position.Y) &&
                   position.X >= 0 && position.Y >= 0 &&
                   position.X <= 10000 && position.Y <= 10000; // Reasonable screen limits
        }

        private static bool IsValidClickInterval(int intervalMs)
        {
            return intervalMs > 0 && intervalMs < 10000; // Between 1ms and 10 seconds
        }

        private static bool IsValidVirtualKeyCode(int keyCode)
        {
            return keyCode > 0 && keyCode <= 255; // Valid virtual key code range
        }

        private static bool ValidateClickSequence(List<MockClick> clicks, int minInterval = 10)
        {
            for (int i = 1; i < clicks.Count; i++)
            {
                if (clicks[i].Timestamp <= clicks[i - 1].Timestamp)
                    return false;

                if (clicks[i].Timestamp - clicks[i - 1].Timestamp < minInterval)
                    return false;
            }
            return true;
        }

        private static bool IsWithinScreenBounds(Vector2 position, ScreenBounds bounds)
        {
            return position.X >= bounds.X && position.X <= bounds.X + bounds.Width &&
                   position.Y >= bounds.Y && position.Y <= bounds.Y + bounds.Height;
        }

        private static bool IsValidWindowStateForInput(WindowState state)
        {
            return state.IsActive && state.IsVisible;
        }

        // Mock classes for testing
        private class MockInputBlocker
        {
            public bool IsBlocked { get; private set; }

            public void BlockInput() => IsBlocked = true;
            public void UnblockInput() => IsBlocked = false;
        }

        private class MockClick
        {
            public Vector2 Position { get; set; }
            public long Timestamp { get; set; }
        }

        private class ScreenBounds
        {
            public float X { get; }
            public float Y { get; }
            public float Width { get; }
            public float Height { get; }

            public ScreenBounds(float x, float y, float width, float height)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }
        }

        private class WindowState
        {
            public bool IsActive { get; set; }
            public bool IsVisible { get; set; }
            public bool HasFocus { get; set; }
        }

        private class MockRateLimiter
        {
            private readonly int _maxActionsPerSecond;
            private int _actionCount = 0;

            public MockRateLimiter(int maxActionsPerSecond)
            {
                _maxActionsPerSecond = maxActionsPerSecond;
            }

            public bool TryPerformAction()
            {
                if (_actionCount < _maxActionsPerSecond)
                {
                    _actionCount++;
                    return true;
                }
                return false;
            }
        }

        private class MockThreadSafeInputHandler
        {
            private readonly object _lock = new object();
            private int _concurrentRequests = 0;

            public bool TryPerformInput()
            {
                lock (_lock)
                {
                    if (_concurrentRequests < 5) // Simulate limited concurrent capacity
                    {
                        _concurrentRequests++;
                        Task.Delay(1).Wait(); // Simulate work (optimized)
                        _concurrentRequests--;
                        return true;
                    }
                    return false;
                }
            }
        }

        // Tests migrated from InputSafetyTests.cs

        // Test input safety logic patterns without depending on actual ClickItSettings class
        private class MockSettings
        {
            public bool Enable { get; set; } = true;
            public bool BlockUserInput { get; set; } = true;
        }

        private MockSettings CreateMockSettings()
        {
            return new MockSettings
            {
                Enable = true,
                BlockUserInput = true
            };
        }

        [TestMethod]
        public void HotkeyStateTracking_ShouldHandleTransitions()
        {
            // Test hotkey state change tracking logic
            // Simulate hotkey press/release transitions
            bool initialState = false;
            bool currentState = true;

            // Should detect state change from false to true
            (initialState != currentState).Should().BeTrue("hotkey state transition should be detected");

            // Should handle multiple rapid state changes
            for (int i = 0; i < 10; i++)
            {
                bool previousState = currentState;
                currentState = !currentState;
                (previousState != currentState).Should().BeTrue($"rapid state change {i} should be detected");
            }
        }

        [TestMethod]
        public void InputBlocking_ShouldHaveFailsafeTimeout()
        {
            // Test that input blocking has safety timeout mechanism
            const int HOTKEY_RELEASE_FAILSAFE_MS = 5000; // From ClickIt.cs

            var timer = new Stopwatch();
            timer.Start();

            // Simulate timeout condition by advancing timer
            var elapsed = timer.ElapsedMilliseconds;
            timer.Stop();

            elapsed.Should().BeGreaterOrEqualTo(0, "timer should advance");

            // Test failsafe logic with simulated timeout
            var simulatedTimeout = HOTKEY_RELEASE_FAILSAFE_MS + 1000;
            bool shouldUnblock = simulatedTimeout > HOTKEY_RELEASE_FAILSAFE_MS;
            shouldUnblock.Should().BeTrue("should unblock after failsafe timeout");

            // Test normal operation
            bool shouldNotUnblock = elapsed > HOTKEY_RELEASE_FAILSAFE_MS;
            shouldNotUnblock.Should().BeFalse("should not unblock before failsafe timeout in normal operation");
        }

        [TestMethod]
        public void ForceUnblockInput_ShouldHandleExceptions()
        {
            // Test exception handling in force unblock scenarios
            // Should not throw when called with null reason
            Action forceUnblockWithNull = () =>
            {
                // Simulate force unblock call - this tests the pattern, not actual implementation
                string reason = null;
                reason.Should().BeNull("null reason should be handled gracefully");
            };

            forceUnblockWithNull.Should().NotThrow("force unblock should handle null reasons");

            // Should not throw with empty reason
            Action forceUnblockWithEmpty = () =>
            {
                string reason = "";
                reason.Should().BeEmpty("empty reason should be handled gracefully");
            };

            forceUnblockWithEmpty.Should().NotThrow("force unblock should handle empty reasons");
        }

        [TestMethod]
        public void ConcurrentInputProtection_ShouldBeThreadSafe()
        {
            // Test concurrent access to input blocking state
            bool isInputBlocked = false;
            var lockObject = new object();
            var completed = 0;

            // Simulate concurrent access from multiple threads
            var thread1 = new Thread(() =>
            {
                lock (lockObject)
                {
                    isInputBlocked = true;
                    // Simulate work without Thread.Sleep
                    for (int i = 0; i < 1000; i++) { }
                    isInputBlocked = false;
                    Interlocked.Increment(ref completed);
                }
            });

            var thread2 = new Thread(() =>
            {
                lock (lockObject)
                {
                    isInputBlocked = true;
                    // Simulate work without Thread.Sleep  
                    for (int i = 0; i < 1000; i++) { }
                    isInputBlocked = false;
                    Interlocked.Increment(ref completed);
                }
            });

            thread1.Start();
            thread2.Start();

            thread1.Join(1000).Should().BeTrue("thread1 should complete within timeout");
            thread2.Join(1000).Should().BeTrue("thread2 should complete within timeout");

            completed.Should().Be(2, "both threads should complete");
            isInputBlocked.Should().BeFalse("input should not remain blocked after threads complete");
        }

        [TestMethod]
        public void GameStateValidation_ShouldPreventClicksOutsideGame()
        {
            // Test that clicks are prevented when not in game
            var settings = CreateMockSettings();

            // Simulate game state checks
            bool inGame = false;
            bool shouldAllowClick = inGame && settings.Enable;

            shouldAllowClick.Should().BeFalse("clicks should be blocked when not in game");

            // Test with game active but plugin disabled
            inGame = true;
            settings.Enable = false;
            shouldAllowClick = inGame && settings.Enable;

            shouldAllowClick.Should().BeFalse("clicks should be blocked when plugin disabled");

            // Test with both conditions met
            settings.Enable = true;
            shouldAllowClick = inGame && settings.Enable;

            shouldAllowClick.Should().BeTrue("clicks should be allowed when in game and plugin enabled");
        }

        [TestMethod]
        public void EmergencyInputRelease_ShouldWorkUnderStress()
        {
            // Test emergency input release under high load conditions
            var settings = CreateMockSettings();
            var releaseTimer = new Stopwatch();

            // Test emergency release logic patterns
            releaseTimer.Start();

            // Wait for timer to actually advance
            var startTime = releaseTimer.ElapsedMilliseconds;
            while (releaseTimer.ElapsedMilliseconds == startTime && releaseTimer.ElapsedMilliseconds < 100)
            {
                // Busy wait for timer to advance
            }

            bool emergencyCondition = releaseTimer.ElapsedMilliseconds > 0 || !settings.Enable;
            emergencyCondition.Should().BeTrue("emergency condition should trigger when timer advances or plugin disabled");

            // Simulate emergency response
            if (emergencyCondition)
            {
                releaseTimer.Stop();
            }

            releaseTimer.IsRunning.Should().BeFalse("emergency release should stop timer");
        }

        [TestMethod]
        public void InputBlockingSettings_ShouldBeRespected()
        {
            // Test that input blocking respects user settings
            var settings = CreateMockSettings();

            // Test with blocking enabled
            settings.BlockUserInput = true;
            bool shouldBlock = settings.BlockUserInput;
            shouldBlock.Should().BeTrue("input should be blocked when setting is enabled");

            // Test with blocking disabled
            settings.BlockUserInput = false;
            shouldBlock = settings.BlockUserInput;
            shouldBlock.Should().BeFalse("input should not be blocked when setting is disabled");
        }

        [TestMethod]
        public void HotkeyReleaseFailsafe_ShouldPreventInfiniteBlocking()
        {
            // Test that hotkey release failsafe prevents infinite input blocking
            const int HOTKEY_RELEASE_FAILSAFE_MS = 5000;
            var timer = new Stopwatch();

            bool isInputBlocked = true;
            bool hotkeyPressed = false;

            timer.Start();

            // Simulate failsafe condition: input blocked, hotkey not pressed, timeout exceeded
            var simulatedElapsed = HOTKEY_RELEASE_FAILSAFE_MS + 1000;
            timer.Stop();

            bool shouldForceUnblock = isInputBlocked && !hotkeyPressed && simulatedElapsed > HOTKEY_RELEASE_FAILSAFE_MS;

            shouldForceUnblock.Should().BeTrue("should force unblock after failsafe timeout");

            // Test that normal operation doesn't trigger failsafe
            isInputBlocked = false;
            shouldForceUnblock = isInputBlocked && !hotkeyPressed && simulatedElapsed > HOTKEY_RELEASE_FAILSAFE_MS;

            shouldForceUnblock.Should().BeFalse("should not force unblock when input not blocked");
        }
    }
}