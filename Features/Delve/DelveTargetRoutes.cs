namespace ClickIt.Features.Delve;

[Flags]
internal enum DelveRouteTarget
{
    None = 0,
    Aul = 1,
    Lich = 2,
    Architect = 4,
    CrystalSpire = 8,
    MoltenCavity = 16,
    HumidFissure = 32,
    MinionAuraItems = 64,
    TimeLostCavern = 128,
    StonewoodHollow = 256,
    HauntedTomb = 512,
    SmugglersStash = 1024,
    Azurite1 = 2048,
    Azurite2 = 4096,
    Azurite3 = 8192
}

internal readonly record struct DelveRouteOptions(int PriorityOrder, int DetourLimit)
{
    internal const int DefaultDetourLimit = 3;
    internal const int MinDetourLimit = 1;
    internal const int MaxDetourLimit = 5;

    // Keep the original encounter names as settings keys so stored order and checkboxes survive label changes.
    internal static readonly IReadOnlyList<string> FastEncounters =
        ["Primeval Chamber", "Abyssal Chamber", "Ruined Chamber", "Azurite Cavity", "Azurite Vault"];

    internal static IEnumerable<string> GetPriorityOrder(ClickItSettings settings)
    {
        int order = GetPackedOrder(settings);
        for (int shift = (FastEncounters.Count - 1) * 4; shift >= 0; shift -= 4)
            yield return FastEncounters[((order >> shift) & 15) - 1];
    }

    internal static DelveRouteOptions FromSettings(ClickItSettings settings)
    {
        int priorities = 0;
        if (settings.EnabledDelveEncounterPriorities is { Count: > 0 } enabled)
        {
            int order = GetPackedOrder(settings);
            for (int shift = (FastEncounters.Count - 1) * 4; shift >= 0; shift -= 4)
            {
                int kind = (order >> shift) & 15;
                if (enabled.Contains(FastEncounters[kind - 1]))
                    priorities = (priorities << 4) | kind;
            }
        }

        return new(priorities, settings.IncludeNearbyDelveTargets.Value
            ? Math.Clamp(settings.DelveTargetDetourLimit.Value, MinDetourLimit, MaxDetourLimit) : 0);
    }

    private static int GetPackedOrder(ClickItSettings settings)
    {
        int seen = 0;
        int order = 0;
        if (settings.DelveEncounterPriorityOrder is { } saved)
            foreach (string name in saved)
                Append(Classify(name));
        for (int kind = 1; kind <= FastEncounters.Count; kind++)
            Append(kind);
        return order;

        void Append(int kind)
        {
            if (kind == 0 || (seen & (1 << kind)) != 0)
                return;
            seen |= 1 << kind;
            order = (order << 4) | kind;
        }
    }

    internal static string PriorityDisplayName(string key) => key switch
    {
        "Primeval Chamber" => "Primeval Ruins",
        "Abyssal Chamber" => "Abyssal City",
        "Ruined Chamber" => "Vaal Outpost",
        "Azurite Cavity" => "Azurite I (Cavity)",
        "Azurite Vault" => "Azurite II (Vault)",
        _ => key
    };

    internal static int Classify(string name, string? asset = null) => asset switch
    {
        "ProtoVaalCity" => 1,
        "AbyssalCity" => 2,
        "VaalCity" => 3,
        "AzuriteVein" => 4,
        "AzuriteVeinII" => 5,
        "BossProtoVaal" or "BossAbyss" or "BossVaalCity" or "AzuriteVeinIII" => 0,
        _ => name switch
        {
            "Primeval Chamber" or "Primeval Ruins" => 1,
            "Abyssal Chamber" or "Abyssal City" => 2,
            "Ruined Chamber" or "Vaal Outpost" => 3,
            "Azurite Cavity" => 4,
            "Azurite Vault" => 5,
            _ => 0
        }
    };

    internal UInt128 Preference(int kind)
    {
        if (kind == 0)
            return 0;
        // Five counters fit in UInt128. Each counter has room for every loaded chart cell.
        for (int order = PriorityOrder, shift = 0; order != 0; order >>= 4, shift += 24)
            if ((order & 15) == kind)
                return (UInt128)1 << shift;
        return 0;
    }
}

