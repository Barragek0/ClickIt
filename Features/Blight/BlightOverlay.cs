namespace ClickIt.Features.Blight
{
    // Map helpers take GRID positions; in-world helpers take WORLD positions + world-space radii.
    public sealed class BlightOverlay : IOverlay
    {
        private const int BlightRefreshIntervalMs = 200;

        private readonly BlightService _blightService;

        private static readonly Color LaneLabelColor = new(235, 235, 235, 215);
        private static readonly Color UnassignedLaneLabelColor = new(255, 210, 70, 230);
        private const int LaneLineWidthMap = 2;
        private const int LaneLineWidthGame = 4;

        private const float TowerDotRadius = 3.5f;
        private const int TowerDotSegments = 12;

        // Map-only arrows per lane icon colored by visual state (1 red spawning, 2 green sending, 3 none).
        private const float PathwayArrowLength = 9f;
        private const float PathwayArrowHalfWidth = 2.5f;
        private const int PathwayArrowThickness = 3;
        private const float MapCullMargin = 60f;
        private static readonly Color PathwayArrowSpawningColor = new(255, 70, 70, 230);
        private static readonly Color PathwayArrowActiveColor = new(70, 255, 90, 230);

        private static readonly IReadOnlyList<int> EmptyPendingNumbers = [];
        private static readonly IReadOnlyList<string> EmptyPendingStrings = [];

        private BlightPlan? _pendingNumbersPlan;
        private int _pendingNumbersCursor = -1;
        private Dictionary<NumVector2, List<int>>? _pendingNumbersByPosition;
        private Dictionary<NumVector2, List<string>>? _pendingNumberTextsByPosition;
        // Grid cells that already drew a tower dot this frame, so overlapping dots never stack.
        private readonly HashSet<(int X, int Y)> _drawnDotKeys = [];
        // Grid cells that already drew a pathway arrow this frame, so overlapping lane arrows never stack.
        private readonly HashSet<(int X, int Y)> _drawnPathwayArrowKeys = [];

        // Per-frame icon screen-visibility arrays (reused across frames; resized on snapshot change).
        private bool[]? _iconMapVis;
        private bool[]? _iconWorldVis;

        // Lane-label render cache rebuilt only when the coverage reference or strategy changes.
        private LaneCoverageResult[]? _labelCacheCoverage;
        private IBlightTowerStrategy? _labelCacheStrategy;
        private string?[]? _labelCacheText;
        private System.Numerics.Vector3[]? _labelCacheWorld;
        private bool[]? _labelCacheUnassigned;

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

        // Coroutine thread — entity refresh + foundation scanning + lane coverage, off the render thread (reported as one Scan stage).
        public void Refresh(OverlayRefreshContext ctx)
        {
            long scanStart = Stopwatch.GetTimestamp();
            long scanAllocStart = GC.GetAllocatedBytesForCurrentThread();
            _blightService.RefreshEntities(ctx.GameController);
            _blightService.ScanFoundations(ctx.Labels);
            _blightService.ComputeLaneCoverage();
            double scanMs = (Stopwatch.GetTimestamp() - scanStart) * 1000.0 / Stopwatch.Frequency;
            long scanBytes = GC.GetAllocatedBytesForCurrentThread() - scanAllocStart;

            Span<long> bytes = stackalloc long[1];
            Span<double> ms = stackalloc double[1];
            bytes[0] = scanBytes;
            ms[0] = scanMs;
            _blightService.RecordBreakdown(bytes, ms);
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

            // Resolve camera/window/player/map once per frame.
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

            DrawIconPathways(rctx, ctx.DrawQueue);
            if (ctx.Settings.BlightDebugShowLaneLabels.Value)
                DrawLaneLabels(rctx, ctx.DrawQueue);

            bool dotsMap = ctx.Settings.BlightVisualizeTowers.Value && ctx.Settings.BlightVisualizeTowersMap.Value;
            bool dotsGame = ctx.Settings.BlightVisualizeTowers.Value && ctx.Settings.BlightVisualizeTowersGame.Value;
            if (dotsMap || dotsGame)
                // Upgrade order numbers ride on the game dots (no separate toggle).
                DrawTowerDots(rctx, ctx.DrawQueue, dotsMap, dotsGame, dotsGame);

            bool rangesMap = ctx.Settings.BlightVisualizeTowerRanges.Value && ctx.Settings.BlightVisualizeTowerRangesMap.Value;
            bool rangesGame = ctx.Settings.BlightVisualizeTowerRanges.Value && ctx.Settings.BlightVisualizeTowerRangesGame.Value;
            if (rangesMap || rangesGame)
                DrawTowerRanges(rctx, ctx.DrawQueue, rangesMap, rangesGame);
        }

        private void DrawTowerDots(RenderContext ctx, DeferredDrawQueue queue, bool dotsMap, bool dotsGame, bool showUpgrades)
        {
            _drawnDotKeys.Clear();
            IReadOnlyList<BlightCachedTower> ordered = _blightService.KnownTowers;
            IBlightTowerStrategy strategy = _blightService.CurrentStrategy;

            (BlightPlan? plan, int cursor) = _blightService.GetPlanSnapshot();

            for (int order = 0; order < ordered.Count; order++)
            {
                BlightCachedTower ft = ordered[order];
                bool hasTower = ft.UpgradeLevel > 0;
                bool isCurrentStep = IsCurrentStepAt(plan, cursor, ft.WorldPosition);
                IReadOnlyList<int> pendingNumbers = GetPendingPlanStepNumbers(plan, cursor, ft.WorldPosition);
                IReadOnlyList<string> pendingNumberTexts = GetPendingNumberTexts(plan, cursor, ft.WorldPosition);

                if (!ShouldRenderTowerDot(isCurrentStep, pendingNumbers.Count))
                    continue;

                // Keep one dot per grid cell; overlapping tower/foundation dots render corrupted.
                if (!_drawnDotKeys.Add(((int)MathF.Round(ft.WorldPosition.X), (int)MathF.Round(ft.WorldPosition.Y))))
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
            => strategy.GetLaneColor(segment);

        private static void DrawTowerDot(DeferredDrawQueue queue, NumVector2 center, float radius, BlightCachedTower ft, bool hasTower, IBlightTowerStrategy strategy)
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
            queue.EnqueueText(text, new Vector2(pos.X + 1f, pos.Y + 1f), Color.Black, 0, align);
            queue.EnqueueText(text, new Vector2(pos.X, pos.Y), color, 0, align);
        }

        private static NumVector2 ProjectGridToLargeMap(NumVector2 playerGrid, NumVector2 mapCenter, float mapScale, NumVector2 gridPos)
        {
            float dx = gridPos.X - playerGrid.X;
            float dy = gridPos.Y - playerGrid.Y;

            return new NumVector2(
                mapCenter.X + (mapScale * ((dx - dy) * SubMap.CameraAngleCos)),
                mapCenter.Y + (mapScale * (-(dx + dy) * SubMap.CameraAngleSin)));
        }

        // Whether a grid position projects onto the visible large-map window (with a margin), like the in-game IsGridPosOnScreen cull.
        private static bool IsOnLargeMapScreen(RenderContext ctx, NumVector2 gridPos)
        {
            NumVector2 pos = ProjectGridToLargeMap(ctx.PlayerGrid, ctx.LargeMapCenter, ctx.LargeMapScale, gridPos);
            return pos.X >= -MapCullMargin && pos.X <= ctx.WindowSize.Width + MapCullMargin
                && pos.Y >= -MapCullMargin && pos.Y <= ctx.WindowSize.Height + MapCullMargin;
        }

        private static readonly Color ActivePathwayColor = new(0, 255, 120, 200);

        // Only flowing lanes render; during the build phase (no lane active yet) every lane draws.
        private void DrawIconPathways(RenderContext ctx, DeferredDrawQueue queue)
        {
            IReadOnlyList<BlightPathwayIcon> icons = _blightService.IconPathwaySnapshot;
            if (icons.Count == 0)
                return;

            bool drawAll = true;
            for (int i = 0; i < icons.Count; i++)
            {
                if (icons[i].IsActive)
                {
                    drawAll = false;
                    break;
                }
            }

            // Per-segment coverage colours; fall back to green while coverage lags the icon scan.
            (NumVector2[] Pathways, LaneCoverageResult[] Coverage)? bundle = _blightService.TryGetRenderBundle();
            NumVector2[]? beamPaths = null;
            LaneCoverageResult[]? coverage = null;
            if (bundle != null && bundle.Value.Pathways.Length == icons.Count)
            {
                beamPaths = bundle.Value.Pathways;
                if (bundle.Value.Coverage.Length == icons.Count)
                    coverage = bundle.Value.Coverage;
            }
            IBlightTowerStrategy strategy = _blightService.CurrentStrategy;

            // Precompute each icon's map/world screen visibility once so edge culling never re-projects the same beam anchor (hundreds of edges share few anchor points).
            EnsureIconVisArrays(icons.Count);
            for (int i = 0; i < icons.Count; i++)
            {
                NumVector2 p = beamPaths != null ? beamPaths[i] : icons[i].GridPos;
                _iconMapVis![i] = ctx.LargeMapOpen && IsOnLargeMapScreen(ctx, p);
                _iconWorldVis![i] = BlightHelpers.IsGridPosOnScreen(ctx.Camera, ctx.WindowSize, p);
            }

            for (int i = 0; i < icons.Count; i++)
            {
                BlightPathwayIcon icon = icons[i];
                if (!drawAll && !icon.IsActive)
                    continue;

                // Draw from the coverage tree's parent links (never the raw beam links) so a just-attached chain never shows a detached stub.
                int next = coverage != null ? coverage[i].ParentIndex : -1;
                if (next >= 0 && next < icons.Count)
                {
                    BlightPathwayIcon to = icons[next];
                    if (drawAll || to.IsActive)
                        EnqueueLaneEdge(ctx, queue, beamPaths, icons, i, next, coverage, strategy, _iconMapVis!, _iconWorldVis!);
                }

                // Draw every extra beam parent at a convergence junction (the tree keeps only Parents[0]).
                if (icon.Parents.Length > 1)
                {
                    for (int x = 1; x < icon.Parents.Length; x++)
                    {
                        int p = icon.Parents[x];
                        if (p < 0 || p >= icons.Count)
                            continue;
                        if (p == next)
                            continue;
                        BlightPathwayIcon to = icons[p];
                        if (!drawAll && !to.IsActive)
                            continue;
                        EnqueueLaneEdge(ctx, queue, beamPaths, icons, i, p, coverage, strategy, _iconMapVis!, _iconWorldVis!);
                    }
                }
            }

            DrawPathwayArrows(ctx, queue, beamPaths, coverage);
        }

        // Map-only arrows per lane icon colored by visual state (1 red spawning, 2 green sending, 3 none).
        private void DrawPathwayArrows(RenderContext ctx, DeferredDrawQueue queue, NumVector2[]? beamPaths, LaneCoverageResult[]? coverage)
        {
            if (!ctx.LargeMapOpen)
                return;
            _drawnPathwayArrowKeys.Clear();
            IReadOnlyList<BlightPathwayIcon> icons = _blightService.IconPathwaySnapshot;
            for (int i = 0; i < icons.Count; i++)
            {
                // Half the arrows: skip every other icon, always keeping branch roots and junctions.
                if ((i & 1) != 0)
                {
                    int parent = coverage != null ? coverage[i].ParentIndex : -1;
                    bool junction = icons[i].Parents.Length > 1 || parent < 0;
                    if (!junction)
                        continue;
                }

                BlightPathwayIcon icon = icons[i];
                Color? arrowColor = icon.VisualState switch
                {
                    1 => PathwayArrowSpawningColor,
                    2 => PathwayArrowActiveColor,
                    _ => null,
                };
                if (arrowColor is not { } color)
                    continue;

                // The beam is the true lane geometry: anchor the arrow at the beam start like the lane edges.
                NumVector2 pos = beamPaths != null ? beamPaths[i] : icon.GridPos;

                // Cull arrows that project outside the visible map region (the web can hold hundreds).
                if (!_iconMapVis![i])
                    continue;

                // Co-located twin lane entities share a cell; keep one arrow per cell.
                if (!_drawnPathwayArrowKeys.Add(((int)MathF.Round(pos.X), (int)MathF.Round(pos.Y))))
                    continue;

                // Direction of travel is pump-ward: from the beam's outward end toward its start; fall back to the lane's pump-ward parent when the beam could not be read.
                NumVector2 dir = new(icon.BeamStart.X - icon.BeamEnd.X, icon.BeamStart.Y - icon.BeamEnd.Y);
                if (dir.X == 0f && dir.Y == 0f)
                {
                    int parent = coverage != null ? coverage[i].ParentIndex : -1;
                    if (parent < 0 || parent >= icons.Count)
                        continue;
                    NumVector2 parentPos = beamPaths != null ? beamPaths[parent] : icons[parent].GridPos;
                    dir = new NumVector2(parentPos.X - pos.X, parentPos.Y - pos.Y);
                    if (dir.X == 0f && dir.Y == 0f)
                        continue;
                }

                EnqueuePathwayArrow(queue, pos, dir, color);
            }
        }

        private static void EnqueuePathwayArrow(DeferredDrawQueue queue, NumVector2 center, NumVector2 dir, Color color)
        {
            float len = MathF.Sqrt((dir.X * dir.X) + (dir.Y * dir.Y));
            float ux = dir.X / len;
            float uy = dir.Y / len;
            float px = -uy;
            float py = ux;
            float half = PathwayArrowLength * 0.5f;
            NumVector2 head = new(center.X + (ux * half), center.Y + (uy * half));
            NumVector2 tail = new(center.X - (ux * half), center.Y - (uy * half));
            NumVector2 back = new(head.X - (ux * PathwayArrowLength * 0.45f), head.Y - (uy * PathwayArrowLength * 0.45f));
            queue.EnqueueLineOnLargeMap(tail, head, PathwayArrowThickness, color);
            queue.EnqueueLineOnLargeMap(head, new NumVector2(back.X + (px * PathwayArrowHalfWidth), back.Y + (py * PathwayArrowHalfWidth)), PathwayArrowThickness, color);
            queue.EnqueueLineOnLargeMap(head, new NumVector2(back.X - (px * PathwayArrowHalfWidth), back.Y - (py * PathwayArrowHalfWidth)), PathwayArrowThickness, color);
        }

        // Reuse the per-frame icon visibility arrays; only resize when the icon snapshot changes.
        private void EnsureIconVisArrays(int count)
        {
            if (_iconMapVis == null || _iconMapVis.Length != count)
            {
                _iconMapVis = new bool[count];
                _iconWorldVis = new bool[count];
            }
        }

        // Draw one lane edge, skipping the zero-length co-located twin edges so stacked forks don't pile lines on the node.
        private static void EnqueueLaneEdge(
            RenderContext ctx,
            DeferredDrawQueue queue,
            NumVector2[]? beamPaths,
            IReadOnlyList<BlightPathwayIcon> icons,
            int fromIdx,
            int toIdx,
            LaneCoverageResult[]? coverage,
            IBlightTowerStrategy strategy,
            bool[] mapVis,
            bool[] worldVis)
        {
            BlightPathwayIcon fromIcon = icons[fromIdx];
            BlightPathwayIcon to = icons[toIdx];
            // Use the beam-anchored positions so lanes follow the beams the monsters walk.
            NumVector2 from = beamPaths != null ? beamPaths[fromIdx] : fromIcon.GridPos;
            NumVector2 toPos = beamPaths != null ? beamPaths[toIdx] : to.GridPos;
            if (toPos == from)
                return;  // co-located twin merged into this node — the edge is zero-length

            Color laneColor = coverage != null ? LaneColorFor(coverage[fromIdx], strategy) : ActivePathwayColor;

            // Cull map edges to the visible map using the per-icon precomputed visibility.
            if (ctx.LargeMapOpen && (mapVis[fromIdx] || mapVis[toIdx]))
                queue.EnqueueLineOnLargeMap(from, toPos, LaneLineWidthMap, laneColor);
            if (worldVis[fromIdx] || worldVis[toIdx])
                queue.EnqueueLineInWorld(from, toPos, LaneLineWidthGame, laneColor);
        }

        private void DrawLaneLabels(RenderContext ctx, DeferredDrawQueue queue)
        {
            (NumVector2[] Pathways, LaneCoverageResult[] Coverage)? bundle = _blightService.TryGetRenderBundle();
            if (bundle == null || bundle.Value.Coverage.Length == 0)
                return;

            LaneCoverageResult[] coverage = bundle.Value.Coverage;
            IBlightTowerStrategy strategy = _blightService.CurrentStrategy;
            if (!ReferenceEquals(coverage, _labelCacheCoverage) || !ReferenceEquals(strategy, _labelCacheStrategy))
                RebuildLaneLabelCache(coverage, bundle.Value.Pathways, strategy);
            if (_labelCacheText == null)
                return;

            string?[]? text = _labelCacheText;
            System.Numerics.Vector3[]? world = _labelCacheWorld;
            bool[]? unassigned = _labelCacheUnassigned;
            if (text == null || world == null || unassigned == null)
                return;

            for (int s = 0; s < coverage.Length; s++)
            {
                string? label = text[s];
                if (label == null)
                    continue;

                // Project the cached terrain-midpoint once and cull on the screen position.
                NumVector2 screen = ctx.Camera.WorldToScreen(world[s]);
                if (!BlightHelpers.IsScreenPosInWindow(screen, ctx.WindowSize, 24f))
                    continue;
                DrawLaneLabel(queue, label, screen, unassigned[s] ? UnassignedLaneLabelColor : LaneLabelColor);
            }
        }

        private void RebuildLaneLabelCache(LaneCoverageResult[] coverage, NumVector2[] pathways, IBlightTowerStrategy strategy)
        {
            _labelCacheCoverage = coverage;
            _labelCacheStrategy = strategy;
            _labelCacheText = null;

            (List<NumVector2> Positions, List<(PumpBranch Branch, List<int> Segments)> Branches, List<int> Unassigned, List<List<int>>? _, List<List<BlightLaneNode>>? Forests) =
                _blightService.GetBranchDebug();
            if (Branches.Count == 0 || Positions.Count != coverage.Length || Forests == null)
                return;

            // The per-branch lane forests are built on the scan thread; the render thread only labels the cached lanes when the coverage reference changes.
            string?[] labelFor = new string?[coverage.Length];
            for (int b = 0; b < Branches.Count; b++)
            {
                (PumpBranch branch, _) = Branches[b];
                if (branch.CoverageSegment < 0)
                    continue;
                List<BlightLaneNode> forest = Forests[b];
                for (int l = 0; l < forest.Count; l++)
                    LabelLanes(forest[l], labelFor);
            }

            // Unassigned lanes get U{n} labels so unattached chains stay visible and distinguishable.
            HashSet<int> unassignedSet = [.. Unassigned];
            for (int u = 0; u < Unassigned.Count; u++)
                labelFor[Unassigned[u]] = $"U{u + 1}";

            IReadOnlySet<BlightTowerType> coverageTypes = BlightCoverageFlags.ForStrategy(strategy);
            IReadOnlyDictionary<NumVector2, System.Numerics.Vector3> worldByGrid = _blightService.PathwayWorldPositions;

            string?[] text = new string?[coverage.Length];
            System.Numerics.Vector3[] world = new System.Numerics.Vector3[coverage.Length];
            bool[] unassigned = new bool[coverage.Length];
            for (int s = 0; s < coverage.Length; s++)
            {
                string? baseLabel = labelFor[s];
                if (baseLabel == null)
                    continue;
                LaneCoverageResult seg = coverage[s];
                text[s] = $"{baseLabel} {BlightCoverageFlags.Compact(seg, coverageTypes)}";
                // The lanes follow the terrain, so labels project at the segment's world midpoint (terrain Z).
                world[s] = ResolveSegmentWorldMidpoint(pathways, coverage, s, worldByGrid)
                    ?? BlightHelpers.GridToWorld(coverage[s].Midpoint);
                unassigned[s] = unassignedSet.Contains(s);
            }

            _labelCacheText = text;
            _labelCacheWorld = world;
            _labelCacheUnassigned = unassigned;
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

        // Lane labels use a light fill with a dark 2-way shadow to stay readable over the lane colours.
        private static void DrawLaneLabel(DeferredDrawQueue queue, string text, NumVector2 pos, Color color)
        {
            queue.EnqueueText(text, new Vector2(pos.X - 1f, pos.Y - 1f), Color.Black, 0, FontAlign.Center);
            queue.EnqueueText(text, new Vector2(pos.X + 1f, pos.Y + 1f), Color.Black, 0, FontAlign.Center);
            queue.EnqueueText(text, new Vector2(pos.X, pos.Y), color, 0, FontAlign.Center);
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
            // Ranges draw from the cached tower snapshot, never the live entities (game-memory reads on the render thread).
            IReadOnlyList<BlightCachedTower> towers = _blightService.KnownTowers;
            IBlightTowerStrategy strategy = _blightService.CurrentStrategy;

            for (int i = 0; i < towers.Count; i++)
            {
                BlightCachedTower t = towers[i];
                if (t.UpgradeLevel <= 0)
                    continue;

                int radius = t.Radius > 0 ? t.Radius : BlightService.GetRadiusForLevel(t.TowerType, t.UpgradeLevel);
                Color rangeColor = strategy.GetTowerRangeColor(t.TowerType);

                if (ctx.LargeMapOpen && rangesMap)
                    queue.EnqueueCircleOnLargeMap(t.WorldPosition, false, radius, rangeColor, LaneLineWidthMap);

                if (rangesGame && t.WorldPos3 is { } world && BlightHelpers.IsWorldPosOnScreen(ctx.Camera, ctx.WindowSize, world))
                    queue.EnqueueCircleInWorld(world, BlightHelpers.GridToWorldRadius(radius), rangeColor, 2, 24, false);
            }
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

            // Exact-key miss: fall back to the tolerance scan once, then cache the result so later frames reuse it.
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
