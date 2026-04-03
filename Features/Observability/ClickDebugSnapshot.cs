using SharpDX;

namespace ClickIt.Features.Observability
{
    public sealed record ClickDebugSnapshot(
        bool HasData,
        string Stage,
        string MechanicId,
        string EntityPath,
        float Distance,
        Vector2 WorldScreenRaw,
        Vector2 WorldScreenAbsolute,
        Vector2 ResolvedClickPoint,
        bool Resolved,
        bool CenterInWindow,
        bool CenterClickable,
        bool ResolvedInWindow,
        bool ResolvedClickable,
        string Notes,
        long Sequence,
        long TimestampMs)
    {
        public static readonly ClickDebugSnapshot Empty = new(
            HasData: false,
            Stage: string.Empty,
            MechanicId: string.Empty,
            EntityPath: string.Empty,
            Distance: 0f,
            WorldScreenRaw: default,
            WorldScreenAbsolute: default,
            ResolvedClickPoint: default,
            Resolved: false,
            CenterInWindow: false,
            CenterClickable: false,
            ResolvedInWindow: false,
            ResolvedClickable: false,
            Notes: string.Empty,
            Sequence: 0,
            TimestampMs: 0);
    }
}