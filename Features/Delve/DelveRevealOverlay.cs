namespace ClickIt.Features.Delve;

internal sealed class DelveRevealOverlay(Func<string> getConfigDirectory) : IOverlay, IDisposable
{
    private const int RefreshMs = 8;
    private const int CellRefreshMs = 300;
    private const int PanSettleMs = 90;
    private const int MovingMarkerRefreshMs = 60;
    private const int MaximumCellRefreshMs = 900;
    private const int StaleViewMs = 128;
    private const int MaxChunks = 128;
    private const int ChunkSide = 8;
    private const int CellsPerChunk = ChunkSide * ChunkSide;
    private const int ConnectionsPerCell = 4;
    private const int TextureLoadsPerFrame = 4;
    private const int TextureRetryMs = 1000;
    private const float MarkerLiftPixels = 2f;
    private static readonly Color ConnectionColor = new(242, 54, 62, 230);
    private static readonly Color TargetRouteColor = new(61, 220, 109, 245);
    private static readonly Color UnknownNodeColor = new(230, 215, 190, 240);
    private ChartSnapshot? _snapshot;
    private ChartCapture? _chartCapture;
    private ChartProjection _lastProjection;
    private RectangleF _lastClip;
    private bool _lastFullMap;
    private long _lastCapture;
    private long _lastPan;
    private long _lastMarkerScan;
    private long _markerChunkHash;
    private RectangleF _markerCoverage;
    private Graphics? _textureGraphics;
    private readonly Dictionary<DelveNodeIcon, Task<DelveNodeIcon.TextureResult>> _textureFiles = [];
    private readonly Dictionary<string, (bool Available, bool Owned, long RetryAt)> _textureLoads = new(StringComparer.Ordinal);

    internal enum WallApproach { None, North, East, South, West }

    internal readonly record struct ChartCell(RectangleF Rect, long[] Links, bool Hidden = true, bool BehindWall = false,
        NumVector2? Anchor = null, bool HasEncounter = false, bool Explored = false, DelveRouteTarget Target = DelveRouteTarget.None,
        WallApproach WallSide = WallApproach.None)
    {
        internal bool NeedsRouteOverlay => Hidden || BehindWall;
        internal NumVector2? RouteAnchor => HasEncounter ? Anchor : null;
    }
    internal readonly record struct CellLink(long Start, long End, bool Horizontal);
    internal readonly record struct RouteLines(ScreenLine[] Revealed, ScreenLine[] Target, ScreenLine[] FracturedWalls);
    internal readonly record struct ChartMarker(RectangleF Rect, NumVector2 Anchor, DelveNodeIcon Icon, string? HoverText = null,
        DelveRouteTarget Target = DelveRouteTarget.None);
    internal readonly record struct Node(NumVector2 Center, float Radius, DelveNodeIcon Icon,
        string? HoverText = null, Task<DelveNodeIcon.TextureResult>? TextureFile = null);
    private sealed record ChartSnapshot(ScreenLine[] Connections, ScreenLine[] TargetRoutes, ScreenLine[] FracturedWalls,
        Node[] Nodes, RectangleF Clip);
    private sealed record ChartCapture(long Address, RectangleF Coverage,
        ChartMarker[] Markers, RouteLines Lines, DelveRouteTarget Targets, long CapturedAt);

    internal readonly record struct ChartProjection(RectangleF OriginalGrid, RectangleF CurrentGrid, float Zoom = 1f)
    {
        private readonly float _scaleX = CurrentGrid.Width / OriginalGrid.Width;
        private readonly float _scaleY = CurrentGrid.Height / OriginalGrid.Height;

        internal RectangleF Project(RectangleF cell)
        {
            return new RectangleF(
                CurrentGrid.X + (cell.X - OriginalGrid.X) * _scaleX,
                CurrentGrid.Y + (cell.Y - OriginalGrid.Y) * _scaleY,
                cell.Width * _scaleX, cell.Height * _scaleY);
        }

        internal NumVector2 Project(NumVector2 point) => new(
            CurrentGrid.X + (point.X - OriginalGrid.X) * _scaleX,
            CurrentGrid.Y + (point.Y - OriginalGrid.Y) * _scaleY);

        internal bool Covers(RectangleF bounds, RectangleF clip)
        {
            RectangleF projected = Project(bounds);
            return projected.Left <= clip.Left && projected.Top <= clip.Top
                && projected.Right >= clip.Right && projected.Bottom >= clip.Bottom;
        }
    }

