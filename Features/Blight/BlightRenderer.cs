namespace ClickIt.Features.Blight;

// Coordinate-space contract: map helpers take GRID positions (GridPosNum); in-world helpers take
// WORLD positions (PosNum) + world-space radii; Camera.WorldToScreen takes WORLD positions.
public sealed class BlightRenderer
{
    private readonly BlightService _blightService;
    private readonly ClickItSettings _settings;

    private static readonly Color PumpColor = new(0, 200, 255, 120);
    internal static readonly Color PhantomLaneColor = new(235, 235, 235, 200);
    private const int LaneLineWidth = 8;

    private const float TowerDotRadius = 3.5f;
    private const int TowerDotSegments = 64;
    private const float PumpGridRadius = 8f;
    private const float PumpWorldCircleRadius = 35f;

    private readonly NumVector2[] _halfDiscBuffer = new NumVector2[TowerDotSegments + 1];

    internal BlightRenderer(BlightService blightService, ClickItSettings settings)
    {
        _blightService = blightService;
        _settings = settings;
    }

    internal void Render(GameController gameController, Graphics graphics)
    {
        if (!_settings.ClickBlightTowers.Value || !_blightService.IsEncounterActive)
            return;

        bool largeMapOpen = gameController.Game?.IngameState?.IngameUi?.Map?.LargeMap?.IsVisible ?? false;

        if (_settings.BlightVisualizePaths.Value)
            DrawBlightLanes(gameController, graphics, largeMapOpen, _blightService);

        if (_settings.BlightDebugShowLaneLabels.Value)
            DrawLaneLabels(gameController, graphics);

        if (_settings.BlightVisualizeTowers.Value)
        {
            bool showUpgrades = _settings.BlightVisualizeUpgrades.Value;
            bool showRanges = _settings.BlightVisualizeTowerRanges.Value;

            DrawTowerDots(gameController, graphics, largeMapOpen, showUpgrades);

            if (showRanges)
                DrawTowerRanges(gameController, graphics, largeMapOpen);
        }

        DrawPump(gameController, graphics, largeMapOpen);
    }

    private void DrawTowerDots(GameController gameController, Graphics graphics, bool largeMapOpen, bool showUpgrades)
    {
        IReadOnlyList<BlightCachedTower> ordered = _blightService.GetFoundationsInPriorityOrder();
        IBlightTowerStrategy strategy = _blightService.CurrentStrategy;
        Camera? camera = gameController.Game?.IngameState?.Camera;
        if (camera == null)
            return;

        BlightPlan? plan = _blightService.CurrentPlan;
        int cursor = _blightService.CurrentPlanCursor;

        for (int order = 0; order < ordered.Count; order++)
        {
            BlightCachedTower ft = ordered[order];
            bool hasTower = ft.UpgradeLevel > 0;
            bool isCurrentStep = IsCurrentStepAt(plan, cursor, ft.WorldPosition);
            IReadOnlyList<int> pendingNumbers = PendingPlanStepNumbers(plan, cursor, ft.WorldPosition);

            if (!ShouldRenderTowerDot(isCurrentStep, pendingNumbers.Count))
                continue;

            if (largeMapOpen)
            {
                NumVector2 mapCenter = ProjectGridToLargeMap(gameController, ft.WorldPosition);
                float mapRadius = TowerDotRadius * GetLargeMapScale(gameController);
                DrawTowerDot(graphics, mapCenter, mapRadius, ft, hasTower, strategy);

                if (isCurrentStep)
                    DrawScreenRing(graphics, mapCenter, mapRadius + CurrentStepRingPad, CurrentStepRingThickness, CurrentStepRingColor);
            }

            // In-world: project WORLD (PosNum) with a screen-space radius; restored foundations have no WorldPos3 and draw only on the map.
            if (ft.WorldPos3 is { } worldPos && IsWorldPosOnScreen(gameController, worldPos))
            {
                NumVector2 screenCenter = camera.WorldToScreen(worldPos);
                float screenRadius = ScreenRadiusForWorldDot(camera, worldPos, GridToWorldRadius(TowerDotRadius));
                DrawTowerDot(graphics, screenCenter, screenRadius, ft, hasTower, strategy);

                if (isCurrentStep)
                    DrawScreenRing(graphics, screenCenter, screenRadius + CurrentStepRingPad, CurrentStepRingThickness, CurrentStepRingColor);

                if (showUpgrades && pendingNumbers.Count > 0)
                    DrawStepNumbers(graphics, pendingNumbers, isCurrentStep, screenCenter);
            }
        }
    }

