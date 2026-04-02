using SharpDX;

namespace ClickIt.Services.Pathfinding.Diagnostics
{
    public sealed record OffscreenMovementDebugSnapshot(
        bool HasData,
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
        string MovementSkillDebug,
        long TimestampMs)
    {
        public static readonly OffscreenMovementDebugSnapshot Empty = new(
            HasData: false,
            Stage: string.Empty,
            TargetPath: string.Empty,
            BuiltPath: false,
            ResolvedFromPath: false,
            ResolvedClickPoint: false,
            WindowCenter: default,
            TargetScreen: default,
            ClickScreen: default,
            PlayerGrid: default,
            TargetGrid: default,
            MovementSkillDebug: string.Empty,
            TimestampMs: 0);
    }
}