    public string Name => "DelveReveal";
    public RenderSection Section => RenderSection.Unknown;
    public OverlayRefreshPolicy RefreshPolicy => OverlayRefreshPolicy.Throttled(RefreshMs);
    public bool UsesRefreshLabels => false;
    public TimingChannel? RefreshTimingChannel => null;
    public ProcessingSection ProcessingSection => ProcessingSection.Unknown;
    public bool IsEnabled(ClickItSettings settings)
        => settings.RevealDelveMap.Value || settings.ShowDelveTargetRoutes.Value || settings.RevealDelveFracturedWalls.Value;

    public void Refresh(OverlayRefreshContext ctx)
    {
        try
        {
            RefreshChart(ctx);
        }
        catch
        {
            // The chart can close or replace its cells during a refresh.
            ClearChart();
        }
    }

    private void RefreshChart(OverlayRefreshContext ctx)
    {
        if (!IsEnabled(ctx.Settings))
        {
            ClearChart();
            return;
        }

        bool revealFullMap = ctx.Settings.RevealDelveMap.Value;
        DelveRouteTarget targets = DelveTargetRoutes.GetSelectedTargets(ctx.Settings);
        if (!revealFullMap && targets == DelveRouteTarget.None && !ctx.Settings.RevealDelveFracturedWalls.Value)
        {
            ClearChart();
            return;
        }

        SubterraneanChart? chart = ctx.GameController?.IngameState?.IngameUi?.DelveWindow;
        if (chart is not { IsValid: true, IsVisible: true }
            || chart.ChildCount == 0
            || chart.GetChildAtIndex(0) is not { IsValid: true, IsVisible: true } viewport
            || chart.GridElement is not { IsValid: true, Address: > 0 } grid
            || !TryReadView(viewport, grid, ctx.WindowArea, out ChartProjection projection, out RectangleF clip))
        {
            ClearChart();
            return;
        }

        long now = Environment.TickCount64;
        long chunkHash = grid.ChildHash;
        bool moved = _lastProjection != projection || _lastClip != clip;
        if (moved)
            _lastPan = now;

        ChartCapture? capture = _chartCapture;
        bool sameChart = capture != null && capture.Address == grid.Address && capture.Targets == targets;
        if (sameChart && (moved || _lastFullMap != revealFullMap))
            PublishView(capture!, projection, clip, now, revealFullMap);

        if (!sameChart || (targets == DelveRouteTarget.None && !projection.Covers(capture!.Coverage, clip))
            || IsCaptureDue(now - capture!.CapturedAt, now - _lastPan))
        {
            RectangleF coverage = GetLocalCoverage(projection, clip);
            capture = CaptureChart(grid, coverage, now, targets);
            if (!TryReadView(viewport, grid, ctx.WindowArea, out projection, out clip))
            {
                ClearChart();
                return;
            }

            now = Environment.TickCount64;
            if (_lastProjection != projection || _lastClip != clip)
                _lastPan = now;
            _chartCapture = capture;
            _markerCoverage = coverage;
            _markerChunkHash = chunkHash;
            _lastMarkerScan = now;
            PublishView(capture, projection, clip, now, revealFullMap);
        }
        else if (ShouldRefreshMarkers(chunkHash != _markerChunkHash, projection.Covers(_markerCoverage, clip),
            now - _lastMarkerScan, now - _lastPan))
        {
            RectangleF coverage = GetLocalCoverage(projection, clip);
            ChartMarker[] markers = CaptureChart(grid, coverage, now, targets, markersOnly: true).Markers;
            if (!TryReadView(viewport, grid, ctx.WindowArea, out projection, out clip))
            {
                ClearChart();
                return;
            }

            now = Environment.TickCount64;
            _chartCapture = capture! with { Markers = markers };
            _markerCoverage = coverage;
            _markerChunkHash = chunkHash;
            _lastMarkerScan = now;
            PublishView(_chartCapture, projection, clip, now, revealFullMap);
        }
        else if (!moved)
        {
            Volatile.Write(ref _lastCapture, now);
        }
    }

    internal static bool IsCaptureDue(long ageMs, long stillMs)
        => ageMs >= MaximumCellRefreshMs || (ageMs >= CellRefreshMs && stillMs >= PanSettleMs);

    internal static bool ShouldRefreshMarkers(bool chunksChanged, bool covered, long ageMs, long stillMs)
        => chunksChanged || !covered || (stillMs < PanSettleMs && ageMs >= MovingMarkerRefreshMs);

    private static RectangleF GetLocalCoverage(ChartProjection projection, RectangleF clip)
        => new ChartProjection(projection.CurrentGrid, projection.OriginalGrid)
            .Project(Expand(clip, clip.Width * 0.25f, clip.Height * 0.25f));