internal static class DelveTargetRoutes
{
    private readonly record struct RouteStep(long Parent, int Tiles, int RemainingNodes, UInt128 Preference);
    internal readonly record struct Detour(int Stop, string? OtherStop, int ExtraEncounters, bool IsFinal = false);
    private readonly record struct RouteSearchStep(long Cell, int Parent, int Tiles, int Encounters, int Targets);
    private readonly record struct RouteSplice(int From, int To, long[] Connector, int Targets, int Encounters, int Tiles);

    internal static DelveRouteTarget GetSelectedTargets(ClickItSettings settings)
        => settings.ShowDelveTargetRoutes.Value
            ? (settings.TargetDelveAul.Value ? DelveRouteTarget.Aul : DelveRouteTarget.None)
                | (settings.TargetDelveLich.Value ? DelveRouteTarget.Lich : DelveRouteTarget.None)
                | (settings.TargetDelveArchitect.Value ? DelveRouteTarget.Architect : DelveRouteTarget.None)
                | (settings.TargetDelveCrystalSpire.Value ? DelveRouteTarget.CrystalSpire : DelveRouteTarget.None)
                | (settings.TargetDelveMoltenCavity.Value ? DelveRouteTarget.MoltenCavity : DelveRouteTarget.None)
                | (settings.TargetDelveHumidFissure.Value ? DelveRouteTarget.HumidFissure : DelveRouteTarget.None)
                | (settings.TargetDelveMinionAuraItems.Value ? DelveRouteTarget.MinionAuraItems : DelveRouteTarget.None)
                | (settings.TargetDelveTimeLostCavern.Value ? DelveRouteTarget.TimeLostCavern : DelveRouteTarget.None)
                | (settings.TargetDelveStonewoodHollow.Value ? DelveRouteTarget.StonewoodHollow : DelveRouteTarget.None)
                | (settings.TargetDelveHauntedTomb.Value ? DelveRouteTarget.HauntedTomb : DelveRouteTarget.None)
                | (settings.TargetDelveSmugglersStash.Value ? DelveRouteTarget.SmugglersStash : DelveRouteTarget.None)
                | (settings.TargetDelveAzurite1.Value ? DelveRouteTarget.Azurite1 : DelveRouteTarget.None)
                | (settings.TargetDelveAzurite2.Value ? DelveRouteTarget.Azurite2 : DelveRouteTarget.None)
                | (settings.TargetDelveAzurite3.Value ? DelveRouteTarget.Azurite3 : DelveRouteTarget.None)
            : DelveRouteTarget.None;

    internal static DelveRouteTarget Classify(string name, string? asset)
        => (asset, name) switch
        {
            ("BossProtoVaal", _) => DelveRouteTarget.Aul,
            ("BossAbyss", _) => DelveRouteTarget.Lich,
            ("BossVaalCity", _) => DelveRouteTarget.Architect,
            ("Minion", _) => DelveRouteTarget.MinionAuraItems,
            ("AzuriteVein", _) => DelveRouteTarget.Azurite1,
            ("AzuriteVeinII", _) => DelveRouteTarget.Azurite2,
            ("AzuriteVeinIII", _) => DelveRouteTarget.Azurite3,
            (_, "Crystal Spire") => DelveRouteTarget.CrystalSpire,
            (_, "Molten Cavity") => DelveRouteTarget.MoltenCavity,
            (_, "Humid Fissure") => DelveRouteTarget.HumidFissure,
            (_, "Time-Lost Cavern") => DelveRouteTarget.TimeLostCavern,
            (_, "Stonewood Hollow") => DelveRouteTarget.StonewoodHollow,
            (_, "Haunted Tomb") => DelveRouteTarget.HauntedTomb,
            ("Fossil", "Smuggler's Stash") => DelveRouteTarget.SmugglersStash,
            _ => DelveRouteTarget.None
        };