    internal static bool ShouldRenderTowerDot(bool isCurrentStep, int pendingStepCount)
        => isCurrentStep || pendingStepCount > 0;

    internal static Color LaneColorFor(LaneCoverageResult segment, IBlightTowerStrategy strategy)
        => segment.IsPhantom ? PhantomLaneColor : strategy.GetLaneColor(segment);

    private void DrawTowerDot(Graphics graphics, NumVector2 center, float radius, BlightCachedTower ft, bool hasTower, IBlightTowerStrategy strategy)
    {
        Color dotColor = hasTower
            ? strategy.GetFoundationColour(hasTower: true, ft.TowerType)
            : strategy.GetFoundationOutline(ft.PlannedTowerType);
        DrawScreenDisc(graphics, center, radius, dotColor);
    }

    private void DrawScreenHalfDisc(Graphics graphics, NumVector2 center, float radius, bool topHalf, Color color)
    {
        NumVector2[] buffer = _halfDiscBuffer;
        for (int i = 0; i <= TowerDotSegments; i++)
        {
            float a = topHalf
                ? MathF.PI + (MathF.PI * i / TowerDotSegments)
                : MathF.PI * i / TowerDotSegments;
            buffer[i] = new NumVector2(
                center.X + (MathF.Cos(a) * radius),
                center.Y + (MathF.Sin(a) * radius));
        }
        graphics.DrawConvexPolyFilled(buffer, color);
    }

    private static void DrawScreenRing(Graphics graphics, NumVector2 center, float radius, int thickness, Color color)
    {
        const int segments = 48;
        for (int i = 0; i < segments; i++)
        {
            float a0 = MathF.PI * 2f * i / segments;
            float a1 = MathF.PI * 2f * (i + 1) / segments;
            graphics.DrawLine(
                new NumVector2(center.X + (MathF.Cos(a0) * radius), center.Y + (MathF.Sin(a0) * radius)),
                new NumVector2(center.X + (MathF.Cos(a1) * radius), center.Y + (MathF.Sin(a1) * radius)),
                thickness,
                color);
        }
    }

    private void DrawScreenDisc(Graphics graphics, NumVector2 center, float radius, Color color)
    {
        DrawScreenHalfDisc(graphics, center, radius, topHalf: true, color);
        DrawScreenHalfDisc(graphics, center, radius, topHalf: false, color);
    }

    private static float ScreenRadiusForWorldDot(Camera camera, System.Numerics.Vector3 worldCenter, float worldRadius)
    {
        NumVector2 c = camera.WorldToScreen(worldCenter);
        NumVector2 px = camera.WorldToScreen(new System.Numerics.Vector3(worldCenter.X + worldRadius, worldCenter.Y, worldCenter.Z));
        NumVector2 py = camera.WorldToScreen(new System.Numerics.Vector3(worldCenter.X, worldCenter.Y + worldRadius, worldCenter.Z));
        return ((px - c).Length() + (py - c).Length()) * 0.5f;
    }

    private static float GetLargeMapScale(GameController gameController)
        => gameController.Game?.IngameState?.IngameUi?.Map?.LargeMap?.MapScale ?? 1f;

    private static readonly Color CurrentStepColor = new(0, 255, 0, 255);

    private static readonly Color CurrentStepRingColor = new(0, 255, 0, 120);

    private const float CurrentStepRingPad = 2f;

    private const int CurrentStepRingThickness = 2;

    private static void DrawTextWithShadow(Graphics graphics, string text, NumVector2 pos, Color color, FontAlign align)
    {
        graphics.DrawText(text, new NumVector2(pos.X + 1f, pos.Y + 1f), Color.Black, align);
        graphics.DrawText(text, pos, color, align);
    }

    private static NumVector2 ProjectGridToLargeMap(GameController gameController, NumVector2 gridPos)
    {
        SubMap? largeMap = gameController.Game?.IngameState?.IngameUi?.Map?.LargeMap;
        NumVector2 playerGrid = gameController.Player?.GridPosNum ?? gridPos;
        NumVector2 mapCenter = largeMap?.MapCenter ?? new NumVector2();
        float mapScale = largeMap?.MapScale ?? 1f;

        float dx = gridPos.X - playerGrid.X;
        float dy = gridPos.Y - playerGrid.Y;

        return new NumVector2(
            mapCenter.X + (mapScale * ((dx - dy) * SubMap.CameraAngleCos)),
            mapCenter.Y + (mapScale * (-(dx + dy) * SubMap.CameraAngleSin)));
    }

