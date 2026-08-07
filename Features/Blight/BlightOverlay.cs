namespace ClickIt.Features.Blight
{
    // Coordinate-space contract: map helpers take GRID positions (GridPosNum); in-world helpers take
    // WORLD positions (PosNum) + world-space radii; Camera.WorldToScreen takes WORLD positions.
    public sealed class BlightOverlay : IOverlay
    {
        private const int BlightRefreshIntervalMs = 200;

        private readonly BlightService _blightService;

        private static readonly Color PumpColor = new(0, 200, 255, 120);
        internal static readonly Color PhantomLaneColor = new(235, 235, 235, 200);
        private static readonly Color LaneLabelColor = new(235, 235, 235, 215);
        private const int LaneLineWidthMap = 2;
        private const int LaneLineWidthGame = 4;

        private const float TowerDotRadius = 3.5f;
        private const int TowerDotSegments = 12;
        private const float PumpGridRadius = 12f;
        private const float PumpWorldCircleRadius = 42f;

        private static readonly IReadOnlyList<int> EmptyPendingNumbers = [];
        private static readonly IReadOnlyList<string> EmptyPendingStrings = [];

        private BlightPlan? _pendingNumbersPlan;
        private int _pendingNumbersCursor = -1;
        private Dictionary<NumVector2, List<int>>? _pendingNumbersByPosition;
        private Dictionary<NumVector2, List<string>>? _pendingNumberTextsByPosition;

        internal BlightOverlay(BlightService blightService)
        {
            _blightService = blightService;
        }

        public string Name => "Blight";

        public RenderSection Section => RenderSection.BlightOverlay;

        public OverlayRefreshPolicy RefreshPolicy => OverlayRefreshPolicy.Throttled(BlightRefreshIntervalMs);

        public TimingChannel? RefreshTimingChannel => TimingChannel.Blight;

        public ProcessingSection ProcessingSection => ProcessingSection.Blight;

        public bool IsEnabled(ClickItSettings settings)
            => settings.ClickBlightTowers.Value;

        // Coroutine thread — entity refresh + foundation scanning + lane coverage, off the render thread.
        public void Refresh(OverlayRefreshContext ctx)
        {
            _blightService.RefreshEntities(ctx.GameController);
            _blightService.ScanFoundations(ctx.Labels);
            _blightService.ComputeLaneCoverage();
        }

        private readonly record struct RenderContext(
            Camera Camera,
            Size2F WindowSize,
            bool LargeMapOpen,
            NumVector2 PlayerGrid,
            NumVector2 LargeMapCenter,
            float LargeMapScale);

        public void Draw(OverlayRenderContext ctx)
        {
            if (!_blightService.IsEncounterActive)
                return;

            // Resolve camera/window/player/map once per frame — the per-foundation and per-lane
            // projection code was re-reading all of these for every tower, dot, and lane segment.
            Camera? camera = ctx.GameController?.Game?.IngameState?.Camera;
            if (camera == null)
                return;
            Size2F windowSize = ctx.WindowArea.Size;
            SubMap? largeMap = ctx.GameController?.Game?.IngameState?.IngameUi?.Map?.LargeMap;
            RenderContext rctx = new(
                camera,
                windowSize,
                largeMap?.IsVisible ?? false,
                ctx.GameController?.Player?.GridPosNum ?? new NumVector2(),
                largeMap?.MapCenter ?? new NumVector2(),
                largeMap?.MapScale ?? 1f);

            bool lanesMap = ctx.Settings.BlightVisualizePaths.Value && ctx.Settings.BlightVisualizePathsMap.Value;
            bool lanesGame = ctx.Settings.BlightVisualizePaths.Value && ctx.Settings.BlightVisualizePathsGame.Value;
            if (lanesMap || lanesGame)
                DrawBlightLanes(rctx, ctx.DrawQueue, _blightService, lanesMap, lanesGame);

            // Lane labels ride on the in-game lane rendering so they stay anchored over the lanes; the
            // debug-box toggle can hide just the labels while the lanes stay visible.
            if (lanesGame && ctx.Settings.BlightDebugShowLaneLabels.Value)
                DrawLaneLabels(rctx, ctx.DrawQueue);

            bool dotsMap = ctx.Settings.BlightVisualizeTowers.Value && ctx.Settings.BlightVisualizeTowersMap.Value;
            bool dotsGame = ctx.Settings.BlightVisualizeTowers.Value && ctx.Settings.BlightVisualizeTowersGame.Value;
            if (dotsMap || dotsGame)
                DrawTowerDots(rctx, ctx.DrawQueue, dotsMap, dotsGame, ctx.Settings.BlightVisualizeUpgrades.Value);

            bool rangesMap = ctx.Settings.BlightVisualizeTowerRanges.Value && ctx.Settings.BlightVisualizeTowerRangesMap.Value;
            bool rangesGame = ctx.Settings.BlightVisualizeTowerRanges.Value && ctx.Settings.BlightVisualizeTowerRangesGame.Value;
            if (rangesMap || rangesGame)
                DrawTowerRanges(rctx, ctx.DrawQueue, rangesMap, rangesGame);

            DrawPump(rctx, ctx.DrawQueue);
        }

        private void DrawTowerDots(RenderContext ctx, DeferredDrawQueue queue, bool dotsMap, bool dotsGame, bool showUpgrades)
        {
            IReadOnlyList<BlightCachedTower> ordered = _blightService.KnownTowers;
            IBlightTowerStrategy strategy = _blightService.CurrentStrategy;

            BlightPlan? plan = _blightService.CurrentPlan;
            int cursor = _blightService.CurrentPlanCursor;

            for (int order = 0; order < ordered.Count; order++)
            {
                BlightCachedTower ft = ordered[order];
                bool hasTower = ft.UpgradeLevel > 0;
                bool isCurrentStep = IsCurrentStepAt(plan, cursor, ft.WorldPosition);
                IReadOnlyList<int> pendingNumbers = GetPendingPlanStepNumbers(plan, cursor, ft.WorldPosition);
                IReadOnlyList<string> pendingNumberTexts = GetPendingNumberTexts(plan, cursor, ft.WorldPosition);

                if (!ShouldRenderTowerDot(isCurrentStep, pendingNumbers.Count))
                    continue;

                if (ctx.LargeMapOpen && dotsMap)
                {
                    NumVector2 mapCenter = ProjectGridToLargeMap(ctx.PlayerGrid, ctx.LargeMapCenter, ctx.LargeMapScale, ft.WorldPosition);
                    float mapRadius = TowerDotRadius * ctx.LargeMapScale;
                    DrawTowerDot(queue, mapCenter, mapRadius, ft, hasTower, strategy);

                    if (isCurrentStep)
                        DrawScreenRing(queue, mapCenter, mapRadius + CurrentStepRingPad, CurrentStepRingThickness, CurrentStepRingColor);
                }

                // In-world: project WORLD (PosNum) with a screen-space radius; restored foundations have no WorldPos3 and draw only on the map.
                if (dotsGame && ft.WorldPos3 is { } worldPos && BlightHelpers.IsWorldPosOnScreen(ctx.Camera, ctx.WindowSize, worldPos))
                {
                    NumVector2 screenCenter = ctx.Camera.WorldToScreen(worldPos);
                    float screenRadius = ScreenRadiusForWorldDot(ctx.Camera, worldPos, BlightHelpers.GridToWorldRadius(TowerDotRadius));
                    DrawTowerDot(queue, screenCenter, screenRadius, ft, hasTower, strategy);

                    if (isCurrentStep)
                        DrawScreenRing(queue, screenCenter, screenRadius + CurrentStepRingPad, CurrentStepRingThickness, CurrentStepRingColor);

                    if (showUpgrades && pendingNumbers.Count > 0)
                        DrawStepNumbers(queue, pendingNumberTexts, isCurrentStep, screenCenter);
                }
            }
        }

        internal static bool ShouldRenderTowerDot(bool isCurrentStep, int pendingStepCount)
            => isCurrentStep || pendingStepCount > 0;

        internal static Color LaneColorFor(LaneCoverageResult segment, IBlightTowerStrategy strategy)
            => segment.IsPhantom ? PhantomLaneColor : strategy.GetLaneColor(segment);

        private void DrawTowerDot(DeferredDrawQueue queue, NumVector2 center, float radius, BlightCachedTower ft, bool hasTower, IBlightTowerStrategy strategy)
        {
            Color dotColor = hasTower
                ? strategy.GetFoundationColour(hasTower: true, ft.TowerType)
                : strategy.GetFoundationOutline(ft.PlannedTowerType);
            queue.EnqueueFilledScreenDisc(center, radius, dotColor, TowerDotSegments);
        }

        private static void DrawScreenRing(DeferredDrawQueue queue, NumVector2 center, float radius, int thickness, Color color)
        {
            const int segments = 24;
            for (int i = 0; i < segments; i++)
            {
                float a0 = MathF.PI * 2f * i / segments;
                float a1 = MathF.PI * 2f * (i + 1) / segments;
                queue.EnqueueLine(
                    new NumVector2(center.X + (MathF.Cos(a0) * radius), center.Y + (MathF.Sin(a0) * radius)),
                    new NumVector2(center.X + (MathF.Cos(a1) * radius), center.Y + (MathF.Sin(a1) * radius)),
                    thickness,
                    color);
            }
        }

        private static float ScreenRadiusForWorldDot(Camera camera, System.Numerics.Vector3 worldCenter, float worldRadius)
        {
            NumVector2 c = camera.WorldToScreen(worldCenter);
            NumVector2 px = camera.WorldToScreen(new System.Numerics.Vector3(worldCenter.X + worldRadius, worldCenter.Y, worldCenter.Z));
            NumVector2 py = camera.WorldToScreen(new System.Numerics.Vector3(worldCenter.X, worldCenter.Y + worldRadius, worldCenter.Z));
            return ((px - c).Length() + (py - c).Length()) * 0.5f;
        }

        private static readonly Color CurrentStepColor = new(0, 255, 0, 255);

        private static readonly Color CurrentStepRingColor = new(0, 255, 0, 120);

        private const float CurrentStepRingPad = 2f;

        private const int CurrentStepRingThickness = 2;

        private static void DrawTextWithShadow(DeferredDrawQueue queue, string text, NumVector2 pos, Color color, FontAlign align)
        {
            queue.EnqueueText(text, new NumVector2(pos.X + 1f, pos.Y + 1f), Color.Black, align);
            queue.EnqueueText(text, pos, color, align);
        }

        private static NumVector2 ProjectGridToLargeMap(NumVector2 playerGrid, NumVector2 mapCenter, float mapScale, NumVector2 gridPos)
        {
            float dx = gridPos.X - playerGrid.X;
            float dy = gridPos.Y - playerGrid.Y;

            return new NumVector2(
                mapCenter.X + (mapScale * ((dx - dy) * SubMap.CameraAngleCos)),
                mapCenter.Y + (mapScale * (-(dx + dy) * SubMap.CameraAngleSin)));
        }

        private static void DrawBlightLanes(RenderContext ctx, DeferredDrawQueue queue, BlightService blight, bool lanesMap, bool lanesGame)
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

                if (ctx.LargeMapOpen && lanesMap)
                    queue.EnqueueLineOnLargeMap(a, b, LaneLineWidthMap, laneColor);

                // Draw the in-world line when EITHER endpoint is on-screen: a lane — especially a
                // phantom bridge spanning a gap — whose far end sits off-screen would otherwise vanish
                // even though the near end (and the player) are on screen.
                if (lanesGame && (BlightHelpers.IsGridPosOnScreen(ctx.Camera, ctx.WindowSize, a) || BlightHelpers.IsGridPosOnScreen(ctx.Camera, ctx.WindowSize, b)))
                    queue.EnqueueLineInWorld(a, b, LaneLineWidthGame, laneColor);
            }
        }

        private void DrawLaneLabels(RenderContext ctx, DeferredDrawQueue queue)
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
            IReadOnlyDictionary<NumVector2, System.Numerics.Vector3> worldByGrid = _blightService.PathwayWorldPositions;

            NumVector2[] pathways = bundle.Value.Pathways;
            for (int s = 0; s < coverage.Length; s++)
            {
                string? label = labelFor[s];
                if (label == null)
                    continue;
                NumVector2 grid = coverage[s].Midpoint;
                if (!BlightHelpers.IsGridPosOnScreen(ctx.Camera, ctx.WindowSize, grid))
                    continue;

                // The lanes are drawn following the terrain, so the label must project at the segment's
                // world midpoint (terrain Z from the pathway entities) — projecting the grid midpoint at
                // Z=0 lands the text vertically off the lane, offset by terrain height and camera angle.
                System.Numerics.Vector3 world = ResolveSegmentWorldMidpoint(pathways, coverage, s, worldByGrid)
                    ?? BlightHelpers.GridToWorld(grid);
                NumVector2 screen = ctx.Camera.WorldToScreen(world);
                LaneCoverageResult seg = coverage[s];
                DrawLaneLabel(queue, $"{label} {BlightCoverageFlags.Compact(seg, coverageTypes)}", screen);
            }
        }

        internal static System.Numerics.Vector3? ResolveSegmentWorldMidpoint(
            NumVector2[] pathways,
            LaneCoverageResult[] coverage,
            int segmentIndex,
            IReadOnlyDictionary<NumVector2, System.Numerics.Vector3> worldByGrid)
        {
            int par = coverage[segmentIndex].ParentIndex;
            if (par < 0 || par >= pathways.Length)
                return null;
            if (!worldByGrid.TryGetValue(pathways[par], out System.Numerics.Vector3 a))
                return null;
            if (!worldByGrid.TryGetValue(pathways[segmentIndex], out System.Numerics.Vector3 b))
                return null;
            return new System.Numerics.Vector3((a.X + b.X) * 0.5f, (a.Y + b.Y) * 0.5f, (a.Z + b.Z) * 0.5f);
        }

        // Lane labels sit on top of the lane colour, so they use a light fill with a dark 2-way shadow
        // to stay readable over both dark lanes and the white phantom bridges.
        private static void DrawLaneLabel(DeferredDrawQueue queue, string text, NumVector2 pos)
        {
            queue.EnqueueText(text, new NumVector2(pos.X - 1f, pos.Y - 1f), Color.Black, FontAlign.Center);
            queue.EnqueueText(text, new NumVector2(pos.X + 1f, pos.Y + 1f), Color.Black, FontAlign.Center);
            queue.EnqueueText(text, pos, LaneLabelColor, FontAlign.Center);
        }

        private static void LabelLanes(BlightLaneNode lane, string?[] labelFor)
        {
            for (int i = 0; i < lane.Segments.Count; i++)
                labelFor[lane.Segments[i]] = $"{lane.Name}.{i + 1}";
            for (int c = 0; c < lane.Children.Count; c++)
                LabelLanes(lane.Children[c], labelFor);
        }

        private void DrawTowerRanges(RenderContext ctx, DeferredDrawQueue queue, bool rangesMap, bool rangesGame)
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

                if (ctx.LargeMapOpen && rangesMap)
                    queue.EnqueueCircleOnLargeMap(entity.GridPosNum, false, radius, rangeColor, LaneLineWidthMap);

                if (rangesGame && BlightHelpers.IsWorldPosOnScreen(ctx.Camera, ctx.WindowSize, entity.PosNum))
                    queue.EnqueueCircleInWorld(entity.PosNum, BlightHelpers.GridToWorldRadius(radius), rangeColor, 2, 24, false);
            }
        }

        private void DrawPump(RenderContext ctx, DeferredDrawQueue queue)
        {
            // The pump entity streams out of scan range when the player walks away from the encounter;
            // fall back to the persisted position so the dot/circle stays visible while it is active.
            Entity? pump = _blightService.PumpEntity;
            NumVector2? pumpGrid = pump != null ? new NumVector2(pump.GridPosNum.X, pump.GridPosNum.Y) : _blightService.PumpGridPosition;
            System.Numerics.Vector3? pumpWorld = pump != null ? pump.PosNum : _blightService.PumpWorldPosition;
            if (!pumpGrid.HasValue || !pumpWorld.HasValue)
                return;

            if (ctx.LargeMapOpen)
                queue.EnqueueFilledCircleOnLargeMap(pumpGrid.Value, false, PumpGridRadius, PumpColor, 16);

            if (BlightHelpers.IsWorldPosOnScreen(ctx.Camera, ctx.WindowSize, pumpWorld.Value))
                queue.EnqueueCircleInWorld(pumpWorld.Value, BlightHelpers.GridToWorldRadius(PumpWorldCircleRadius), PumpColor, 4, 24, false);
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

        internal IReadOnlyList<int> GetPendingPlanStepNumbers(BlightPlan? plan, int cursor, NumVector2 position)
            => GetPendingNumbers(plan, cursor, position, EmptyPendingNumbers, static n => n);

        // Pre-formatted step-number strings so the per-frame text draw never allocates ToString.
        internal IReadOnlyList<string> GetPendingNumberTexts(BlightPlan? plan, int cursor, NumVector2 position)
            => GetPendingNumbers(plan, cursor, position, EmptyPendingStrings, static n => n.ToString());

        private IReadOnlyList<T> GetPendingNumbers<T>(
            BlightPlan? plan, int cursor, NumVector2 position,
            IReadOnlyList<T> empty, Func<int, T> convert)
        {
            if (plan == null || plan.Steps.Count == 0)
                return empty;

            if (!ReferenceEquals(_pendingNumbersPlan, plan) || _pendingNumbersCursor != cursor)
            {
                _pendingNumbersPlan = plan;
                _pendingNumbersCursor = cursor;
                RebuildPendingNumberCaches(plan, cursor);
            }

            // Re-read the cache AFTER the rebuild (RebuildPendingNumberCaches swaps the dictionaries).
            Dictionary<NumVector2, List<T>> cache = PendingCache<T>();
            if (cache.TryGetValue(position, out List<T>? numbers))
                return numbers;

            // Exact-key miss (positions only ever differ by the <1 grid-unit tolerance): fall back to the
            // tolerance scan once, then cache the result so later frames reuse it.
            List<T> computed = [];
            foreach (int number in PendingPlanStepNumbers(plan, cursor, position))
                computed.Add(convert(number));
            cache[position] = computed;
            return computed;
        }

        private Dictionary<NumVector2, List<T>> PendingCache<T>()
            => typeof(T) == typeof(int)
                ? (Dictionary<NumVector2, List<T>>)(object)_pendingNumbersByPosition!
                : (Dictionary<NumVector2, List<T>>)(object)_pendingNumberTextsByPosition!;

        private void RebuildPendingNumberCaches(BlightPlan plan, int cursor)
        {
            Dictionary<NumVector2, List<int>> numbers = [];
            Dictionary<NumVector2, List<string>> texts = [];
            for (int i = cursor; i < plan.Steps.Count; i++)
            {
                BlightPlanStep step = plan.Steps[i];
                if (!numbers.TryGetValue(step.FoundationPosition, out List<int>? numberList))
                {
                    numberList = [];
                    numbers[step.FoundationPosition] = numberList;
                    texts[step.FoundationPosition] = [];
                }
                numberList.Add(i + 1);
                texts[step.FoundationPosition].Add((i + 1).ToString());
            }
            _pendingNumbersByPosition = numbers;
            _pendingNumberTextsByPosition = texts;
        }

        internal static bool IsCurrentStepAt(BlightPlan? plan, int cursor, NumVector2 position)
        {
            if (plan == null || cursor < 0 || cursor >= plan.Steps.Count)
                return false;
            BlightPlanStep step = plan.Steps[cursor];
            return BlightHelpers.SameGridPosition(step.FoundationPosition, position);
        }

        private static void DrawStepNumbers(
            DeferredDrawQueue queue,
            IReadOnlyList<string> numbers,
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
                DrawTextWithShadow(queue, numbers[i], new NumVector2(center.X, y), numberColor, FontAlign.Center);
                if (current)
                    DrawTextWithShadow(queue, ">", new NumVector2(center.X - markerOffset, y), CurrentStepColor, FontAlign.Center);
            }
        }
    }
}
