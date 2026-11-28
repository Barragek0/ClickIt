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

internal readonly record struct DelveRouteOptions(int Discounts, bool CombineTargets = false)
{

    internal const string CityEncounters = "City Encounters";
    // Store costs in quarter encounters so score comparisons stay exact.
    internal const int EncounterCost = 4;
    private const int DiscountBits = 3;
    private const int CityKind = 1;
    private const int CavityKind = 2;
    private const int VaultKind = 3;
    private const string Cavity = "Azurite Cavity";
    private const string Vault = "Azurite Vault";

    internal static string[] GetPriorityOrder(ClickItSettings settings)
    {
        int order = GetPackedOrder(settings);
        return [PriorityKey((order >> 4) & 3), PriorityKey((order >> 2) & 3), PriorityKey(order & 3)];
    }

    private static string PriorityKey(int kind) => kind switch
    {
        CityKind => CityEncounters,
        CavityKind => Cavity,
        _ => Vault
    };

    private static int GetPackedOrder(ClickItSettings settings)
    {
        int order = 0;
        int seen = 0;
        List<string>? saved = settings.DelveEncounterPriorityOrder;
        // Older orders predate the movable city row.
        if (saved?.Contains(CityEncounters) != true)
            Append(CityKind);
        if (saved != null)
        {
            foreach (string name in saved)
                Append(Classify(name));
        }
        for (int kind = CityKind; kind <= VaultKind; kind++)
            Append(kind);
        return order;

        void Append(int kind)
        {
            if (kind == 0 || (seen & (1 << kind)) != 0)
                return;
            seen |= 1 << kind;
            order = (order << 2) | kind;
        }
    }

    internal static bool IsPriorityEnabled(ClickItSettings settings, string key)
    {
        int kind = Classify(key);
        if (kind != 0 && settings.EnabledDelveEncounterPriorities is { } enabled)
            foreach (string name in enabled)
                if (Classify(name) == kind)
                    return true;
        return false;
    }

    internal static int DiscountAtRow(int row) => EncounterCost - row;

    internal static DelveRouteOptions FromSettings(ClickItSettings settings)
    {
        int discounts = 0;
        if (settings.EnabledDelveEncounterPriorities is { Count: > 0 })
        {
            int order = GetPackedOrder(settings);
            for (int row = 0; row < 3; row++)
            {
                int kind = (order >> ((2 - row) * 2)) & 3;
                if (IsPriorityEnabled(settings, PriorityKey(kind)))
                    discounts |= DiscountAtRow(row) << (kind * DiscountBits);
            }
        }

        return new(discounts, settings.CombineDelveTargetRoutes.Value);
    }

    internal static string PriorityDisplayName(string key) => key switch
    {
        Cavity => "Azurite I",
        Vault => "Azurite II",
        _ => key
    };

    internal static int Classify(string name, string? asset = null) => asset switch
    {
        "ProtoVaalCity" or "AbyssalCity" or "VaalCity" => CityKind,
        "AzuriteVein" => CavityKind,
        "AzuriteVeinII" => VaultKind,
        "BossProtoVaal" or "BossAbyss" or "BossVaalCity" or "AzuriteVeinIII" => 0,
        _ => name switch
        {
            CityEncounters or "Primeval Chamber" or "Primeval Ruins" or "Abyssal Chamber" or "Abyssal City"
                or "Ruined Chamber" or "Vaal Outpost" => CityKind,
            Cavity => CavityKind,
            Vault => VaultKind,
            _ => 0
        }
    };

    internal int Discount(int kind) => (Discounts >> (kind * DiscountBits)) & ((1 << DiscountBits) - 1);
}
internal static class DelveTargetRoutes
{
    private readonly record struct RouteStep(long Parent, int Tiles, int RemainingNodes, int WeightedCost);
    internal readonly record struct Detour(int Stop);

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
        Dictionary<(long, long), int>? routeLegs = null,
        RectangleF? goalViewport = null)
    {
        HashSet<(long, long)> routes = [];
        List<long> goals = [];
        List<(long Address, float Y, float X)> starts = [];
        // Only a combined trip is limited to the viewport; individual routes keep covering
        // the whole captured chart.
        RectangleF? viewport = options.CombineTargets ? goalViewport : null;
        foreach ((long address, var cell) in cells)
        {
            if (cell.Explored)
                starts.Add((address, cell.Rect.Y, cell.Rect.X));
            else if ((cell.Target & targets) != 0 && WithinGoalViewport(cell.Rect, viewport))
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
        bool usePriorities = options.Discounts != 0;
        Queue<long>? pending = usePriorities ? null : new(starts.Count);
        PriorityQueue<long, (int Cost, int Tiles, int Sequence)>? weighted = usePriorities ? new(starts.Count) : null;
        int sequence = 0;
        for (int i = 0; i < starts.Count; i++)
        {
            long address = starts[i].Address;
            previous.Add(address, new RouteStep(address, 0, 0, 0));
            Enqueue(address, previous[address]);
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

        if (options.CombineTargets && detours != null && routeLegs != null && goals.Count > 1)
            DelveCombinedRoutePlanner.Plan(cells, neighbors, goals, starts.Select(start => start.Address).ToArray(),
                routes, routeLegs, detours, counts, options);

        int unresolvedGoals = goals.Count - (detours?.Count ?? 0);
        if (unresolvedGoals == 0)
            return routes;
        bool countEncounters = counts != null || usePriorities || detours != null;
        while (TryTakeNext(out long address))
        {
            RouteStep parent = previous[address];
            if (usePriorities && IsUnplannedGoal(address) && --unresolvedGoals == 0)
                break;
            if (!neighbors.TryGetValue(address, out var adjacent))
                continue;

            for (int i = 0; i < adjacent.Count; i++)
            {
                long next = adjacent[i];
                bool discovered = previous.TryGetValue(next, out RouteStep existing);
                if (discovered && !usePriorities)
                    continue;

                var cell = cells[next];
                bool unfinished = countEncounters && IsUnfinished(cell);
                RouteStep candidate = new(address, parent.Tiles + 1, parent.RemainingNodes + (unfinished ? 1 : 0),
                    parent.WeightedCost + (unfinished && usePriorities ? DelveRouteOptions.EncounterCost - options.Discount(cell.EncounterKind) : 0));
                if (discovered && (candidate.WeightedCost, candidate.Tiles).CompareTo((existing.WeightedCost, existing.Tiles)) >= 0)
                    continue;

                previous[next] = candidate;
                if (!usePriorities && IsUnplannedGoal(next) && --unresolvedGoals == 0)
                    break;
                Enqueue(next, candidate);
            }
            if (unresolvedGoals == 0 && !usePriorities)
                break;
        }

        HashSet<(long, long)> directEdges = detours is { Count: > 0 } ? [] : routes;
        for (int i = 0; i < goals.Count; i++)
        {
            long goal = goals[i];
            if (!previous.TryGetValue(goal, out var step))
                continue;

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

        bool IsUnplannedGoal(long address)
            => !cells[address].Explored && (cells[address].Target & targets) != 0 && detours?.ContainsKey(address) != true;

        void Enqueue(long address, RouteStep step)
        {
            if (weighted != null)
                weighted.Enqueue(address, (step.WeightedCost, step.Tiles, sequence++));
            else
                pending!.Enqueue(address);
        }

        bool TryTakeNext(out long address)
        {
            if (weighted == null)
                return pending!.TryDequeue(out address);

            while (weighted.TryDequeue(out address, out var rank))
            {
                RouteStep step = previous[address];
                if (rank.Cost == step.WeightedCost && rank.Tiles == step.Tiles)
                    return true;
            }

            return false;
        }

        void WriteCount(long goal, int count)
            => counts?.Add(goal, count.ToString(System.Globalization.CultureInfo.InvariantCulture));

        void AddNeighbor(long first, long second)
        {
            if (!neighbors.TryGetValue(first, out var adjacent))
                neighbors.Add(first, adjacent = new List<long>(4));
            adjacent.Add(second);
        }
    }

    internal static bool IsUnfinished(DelveRevealOverlay.ChartCell cell)
        => cell is { HasEncounter: true, Visited: false, Explored: false };

    // A null viewport means the caller does not restrict goals at all.
    internal static bool WithinGoalViewport(RectangleF rect, RectangleF? goalViewport)
        => goalViewport is not { } bounds || bounds.Intersects(rect);

    internal static (long, long) LinkKey(long first, long second)
        => first < second ? (first, second) : (second, first);
}