    internal static HashSet<(long, long)> FindShortestRoutes(
        IReadOnlyDictionary<long, DelveRevealOverlay.ChartCell> cells,
        List<DelveRevealOverlay.CellLink> links,
        DelveRouteTarget targets,
        Dictionary<long, string>? counts,
        DelveRouteOptions options = default,
        Dictionary<long, Detour>? detours = null,
        HashSet<(long, long)>? detourLinks = null)
    {
        HashSet<(long, long)> routes = [];
        List<long> goals = [];
        List<(long Address, float Y, float X)> starts = [];
        foreach ((long address, var cell) in cells)
        {
            if (cell.Explored)
                starts.Add((address, cell.Rect.Y, cell.Rect.X));
            else if ((cell.Target & targets) != 0)
                goals.Add(address);
        }
        if (goals.Count == 0 || starts.Count == 0)
            return routes;

        starts.Sort(static (a, b) =>
        {
            int order = a.Y.CompareTo(b.Y);
            if (order == 0) order = a.X.CompareTo(b.X);
            return order == 0 ? a.Address.CompareTo(b.Address) : order;
        });
        Dictionary<long, RouteStep> previous = [];
        Queue<long> pending = new(starts.Count);
        for (int i = 0; i < starts.Count; i++)
        {
            long address = starts[i].Address;
            previous.Add(address, new RouteStep(address, 0, 0, 0));
            pending.Enqueue(address);
        }

        Dictionary<long, List<long>> neighbors = [];
        for (int i = 0; i < links.Count; i++)
        {
            var link = links[i];
            AddNeighbor(link.Start, link.End);
            AddNeighbor(link.End, link.Start);
        }

        // Chunk traversal order and addresses can change when the chart streams new cells.
        // Use chart positions so equal-length paths keep the same preference.
        Comparison<long> comparePosition = (left, right) =>
        {
            RectangleF a = cells[left].Rect;
            RectangleF b = cells[right].Rect;
            int rows = a.Y.CompareTo(b.Y);
            return rows != 0 ? rows : a.X.CompareTo(b.X);
        };
        foreach (List<long> adjacent in neighbors.Values)
            adjacent.Sort(comparePosition);

        // Every confirmed shared-edge connection advances one tile. Searching from all
        // explored encounters together finds the closest one by route distance for each goal.
        int unresolvedGoals = goals.Count;
        bool usePriorities = options.PriorityOrder != 0;
        bool countEncounters = counts != null || usePriorities || detours != null;
        int furthestGoal = int.MaxValue;
        while (pending.TryDequeue(out long address))
        {
            RouteStep parent = previous[address];
            if (parent.Tiles >= furthestGoal)
                break;
            if (!neighbors.TryGetValue(address, out var adjacent))
                continue;

            for (int i = 0; i < adjacent.Count; i++)
            {
                long next = adjacent[i];
                bool discovered = previous.TryGetValue(next, out RouteStep existing);
                if (discovered && (!usePriorities || existing.Tiles != parent.Tiles + 1))
                    continue;

                var cell = cells[next];
                bool unfinished = countEncounters && IsUnfinished(cell);
                RouteStep candidate = new(address, parent.Tiles + 1, parent.RemainingNodes + (unfinished ? 1 : 0),
                    parent.Preference + (unfinished && usePriorities ? options.Preference(cell.EncounterKind) : 0));
                if (discovered)
                {
                    // All parents at this distance are processed before the child is dequeued.
                    if (candidate.RemainingNodes < existing.RemainingNodes
                        || (candidate.RemainingNodes == existing.RemainingNodes && candidate.Preference > existing.Preference))
                        previous[next] = candidate;
                    continue;
                }

                previous.Add(next, candidate);
                if ((cell.Target & targets) != 0 && --unresolvedGoals == 0)
                {
                    if (!usePriorities)
                        break;
                    furthestGoal = candidate.Tiles;
                }
                pending.Enqueue(next);
            }
            if (unresolvedGoals == 0 && !usePriorities)
                break;
        }

        Dictionary<long, List<long>>? planned = detours != null && options.DetourLimit > 0 && goals.Count > 1
            ? PlanDetours(cells, neighbors, goals, previous, options.DetourLimit, detours)
            : null;
        // A connector may contain a direct edge without containing the rest of its path.
        HashSet<(long, long)> directEdges = planned is { Count: > 0 } ? [] : routes;

        for (int i = 0; i < goals.Count; i++)
        {
            long goal = goals[i];
            if (!previous.TryGetValue(goal, out var step))
                continue;

            if (planned != null && planned.TryGetValue(goal, out var plan))
            {
                HashSet<(long, long)> direct = [];
                for (long cell = goal; previous[cell].Parent != cell; cell = previous[cell].Parent)
                    direct.Add(LinkKey(cell, previous[cell].Parent));
                int remaining = 0;
                for (int index = 1; index < plan.Count; index++)
                {
                    long cell = plan[index];
                    if (IsUnfinished(cells[cell]))
                        remaining++;
                    if (detours!.ContainsKey(cell))
                        WriteCount(cell, remaining);

                    var link = LinkKey(plan[index - 1], cell);
                    routes.Add(link);
                    if (!direct.Contains(link))
                        detourLinks?.Add(link);
                }
                continue;
            }

            if (detours?.ContainsKey(goal) == true)
                continue; // The combined route supplies this stop's path and count.

            WriteCount(goal, step.RemainingNodes);
            long address = goal;
            while (step.Parent != address)
            {
                var link = LinkKey(step.Parent, address);
                if (!directEdges.Add(link))
                    break;
                if (!ReferenceEquals(directEdges, routes))
                    routes.Add(link);
                address = step.Parent;
                step = previous[address];
            }
        }

        return routes;

        void WriteCount(long goal, int count)
            => counts?.Add(goal, count.ToString(System.Globalization.CultureInfo.InvariantCulture));

        void AddNeighbor(long first, long second)
        {
            if (!neighbors.TryGetValue(first, out var adjacent))
                neighbors.Add(first, adjacent = new List<long>(4));
            adjacent.Add(second);
        }
    }

