namespace ClickIt.Features.Delve;

internal sealed class DelveRevealOverlay : IOverlay
{
    private const int RefreshMs = 33;
    private const int CellRefreshMs = 300;
    private const int MaxChunks = 128;
    private const int ChunkSide = 8;
    private const int CellsPerChunk = ChunkSide * ChunkSide;
    private const int ConnectionsPerCell = 4;
    private const int TextureLoadsPerFrame = 4;
    private const int TextureRetryMs = 1000;
    private const float MarkerLiftPixels = 2f;
    private static readonly Color ConnectionColor = new(242, 54, 62, 230);
    private static readonly Color UnknownNodeColor = new(230, 215, 190, 240);
    private ChartSnapshot? _snapshot;
    private ChartCapture? _chartCapture;
    private RectangleF _lastGrid;
    private RectangleF _lastClip;
    private long _lastCapture;
    private Graphics? _textureGraphics;
    private readonly Dictionary<string, (bool Available, long RetryAt)> _textureLoads = new(StringComparer.Ordinal);

    internal readonly record struct ChartCell(RectangleF Rect, long[] Links, bool Hidden = true, bool BehindWall = false,
        NumVector2? Anchor = null);
    private readonly record struct CellLink(long Start, long End, bool Horizontal);
    internal readonly record struct ChartMarker(RectangleF Rect, NumVector2 Anchor, DelveNodeIcon Icon);
    internal readonly record struct Node(NumVector2 Center, float Radius, DelveNodeIcon Icon);
    private sealed record ChartSnapshot(ScreenLine[] Connections, Node[] Nodes, RectangleF Clip);
    private sealed record ChartCapture(long Address, RectangleF Grid, RectangleF Coverage,
        ChartMarker[] Markers, ScreenLine[] Connections, long CapturedAt);

    internal readonly record struct ChartProjection(RectangleF OriginalGrid, RectangleF CurrentGrid)
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
    public bool IsEnabled(ClickItSettings settings) => settings.RevealDelveMap.Value;

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

        SubterraneanChart? chart = ctx.GameController?.IngameState?.IngameUi?.DelveWindow;
        if (chart is not { IsValid: true, IsVisible: true }
            || chart.ChildCount == 0
            || chart.GetChildAtIndex(0) is not { IsValid: true, IsVisible: true } viewport
            || !LabelGeometry.TryGetElementRect(viewport, out RectangleF chartBounds)
            || chart.GridElement is not { IsValid: true, Address: > 0 } grid
            || !LabelGeometry.TryGetElementRect(grid, out RectangleF gridBounds)
            || !TryGetScreenClip(chartBounds, ctx.WindowArea, out RectangleF clip))
        {
            ClearChart();
            return;
        }

        long now = Environment.TickCount64;
        ChartCapture? capture = _chartCapture;
        if (capture == null || capture.Address != grid.Address || now - capture.CapturedAt >= CellRefreshMs
            || !new ChartProjection(capture.Grid, gridBounds).Covers(capture.Coverage, clip))
        {
            RectangleF coverage = Expand(clip, clip.Width * 0.25f, clip.Height * 0.25f);
            capture = CaptureChart(grid, gridBounds, coverage, now);
            _chartCapture = capture;
        }
        else if (_lastGrid == gridBounds && _lastClip == clip && _snapshot != null)
        {
            Volatile.Write(ref _lastCapture, now);
            return;
        }

        _lastGrid = gridBounds;
        _lastClip = clip;
        var projection = new ChartProjection(capture.Grid, gridBounds);
        Node[] nodes = BuildMarkers(capture.Markers, clip, projection);
        foreach (Node node in nodes)
            node.Icon.Prepare();

