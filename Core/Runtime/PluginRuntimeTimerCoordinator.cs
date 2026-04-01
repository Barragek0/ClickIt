using System.Diagnostics;

namespace ClickIt.Core.Runtime
{
    internal static class PluginRuntimeTimerCoordinator
    {
        internal static void StartAll(Stopwatch lastRenderTimer, Stopwatch lastTickTimer, Stopwatch timer, Stopwatch secondTimer)
        {
            lastRenderTimer?.Start();
            lastTickTimer?.Start();
            timer?.Start();
            secondTimer?.Start();
        }

        internal static void StopAll(Stopwatch lastRenderTimer, Stopwatch lastTickTimer, Stopwatch timer, Stopwatch secondTimer)
        {
            lastRenderTimer?.Stop();
            lastTickTimer?.Stop();
            timer?.Stop();
            secondTimer?.Stop();
        }
    }
}