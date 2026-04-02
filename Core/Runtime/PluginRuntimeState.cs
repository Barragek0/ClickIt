using System.Diagnostics;
using ExileCore.Shared;

namespace ClickIt.Core.Runtime
{
    internal sealed class PluginRuntimeState
    {
        public Coroutine? AltarCoroutine { get; set; }
        public Coroutine? ClickLabelCoroutine { get; set; }
        public Coroutine? ManualUiHoverCoroutine { get; set; }
        public Coroutine? DelveFlareCoroutine { get; set; }
        public Coroutine? DeepMemoryDumpCoroutine { get; set; }
        public Stopwatch LastRenderTimer { get; } = new();
        public Stopwatch LastTickTimer { get; } = new();
        public Stopwatch Timer { get; } = new();
        public Stopwatch SecondTimer { get; } = new();
        public bool LastHotkeyState { get; set; }
        public bool WorkFinished { get; set; }
        public bool IsShuttingDown { get; set; }
    }
}