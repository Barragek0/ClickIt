namespace ClickIt.Shared.Rendering
{
    /// <summary>
    /// Per-feature overlay surface. One implementation per feature overlay (e.g. StrongboxOverlay).
    /// Refresh runs on the overlay's own coroutine (off the render thread); Draw runs every frame.
    /// Draw must never read game memory or allocate — it draws cached state with fresh projection.
    /// </summary>
    public interface IOverlay
    {
        /// <summary>Stable overlay name, used in coroutine/log naming.</summary>
        string Name { get; }

        /// <summary>Perf-table key; the host records per-frame timing under this section (0 when gated off).</summary>
        RenderSection Section { get; }

        /// <summary>Cadence knob: None (pure per-frame) or Throttled/DirtyTracked (coroutine-driven refresh).</summary>
        OverlayRefreshPolicy RefreshPolicy { get; }

        /// <summary>Optional coroutine timing channel for the refresh cadence (matches the perf table's coroutine rows).</summary>
        TimingChannel? RefreshTimingChannel { get; }

        /// <summary>Perf-table key for the feature's domain work; the host records refresh processing under this section.</summary>
        ProcessingSection ProcessingSection { get; }

        /// <summary>Settings gate. The host skips Draw (and records ~0ms) when false.</summary>
        bool IsEnabled(ClickItSettings settings);

        /// <summary>Coroutine thread: read game memory, compute derived data, swap the snapshot.</summary>
        void Refresh(OverlayRefreshContext ctx);

        /// <summary>Render thread: draw cached state with fresh per-frame projection, enqueue only.</summary>
        void Draw(OverlayRenderContext ctx);
    }
}