    private static bool TryReadView(Element viewport, DelveElement grid, RectangleF window,
        out ChartProjection projection, out RectangleF clip)
    {
        projection = default;
        clip = default;
        if (!viewport.IsVisible || !grid.IsValid || !LabelGeometry.TryGetElementRect(viewport, out RectangleF chartBounds)
            || !TryGetScreenClip(chartBounds, window, out clip)
            || !LabelGeometry.TryGetElementRect(grid, out RectangleF gridBounds)
            || !TryGetLocalRect(0, 0, grid.Width, grid.Height, out RectangleF localBounds))
            return false;

        float zoom = grid.Scale;
        projection = new ChartProjection(localBounds, gridBounds, float.IsFinite(zoom) && zoom > 0f ? MathF.Min(1f, zoom) : 1f);
        return true;
    }

    private void PublishView(ChartCapture capture, ChartProjection projection, RectangleF clip, long now, bool revealFullMap)
    {
        _lastProjection = projection;
        _lastClip = clip;
        _lastFullMap = revealFullMap;
        Node[] nodes = BuildMarkers(capture.Markers, clip, projection, revealFullMap, capture.Targets);
        for (int i = 0; i < nodes.Length; i++)
        {
            Node node = nodes[i];
            if (node.Icon.AssetName == null)
                continue;

            if (!_textureFiles.TryGetValue(node.Icon, out var textureFile))
            {
                textureFile = node.Icon.LoadAsync(getConfigDirectory);
                _textureFiles.Add(node.Icon, textureFile);
            }

            nodes[i] = node with { TextureFile = textureFile };
        }

        Volatile.Write(ref _snapshot, new ChartSnapshot(
            revealFullMap ? ProjectConnections(capture.Lines.Revealed, clip, projection) : [],
            ProjectConnections(capture.Lines.Target, clip, projection),
            ProjectConnections(capture.Lines.FracturedWalls, clip, projection), nodes, clip));
        Volatile.Write(ref _lastCapture, now);
    }

    private void ClearChart()
    {
        _chartCapture = null;
        _lastProjection = default;
        _lastClip = default;
        Volatile.Write(ref _snapshot, null);
    }

    internal static bool TryGetScreenClip(RectangleF chart, RectangleF window, out RectangleF clip)
    {
        float left = SystemMath.Max(chart.Left, 0);
        float top = SystemMath.Max(chart.Top, 0);
        float right = SystemMath.Min(chart.Right, window.Width);
        float bottom = SystemMath.Min(chart.Bottom, window.Height);
        clip = new RectangleF(left, top, right - left, bottom - top);
        return clip.Width > 0 && clip.Height > 0;
    }

