namespace ClickIt.Shared.Rendering
{
    /// <summary>
    /// Coroutine-thread refresh context handed to refreshable overlays. Labels is the
    /// cached label snapshot (CachedLabels.Value); never a fresh scan.
    /// </summary>
    public readonly record struct OverlayRefreshContext(
        GameController? GameController,
        IReadOnlyList<LabelOnGround>? Labels,
        RectangleF WindowArea,
        ClickItSettings Settings);
}