    private static void DrawBlightLanes(GameController gameController, Graphics graphics, bool largeMapOpen, BlightService blight)
    {
        (NumVector2[] Pathways, LaneCoverageResult[] Coverage)? bundle = blight.TryGetRenderBundle();
        if (bundle == null || bundle.Value.Pathways.Length < 2)
            return;

        NumVector2[] pathways = bundle.Value.Pathways;
        LaneCoverageResult[] coverage = bundle.Value.Coverage;

        IBlightTowerStrategy strategy = blight.CurrentStrategy;

        for (int s = 0; s < coverage.Length; s++)
        {
            int par = coverage[s].ParentIndex;
            if (par < 0 || par == s || par >= pathways.Length || coverage[s].IsPumpStub)
                continue;

            NumVector2 a = pathways[par];
            NumVector2 b = pathways[s];
            Color laneColor = LaneColorFor(coverage[s], strategy);

            if (largeMapOpen)
                graphics.DrawLineOnLargeMap(a, b, LaneLineWidth, laneColor);

            if (IsGridPosOnScreen(gameController, a))
                graphics.DrawLineInWorld(a, b, LaneLineWidth, laneColor);
        }
    }

    private void DrawLaneLabels(GameController gameController, Graphics graphics)
    {
        (NumVector2[] Pathways, LaneCoverageResult[] Coverage)? bundle = _blightService.TryGetRenderBundle();
        if (bundle == null || bundle.Value.Coverage.Length == 0)
            return;

        LaneCoverageResult[] coverage = bundle.Value.Coverage;
        (List<NumVector2> Positions, List<(PumpBranch Branch, List<int> Segments)> Branches) =
            _blightService.GetBranchDebug();
        if (Branches.Count == 0 || Positions.Count != coverage.Length)
            return;

        List<List<int>> children = BlightLaneTopology.BuildCoverageChildren(coverage);
        string?[] labelFor = new string?[coverage.Length];
        for (int b = 0; b < Branches.Count; b++)
        {
            (PumpBranch branch, List<int> segments) = Branches[b];
            if (branch.CoverageSegment < 0)
                continue;
            char letter = (char)('A' + (b % 26));
            List<BlightLaneNode> forest = BlightLaneTopology.BuildBranchLaneForest(
                coverage, children, segments, branch.CoverageSegment, letter.ToString());
            for (int l = 0; l < forest.Count; l++)
                LabelLanes(forest[l], labelFor);
        }

        IBlightTowerStrategy strategy = _blightService.CurrentStrategy;
        IReadOnlySet<BlightTowerType> coverageTypes = BlightCoverageFlags.ForStrategy(strategy);
        Camera? camera = gameController.Game?.IngameState?.Camera;
        if (camera == null)
            return;

        float gridToWorld = 1f / PoeMapExtension.WorldToGridConversion;
        for (int s = 0; s < coverage.Length; s++)
        {
            string? label = labelFor[s];
            if (label == null)
                continue;
            NumVector2 grid = coverage[s].Midpoint;
            if (!IsGridPosOnScreen(gameController, grid))
                continue;
            NumVector2 screen = camera.WorldToScreen(new System.Numerics.Vector3(
                grid.X * gridToWorld, grid.Y * gridToWorld, 0f));
            LaneCoverageResult seg = coverage[s];
            DrawTextWithShadow(graphics, $"{label} {BlightCoverageFlags.Compact(seg, coverageTypes)}", screen,
                LaneColorFor(seg, strategy), FontAlign.Center);
        }
    }

    private static void LabelLanes(BlightLaneNode lane, string?[] labelFor)
    {
        for (int i = 0; i < lane.Segments.Count; i++)
            labelFor[lane.Segments[i]] = $"{lane.Name}{i + 1}";
        for (int c = 0; c < lane.Children.Count; c++)
            LabelLanes(lane.Children[c], labelFor);
    }

