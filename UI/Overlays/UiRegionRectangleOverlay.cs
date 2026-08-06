namespace ClickIt.UI.Overlays;

internal sealed class UiRegionRectangleOverlay(ClickItSettings settings, AreaService areaService)
{
    private readonly ClickItSettings _settings = settings;
    private readonly AreaService _areaService = areaService;

    private static readonly Vector4 FullScreenColor = ToVec4(Color.LightSkyBlue);
    private static readonly Vector4 LifeColor = ToVec4(Color.Red);
    private static readonly Vector4 SkillsColor = ToVec4(Color.DeepSkyBlue);
    private static readonly Vector4 BuffsColor = ToVec4(Color.Plum);
    private static readonly Vector4 ChatColor = ToVec4(Color.Green);
    private static readonly Vector4 MapColor = ToVec4(Color.Pink);
    private static readonly Vector4 XpBarColor = ToVec4(Color.Orange);
    private static readonly Vector4 MirageColor = ToVec4(Color.Cyan);
    private static readonly Vector4 AltarColor = ToVec4(Color.Gold);
    private static readonly Vector4 RitualColor = ToVec4(Color.LawnGreen);
    private static readonly Vector4 SentinelColor = ToVec4(Color.LightCoral);
    private static readonly Vector4 QuestTrackerColor = ToVec4(Color.MediumPurple);

    internal void Render()
    {
        if (!_settings.DebugShowUnclickableScreenRegions.Value)
            return;

        ImDrawListPtr draw = ImGui.GetForegroundDrawList();

        DrawRect(draw, _areaService.FullScreenRectangle, FullScreenColor);
        DrawRect(draw, _areaService.HealthSquareRectangle, LifeColor);
        DrawRect(draw, _areaService.FlaskRectangle, LifeColor);
        DrawRect(draw, _areaService.FlaskTertiaryRectangle, LifeColor);
        DrawRect(draw, _areaService.SkillsRectangle, SkillsColor);
        DrawRect(draw, _areaService.SkillsTertiaryRectangle, SkillsColor);
        DrawRect(draw, _areaService.ManaSquareRectangle, SkillsColor);
        DrawRect(draw, _areaService.ChatPanelBlockedRectangle, ChatColor);
        DrawRect(draw, _areaService.MapPanelBlockedRectangle, MapColor);
        DrawRect(draw, _areaService.XpBarBlockedRectangle, XpBarColor);
        DrawRect(draw, _areaService.MirageBlockedRectangle, MirageColor);
        DrawRect(draw, _areaService.AltarBlockedRectangle, AltarColor);
        DrawRect(draw, _areaService.RitualBlockedRectangle, RitualColor);
        DrawRect(draw, _areaService.SentinelBlockedRectangle, SentinelColor);

        DrawRects(draw, _areaService.BuffsAndDebuffsRectangles, BuffsColor);
        if (_areaService.BuffsAndDebuffsRectangles.Count == 0)
            DrawRect(draw, _areaService.BuffsAndDebuffsRectangle, BuffsColor);
        DrawRects(draw, _areaService.QuestTrackerBlockedRectangles, QuestTrackerColor);
    }

    // AreaService rectangles are standard X/Y/W/H client coordinates.
    internal static bool TryGetDrawRect(RectangleF rect, out NumVector2 min, out NumVector2 max)
    {
        if (rect.Width <= 0f || rect.Height <= 0f)
        {
            min = default;
            max = default;
            return false;
        }

        min = new NumVector2(rect.X, rect.Y);
        max = new NumVector2(rect.X + rect.Width, rect.Y + rect.Height);
        return true;
    }

    private static void DrawRect(ImDrawListPtr draw, RectangleF rect, Vector4 color)
    {
        if (!TryGetDrawRect(rect, out NumVector2 min, out NumVector2 max))
            return;
        draw.AddRect(min, max, ImGui.GetColorU32(color));
    }

    private static void DrawRects(ImDrawListPtr draw, IReadOnlyList<RectangleF> rects, Vector4 color)
    {
        for (int i = 0; i < rects.Count; i++)
            DrawRect(draw, rects[i], color);
    }

    private static Vector4 ToVec4(Color c)
        => new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
}