    private static ChartCapture CaptureChart(DelveElement grid, RectangleF coverage, long now,
        DelveRouteTarget targets, bool markersOnly = false)
    {
        if (!DynamicAccess.TryGetDynamicValue(grid, static element => element.Cells, out object? rawChunks)
            || rawChunks is not IList chunks)
            return new ChartCapture(grid.Address, coverage, [], new RouteLines([], [], []), targets, now);

        // Routes can begin at explored nodes beyond the viewport.
        bool planRoutes = !markersOnly && targets != DelveRouteTarget.None;
        Dictionary<long, ChartCell> cells = [];
        List<ChartMarker> markers = [];
        List<DelveCell> revealedCells = [];
        bool hasHiddenRoute = false;
        for (int i = 0; i < chunks.Count && i < MaxChunks; i++)
        {
            if (chunks[i] is not DelveBigCell chunkElement
                || !TryGetLocalRect(chunkElement.X, chunkElement.Y, chunkElement.Width, chunkElement.Height, out RectangleF chunkRect)
                || (!planRoutes && !MayAffectCoverage(chunkRect, coverage))
                || !DynamicAccess.TryGetDynamicValue(chunkElement, static chunk => chunk.Cells, out object? rawCells)
                || rawCells is not IList chunk)
                continue;

            for (int j = 0; j < chunk.Count && j < CellsPerChunk; j++)
            {
                if (chunk[j] is not DelveCell { IsValid: true } cell || cell.Address == 0
                    || !TryGetLocalRect(chunkRect.X + cell.X, chunkRect.Y + cell.Y, cell.Width, cell.Height, out RectangleF rect)
                    || (!planRoutes && !Expand(coverage, rect.Width, rect.Height).Intersects(rect))
                    || cells.ContainsKey(cell.Address))
                    continue;

                bool? gameDraws = ReadVisibility(cell, static node => node.GameDraws);
                bool? visited = ReadVisibility(cell, static node => node.IsVisited);
                if (!gameDraws.HasValue || !visited.HasValue)
                    continue;

                bool hidden = IsHidden(gameDraws, visited);
                if (markersOnly && !hidden)
                    continue;

                // The game can show a destination icon before it shows the route behind the wall.
                bool behindWall = !markersOnly && !visited.Value && ReadVisibility(cell, static node => node.IsBehindWall) == true;
                bool needsMarker = hidden && (planRoutes || coverage.Intersects(rect));
                if (markersOnly && !needsMarker)
                    continue;

                NumVector2? anchor = planRoutes || hidden || behindWall ? ReadAnchor(cell) : null;
                string name = string.Empty;
                object? feature = planRoutes || anchor.HasValue ? ReadNamedFeature(cell, out name) : null;
                string image = string.Empty;
                if (feature != null)
                    DynamicAccess.TryReadString(feature, static value => value.Image, out image);
                DelveNodeIcon? icon = feature == null ? null : DelveNodeIcon.Resolve(image);
                DelveRouteTarget target = targets != DelveRouteTarget.None && !visited.Value
                    ? DelveTargetRoutes.Classify(name, icon?.AssetName)
                    : DelveRouteTarget.None;
                if (needsMarker && anchor is { } markerAnchor && icon != null)
                    markers.Add(new ChartMarker(rect, markerAnchor, icon, icon.GetTooltip(name), target));
                if (markersOnly)
                    continue;

                hasHiddenRoute |= hidden || behindWall;
                long[] links = planRoutes || hidden || behindWall ? ReadLinks(cell) : [];
                cells.Add(cell.Address, new ChartCell(rect, links, hidden, behindWall, anchor, feature != null,
                    visited.Value && !string.IsNullOrWhiteSpace(image), target,
                    behindWall ? ReadWallApproach(cell) : WallApproach.None));
                if (!planRoutes && !hidden && !behindWall)
                    revealedCells.Add(cell);
            }
        }

        // The chart may report a boundary link from only one side. Keep the revealed
        // endpoint's anchor so the overlay can join the game's visible route.
        if (!planRoutes && hasHiddenRoute)
        {
            var routeEntrances = new HashSet<long>();
            foreach (ChartCell cell in cells.Values)
            {
                if (cell.NeedsRouteOverlay)
                    routeEntrances.UnionWith(cell.Links);
            }

            foreach (DelveCell cell in revealedCells)
            {
                ChartCell captured = cells[cell.Address];
                long[] links = ReadLinks(cell);
                foreach (long address in links)
                {
                    if (cells.TryGetValue(address, out ChartCell target) && target.NeedsRouteOverlay)
                    {
                        routeEntrances.Add(cell.Address);
                        break;
                    }
                }

                if (!routeEntrances.Contains(cell.Address))
                    continue;

                NumVector2? anchor = ReadAnchor(cell);
                cells[cell.Address] = captured with
                {
                    Links = links,
                    Anchor = anchor,
                    HasEncounter = anchor.HasValue && ReadNamedFeature(cell, out _) != null
                };
            }
        }

        RouteLines lines = markersOnly ? new RouteLines([], [], []) : BuildRouteLines(cells, targets);
        return new ChartCapture(grid.Address, coverage, [.. markers], lines, targets, now);
    }

    private static bool TryGetLocalRect(float x, float y, float width, float height, out RectangleF rect)
    {
        rect = new RectangleF(x, y, width, height);
        return float.IsFinite(x) && float.IsFinite(y) && float.IsFinite(width) && float.IsFinite(height)
            && width > 0f && height > 0f;
    }

    // A connection can cross the viewport with its other endpoint one cell beyond the capture.
    internal static bool MayAffectCoverage(RectangleF chunk, RectangleF coverage)
        => Expand(chunk, chunk.Width / ChunkSide, chunk.Height / ChunkSide).Intersects(coverage);

    private static RectangleF Expand(RectangleF rect, float x, float y)
        => new(rect.X - x, rect.Y - y, rect.Width + 2f * x, rect.Height + 2f * y);

    private static long[] ReadLinks(DelveCell cell)
    {
        if (!DynamicAccess.TryGetDynamicValue(cell, static node => node.Connections, out object? raw)
            || raw is not IList links)
            return [];

        Span<long> targets = stackalloc long[ConnectionsPerCell];
        int count = 0;
        for (int i = 0; i < links.Count && i < ConnectionsPerCell; i++)
        {
            object? link = links[i];
            if (DynamicAccess.TryReadBool(link, static value => value.IsLinked, out bool linked) && linked
                && DynamicAccess.TryGetDynamicValue(link, static value => value.DelveCell, out object? target)
                && target is DelveCell { Address: > 0 } neighbor)
                targets[count++] = neighbor.Address;
        }

        return targets[..count].ToArray();
    }

