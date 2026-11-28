namespace ClickIt.Features.Delve;

internal static class DelveCombinedRoutePlanner
{
    // A leg may retrace at most this many already-visited tiles. More means the next
    // target is not nearby and the route ends at the previous stop instead.
    internal const int MaxVisitedTilesPerLeg = 5;

    private sealed record Leg(Leg? Previous, List<long> Path, long Target, int Encounters);
    private sealed record Tour(Leg? Last, HashSet<long> Cleared, HashSet<long> Targets, int Tiles, int Discount, int Order)
    {
        internal (int, int, int, int) Rank => (Cleared.Count, Tiles, -Discount, Order);
    }

    private readonly record struct Step(long Parent, int Encounters, int Visited, int Tiles, int Discount)
    {
        internal (int, int, int, int) Rank => (Encounters, Visited, Tiles, -Discount);
    }

    internal static void Plan(
        IReadOnlyDictionary<long, DelveRevealOverlay.ChartCell> cells,
        Dictionary<long, List<long>> neighbors,
        List<long> goals,
        long[] explored,
        HashSet<(long, long)> routes,
        Dictionary<(long, long), int> routeLegs,
        Dictionary<long, DelveTargetRoutes.Detour> stops,
        Dictionary<long, string>? counts,
        DelveRouteOptions options)
    {
        HashSet<long> seen = [];
        Queue<long> pending = new();
        HashSet<long> selected = new(goals);
        HashSet<long> group = [];
        HashSet<long> component = [];
        var origins = new List<long>();
        foreach (long goal in goals)
        {
            if (!seen.Add(goal))
                continue;
            group.Clear();
            component.Clear();
            origins.Clear();
            pending.Enqueue(goal);
            while (pending.TryDequeue(out long cell))
            {
                component.Add(cell);
                if (selected.Contains(cell))
                    group.Add(cell);
                if (neighbors.TryGetValue(cell, out var adjacent))
                    foreach (long next in adjacent)
                        if (seen.Add(next))
                            pending.Enqueue(next);
            }

            if (group.Count < 2)
                continue;
            foreach (long origin in explored)
                if (component.Contains(origin))
                    origins.Add(origin);

            Tour? best = FindTour(cells, neighbors, group, origins, options);
            if (best?.Last == null)
                continue;

            List<Leg> legs = [];
            for (Leg? leg = best.Last; leg != null; leg = leg.Previous)
                legs.Add(leg);
            legs.Reverse();
            if (legs.Count < 2)
                continue; // A lone stop adds nothing its direct route does not already show.
            for (int i = 0; i < legs.Count; i++)
            {
                Leg leg = legs[i];
                for (int j = 1; j < leg.Path.Count; j++)
                {
                    var edge = DelveTargetRoutes.LinkKey(leg.Path[j - 1], leg.Path[j]);
                    routes.Add(edge);
                    // A retraced edge retains the colour of the first leg that uses it.
                    routeLegs.TryAdd(edge, i);
                }
                stops.Add(leg.Target, new DelveTargetRoutes.Detour(i + 1));
                counts?.Add(leg.Target, leg.Encounters.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }
    }

    private static Tour? FindTour(
        IReadOnlyDictionary<long, DelveRevealOverlay.ChartCell> cells,
        Dictionary<long, List<long>> neighbors,
        HashSet<long> goals,
        List<long> origins,
        DelveRouteOptions options)
    {
        // The cost of a leg depends on every encounter cleared by the earlier legs.
        // Compare more stop orders for small groups and bound the work on large charts.
        int width = goals.Count <= 4 ? 24 : goals.Count <= 8 ? 6 : goals.Count <= 16 ? 2 : 1;
        List<Tour> tours = [new(null, [], [], 0, 0, 0)];
        List<Tour> nextTours = [];
        Dictionary<long, Step> previous = [];
        PriorityQueue<long, ((int, int, int, int) Cost, int Order)> pending = new();
        int order = 0;
        Tour? best = null;
        for (int stop = 0; stop < goals.Count; stop++)
        {
            nextTours.Clear();
            foreach (Tour tour in tours)
            {
                previous.Clear();
                pending.Clear();
                int sequence = 0;
                if (tour.Last != null)
                    AddStart(tour.Last.Target);
                else
                    foreach (long origin in origins)
                        AddStart(origin);

                int found = 0;
                while (pending.TryDequeue(out long cell, out var rank))
                {
                    Step step = previous[cell];
                    if (rank.Cost != step.Rank)
                        continue;
                    if (goals.Contains(cell) && !tour.Targets.Contains(cell))
                    {
                        int encounters = tour.Cleared.Count + step.Encounters;
                        int tiles = tour.Tiles + step.Tiles;
                        int discount = tour.Discount + step.Discount;
                        if (nextTours.Count < width || (encounters, tiles, -discount, order).CompareTo(nextTours[^1].Rank) < 0)
                        {
                            List<long> path = [];
                            HashSet<long> cleared = new(tour.Cleared);
                            for (long node = cell; ; node = previous[node].Parent)
                            {
                                path.Add(node);
                                if (DelveTargetRoutes.IsUnfinished(cells[node]))
                                    cleared.Add(node);
                                if (node == previous[node].Parent)
                                    break;
                            }
                            path.Reverse();
                            HashSet<long> visited = new(tour.Targets) { cell };
                            nextTours.Add(new Tour(new Leg(tour.Last, path, cell, encounters), cleared, visited, tiles, discount, order++));
                            nextTours.Sort(static (left, right) => left.Rank.CompareTo(right.Rank));
                            if (nextTours.Count > width)
                                nextTours.RemoveAt(width);
                        }
                        if (++found >= width)
                            break;
                        continue; // Passing an unfinished target makes it the next stop.
                    }

                    if (!neighbors.TryGetValue(cell, out var adjacent))
                        continue;
                    foreach (long next in adjacent)
                    {
                        int backtrack = step.Visited + (cells[next].Visited ? 1 : 0);
                        if (backtrack > MaxVisitedTilesPerLeg)
                            continue;
                        bool unfinished = DelveTargetRoutes.IsUnfinished(cells[next]) && !tour.Cleared.Contains(next);
                        Step candidate = new(cell, step.Encounters + (unfinished ? 1 : 0), backtrack, step.Tiles + 1,
                            step.Discount + (unfinished ? options.Discount(cells[next].EncounterKind) : 0));
                        if (previous.TryGetValue(next, out Step existing) && existing.Rank.CompareTo(candidate.Rank) <= 0)
                            continue;
                        previous[next] = candidate;
                        pending.Enqueue(next, (candidate.Rank, sequence++));
                    }
                }

                void AddStart(long origin)
                {
                    Step start = new(origin, 0, 0, 0, 0);
                    previous.Add(origin, start);
                    pending.Enqueue(origin, (start.Rank, sequence++));
                }
            }

            if (nextTours.Count == 0)
                break; // No remaining goal is reachable within the backtrack budget.
            best = nextTours[0];
            (tours, nextTours) = (nextTours, tours);
        }
        return best;
    }
}
