namespace ClickIt.Shared.Rendering
{
    public enum OverlayRefreshMode
    {
        None = 0,
        Throttled = 1,
        DirtyTracked = 2
    }

    /// <summary>
    /// Declares how often an overlay's Refresh (coroutine) work runs.
    /// None = pure per-frame overlay (no coroutine). Throttled = fixed-interval snapshot.
    /// DirtyTracked = interval snapshot that overlays pair with fingerprint-based invalidation.
    /// </summary>
    public readonly record struct OverlayRefreshPolicy(OverlayRefreshMode Mode, int IntervalMs)
    {
        public static OverlayRefreshPolicy None { get; } = new(OverlayRefreshMode.None, 0);

        public static OverlayRefreshPolicy Throttled(int intervalMs)
            => new(OverlayRefreshMode.Throttled, intervalMs);

        public static OverlayRefreshPolicy DirtyTracked(int intervalMs)
            => new(OverlayRefreshMode.DirtyTracked, intervalMs);
    }
}