    internal static ScreenLine[] BuildConnections(IReadOnlyDictionary<long, ChartCell> cells)
    {
        RouteLines lines = BuildRouteLines(cells, DelveRouteTarget.None);
        return [.. lines.Revealed, .. lines.FracturedWalls];
    }

    internal static RouteLines BuildRouteLines(IReadOnlyDictionary<long, ChartCell> cells, DelveRouteTarget targets)
    {
        const float tolerance = 0.75f;
        List<CellLink> links = [];
        HashSet<(long, long)> seen = [];
        foreach ((long address, ChartCell cell) in cells)
        {
            foreach (long neighbor in cell.Links)
            {
                if (neighbor == address || !cells.TryGetValue(neighbor, out ChartCell target)
                    || (targets == DelveRouteTarget.None && !(cell.NeedsRouteOverlay || target.NeedsRouteOverlay))
                    || !seen.Add(DelveTargetRoutes.LinkKey(address, neighbor)))
                    continue;

                var start = cell.Rect.Center;
                var end = target.Rect.Center;
                float dx = MathF.Abs(end.X - start.X);
                float dy = MathF.Abs(end.Y - start.Y);
                // Reject links that don't cross an edge shared by the two cells.
                if (dy <= tolerance && MathF.Abs(dx - (cell.Rect.Width + target.Rect.Width) * 0.5f) <= tolerance)
                    links.Add(start.X < end.X ? new CellLink(address, neighbor, true) : new CellLink(neighbor, address, true));
                else if (dx <= tolerance && MathF.Abs(dy - (cell.Rect.Height + target.Rect.Height) * 0.5f) <= tolerance)
                    links.Add(start.Y < end.Y ? new CellLink(address, neighbor, false) : new CellLink(neighbor, address, false));
            }
        }

        // The red wall overlay needs a revealed approach. A green route can guide the
        // player through darkness to reach the approach, so plan it on the full graph.
        HashSet<(long, long)> routes = targets == DelveRouteTarget.None
            ? []
            : DelveTargetRoutes.FindShortestRoutes(cells, links, targets);

        HashSet<(long, long)> accessibleWallLinks = FindAccessibleWallLinks(cells, links);
        links.RemoveAll(link =>
        {
            var key = DelveTargetRoutes.LinkKey(link.Start, link.End);
            if (routes.Contains(key))
                return false;

            ChartCell start = cells[link.Start];
            ChartCell end = cells[link.End];
            return start.BehindWall || end.BehindWall ? !accessibleWallLinks.Contains(key) : !(start.Hidden || end.Hidden);
        });

        if (links.Count == 0)
            return new RouteLines([], [], []);

        // IconPos can vary even on unnamed cells along one straight run.
        long[] anchors = cells.Where(static entry => entry.Value.RouteAnchor.HasValue)
            .OrderBy(static entry => entry.Value.Hidden).ThenBy(static entry => entry.Value.Rect.Y)
            .ThenBy(static entry => entry.Value.Rect.X).ThenBy(static entry => entry.Key)
            .Select(static entry => entry.Key).ToArray();
        var laneX = ResolveLanePositions(cells, links, anchors, horizontal: false);
        var laneY = ResolveLanePositions(cells, links, anchors, horizontal: true);
        List<ScreenLine> connections = new(links.Count);
        List<ScreenLine> targetLines = [];
        List<ScreenLine> wallLines = [];
        foreach (CellLink link in links)
        {
            ChartCell startCell = cells[link.Start];
            ChartCell endCell = cells[link.End];
            List<ScreenLine> lines = routes.Contains(DelveTargetRoutes.LinkKey(link.Start, link.End))
                ? targetLines
                : startCell.BehindWall || endCell.BehindWall ? wallLines : connections;
            var start = new NumVector2(laneX.GetValueOrDefault(link.Start, startCell.Rect.Center.X),
                laneY.GetValueOrDefault(link.Start, startCell.Rect.Center.Y));
            var end = new NumVector2(laneX.GetValueOrDefault(link.End, endCell.Rect.Center.X),
                laneY.GetValueOrDefault(link.End, endCell.Rect.Center.Y));

            if (start.X == end.X || start.Y == end.Y)
            {
                lines.Add(new ScreenLine(start, end));
                continue;
            }

            // Join two different recorded offsets at the shared edge, keeping each cell's run straight.
            if (link.Horizontal)
            {
                float edge = (startCell.Rect.Right + endCell.Rect.Left) * 0.5f;
                lines.Add(new ScreenLine(start, new NumVector2(edge, start.Y)));
                lines.Add(new ScreenLine(new NumVector2(edge, start.Y), new NumVector2(edge, end.Y)));
                lines.Add(new ScreenLine(new NumVector2(edge, end.Y), end));
            }
            else
            {
                float edge = (startCell.Rect.Bottom + endCell.Rect.Top) * 0.5f;
                lines.Add(new ScreenLine(start, new NumVector2(start.X, edge)));
                lines.Add(new ScreenLine(new NumVector2(start.X, edge), new NumVector2(end.X, edge)));
                lines.Add(new ScreenLine(new NumVector2(end.X, edge), end));
            }
        }

        return new RouteLines([.. connections], [.. targetLines], [.. wallLines]);
    }