        Volatile.Write(ref _snapshot, new ChartSnapshot(
            ProjectConnections(capture.Connections, clip, projection), nodes, clip));
        Volatile.Write(ref _lastCapture, now);
    }

    private void ClearChart()
    {
        _chartCapture = null;
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

    private static ChartCapture CaptureChart(DelveElement grid, RectangleF gridBounds, RectangleF coverage, long now)
    {
        if (!DynamicAccess.TryGetDynamicValue(grid, static element => element.Cells, out object? rawChunks)
            || rawChunks is not IList chunks)
            return new ChartCapture(grid.Address, gridBounds, coverage, [], [], now);

        Dictionary<long, ChartCell> cells = [];
        List<ChartMarker> markers = [];
        List<DelveCell> revealedCells = [];
        bool hasWall = false;
        for (int i = 0; i < chunks.Count && i < MaxChunks; i++)
        {
            if (chunks[i] is not DelveBigCell chunkElement
                || (LabelGeometry.TryGetElementRect(chunkElement, out RectangleF chunkRect) && !MayAffectCoverage(chunkRect, coverage))
                || !DynamicAccess.TryGetDynamicValue(chunkElement, static chunk => chunk.Cells, out object? rawCells)
                || rawCells is not IList chunk)
                continue;

            for (int j = 0; j < chunk.Count && j < CellsPerChunk; j++)
            {
                if (chunk[j] is not DelveCell { IsValid: true } cell || cell.Address == 0
                    || !LabelGeometry.TryGetElementRect(cell, out RectangleF rect)
                    || !Expand(coverage, rect.Width, rect.Height).Intersects(rect)
                    || cells.ContainsKey(cell.Address))
                    continue;

                bool? gameDraws = ReadVisibility(cell, static node => node.GameDraws);
                bool? visited = ReadVisibility(cell, static node => node.IsVisited);
                if (!gameDraws.HasValue || !visited.HasValue)
                    continue;

                bool hidden = IsHidden(gameDraws, visited);
                // The game can show a destination icon before it shows the route behind the wall.
                bool behindWall = !visited.Value && ReadVisibility(cell, static node => node.IsBehindWall) == true;
                hasWall |= behindWall;
                long[] links = hidden || behindWall ? ReadLinks(cell) : [];
                bool needsMarker = hidden && coverage.Intersects(rect);
                NumVector2? anchor = hidden || behindWall ? ReadAnchor(cell) : null;
                object? feature = anchor.HasValue && (needsMarker || anchor == new NumVector2(0.5f))
                    ? ReadNamedFeature(cell)
                    : null;
                cells.Add(cell.Address, new ChartCell(rect, links, hidden, behindWall, RouteAnchor(anchor, feature)));
                if (!hidden && !behindWall)
                    revealedCells.Add(cell);
                if (needsMarker && anchor is { } markerAnchor && feature != null)
                    markers.Add(new ChartMarker(rect, markerAnchor, ReadIcon(feature)));
            }
        }

        // A wall entrance can be listed from the revealed side of the connection.
        if (hasWall)
        {
            var wallEntrances = new HashSet<long>();
            foreach (ChartCell cell in cells.Values)
            {
                if (cell.BehindWall)
                    wallEntrances.UnionWith(cell.Links);
            }

            foreach (DelveCell cell in revealedCells)
            {
                ChartCell captured = cells[cell.Address];
                long[] links = ReadLinks(cell);
                foreach (long address in links)
                {
                    if (cells.TryGetValue(address, out ChartCell target) && target.BehindWall)
                    {
                        wallEntrances.Add(cell.Address);
                        break;
                    }
                }

                if (!wallEntrances.Contains(cell.Address))
                    continue;

                cells[cell.Address] = captured with { Links = links, Anchor = ReadAnchor(cell) };
            }
        }

        return new ChartCapture(grid.Address, gridBounds, coverage, [.. markers], BuildConnections(cells), now);
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
        const float tolerance = 0.75f;
        List<CellLink> links = [];
        HashSet<(long, long)> seen = [];
        foreach ((long address, ChartCell cell) in cells)
        {
            foreach (long neighbor in cell.Links)
            {
                if (neighbor == address || !cells.TryGetValue(neighbor, out ChartCell target)
                    || !(cell.BehindWall || target.BehindWall || (cell.Hidden && target.Hidden))
                    || !seen.Add((SystemMath.Min(address, neighbor), SystemMath.Max(address, neighbor))))
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

        if (links.Count == 0)
            return [];

        long[] anchors = cells.Where(static entry => entry.Value.Anchor.HasValue)
            .OrderBy(static entry => entry.Value.Hidden).ThenBy(static entry => entry.Key)
            .Select(static entry => entry.Key).ToArray();
        var laneX = ResolveLanePositions(cells, links, anchors, horizontal: false);
        var laneY = ResolveLanePositions(cells, links, anchors, horizontal: true);
        List<ScreenLine> connections = new(links.Count);
        foreach (CellLink link in links)
        {
            ChartCell startCell = cells[link.Start];
            ChartCell endCell = cells[link.End];
            var start = new NumVector2(laneX.GetValueOrDefault(link.Start, startCell.Rect.Center.X),
                laneY.GetValueOrDefault(link.Start, startCell.Rect.Center.Y));
            var end = new NumVector2(laneX.GetValueOrDefault(link.End, endCell.Rect.Center.X),
                laneY.GetValueOrDefault(link.End, endCell.Rect.Center.Y));

            if (start.X == end.X || start.Y == end.Y)
            {
                connections.Add(new ScreenLine(start, end));
                continue;
            }

            // Join two different recorded offsets at the shared edge, keeping each cell's run straight.
            if (link.Horizontal)
            {
                float edge = (startCell.Rect.Right + endCell.Rect.Left) * 0.5f;
                connections.Add(new ScreenLine(start, new NumVector2(edge, start.Y)));
                connections.Add(new ScreenLine(new NumVector2(edge, start.Y), new NumVector2(edge, end.Y)));
                connections.Add(new ScreenLine(new NumVector2(edge, end.Y), end));
            }
            else
            {
                float edge = (startCell.Rect.Bottom + endCell.Rect.Top) * 0.5f;
                connections.Add(new ScreenLine(start, new NumVector2(start.X, edge)));
                connections.Add(new ScreenLine(new NumVector2(start.X, edge), new NumVector2(end.X, edge)));
                connections.Add(new ScreenLine(new NumVector2(end.X, edge), end));
            }
        }

        return [.. connections];
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
            NumVector2 anchor = cell.Anchor.GetValueOrDefault();
            positions.Add(address, horizontal
                ? cell.Rect.Y + cell.Rect.Height * anchor.Y
                : cell.Rect.X + cell.Rect.Width * anchor.X);
            pending.Enqueue(address);
        }

        while (pending.TryDequeue(out long address))
        {
            float position = positions[address];
            if (previous.TryGetValue(address, out long before) && positions.TryAdd(before, position))
                pending.Enqueue(before);
            if (next.TryGetValue(address, out long after) && positions.TryAdd(after, position))
                pending.Enqueue(after);
        }

        return positions;
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

    // Unnamed cells behind walls often report the exact tile center as a placeholder.
    private static NumVector2? RouteAnchor(NumVector2? anchor, object? feature)
        => anchor == new NumVector2(0.5f) && feature == null ? null : anchor;

    private static object? ReadNamedFeature(DelveCell cell)
    {
        if (!DynamicAccess.TryGetDynamicValue(cell, static node => node.Feature, out object? feature)
            || !DynamicAccess.TryReadString(feature, static value => value.Name, out string name)
            || string.IsNullOrWhiteSpace(name))
            return null;

        return feature;
    }

    private static DelveNodeIcon ReadIcon(object feature)
    {
        DynamicAccess.TryReadString(feature, static value => value.Image, out string image);
        return DelveNodeIcon.Resolve(image);
    }

    private static bool? ReadVisibility(DelveCell cell, Func<dynamic, object?> selector)
        => DynamicAccess.TryReadBool(cell, selector, out bool value) ? value : null;

    internal static bool IsHidden(bool? gameDraws, bool? isVisited) => gameDraws == false && isVisited == false;

    internal static Node[] BuildMarkers(ChartMarker[] markers, RectangleF clip, ChartProjection projection)
    {
        List<Node> nodes = [];
        foreach (ChartMarker marker in markers)
        {
            RectangleF rect = projection.Project(marker.Rect);
            var anchor = new NumVector2(rect.X + rect.Width * marker.Anchor.X,
                rect.Y + rect.Height * (marker.Anchor.Y - 0.06f) - MarkerLiftPixels);
            float radius = SystemMath.Clamp(SystemMath.Min(rect.Width, rect.Height) * 0.42f, 5f, 32f);
            if (clip.Contains(anchor.X - radius, anchor.Y - radius) && clip.Contains(anchor.X + radius, anchor.Y + radius))
                nodes.Add(new Node(anchor, radius, marker.Icon));
        }

        return [.. nodes];
    }

    public void Draw(OverlayRenderContext ctx)
    {
        ChartSnapshot? snapshot = Volatile.Read(ref _snapshot);
        long now = Environment.TickCount64;
        if (snapshot == null || now - Volatile.Read(ref _lastCapture) > RefreshMs * 8)
            return;

        ctx.DrawQueue.EnqueueLines(snapshot.Connections, 2, ConnectionColor);

        if (!ReferenceEquals(ctx.Graphics, _textureGraphics))
        {
            _textureGraphics = ctx.Graphics;
            _textureLoads.Clear();
        }

        int loadsRemaining = TextureLoadsPerFrame;
        string? textureError = null;
        foreach (Node node in snapshot.Nodes)
        {
            DelveNodeIcon.TextureResult texture = node.Icon.Texture;
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
    }

    private bool TryLoadTexture(Graphics graphics, string name, string path, long now, ref int loadsRemaining)
    {
        if (_textureLoads.TryGetValue(name, out var cached) && (cached.Available || cached.RetryAt > now))
            return cached.Available;
        if (loadsRemaining == 0)
            return false;

        loadsRemaining--;
        bool available;
        try { available = graphics.HasImage(name) || graphics.InitImage(name, path); }
        catch { available = false; }
        _textureLoads[name] = (available, now + TextureRetryMs);
        return available;
    }
}
