using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ClickIt.Tests
{
    // Simple key enum used by integration scenarios
    public enum MockKeys { None = 0, F1 = 1, F2 = 2 }

    public class MockSettingsUpdate
    {
        public string PropertyName { get; set; }
        public object OldValue { get; set; }
        public object NewValue { get; set; }
    }

    public class MockSettingsUpdateHandler
    {
        public List<MockSettingsUpdate> UpdatesReceived { get; } = new List<MockSettingsUpdate>();
        public void OnUpdate(MockSettingsUpdate update) => UpdatesReceived.Add(update);
    }

    public class MockSettingsPersistence
    {
        private MockClickItSettings _stored;
        public void Save(MockClickItSettings settings)
        {
            // simple shallow copy for tests
            _stored = new MockClickItSettings();
            foreach (var kv in settings.ModWeights)
                _stored.ModWeights[kv.Key] = kv.Value;
            _stored.ClickDistance = settings.ClickDistance;
            _stored.CorruptAllEssences = settings.CorruptAllEssences;
            _stored.DebugMode = settings.DebugMode;
            _stored.ClickLabelKey = settings.ClickLabelKey;
            _stored.LazyModeDisableKey = settings.LazyModeDisableKey;
        }

        public void Load(MockClickItSettings settings)
        {
            if (_stored == null) return;
            settings.ClickDistance = _stored.ClickDistance;
            settings.CorruptAllEssences = _stored.CorruptAllEssences;
            settings.DebugMode = _stored.DebugMode;
            settings.ClickLabelKey = _stored.ClickLabelKey;
            settings.LazyModeDisableKey = _stored.LazyModeDisableKey;
            settings.ModWeights.Clear();
            foreach (var kv in _stored.ModWeights)
                settings.ModWeights[kv.Key] = kv.Value;
        }
    }

    public class MockSettingsValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
    }

    public class MockSettingsValidator
    {
        public MockSettingsValidationResult ValidateSettings(MockClickItSettings settings)
        {
            var result = new MockSettingsValidationResult { IsValid = true };
            if (settings.ClickDistance < 0)
            {
                result.IsValid = false;
                result.Errors.Add("ClickDistance must be non-negative");
            }
            if (settings.ClickLabelKey == MockKeys.None)
            {
                result.IsValid = false;
                result.Errors.Add("ClickLabelKey is not set");
            }
            foreach (var kv in settings.ModWeights)
            {
                if (kv.Value < 0 || kv.Value > 100)
                {
                    result.IsValid = false;
                    result.Errors.Add($"weight for {kv.Key} must be between 0 and 100");
                }
            }
            return result;
        }
    }

    public class AsyncOperation
    {
        public string Name { get; }
        public Task CompletionTask { get; private set; }
        public bool CompletedSuccessfully { get; private set; }

        public AsyncOperation(string name)
        {
            Name = name;
            CompletionTask = Task.CompletedTask;
            CompletedSuccessfully = true;
        }

        public void Start()
        {
            // no-op quick completion
            CompletionTask = Task.CompletedTask;
            CompletedSuccessfully = true;
        }
    }

    public class PerformanceMetrics
    {
        public double AverageFrameTime { get; set; }
        public double MemoryUsageMB { get; set; }
        public double CpuUsagePercent { get; set; }
        public int GarbageCollections { get; set; }
    }

    public class PerformanceMonitor
    {
        public void StartMonitoring() { }
        public PerformanceMetrics StopAndGetMetrics()
        {
            return new PerformanceMetrics { AverageFrameTime = 10, MemoryUsageMB = 50, CpuUsagePercent = 10, GarbageCollections = 1 };
        }
    }

    // Minimal mock of the ClickIt plugin instance used in end-to-end tests
    public class MockClickItInstance
    {
        public bool IsDisposed { get; private set; }
        public int ActiveCoroutinesCount { get; private set; }

        public bool ProcessInputState(object input)
        {
            return true;
        }

        public bool InitializePlugin()
        {
            IsDisposed = false;
            ActiveCoroutinesCount = 0;
            return true;
        }

        public void PerformWorkCycle()
        {
            // simulate some work
            ActiveCoroutinesCount = Math.Max(0, ActiveCoroutinesCount - 1);
        }

        public void CleanupPlugin()
        {
            IsDisposed = true;
            ActiveCoroutinesCount = 0;
        }

        public MockState GetCurrentState()
        {
            return new MockState { IsConsistent = true };
        }

        public void ExecuteLoadTest(object loadTest)
        {
            // no-op
        }
    }

    public class MockState
    {
        public bool IsConsistent { get; set; }
    }

    public class MockWorkflowResult
    {
        public bool IsValid { get; set; }
        public string DecisionReasoning { get; set; }
        public bool ClickShouldProceed { get; set; }
    }

    public static class IntegrationHelpers
    {
        public static MockClickItInstance CreateClickItInstance() => new MockClickItInstance();
        public static object CreateInputState(bool hotkeyPressed, int timestamp) => new { HotkeyPressed = hotkeyPressed, Timestamp = timestamp };
        public static MockAltarComponent CreateComplexAltarScenario() => TestFactories.CreateComplexTestAltarComponent();
        public static MockWorkflowResult SimulateCompleteWorkflow(object altarComponent) => new MockWorkflowResult { IsValid = true, DecisionReasoning = "ok", ClickShouldProceed = true };
        public static object CreateHighLoadScenario() => new { Items = new int[1000] };
        public static bool ProcessHighLoadElements(object scenario) => true;
        public static object CreateLoadTestScenario() => new { Intensity = 1 };
    }
}
