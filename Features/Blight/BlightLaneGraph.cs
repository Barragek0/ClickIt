namespace ClickIt.Features.Blight;

internal sealed record LaneGraph(
    IReadOnlyList<NumVector2> Positions,
    List<List<int>> Adjacency,
    List<(int A, int B)> Edges)
{
    internal int Count => Positions.Count;
}

internal static class BlightLaneGraph
{
    internal static LaneGraph Build(
        IReadOnlyList<NumVector2> positions,
        float connectDistance = 45f,
        float continuationCos = 0.95f,
        float directionBinWidth = 0.6f)
    {
        int n = positions.Count;
        List<List<int>> adjacency = new(n);
        for (int i = 0; i < n; i++) adjacency.Add([]);

        if (n < 2)
            return new LaneGraph([.. positions], adjacency, []);

        float connectSq = connectDistance * connectDistance;

        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                float dx = positions[i].X - positions[j].X;
                float dy = positions[i].Y - positions[j].Y;
                if ((dx * dx) + (dy * dy) > connectSq)
                    continue;
                adjacency[i].Add(j);
                adjacency[j].Add(i);
            }
        }

        List<List<(int Nearest, int NearestCont)>> bins = new(n);
        for (int i = 0; i < n; i++) bins.Add([]);

        for (int i = 0; i < n; i++)
        {
            List<int> nbrs = adjacency[i];
            List<(int Nearest, int NearestCont)> nodeBins = bins[i];
            for (int t = 0; t < nbrs.Count; t++)
            {
                int j = nbrs[t];
                float angle = MathF.Atan2(positions[j].Y - positions[i].Y, positions[j].X - positions[i].X);
                float d2 = DistSq(positions[i], positions[j]);
                bool continuation = ContinuesBeyond(i, j, positions, adjacency, continuationCos)
                    || ContinuesBeyond(j, i, positions, adjacency, continuationCos);

                int bin = -1;
                for (int b = 0; b < nodeBins.Count; b++)
                {
                    float diff = MathF.Abs(angle - BinAngle(positions, i, nodeBins[b].Nearest));
                    if (diff > MathF.PI) diff = MathF.Tau - diff;
                    if (diff <= directionBinWidth) { bin = b; break; }
                }

                if (bin >= 0)
                {
                    int nearest = nodeBins[bin].Nearest;
                    if (d2 < DistSq(positions[i], positions[nearest]))
                        nodeBins[bin] = (j, nodeBins[bin].NearestCont);
                    if (continuation)
                    {
                        int cont = nodeBins[bin].NearestCont;
                        if (cont < 0 || d2 < DistSq(positions[i], positions[cont]))
                            nodeBins[bin] = (nodeBins[bin].Nearest, j);
                    }
                }
                else
                {
                    nodeBins.Add((j, continuation ? j : -1));
                }
            }
        }

        const float TightCos = 0.95f;
        const float LooseCos = 0.9f;
        HashSet<(int A, int B)> kept = [];
        for (int i = 0; i < n; i++)
        {
            List<(int Nearest, int NearestCont)> nodeBins = bins[i];
            for (int b = 0; b < nodeBins.Count; b++)
            {
                int nearest = nodeBins[b].Nearest;
                int cont = nodeBins[b].NearestCont;
                if (nearest < 0)
                    continue;

                bool tIJ = ContinuesBeyond(i, nearest, positions, adjacency, TightCos);
                bool tJI = ContinuesBeyond(nearest, i, positions, adjacency, TightCos);
                bool lIJ = ContinuesBeyond(i, nearest, positions, adjacency, LooseCos);
                bool lJI = ContinuesBeyond(nearest, i, positions, adjacency, LooseCos);
                bool endI = IsLaneEnd(i, nearest, positions, adjacency);
                bool endJ = IsLaneEnd(nearest, i, positions, adjacency);

                bool keepNearest = adjacency[i].Count == 1 || adjacency[nearest].Count == 1
                    || (tIJ && tJI) || (tIJ && !lJI) || (tJI && !lIJ)
                    || (lIJ && endI) || (lJI && endJ);
                if (nearest > i && keepNearest)
                    kept.Add((i, nearest));

                if (cont >= 0 && cont != nearest && cont > i
                    && ContinuesBeyond(i, cont, positions, adjacency, LooseCos)
                    && !(lIJ || lJI))
                    kept.Add((i, cont));
            }
        }

        List<List<int>> result = new(n);
        for (int i = 0; i < n; i++) result.Add([]);
        List<(int A, int B)> edges = [.. kept];
        edges.Sort();
        for (int e = 0; e < edges.Count; e++)
        {
            int a = edges[e].A, b = edges[e].B;
            result[a].Add(b);
            result[b].Add(a);
        }
        for (int i = 0; i < n; i++)
            result[i].Sort();

        return new LaneGraph([.. positions], result, edges);
    }

    internal static LaneGraph BuildIdBased(
        IReadOnlyList<NumVector2> positions,
        IReadOnlyList<long> ids,
        NumVector2 pump,
        float connectDistance = 45f,
        float forkJoinDistance = 5f,
        float pumpRootRadius = 30f)
    {
        int n = positions.Count;
        List<List<int>> adjacency = new(n);
        for (int i = 0; i < n; i++) adjacency.Add([]);
        if (n < 2)
            return new LaneGraph([.. positions], adjacency, []);

        float connectSq = connectDistance * connectDistance;
        float forkSq = forkJoinDistance * forkJoinDistance;
        float pumpRadiusSq = pumpRootRadius * pumpRootRadius;

        Dictionary<long, int> indexById = new(n);
        for (int i = 0; i < n; i++)
            indexById.TryAdd(ids[i], i);

        int hubCenter = 0;
        float best = float.MaxValue;
        bool[] nearPump = new bool[n];
        for (int i = 0; i < n; i++)
        {
            float dx = positions[i].X - pump.X;
            float dy = positions[i].Y - pump.Y;
            float dSq = (dx * dx) + (dy * dy);
            nearPump[i] = dSq <= pumpRadiusSq;
            if (dSq < best) { best = dSq; hubCenter = i; }
        }

        int[] comp = new int[n];
        for (int i = 0; i < n; i++) comp[i] = i;

        HashSet<(int A, int B)> kept = [];
        for (int i = 0; i < n; i++)
        {
            // Consecutive ids within the connect distance are the same lane (id runs are the
            // game's ground truth for lane membership).
            if (indexById.TryGetValue(ids[i] + 1, out int next) && DistSq(positions[i], positions[next]) <= connectSq)
                kept.Add((SystemMath.Min(i, next), SystemMath.Max(i, next)));
            if (indexById.TryGetValue(ids[i] - 1, out int prev) && DistSq(positions[i], positions[prev]) <= connectSq)
                kept.Add((SystemMath.Min(i, prev), SystemMath.Max(i, prev)));

            for (int j = i + 1; j < n; j++)
            {
                // Shared fork/hub positions (multiple entities at one fork point).
                if (DistSq(positions[i], positions[j]) <= forkSq)
                    kept.Add((i, j));
            }
        }

        // Pump hub: lanes converge at the pump.  Each pump-near point whose id/fork component is
        // not yet joined to the hub joins the hub center — one clean edge per lane, never a
        // skip-ahead edge inside a lane that already reaches the hub.
        List<(int A, int B)> initialEdges = [.. kept];
        for (int e = 0; e < initialEdges.Count; e++)
            Union(comp, initialEdges[e].A, initialEdges[e].B);
        int hubRoot = Find(comp, hubCenter);
        for (int i = 0; i < n; i++)
        {
            if (i == hubCenter || !nearPump[i] || Find(comp, i) == hubRoot)
                continue;
            if (DistSq(positions[i], positions[hubCenter]) > connectSq)
                continue;
            kept.Add((SystemMath.Min(i, hubCenter), SystemMath.Max(i, hubCenter)));
            Union(comp, i, hubCenter);
        }

        List<(int A, int B)> edges = [.. kept];
        edges.Sort();
        List<List<int>> result = new(n);
        for (int i = 0; i < n; i++) result.Add([]);
        for (int e = 0; e < edges.Count; e++)
        {
            int a = edges[e].A, b = edges[e].B;
            result[a].Add(b);
            result[b].Add(a);
        }
        for (int i = 0; i < n; i++)
            result[i].Sort();

        return new LaneGraph([.. positions], result, edges);
    }

    private static int Find(int[] comp, int i)
    {
        while (comp[i] != i)
        {
            comp[i] = comp[comp[i]];
            i = comp[i];
        }
        return i;
    }

    private static void Union(int[] comp, int a, int b)
    {
        int ra = Find(comp, a), rb = Find(comp, b);
        if (ra != rb)
            comp[ra] = rb;
    }

    private static bool ContinuesBeyond(
        int node, int from,
        IReadOnlyList<NumVector2> positions,
        List<List<int>> adjacency,
        float continuationCos)
    {
        float inX = positions[node].X - positions[from].X;
        float inY = positions[node].Y - positions[from].Y;
        float inLenSq = (inX * inX) + (inY * inY);
        if (inLenSq <= 0.001f)
            return false;

        List<int> nbrs = adjacency[node];
        for (int t = 0; t < nbrs.Count; t++)
        {
            int k = nbrs[t];
            if (k == from)
                continue;
            float dx = positions[k].X - positions[node].X;
            float dy = positions[k].Y - positions[node].Y;
            float dLenSq = (dx * dx) + (dy * dy);
            if (dLenSq <= 0.001f)
                continue;
            float dot = (inX * dx) + (inY * dy);
            if (dot <= 0f)
                continue;
            if (dot / (MathF.Sqrt(inLenSq) * MathF.Sqrt(dLenSq)) >= continuationCos)
                return true;
        }
        return false;
    }

    private static bool IsLaneEnd(
        int node, int from,
        IReadOnlyList<NumVector2> positions,
        List<List<int>> adjacency)
    {
        float hx = positions[node].X - positions[from].X;
        float hy = positions[node].Y - positions[from].Y;
        List<int> nbrs = adjacency[node];
        for (int t = 0; t < nbrs.Count; t++)
        {
            int k = nbrs[t];
            if (k == from)
                continue;
            float dx = positions[k].X - positions[node].X;
            float dy = positions[k].Y - positions[node].Y;
            if ((dx * hx) + (dy * hy) > 0f)
                return false;
        }
        return true;
    }

    internal static int[] BuildPumpRootedParents(LaneGraph graph, NumVector2 pump, out bool[] connected)
    {
        int n = graph.Count;
        int[] parent = new int[n];
        Array.Fill(parent, -1);
        connected = new bool[n];
        if (n == 0)
            return parent;

        int root = 0;
        float best = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            float dx = graph.Positions[i].X - pump.X;
            float dy = graph.Positions[i].Y - pump.Y;
            float dSq = (dx * dx) + (dy * dy);
            if (dSq < best) { best = dSq; root = i; }
        }

        bool[] visited = new bool[n];
        visited[root] = true;
        connected[root] = true;
        Queue<int> queue = new();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            int cur = queue.Dequeue();
            List<int> nbrs = graph.Adjacency[cur];
            for (int t = 0; t < nbrs.Count; t++)
            {
                int j = nbrs[t];
                if (visited[j])
                    continue;
                visited[j] = true;
                connected[j] = true;
                parent[j] = cur;
                queue.Enqueue(j);
            }
        }
        return parent;
    }

    private static float DistSq(NumVector2 a, NumVector2 b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return (dx * dx) + (dy * dy);
    }

    private static float BinAngle(IReadOnlyList<NumVector2> positions, int i, int j)
        => MathF.Atan2(positions[j].Y - positions[i].Y, positions[j].X - positions[i].X);
}
