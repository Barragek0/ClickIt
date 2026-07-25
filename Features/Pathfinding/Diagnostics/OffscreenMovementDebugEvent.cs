namespace ClickIt.Features.Pathfinding.Diagnostics
{
    public sealed record OffscreenMovementDebugEvent(
        string Stage,
        string TargetPath,
        bool BuiltPath,
        bool ResolvedFromPath,
        bool ResolvedClickPoint,
        Vector2 WindowCenter,
        Vector2 TargetScreen,
        Vector2 ClickScreen,
        Vector2 PlayerGrid,
        Vector2 TargetGrid,
        string MovementSkillDebug);
}