    private void DrawTowerRanges(GameController gameController, Graphics graphics, bool largeMapOpen)
    {
        IReadOnlyList<(Entity Entity, string TowerId)> towers = _blightService.TowerEntities;
        IBlightTowerStrategy strategy = _blightService.CurrentStrategy;

        for (int i = 0; i < towers.Count; i++)
        {
            (Entity entity, string towerId) = towers[i];
            int radius = _blightService.GetTowerRadiusCached(towerId);

            BlightTowerType? mapped = BlightHelpers.MapTowerIdToType(towerId);
            Color rangeColor = mapped.HasValue
                ? strategy.GetTowerRangeColor(mapped.Value)
                : Color.Gray;

            if (largeMapOpen)
                graphics.DrawCircleOnLargeMap(entity.GridPosNum, false, radius, rangeColor, 4);

            if (IsWorldPosOnScreen(gameController, entity.PosNum))
                graphics.DrawCircleInWorld(entity.PosNum, GridToWorldRadius(radius), rangeColor, 2, 40, false);
        }
    }

    private void DrawPump(GameController gameController, Graphics graphics, bool largeMapOpen)
    {
        Entity? pump = _blightService.PumpEntity;
        if (pump == null) return;

        if (largeMapOpen)
            graphics.DrawFilledCircleOnLargeMap(pump.GridPosNum, false, PumpGridRadius, PumpColor, 16);

        if (IsWorldPosOnScreen(gameController, pump.PosNum))
            graphics.DrawCircleInWorld(pump.PosNum, GridToWorldRadius(PumpWorldCircleRadius), PumpColor, 3, 40, false);
    }

    internal static IReadOnlyList<int> PendingPlanStepNumbers(
        BlightPlan? plan, int cursor, NumVector2 position)
    {
        if (plan == null || plan.Steps.Count == 0)
            return [];

        List<int> result = [];
        for (int i = cursor; i < plan.Steps.Count; i++)
        {
            BlightPlanStep step = plan.Steps[i];
            if (BlightHelpers.SameGridPosition(step.FoundationPosition, position))
                result.Add(i + 1);
        }
        return result;
    }

    internal static bool IsCurrentStepAt(BlightPlan? plan, int cursor, NumVector2 position)
    {
        if (plan == null || cursor < 0 || cursor >= plan.Steps.Count)
            return false;
        BlightPlanStep step = plan.Steps[cursor];
        return BlightHelpers.SameGridPosition(step.FoundationPosition, position);
    }

    private static void DrawStepNumbers(
        Graphics graphics,
        IReadOnlyList<int> numbers,
        bool isCurrentStep,
        NumVector2 center)
    {
        const float lineHeight = 15f;
        const float topPadding = 4f;
        const float markerOffset = 20f;

        float startY = center.Y - ((numbers.Count - 1) * lineHeight * 0.5f) - topPadding;
        for (int i = 0; i < numbers.Count; i++)
        {
            float y = startY + (i * lineHeight);
            bool current = isCurrentStep && i == 0;
            Color numberColor = current ? CurrentStepColor : Color.White;
            DrawTextWithShadow(graphics, numbers[i].ToString(), new NumVector2(center.X, y), numberColor, FontAlign.Center);
            if (current)
                DrawTextWithShadow(graphics, ">", new NumVector2(center.X - markerOffset, y), CurrentStepColor, FontAlign.Center);
        }
    }

    private static float GridToWorldRadius(float gridRadius) => gridRadius / PoeMapExtension.WorldToGridConversion;

    private static bool IsGridPosOnScreen(GameController gameController, NumVector2 gridPos)
    {
        float scale = 1f / PoeMapExtension.WorldToGridConversion;
        return IsWorldPosOnScreen(gameController, new System.Numerics.Vector3(gridPos.X * scale, gridPos.Y * scale, 0f));
    }

    private static bool IsWorldPosOnScreen(GameController gameController, System.Numerics.Vector3 worldPos)
    {
        try
        {
            Camera camera = gameController.Game.IngameState.Camera;
            NumVector2 screenPos = camera.WorldToScreen(worldPos);
            Size2F windowSize = gameController.Window.GetWindowRectangleTimeCache.Size;

            const float allowance = 200f;
            return screenPos.X >= -allowance
                && screenPos.X <= windowSize.Width + allowance
                && screenPos.Y >= -allowance
                && screenPos.Y <= windowSize.Height + allowance;
        }
        catch
        {
            return false;
        }
    }
}