    private static HashSet<(long, long)> FindAccessibleWallLinks(IReadOnlyDictionary<long, ChartCell> cells,
        List<CellLink> links)
    {
        Dictionary<long, List<long>> wallNeighbors = [];
        foreach (CellLink link in links)
        {
            AddWallNeighbor(link.Start, link.End);
            AddWallNeighbor(link.End, link.Start);
        }

        HashSet<(long, long)> accessibleLinks = [];
        HashSet<long> accessibleWalls = [];
        Queue<long> pending = new();
        foreach ((long address, List<long> adjacent) in wallNeighbors)
        {
            ChartCell wall = cells[address];
            if (wall.WallSide == WallApproach.None)
                continue;

            foreach (long neighbor in adjacent)
            {
                ChartCell approach = cells[neighbor];
                // A revealed destination behind a wall does not reveal the approach to it.
                if (approach.NeedsRouteOverlay || !IsOnWallApproachSide(wall, approach))
                    continue;

                accessibleLinks.Add(DelveTargetRoutes.LinkKey(address, neighbor));
                if (accessibleWalls.Add(address))
                    pending.Enqueue(address);
            }
        }

        HashSet<long> seen = new(accessibleWalls);
        while (pending.TryDequeue(out long address))
        {
            foreach (long neighbor in wallNeighbors[address])
            {
                ChartCell target = cells[neighbor];
                if (target.WallSide != WallApproach.None && !accessibleWalls.Contains(neighbor))
                    continue;

                accessibleLinks.Add(DelveTargetRoutes.LinkKey(address, neighbor));
                if (target.BehindWall && seen.Add(neighbor) && wallNeighbors.ContainsKey(neighbor))
                    pending.Enqueue(neighbor);
            }
        }

        return accessibleLinks;

        void AddWallNeighbor(long address, long neighbor)
        {
            if (!cells[address].BehindWall)
                return;
            if (!wallNeighbors.TryGetValue(address, out var adjacent))
                wallNeighbors.Add(address, adjacent = new List<long>(ConnectionsPerCell));
            adjacent.Add(neighbor);
        }
    }

    private static bool IsOnWallApproachSide(ChartCell wall, ChartCell approach)
    {
        var delta = approach.Rect.Center - wall.Rect.Center;
        return wall.WallSide switch
        {
            WallApproach.North => delta.Y < -MathF.Abs(delta.X),
            WallApproach.East => delta.X > MathF.Abs(delta.Y),
            WallApproach.South => delta.Y > MathF.Abs(delta.X),
            WallApproach.West => delta.X < -MathF.Abs(delta.Y),
            _ => false
        };
    }

    private static Dictionary<long, float> ResolveLanePositions(IReadOnlyDictionary<long, ChartCell> cells,
        List<CellLink> links, long[] anchors, bool horizontal)
    {
        Dictionary<long, long> previous = [];
        Dictionary<long, long> next = [];
        foreach (CellLink link in links)
        {
            if (link.Horizontal != horizontal)
                continue;
            next[link.Start] = link.End;
            previous[link.End] = link.Start;
        }

        Dictionary<long, float> positions = [];
        Queue<long> pending = new(anchors.Length);
        // Visit the nearest anchored cell first on each straight run. Give revealed anchors priority on ties.
        foreach (long address in anchors)
        {
            ChartCell cell = cells[address];
            NumVector2 anchor = cell.RouteAnchor.GetValueOrDefault();
            positions.Add(address, horizontal
                ? cell.Rect.Y + cell.Rect.Height * anchor.Y
                : cell.Rect.X + cell.Rect.Width * anchor.X);
            pending.Enqueue(address);
        }

        PropagatePositions();
        // Use a single centerline for runs without an encounter anchor. Tiny differences
        // in captured cell rectangles should never introduce bends.
        foreach (CellLink link in links)
        {
            if (link.Horizontal != horizontal)
                continue;

            var center = cells[link.Start].Rect.Center;
            if (positions.TryAdd(link.Start, horizontal ? center.Y : center.X))
            {
                pending.Enqueue(link.Start);
                PropagatePositions();
            }
        }

        return positions;

        void PropagatePositions()
        {
            while (pending.TryDequeue(out long address))
            {
                float position = positions[address];
                if (previous.TryGetValue(address, out long before) && positions.TryAdd(before, position))
                    pending.Enqueue(before);
                if (next.TryGetValue(address, out long after) && positions.TryAdd(after, position))
                    pending.Enqueue(after);
            }
        }
    }

