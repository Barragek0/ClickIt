using RectangleF = SharpDX.RectangleF;

namespace ClickIt.Features.Area
{
    internal sealed class AreaBlockedSnapshot
    {
        internal RectangleF FullScreenRectangle { get; init; } = RectangleF.Empty;
        internal RectangleF HealthAndFlaskRectangle { get; init; } = RectangleF.Empty;
        internal RectangleF ManaAndSkillsRectangle { get; init; } = RectangleF.Empty;
        internal RectangleF HealthSquareRectangle { get; init; } = RectangleF.Empty;
        internal RectangleF FlaskRectangle { get; init; } = RectangleF.Empty;
        internal RectangleF FlaskTertiaryRectangle { get; init; } = RectangleF.Empty;
        internal RectangleF SkillsRectangle { get; init; } = RectangleF.Empty;
        internal RectangleF SkillsTertiaryRectangle { get; init; } = RectangleF.Empty;
        internal RectangleF ManaSquareRectangle { get; init; } = RectangleF.Empty;
        internal RectangleF BuffsAndDebuffsRectangle { get; init; } = RectangleF.Empty;
        internal RectangleF ChatPanelBlockedRectangle { get; init; } = RectangleF.Empty;
        internal RectangleF MapPanelBlockedRectangle { get; init; } = RectangleF.Empty;
        internal RectangleF XpBarBlockedRectangle { get; init; } = RectangleF.Empty;
        internal RectangleF MirageBlockedRectangle { get; init; } = RectangleF.Empty;
        internal RectangleF AltarBlockedRectangle { get; init; } = RectangleF.Empty;
        internal RectangleF RitualBlockedRectangle { get; init; } = RectangleF.Empty;
        internal RectangleF SentinelBlockedRectangle { get; init; } = RectangleF.Empty;
        internal IReadOnlyList<RectangleF> BuffsAndDebuffsRectangles { get; init; } = [];
        internal IReadOnlyList<RectangleF> QuestTrackerBlockedRectangles { get; init; } = [];
    }
}