    private static bool IsUnfinished(DelveRevealOverlay.ChartCell cell)
        => cell is { HasEncounter: true, Visited: false, Explored: false };

    private static Dictionary<long, List<long>> PlanDetours(
        IReadOnlyDictionary<long, DelveRevealOverlay.ChartCell> cells,
        Dictionary<long, List<long>> neighbors, List<long> goals,
        Dictionary<long, RouteStep> previous, int limit, Dictionary<long, Detour> stops)
    {
        const int maxExtraTiles = 12;
        const int maxConnectorTiles = 12;
        Dictionary<long, List<long>> result = [];
        var reachable = goals.FindAll(previous.ContainsKey);
        reachable.Sort((a, b) =>
        {
            int order = previous[b].Tiles.CompareTo(previous[a].Tiles);
            if (order == 0)
                order = cells[a].Rect.Y.CompareTo(cells[b].Rect.Y);
            if (order == 0)
                order = cells[a].Rect.X.CompareTo(cells[b].Rect.X);
            return order == 0 ? a.CompareTo(b) : order;
        });

        HashSet<long> goalCells = new(reachable);
        Dictionary<long, int> routeIndex = [];
        Dictionary<long, int> distanceToTarget = [];
        Queue<long> frontier = new();
        List<RouteSearchStep> search = [];
        HashSet<(long Cell, int Encounters, int Targets)> seen = [];
        List<(int Encounters, int Targets)> prefix = [];
        List<long> routeGoals = [];

        foreach (long destination in reachable)
        {
            if (stops.ContainsKey(destination))
                continue;

            RouteStep direct = previous[destination];
            List<long> route = new(direct.Tiles + 1);
            for (long cell = destination; ; cell = previous[cell].Parent)
            {
                route.Add(cell);
                if (previous[cell].Parent == cell)
                    break;
            }
            route.Reverse();
            if (route.Exists(stops.ContainsKey))
                continue;
            bool changed = false;
            while (true)
            {
                routeIndex.Clear();
                prefix.Clear();
                int encounters = 0;
                int targets = 0;
                for (int i = 0; i < route.Count; i++)
                {
                    long cell = route[i];
                    routeIndex.Add(cell, i);
                    if (IsUnfinished(cells[cell]))
                        encounters++;
                    if (goalCells.Contains(cell) && !stops.ContainsKey(cell))
                        targets++;
                    prefix.Add((encounters, targets));
                }

                // Only explore branches close enough to an unclaimed target to reach it within the connector cap.
                distanceToTarget.Clear();
                frontier.Clear();
                foreach (long goal in reachable)
                    if (!routeIndex.ContainsKey(goal) && !stops.ContainsKey(goal))
                    {
                        frontier.Enqueue(goal);
                        distanceToTarget.Add(goal, 0);
                    }
                while (frontier.TryDequeue(out long cell))
                {
                    int nextDistance = distanceToTarget[cell] + 1;
                    if (nextDistance >= maxConnectorTiles || !neighbors.TryGetValue(cell, out var adjacent))
                        continue;
                    foreach (long next in adjacent)
                        if (!routeIndex.ContainsKey(next) && !cells[next].Explored && distanceToTarget.TryAdd(next, nextDistance))
                            frontier.Enqueue(next);
                }

                RouteSplice? best = null;
                for (int from = 0; from + 1 < route.Count; from++)
                {
                    if (!neighbors.TryGetValue(route[from], out var adjacent))
                        continue;
                    bool canReachTarget = false;
                    foreach (long next in adjacent)
                        if (distanceToTarget.ContainsKey(next))
                        {
                            canReachTarget = true;
                            break;
                        }
                    if (!canReachTarget)
                        continue;

                    search.Clear();
                    seen.Clear();
                    search.Add(new RouteSearchStep(route[from], -1, 0, 0, 0));
                    for (int index = 0; index < search.Count; index++)
                    {
                        var current = search[index];
                        if (current.Tiles >= maxConnectorTiles || !neighbors.TryGetValue(current.Cell, out var exits))
                            continue;

                        foreach (long next in exits)
                        {
                            int tiles = current.Tiles + 1;
                            if (routeIndex.TryGetValue(next, out int to))
                            {
                                // Rejoin farther along the route and count only the stretch being replaced.
                                if (to <= from || current.Targets == 0)
                                    continue;
                                int gain = current.Targets - (prefix[to - 1].Targets - prefix[from].Targets);
                                int newEncounters = encounters + current.Encounters
                                    - (prefix[to - 1].Encounters - prefix[from].Encounters);
                                int newTiles = route.Count - 1 + tiles - (to - from);
                                if (gain <= 0 || newEncounters > direct.RemainingNodes + limit || newTiles > direct.Tiles + maxExtraTiles
                                    || best is { } prior && (gain < prior.Targets
                                        || gain == prior.Targets && (newEncounters > prior.Encounters
                                            || newEncounters == prior.Encounters && newTiles >= prior.Tiles)))
                                    continue;

                                long[] connector = new long[tiles - 1];
                                for (int cursor = index, i = connector.Length - 1; i >= 0; i--, cursor = search[cursor].Parent)
                                    connector[i] = search[cursor].Cell;
                                best = new RouteSplice(from, to, connector, gain, newEncounters, newTiles);
                                continue;
                            }

                            if (cells[next].Explored || stops.ContainsKey(next)
                                || current.Targets == 0 && (!distanceToTarget.TryGetValue(next, out int distance)
                                    || tiles + distance >= maxConnectorTiles))
                                continue;
                            int extra = current.Encounters + (IsUnfinished(cells[next]) ? 1 : 0);
                            int found = current.Targets + (goalCells.Contains(next) ? 1 : 0);
                            if (extra + prefix[from].Encounters > direct.RemainingNodes + limit)
                                continue;

                            bool repeated = false;
                            for (int cursor = index; cursor > 0; cursor = search[cursor].Parent)
                                if (search[cursor].Cell == next)
                                {
                                    repeated = true;
                                    break;
                                }
                            if (!repeated && seen.Add((next, extra, found)))
                                search.Add(new RouteSearchStep(next, index, tiles, extra, found));
                        }
                    }
                }

                if (best is not { } splice)
                    break;
                route.RemoveRange(splice.From + 1, splice.To - splice.From - 1);
                route.InsertRange(splice.From + 1, splice.Connector);
                changed = true;
            }

            if (!changed)
                continue;

            routeGoals.Clear();
            foreach (long cell in route)
                if (goalCells.Contains(cell) && !stops.ContainsKey(cell))
                    routeGoals.Add(cell);
            int added = Math.Max(0, prefix[^1].Encounters - direct.RemainingNodes);
            for (int i = 0; i < routeGoals.Count; i++)
            {
                bool isFinal = i == routeGoals.Count - 1;
                long other = isFinal ? routeGoals[i - 1] : routeGoals[i + 1];
                stops.Add(routeGoals[i], new Detour(i + 1, cells[other].EncounterName, added, isFinal));
            }
            result.Add(destination, route);
        }
        return result;
    }

    internal static (long, long) LinkKey(long first, long second)
        => first < second ? (first, second) : (second, first);
}
