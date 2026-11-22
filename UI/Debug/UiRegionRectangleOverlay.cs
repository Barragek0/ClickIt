namespace ClickIt.UI.Debug
{
    /// <summary>
    /// Debug-only overlay: draws the AreaService UI-region rectangles (blocked screen regions)
    /// every frame via the ImGui foreground draw list when Debug Mode and its setting are enabled.
    /// </summary>
    internal sealed class UiRegionRectangleOverlay(ClickItSettings settings, AreaService areaService)
    {
        private readonly ClickItSettings _settings = settings;
        private readonly AreaService _areaService = areaService;

        private static readonly Vector4 FullScreenColor = ToVec4(Color.LightSkyBlue);
        private static readonly Vector4 LifeColor = ToVec4(Color.Red);
        private static readonly Vector4 SkillsColor = ToVec4(Color.DeepSkyBlue);
        private static readonly Vector4 BuffsColor = ToVec4(Color.HotPink);
        private static readonly Vector4 DebuffsColor = ToVec4(Color.MediumPurple);
        private static readonly Vector4 ChatColor = ToVec4(Color.Green);
        private static readonly Vector4 MapColor = ToVec4(Color.Pink);
        private static readonly Vector4 XpBarColor = ToVec4(Color.Orange);
        private static readonly Vector4 ButtonsColor = ToVec4(Color.Cyan);
        private static readonly Vector4 QuestTrackerColor = ToVec4(Color.MediumPurple);

        internal static bool IsEnabled(ClickItSettings settings)
            => settings.DebugMode.Value && settings.DebugShowUnclickableScreenRegions.Value;

        internal void Render()
        {
            if (!IsEnabled(_settings))
                return;

            ImDrawListPtr draw = ImGui.GetForegroundDrawList();

            // The screen bounds are only a reference frame; individual blocked regions carry labels.
            DrawRect(draw, _areaService.FullScreenRectangle, FullScreenColor);
            DrawRect(draw, _areaService.HealthSquareRectangle, LifeColor, "HP Orb");
            DrawRect(draw, _areaService.FlaskRectangle, LifeColor, "Flasks");
            DrawRect(draw, _areaService.FlaskTertiaryRectangle, LifeColor, "Flasks");
            DrawRect(draw, _areaService.SkillsRectangle, SkillsColor, "Skills");
            DrawRect(draw, _areaService.SkillsTertiaryRectangle, SkillsColor, "Skills");
            DrawRect(draw, _areaService.ManaSquareRectangle, SkillsColor, "Mana Orb");
            DrawRect(draw, _areaService.ChatPanelBlockedRectangle, ChatColor, AreaBlockedRectangleResolver.GetLabel(AreaBlockedRectangleKind.ChatPanel));
            DrawRect(draw, _areaService.MapPanelBlockedRectangle, MapColor, AreaBlockedRectangleResolver.GetLabel(AreaBlockedRectangleKind.MapPanel));
            DrawRect(draw, _areaService.XpBarBlockedRectangle, XpBarColor, AreaBlockedRectangleResolver.GetLabel(AreaBlockedRectangleKind.XpBar));
            DrawRects(draw, _areaService.ButtonRectangles, ButtonsColor, "Button");

            DrawRect(draw, _areaService.BuffsRectangle, BuffsColor, "Buffs");
            DrawRect(draw, _areaService.DebuffsRectangle, DebuffsColor, "Debuffs");
            DrawRects(draw, _areaService.QuestTrackerBlockedRectangles, QuestTrackerColor, "Quest Tracker");
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

        private static void DrawRect(ImDrawListPtr draw, RectangleF rect, Vector4 color, string? label = null)
        {
            if (!TryGetDrawRect(rect, out NumVector2 min, out NumVector2 max))
                return;
            uint colorU32 = ImGui.GetColorU32(color);
            draw.AddRect(min, max, colorU32);

            if (string.IsNullOrWhiteSpace(label))
                return;

            NumVector2 textSize = ImGui.CalcTextSize(label);
            NumVector2 textPosition = new(
                rect.X + ((rect.Width - textSize.X) * 0.5f),
                rect.Y + ((rect.Height - textSize.Y) * 0.5f));
            draw.AddText(textPosition, colorU32, label);
        }

        private static void DrawRects(ImDrawListPtr draw, IReadOnlyList<RectangleF> rects, Vector4 color, string label)
        {
            for (int i = 0; i < rects.Count; i++)
                DrawRect(draw, rects[i], color, label);
        }

        private static Vector4 ToVec4(Color c)
            => new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
    }
}
