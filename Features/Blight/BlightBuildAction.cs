namespace ClickIt.Features.Blight;

internal enum BlightBuildActionKind
{
    None,
    ClickPosition,
    WalkToTarget,
    WalkToPosition,
    Complete,
    Error,
}

internal readonly record struct BlightBuildAction(
    BlightBuildActionKind Kind,
    Vector2 ClickPosition = default,
    string? DebugMessage = null,
    NumVector2 GridPosition = default);
