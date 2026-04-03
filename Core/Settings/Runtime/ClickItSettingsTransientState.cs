namespace ClickIt.Core.Settings.Runtime
{
    internal sealed class ClickItSettingsTransientState
    {
        internal ClickItSettingsUiState UiState { get; } = new();
        internal ClickItSettingsRuntimeCacheState RuntimeCache { get; } = new();
        internal bool MemoryDumpInProgress { get; set; }
        internal int MemoryDumpProgressPercent { get; set; }
        internal bool MemoryDumpLastRunSucceeded { get; set; }
        internal string MemoryDumpStatusText { get; set; } = string.Empty;
        internal string MemoryDumpOutputPath { get; set; } = string.Empty;
    }
}