    internal static ScreenLine[] ProjectConnections(IReadOnlyList<ScreenLine> connections, RectangleF clip, ChartProjection projection)
    {
        List<ScreenLine> visible = [];
        var clipStart = new NumVector2(clip.Left, clip.Top);
        var clipEnd = new NumVector2(clip.Right, clip.Bottom);
        foreach (ScreenLine connection in connections)
        {
            NumVector2 start = projection.Project(connection.Start);
            NumVector2 end = projection.Project(connection.End);
            NumVector2 min = NumVector2.Max(NumVector2.Min(start, end), clipStart);
            NumVector2 max = NumVector2.Min(NumVector2.Max(start, end), clipEnd);
            if (min.X <= max.X && min.Y <= max.Y && min != max)
                visible.Add(new ScreenLine(min, max));
        }

        return [.. visible];
    }

    private static NumVector2? ReadAnchor(DelveCell cell)
        => DynamicAccess.TryGetDynamicValue(cell, static node => node.IconPos, out object? raw) && raw is NumVector2 position
            ? NormalizeAnchor(position, cell.Width, cell.Height)
            : null;

    internal static NumVector2? NormalizeAnchor(NumVector2 position, float width, float height)
    {
        // These default values occur on both named and unnamed cells with no usable layout anchor.
        if (position == new NumVector2(27, 27)
            || !(position.X > 0 && position.X < width && position.Y > 0 && position.Y < height)
            || !float.IsFinite(width) || !float.IsFinite(height))
            return null;

        return new NumVector2(position.X / width, position.Y / height);
    }

    private static object? ReadNamedFeature(DelveCell cell, out string name)
    {
        name = string.Empty;
        if (!DynamicAccess.TryGetDynamicValue(cell, static node => node.Feature, out object? feature)
            || !DynamicAccess.TryReadString(feature, static value => value.Name, out name)
            || string.IsNullOrWhiteSpace(name))
            return null;

        return feature;
    }

    private static WallApproach ReadWallApproach(DelveCell cell)
    {
        if (!DynamicAccess.TryGetDynamicValue(cell, static node => node.Feature, out object? feature)
            || !DynamicAccess.TryReadString(feature, static value => value.Id, out string id))
            return WallApproach.None;

        return id switch
        {
            "ObstructionNorth" => WallApproach.North,
            "ObstructionEast" => WallApproach.East,
            "ObstructionSouth" => WallApproach.South,
            "ObstructionWest" => WallApproach.West,
            _ => WallApproach.None
        };
    }

    private static bool? ReadVisibility(DelveCell cell, Func<dynamic, object?> selector)
        => DynamicAccess.TryReadBool(cell, selector, out bool value) ? value : null;

    internal static bool IsHidden(bool? gameDraws, bool? isVisited) => gameDraws == false && isVisited == false;

    internal static Node[] BuildMarkers(ChartMarker[] markers, RectangleF clip, ChartProjection projection,
        bool revealFullMap = true, DelveRouteTarget targets = DelveRouteTarget.None)
    {
        List<Node> nodes = [];
        // At low chart zoom the stock encounter artwork shrinks faster than the tiles.
        float zoomCorrection = MathF.Sqrt(MathF.Sqrt(projection.Zoom));
        foreach (ChartMarker marker in markers)
        {
            if (!revealFullMap && (marker.Target & targets) == 0)
                continue;

            RectangleF rect = projection.Project(marker.Rect);
            var anchor = new NumVector2(rect.X + rect.Width * marker.Anchor.X,
                rect.Y + rect.Height * (marker.Anchor.Y - 0.06f) - MarkerLiftPixels);
            float radius = MathF.Min(SystemMath.Min(rect.Width, rect.Height) * 0.42f, 32f) * zoomCorrection;
            if (clip.Contains(anchor.X - radius, anchor.Y - radius) && clip.Contains(anchor.X + radius, anchor.Y + radius))
                nodes.Add(new Node(anchor, radius, marker.Icon, marker.HoverText));
        }

        return [.. nodes];
    }

