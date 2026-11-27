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

internal static class DelveTargetRoutes
{
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
        Dictionary<long, string>? counts)
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
        Dictionary<long, (long Parent, int RemainingNodes)> previous = [];
        Queue<long> pending = new(starts.Count);
        for (int i = 0; i < starts.Count; i++)
        {
            long address = starts[i].Address;
            previous.Add(address, (address, 0));
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
        while (pending.TryDequeue(out long address))
        {
            if (!neighbors.TryGetValue(address, out var adjacent))
                continue;

            int remainingAtParent = counts == null ? 0 : previous[address].RemainingNodes;
            for (int i = 0; i < adjacent.Count; i++)
            {
                long next = adjacent[i];
                if (previous.ContainsKey(next))
                    continue;

                var cell = cells[next];
                int remainingNodes = remainingAtParent;
                if (counts != null && cell is { HasEncounter: true, Visited: false, Explored: false })
                    remainingNodes++;
                previous.Add(next, (address, remainingNodes));
                if ((cell.Target & targets) != 0 && --unresolvedGoals == 0)
                    break;
                pending.Enqueue(next);
            }
            if (unresolvedGoals == 0)
                break;
        }

        for (int i = 0; i < goals.Count; i++)
        {
            long goal = goals[i];
            if (!previous.TryGetValue(goal, out var step))
                continue;

            counts?.Add(goal, step.RemainingNodes.ToString(System.Globalization.CultureInfo.InvariantCulture));
            long address = goal;
            while (step.Parent != address && routes.Add(LinkKey(step.Parent, address)))
            {
                address = step.Parent;
                step = previous[address];
            }
        }

        return routes;

        void AddNeighbor(long first, long second)
        {
            if (!neighbors.TryGetValue(first, out var adjacent))
                neighbors.Add(first, adjacent = new List<long>(4));
            adjacent.Add(second);
        }
    }

    internal static (long, long) LinkKey(long first, long second)
        => first < second ? (first, second) : (second, first);
}
