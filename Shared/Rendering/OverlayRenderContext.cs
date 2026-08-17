namespace ClickIt.Shared.Rendering
{
    /// <summary>
    /// Per-frame draw context handed to every overlay. Created once per frame by the host;
    /// WindowArea is the time-cached window rectangle (never a fresh read).
    /// </summary>
    public readonly record struct OverlayRenderContext(
        ClickItSettings Settings,
        GameController? GameController,
        Graphics? Graphics,
        RectangleF WindowArea,
        IReadOnlyList<LabelOnGround>? Labels,
        DeferredDrawQueue DrawQueue);
}