    public void Draw(OverlayRenderContext ctx)
    {
        ChartSnapshot? snapshot = Volatile.Read(ref _snapshot);
        long now = Environment.TickCount64;
        if (snapshot == null || now - Volatile.Read(ref _lastCapture) > StaleViewMs)
            return;

        if (ctx.Settings.RevealDelveMap.Value)
            ctx.DrawQueue.EnqueueLines(snapshot.Connections, 2, ConnectionColor);
        if (ctx.Settings.RevealDelveFracturedWalls.Value)
            ctx.DrawQueue.EnqueueLines(snapshot.FracturedWalls, 2, ConnectionColor);
        if (ctx.Settings.ShowDelveTargetRoutes.Value)
            ctx.DrawQueue.EnqueueLines(snapshot.TargetRoutes, 2, TargetRouteColor);

        if (!ReferenceEquals(ctx.Graphics, _textureGraphics))
        {
            Dispose();
            _textureGraphics = ctx.Graphics;
        }

        int loadsRemaining = TextureLoadsPerFrame;
        string? textureError = null;
        foreach (Node node in snapshot.Nodes)
        {
            DelveNodeIcon.TextureResult texture = node.TextureFile is { IsCompletedSuccessfully: true } file
                ? file.Result
                : default;
            textureError ??= texture.Error;
            if (node.Icon.TextureName is { } name && texture.Path is { } path
                && ctx.Graphics is { } graphics && TryLoadTexture(graphics, name, path, now, ref loadsRemaining))
            {
                float diameter = node.Radius * 2f;
                ctx.DrawQueue.EnqueueImage(name, new RectangleF(node.Center.X - node.Radius,
                    node.Center.Y - node.Radius, diameter, diameter));
            }
            else
            {
                ctx.DrawQueue.EnqueueFilledScreenDisc(node.Center, SystemMath.Min(node.Radius, 4f), UnknownNodeColor, 12);
            }
        }

        if (textureError != null)
            ctx.DrawQueue.EnqueueText(textureError, new Vector2(snapshot.Clip.Center.X, snapshot.Clip.Top + 30f),
                Color.OrangeRed, 16, FontAlign.Center, shadow: true);

        if (snapshot.Nodes.Length > 0 && ctx.Graphics is { } tooltipGraphics)
        {
            SystemDrawingPoint cursor = Mouse.GetCursorPosition();
            var mouse = new NumVector2(cursor.X - ctx.WindowArea.X, cursor.Y - ctx.WindowArea.Y);
            if (snapshot.Clip.Contains(mouse.X, mouse.Y) && FindHoverText(snapshot.Nodes, mouse) is { } hoverText)
            {
                float textHeight = tooltipGraphics.MeasureText(hoverText).Y;
                (Vector2 position, FontAlign align) = PositionHoverTooltip(mouse, snapshot.Clip, textHeight);
                ctx.DrawQueue.EnqueueText(hoverText, position, Color.White, 16, align, shadow: true);
            }
        }
    }

    internal static (Vector2 Position, FontAlign Align) PositionHoverTooltip(NumVector2 cursor, RectangleF clip, float textHeight)
    {
        bool nearRight = cursor.X > clip.Center.X;
        // Leave room for the text and its shadow, even at the top edge of the chart.
        return (new Vector2(cursor.X + (nearRight ? -12f : 12f), cursor.Y - textHeight - 6f),
            nearRight ? FontAlign.Right : FontAlign.Left);
    }

    internal static string? FindHoverText(Node[] nodes, NumVector2 cursor)
    {
        float nearest = float.PositiveInfinity;
        string? name = null;
        foreach (Node node in nodes)
        {
            if (node.HoverText == null || MathF.Abs(cursor.X - node.Center.X) > node.Radius
                || MathF.Abs(cursor.Y - node.Center.Y) > node.Radius)
                continue;

            float distance = NumVector2.DistanceSquared(cursor, node.Center);
            if (distance < nearest)
            {
                nearest = distance;
                name = node.HoverText;
            }
        }
        return name;
    }

    private bool TryLoadTexture(Graphics graphics, string name, string path, long now, ref int loadsRemaining)
    {
        if (_textureLoads.TryGetValue(name, out var cached) && (cached.Available || cached.RetryAt > now))
            return cached.Available;
        if (loadsRemaining == 0)
            return false;

        loadsRemaining--;
        bool available = false;
        bool owned = false;
        try
        {
            available = graphics.HasImage(name);
            if (!available)
                available = owned = graphics.InitImage(name, path);
        }
        catch
        {
            // Retry when the graphics backend is ready.
        }
        _textureLoads[name] = (available, owned, now + TextureRetryMs);
        return available;
    }

    public void Dispose()
    {
        if (_textureGraphics is { } graphics)
        {
            foreach (var entry in _textureLoads)
            {
                if (!entry.Value.Owned)
                    continue;

                try { graphics.DisposeTexture(entry.Key); }
                catch { /* The host may have already torn down the graphics backend. */ }
            }
        }

        _textureLoads.Clear();
        _textureGraphics = null;
    }
}
