namespace ClickIt.Core.Runtime;

// The plugin's coroutine names, without a "ClickIt." prefix — ExileCore's runner already groups coroutines under the owning plugin, so the prefix is redundant in its coroutine list UI. Shutdown and hotkey re-attach look coroutines up by these exact names.
internal static class PluginCoroutineNames
{
    internal const string AltarScan = "Scan Altars";
    internal const string BlockedUiRefresh = "Blocked UI";
    internal const string ClickLogic = "Click";
    internal const string ManualUiHover = "Manual UI Hover";
    internal const string DelveFlare = "Flare";
    internal const string GameStateDump = "Game State Dump";

    // Overlay refresh coroutines are stopped by the OverlayRenderHost, so they don't participate in the named shutdown scan. Each keeps a unique name — ExileCore's runner keys coroutine scheduling by name, so sharing one name across overlays stops all but the first from running.
    internal static string OverlayRefresh(string overlayName) => $"{overlayName} Overlay";

    internal static bool IsTrackedName(string? name)
        => name is AltarScan or BlockedUiRefresh or ClickLogic or ManualUiHover or DelveFlare or GameStateDump;
}
