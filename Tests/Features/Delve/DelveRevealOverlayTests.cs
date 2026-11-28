namespace ClickIt.Tests.Features.Delve;

[TestClass]
public sealed class DelveRevealOverlayTests
{
    [TestMethod]
    [TestCategory("Regression")]
    [DataRow(false)]
    [DataRow(true)]
    public void CombinedTargetRoutes_BacktrackThroughClearedNodes_AndMatchLabelsToLegs(bool includeFourthTarget)
    {
        var settings = new ClickItSettings();
        DelveRouteOptions.FromSettings(settings).CombineTargets.Should().BeTrue();
        Dictionary<long, DelveRevealOverlay.ChartCell> cells = new()
        {
            [1] = new(new RectangleF(20, 40, 20, 20), [2], Explored: true),
            [2] = new(new RectangleF(40, 40, 20, 20), [3, 4], HasEncounter: true),
            [3] = new(new RectangleF(40, 20, 20, 20), [], HasEncounter: true, Target: DelveRouteTarget.Aul),
            [4] = new(new RectangleF(60, 40, 20, 20), [5], HasEncounter: true),
            [5] = new(new RectangleF(80, 40, 20, 20), [6], HasEncounter: true, Target: DelveRouteTarget.Lich),
            [6] = new(new RectangleF(100, 40, 20, 20), includeFourthTarget ? [7] : [],
                HasEncounter: true, Target: DelveRouteTarget.Architect),
            [100] = new(new RectangleF(20, 80, 20, 20), [101], Explored: true),
            [101] = new(new RectangleF(40, 80, 20, 20), [], HasEncounter: true, Target: DelveRouteTarget.CrystalSpire),
        };
        var targets = DelveRouteTarget.Aul | DelveRouteTarget.Lich | DelveRouteTarget.Architect | DelveRouteTarget.CrystalSpire;
        if (includeFourthTarget)
        {
            cells[7] = new(new RectangleF(120, 40, 20, 20), [], HasEncounter: true, Target: DelveRouteTarget.MoltenCavity);
            targets |= DelveRouteTarget.MoltenCavity;
        }

        var lines = DelveRevealOverlay.BuildRouteLines(cells, targets, showTargetCounts: true, routeOptions: DelveRouteOptions.FromSettings(settings));

        lines.TargetCounts![3].Should().Be("2");
        lines.TargetCounts[5].Should().Be("4");
        lines.TargetCounts[6].Should().Be("5");
        lines.TargetCounts[101].Should().Be("1");
        lines.Detours![3].Stop.Should().Be(1);
        lines.Detours[5].Stop.Should().Be(2);
        lines.Detours[6].Stop.Should().Be(3);
        lines.Detours.Should().NotContainKey(101);
        lines.LegLines![0].Should().HaveCount(2);
        lines.LegLines[1].Should().HaveCount(2);
        lines.LegLines[2].Should().HaveCount(1);
        if (includeFourthTarget)
            lines.LegLines[3].Should().HaveCount(1);
        lines.Target.Should().ContainSingle();

        var chart = new RectangleF(0, 0, 180, 120);
        var projection = new DelveRevealOverlay.ChartProjection(chart, chart);
        var markers = lines.Detours.Keys.Select(address => new DelveRevealOverlay.ChartMarker(
            cells[address].Rect, new NumVector2(.5f, .5f), DelveNodeIcon.Resolve("Fossil"),
            Target: cells[address].Target, Address: address)).ToArray();
        var nodes = DelveRevealOverlay.BuildMarkers(markers, chart, projection, targets: targets,
            targetCounts: lines.TargetCounts, detours: lines.Detours);
        nodes.Select(node => (node.DetourLabel, node.Leg)).Should().Contain(("#1 / 2", 0)).And
            .Contain(("#2 / 4", 1)).And.Contain(("#3 / 5", 2));
        if (includeFourthTarget)
            nodes.Select(node => (node.DetourLabel, node.Leg)).Should().Contain(("#4 / 6", 3));

        settings.CombineDelveTargetRoutes.Value = false;
        var separate = DelveRevealOverlay.BuildRouteLines(cells, targets, showTargetCounts: true,
            routeOptions: DelveRouteOptions.FromSettings(settings));
        separate.Detours.Should().BeNull();
        separate.TargetCounts![5].Should().Be("3");
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void CombinedTargetRoutes_StopBeforeALegThroughTooManyExploredTiles()
    {
        var settings = new ClickItSettings();
        Dictionary<long, DelveRevealOverlay.ChartCell> cells = new()
        {
            [1] = new(new RectangleF(0, 0, 20, 20), [2, 20], Explored: true, Visited: true),
            [2] = new(new RectangleF(20, 0, 20, 20), [10], HasEncounter: true),
            [10] = new(new RectangleF(40, 0, 20, 20), [11], HasEncounter: true, Target: DelveRouteTarget.Aul),
            [11] = new(new RectangleF(60, 0, 20, 20), [12], HasEncounter: true, Target: DelveRouteTarget.Lich),
            [12] = new(new RectangleF(80, 0, 20, 20), [], HasEncounter: true, Target: DelveRouteTarget.Architect),
            [30] = new(new RectangleF(0, 140, 20, 20), [], HasEncounter: true, Target: DelveRouteTarget.CrystalSpire)
        };
        for (int i = 0; i < 6; i++)
            cells.Add(20 + i, new(new RectangleF(0, 20 * (i + 1), 20, 20), i < 5 ? [21 + i] : [30], Visited: true));
        const DelveRouteTarget selected = DelveRouteTarget.Aul | DelveRouteTarget.Lich
            | DelveRouteTarget.Architect | DelveRouteTarget.CrystalSpire;

        var lines = DelveRevealOverlay.BuildRouteLines(cells, selected, showTargetCounts: true,
            routeOptions: DelveRouteOptions.FromSettings(settings));

        lines.Detours!.Keys.Should().BeEquivalentTo(new long[] { 10, 11, 12 });
        lines.Detours[10].Stop.Should().Be(1);
        lines.Detours[11].Stop.Should().Be(2);
        lines.Detours[12].Stop.Should().Be(3);
        lines.TargetCounts.Should().BeEquivalentTo(new Dictionary<long, string>
        {
            [10] = "2",
            [11] = "3",
            [12] = "4",
            [30] = "1"
        });
        lines.LegLines![0].Should().HaveCount(2);
        lines.LegLines[1].Should().HaveCount(1);
        lines.LegLines[2].Should().HaveCount(1);
        lines.Target.Should().HaveCount(7);
    }

    [TestMethod]
    [TestCategory("Boundary")]
    [DataRow(true)]
    [DataRow(false)]
    public void CombinedTargetRoutes_RespectTheLegBacktrackBudget(bool overBudget)
    {
        var settings = new ClickItSettings();
        int depth = DelveCombinedRoutePlanner.MaxVisitedTilesPerLeg + (overBudget ? 1 : 0);
        Dictionary<long, DelveRevealOverlay.ChartCell> cells = new()
        {
            [1] = new(new RectangleF(0, 0, 20, 20), [2], Explored: true, Visited: true),
            [2] = new(new RectangleF(20, 0, 20, 20), [10], HasEncounter: true),
            [10] = new(new RectangleF(40, 0, 20, 20), [20], HasEncounter: true, Target: DelveRouteTarget.Aul),
            [30] = new(new RectangleF(40, 20 * (depth + 1), 20, 20), [], HasEncounter: true, Target: DelveRouteTarget.Lich)
        };
        for (int i = 0; i < depth; i++)
            cells.Add(20 + i, new(new RectangleF(40, 20 * (i + 1), 20, 20), i + 1 < depth ? [21 + i] : [30], Visited: true));
        const DelveRouteTarget selected = DelveRouteTarget.Aul | DelveRouteTarget.Lich;

        var lines = DelveRevealOverlay.BuildRouteLines(cells, selected, showTargetCounts: true,
            routeOptions: DelveRouteOptions.FromSettings(settings));

        if (overBudget)
        {
            lines.Detours.Should().BeEmpty();
            lines.LegLines.Should().BeNull();
        }
        else
        {
            lines.Detours!.Keys.Should().BeEquivalentTo(new long[] { 10, 30 });
            lines.Detours[10].Stop.Should().Be(1);
            lines.Detours[30].Stop.Should().Be(2);
            lines.LegLines.Should().NotBeNull();
        }
        lines.TargetCounts![10].Should().Be("2");
        lines.TargetCounts[30].Should().Be("3");
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void CombinedTargetRoutes_OnlyChainGoalsInsideTheViewport()
    {
        var settings = new ClickItSettings();
        Dictionary<long, DelveRevealOverlay.ChartCell> cells = new()
        {
            [1] = new(new RectangleF(0, 0, 20, 20), [2], Explored: true),
            [2] = new(new RectangleF(20, 0, 20, 20), [10], HasEncounter: true),
            [10] = new(new RectangleF(40, 0, 20, 20), [11], HasEncounter: true, Target: DelveRouteTarget.Aul),
            [11] = new(new RectangleF(60, 0, 20, 20), [12], HasEncounter: true, Target: DelveRouteTarget.Lich),
            [12] = new(new RectangleF(80, 0, 20, 20), [], HasEncounter: true, Target: DelveRouteTarget.Architect)
        };
        const DelveRouteTarget selected = DelveRouteTarget.Aul | DelveRouteTarget.Lich | DelveRouteTarget.Architect;

        var near = DelveRevealOverlay.BuildRouteLines(cells, selected, showTargetCounts: true,
            routeOptions: DelveRouteOptions.FromSettings(settings), goalViewport: new RectangleF(0, 0, 70, 20));

        near.Detours!.Keys.Should().BeEquivalentTo(new long[] { 10, 11 });
        near.Detours[11].Stop.Should().Be(2);
        near.TargetCounts.Should().BeEquivalentTo(new Dictionary<long, string> { [10] = "2", [11] = "3" });

        var single = DelveRevealOverlay.BuildRouteLines(cells, selected, showTargetCounts: true,
            routeOptions: DelveRouteOptions.FromSettings(settings), goalViewport: new RectangleF(0, 0, 50, 20));

        single.Detours.Should().BeEmpty();
        single.TargetCounts.Should().BeEquivalentTo(new Dictionary<long, string> { [10] = "2" });
        single.Target.Should().HaveCount(2);

        // The same goals are chained whenever the trip is not combined.
        var separate = DelveRevealOverlay.BuildRouteLines(cells, selected, showTargetCounts: true,
            goalViewport: new RectangleF(0, 0, 50, 20));

        separate.Detours.Should().BeNull();
        separate.TargetCounts.Should().BeEquivalentTo(new Dictionary<long, string> { [10] = "2", [11] = "3", [12] = "4" });
    }

    [TestMethod]
    [TestCategory("Boundary")]
    [DataRow(0.5f, 0f, 0f, 500f, 250f)]
    [DataRow(1f, -250f, -125f, 750f, 375f)]
    [DataRow(2f, -750f, -375f, 1250f, 625f)]
    public void GetRouteViewport_ExpandsToTheFullyZoomedOutArea(float chartScale, float left, float top, float right, float bottom)
    {
        // The chart is drawn at 200px while the 100x50 clip shows a corner of it, so the
        // visible local area is 500x250 and expansion grows it around its centre.
        var projection = new DelveRevealOverlay.ChartProjection(new RectangleF(0, 0, 1000, 1000), new RectangleF(0, 0, 200, 200));
        var clip = new RectangleF(0, 0, 100, 50);

        var viewport = DelveRevealOverlay.GetRouteViewport(projection, clip, chartScale);

        viewport.Left.Should().BeApproximately(left, 0.01f);
        viewport.Top.Should().BeApproximately(top, 0.01f);
        viewport.Right.Should().BeApproximately(right, 0.01f);
        viewport.Bottom.Should().BeApproximately(bottom, 0.01f);
    }

    [TestMethod]
    [TestCategory("Boundary")]
    public void BuildMarkers_CyclesLegColoursPastThePalette()
    {
        var chart = new RectangleF(0, 0, 400, 100);
        var projection = new DelveRevealOverlay.ChartProjection(chart, chart);
        DelveNodeIcon icon = DelveNodeIcon.Resolve("Fossil");
        DelveRevealOverlay.ChartMarker[] markers = new DelveRevealOverlay.ChartMarker[9];
        Dictionary<long, DelveTargetRoutes.Detour> detours = [];
        for (int stop = 1; stop <= 9; stop++)
        {
            markers[stop - 1] = new(new RectangleF(40 * stop, 10, 20, 20), new NumVector2(.5f, .5f), icon, Address: stop);
            detours.Add(stop, new DelveTargetRoutes.Detour(stop));
        }

        var nodes = DelveRevealOverlay.BuildMarkers(markers, chart, projection, detours: detours);

        nodes.Select(node => node.Leg).Should().Equal(0, 1, 2, 3, 4, 5, 6, 7, 0);
    }

    [TestMethod]
    [TestCategory("Settings")]
    public void AzuritePriority_AppendsToSavedOrderAndIsOptIn()
    {
        ClickItSettings settings = new()
        {
            DelveEncounterPriorityOrder = ["Primeval Chamber", "Abyssal Chamber", "Ruined Chamber", "Azurite Cavity"]
        };
        // Rows show the tier while the stored keys keep the chart names.
        DelveRouteOptions.GetPriorityOrder(settings).TakeLast(2).Should().Equal("Azurite Cavity", "Azurite Vault");
        DelveRouteOptions.PriorityDisplayName("Azurite Cavity").Should().Be("Azurite I");
        DelveRouteOptions.PriorityDisplayName("Azurite Vault").Should().Be("Azurite II");
        int kind = DelveRouteOptions.Classify("Azurite Vault");
        DelveRouteOptions.Classify("Contents Unknown", "AzuriteVeinII").Should().Be(kind);
        DelveRouteOptions.Classify("Azurite Vault", "AzuriteVeinIII").Should().Be(0);
        DelveRouteOptions.FromSettings(settings).Discount(kind).Should().Be(0);

        settings.EnabledDelveEncounterPriorities.AddRange(["Azurite Cavity", "Azurite Vault"]);
        DelveRouteOptions options = DelveRouteOptions.FromSettings(settings);
        options.Discount(kind).Should().Be(2);
        options.Discount(DelveRouteOptions.Classify("Azurite Cavity")).Should().Be(3);
        options.Discount(DelveRouteOptions.Classify("Primeval Chamber")).Should().Be(4);

        settings.DelveEncounterPriorityOrder = ["Azurite Vault", "Primeval Chamber"];
        options = DelveRouteOptions.FromSettings(settings);
        options.Discount(kind).Should().Be(3);
        options.Discount(DelveRouteOptions.Classify("Azurite Cavity")).Should().Be(2);
    }

    [TestMethod]
    [TestCategory("Regression")]
    [DataRow("Primeval Chamber", "Primeval Ruins", "ProtoVaalCity", "BossProtoVaal")]
    [DataRow("Abyssal Chamber", "Abyssal City", "AbyssalCity", "BossAbyss")]
    [DataRow("Ruined Chamber", "Vaal Outpost", "VaalCity", "BossVaalCity")]
    public void CityPriority_UsesChartNamesAndIcons_AndPreservesSavedKeys(string savedKey, string chartName,
        string cityAsset, string bossAsset)
    {
        ClickItSettings settings = new()
        {
            DelveEncounterPriorityOrder = [savedKey],
            EnabledDelveEncounterPriorities = [savedKey]
        };
        var options = DelveRouteOptions.FromSettings(settings);
        DelveRouteOptions.GetPriorityOrder(settings).Should().Equal("City Encounters", "Azurite Cavity", "Azurite Vault");
        int kind = DelveRouteOptions.Classify("City Encounters");
        DelveRouteOptions.Classify(savedKey).Should().Be(kind);
        DelveRouteOptions.Classify(chartName).Should().Be(kind);
        DelveRouteOptions.Classify("Contents Unknown", cityAsset).Should().Be(kind);
        options.Discount(kind).Should().Be(4);
        DelveRouteOptions.Classify(chartName, bossAsset).Should().Be(0);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void TargetRoutes_RespectCityRowPositionAndCanChooseALongerRoute()
    {
        Dictionary<long, DelveRevealOverlay.ChartCell> cells = new()
        {
            [1] = new(new RectangleF(0, 0, 20, 20), [], Explored: true),
            [2] = new(new RectangleF(20, 0, 20, 20), [], HasEncounter: true, EncounterKind: DelveRouteOptions.Classify("Azurite Cavity")),
            [3] = new(new RectangleF(0, 20, 20, 20), [], HasEncounter: true, EncounterKind: DelveRouteOptions.Classify("Primeval Ruins")),
            [4] = new(new RectangleF(20, 20, 20, 20), [], HasEncounter: true, Target: DelveRouteTarget.Aul)
        };
        List<DelveRevealOverlay.CellLink> links = [new(1, 2, true), new(1, 3, false), new(2, 4, false), new(3, 4, true)];
        ClickItSettings settings = new();
        settings.EnabledDelveEncounterPriorities.Should().Equal("Primeval Chamber", "Abyssal Chamber", "Ruined Chamber");
        DelveTargetRoutes.FindShortestRoutes(cells, links, DelveRouteTarget.Aul, null, DelveRouteOptions.FromSettings(settings))
            .Should().BeEquivalentTo(new[] { (1L, 3L), (3L, 4L) });

        settings.DelveEncounterPriorityOrder = ["Azurite Cavity", "Azurite Vault", DelveRouteOptions.CityEncounters];
        settings.EnabledDelveEncounterPriorities.Add("Azurite Cavity");
        DelveRouteOptions.GetPriorityOrder(settings).Should().Equal("Azurite Cavity", "Azurite Vault", DelveRouteOptions.CityEncounters);
        DelveTargetRoutes.FindShortestRoutes(cells, links, DelveRouteTarget.Aul, null, DelveRouteOptions.FromSettings(settings))
            .Should().BeEquivalentTo(new[] { (1L, 2L), (2L, 4L) });

        settings.EnabledDelveEncounterPriorities = ["Primeval Chamber"];
        DelveTargetRoutes.FindShortestRoutes(cells, links, DelveRouteTarget.Aul, null, DelveRouteOptions.FromSettings(settings))
            .Should().BeEquivalentTo(new[] { (1L, 3L), (3L, 4L) });

        links.RemoveAt(3);
        cells.Add(5, new(new RectangleF(40, 20, 20, 20), []));
        links.Add(new(3, 5, true));
        links.Add(new(5, 4, true));
        var counts = new Dictionary<long, string>();
        DelveTargetRoutes.FindShortestRoutes(cells, links, DelveRouteTarget.Aul, counts, DelveRouteOptions.FromSettings(settings))
            .Should().BeEquivalentTo(new[] { (1L, 3L), (3L, 5L), (4L, 5L) });
        counts[4].Should().Be("2");

        settings.EnabledDelveEncounterPriorities.Clear();
        DelveTargetRoutes.FindShortestRoutes(cells, links, DelveRouteTarget.Aul, null, DelveRouteOptions.FromSettings(settings))
            .Should().BeEquivalentTo(new[] { (1L, 2L), (2L, 4L) });
    }

    [TestMethod]
    [TestCategory("Regression")]
    [DataRow(false, 10, 100L)]
    [DataRow(true, 8, 10L)]
    public void PriorityRow_ChangesRouteChoiceWithoutDiscountingTheDisplayedCount(
        bool moveCitiesToBottom, int actualCount, long selectedFirst)
    {
        Dictionary<long, DelveRevealOverlay.ChartCell> cells = new()
        {
            [1] = new(new RectangleF(0, 0, 20, 20), [], Explored: true),
            [2] = new(new RectangleF(200, 20, 20, 20), [], HasEncounter: true, Target: DelveRouteTarget.Aul)
        };
        List<DelveRevealOverlay.CellLink> links = [];
        AddRoute(10, 7, 0, 0);
        AddRoute(100, 9, 3, 20);
        var settings = new ClickItSettings();
        if (moveCitiesToBottom)
            settings.DelveEncounterPriorityOrder = ["Azurite Cavity", "Azurite Vault", DelveRouteOptions.CityEncounters];
        Dictionary<long, string> counts = [];

        var route = DelveTargetRoutes.FindShortestRoutes(cells, links, DelveRouteTarget.Aul, counts, DelveRouteOptions.FromSettings(settings));

        route.Should().Contain((1L, selectedFirst));
        route.Should().HaveCount(actualCount);
        counts[2].Should().Be(actualCount.ToString(System.Globalization.CultureInfo.InvariantCulture));

        void AddRoute(long first, int intermediateCount, int discountedCount, float y)
        {
            long parent = 1;
            for (int i = 0; i < intermediateCount; i++)
            {
                long address = first + i;
                int kind = i < discountedCount ? DelveRouteOptions.Classify("Primeval Ruins") : 0;
                cells.Add(address, new(new RectangleF((i + 1) * 20, y, 20, 20), [], HasEncounter: true, EncounterKind: kind));
                links.Add(new(parent, address, true));
                parent = address;
            }
            links.Add(new(parent, 2, true));
        }
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void TargetRoutes_StartAtTheClosestExploredEncounterAndKeepOtherHiddenLinksRed()
    {
        Dictionary<long, DelveRevealOverlay.ChartCell> cells = new()
        {
            [1] = new(new RectangleF(0, 0, 20, 20), [2], Hidden: false, Explored: true),
            [2] = new(new RectangleF(20, 0, 20, 20), [3], Hidden: false),
            [3] = new(new RectangleF(40, 0, 20, 20), [4, 5], Target: DelveRouteTarget.Aul),
            [4] = new(new RectangleF(60, 0, 20, 20), [], Hidden: false, Explored: true),
            [5] = new(new RectangleF(40, 20, 20, 20), [], Target: DelveRouteTarget.MoltenCavity),
            [6] = new(new RectangleF(20, 20, 20, 20), [], Target: DelveRouteTarget.Aul)
        };

        var lines = DelveRevealOverlay.BuildRouteLines(cells, DelveRouteTarget.Aul);

        lines.Target.Should().Equal(new ScreenLine(new NumVector2(50, 10), new NumVector2(70, 10)));
        lines.Revealed.Should().BeEquivalentTo(new ScreenLine[]
        {
            new(new NumVector2(30, 10), new NumVector2(50, 10)),
            new(new NumVector2(50, 10), new NumVector2(50, 30))
        });
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void Overlay_ReportsDrawCostWithoutAddingClickProcessing()
    {
        var overlay = new DelveRevealOverlay(() => string.Empty);

        overlay.Section.Should().Be(RenderSection.DelveRevealOverlay);
        overlay.ProcessingSection.Should().Be(ProcessingSection.Unknown);
        overlay.RefreshTimingChannel.Should().BeNull();
    }

    [TestMethod]
    [TestCategory("Settings")]
    public void TargetRoutes_DefaultToThreeBossesThreeFossilsAndMinionAuraItems()
    {
        var settings = new ClickItSettings();
        var expected = DelveRouteTarget.Aul | DelveRouteTarget.Lich | DelveRouteTarget.Architect
            | DelveRouteTarget.CrystalSpire | DelveRouteTarget.MoltenCavity | DelveRouteTarget.HumidFissure
            | DelveRouteTarget.MinionAuraItems;
        var overlay = new DelveRevealOverlay(() => string.Empty);
        settings.RevealDelveMap.Value.Should().BeFalse();
        settings.ShowDelveTargetRoutes.Value.Should().BeTrue();
        settings.RevealDelveFracturedWalls.Value.Should().BeFalse();
        overlay.IsEnabled(settings).Should().BeTrue();
        DelveTargetRoutes.GetSelectedTargets(settings).Should().Be(expected);
        settings.TargetDelveAzurite1.Value = true;
        DelveTargetRoutes.GetSelectedTargets(settings).Should().Be(expected | DelveRouteTarget.Azurite1);
        settings.TargetDelveAzurite1.Value = false;
        settings.TargetDelveAzurite2.Value = true;
        DelveTargetRoutes.GetSelectedTargets(settings).Should().Be(expected | DelveRouteTarget.Azurite2);
        settings.TargetDelveAzurite2.Value = false;
        settings.TargetDelveAzurite3.Value = true;
        DelveTargetRoutes.GetSelectedTargets(settings).Should().Be(expected | DelveRouteTarget.Azurite3);
        settings.TargetDelveAzurite3.Value = false;
        settings.TargetDelveCrystalSpire.Value = false;
        settings.TargetDelveTimeLostCavern.Value = true;
        DelveTargetRoutes.GetSelectedTargets(settings).Should()
            .Be((expected & ~DelveRouteTarget.CrystalSpire) | DelveRouteTarget.TimeLostCavern);
        settings.ShowDelveTargetRoutes.Value = false;
        overlay.IsEnabled(settings).Should().BeFalse();
        settings.RevealDelveFracturedWalls.Value = true;
        overlay.IsEnabled(settings).Should().BeTrue();
        settings.RevealDelveFracturedWalls.Value = false;
        overlay.IsEnabled(settings).Should().BeFalse();
        settings.RevealDelveMap.Value = true;
        overlay.IsEnabled(settings).Should().BeTrue();
        DelveTargetRoutes.GetSelectedTargets(settings).Should().Be(DelveRouteTarget.None);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void BuildRouteLines_CountsUnfinishedEncountersOnEachGreenRoute()
    {
        Dictionary<long, DelveRevealOverlay.ChartCell> cells = new()
        {
            [1] = new(new RectangleF(0, 0, 20, 20), [2], HasEncounter: true, Explored: true),
            [2] = new(new RectangleF(20, 0, 20, 20), [3], HasEncounter: true),
            [3] = new(new RectangleF(40, 0, 20, 20), [4, 9], BehindWall: true),
            [4] = new(new RectangleF(60, 0, 20, 20), [5], HasEncounter: true, Visited: true),
            [5] = new(new RectangleF(80, 0, 20, 20), [6], HasEncounter: true),
            [6] = new(new RectangleF(100, 0, 20, 20), [7], HasEncounter: true),
            [7] = new(new RectangleF(120, 0, 20, 20), [8], HasEncounter: true),
            [8] = new(new RectangleF(140, 0, 20, 20), [12], HasEncounter: true),
            [9] = new(new RectangleF(40, 20, 20, 20), [], HasEncounter: true, Target: DelveRouteTarget.Aul),
            [10] = new(new RectangleF(40, 40, 20, 20), [], HasEncounter: true, Target: DelveRouteTarget.Aul),
            [11] = new(new RectangleF(160, 20, 20, 20), [12], HasEncounter: true, Target: DelveRouteTarget.HumidFissure),
            [12] = new(new RectangleF(160, 0, 20, 20), [], HasEncounter: true, Target: DelveRouteTarget.MoltenCavity)
        };
        const DelveRouteTarget selected = DelveRouteTarget.Aul | DelveRouteTarget.MoltenCavity;

        var withCounts = DelveRevealOverlay.BuildRouteLines(cells, selected, showTargetCounts: true);
        var withoutCounts = DelveRevealOverlay.BuildRouteLines(cells, selected);

        withCounts.Target.Should().Equal(withoutCounts.Target);
        withCounts.TargetCounts.Should().BeEquivalentTo(new Dictionary<long, string> { [9] = "2", [12] = "6" });
        withoutCounts.TargetCounts.Should().BeNull();
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void BuildMarkers_ShowsOnlyOnScreenEncounterIconsAlongSelectedRoutesWhenEnabled()
    {
        Dictionary<long, DelveRevealOverlay.ChartCell> cells = new()
        {
            [1] = new(new RectangleF(0, 0, 20, 20), [2], Hidden: false, HasEncounter: true, Explored: true),
            [2] = new(new RectangleF(20, 0, 20, 20), [3], HasEncounter: true),
            [3] = new(new RectangleF(40, 0, 20, 20), [4, 5], BehindWall: true),
            [4] = new(new RectangleF(60, 0, 20, 20), [], HasEncounter: true, Target: DelveRouteTarget.MoltenCavity),
            [5] = new(new RectangleF(40, 20, 20, 20), [], HasEncounter: true, Target: DelveRouteTarget.HumidFissure)
        };
        var chart = new RectangleF(0, -5, 95, 65);
        var projection = new DelveRevealOverlay.ChartProjection(chart, chart);
        const DelveRouteTarget selected = DelveRouteTarget.MoltenCavity;
        var lines = DelveRevealOverlay.BuildRouteLines(cells, selected, showTargetCounts: true);
        var anchor = new NumVector2(.5f, .5f);
        DelveRevealOverlay.ChartMarker[] markers =
        [
            new(cells[1].Rect, anchor, DelveNodeIcon.Resolve("AzuriteVein"), Address: 1, DrawIcon: false),
            new(cells[2].Rect, anchor, DelveNodeIcon.Resolve("AzuriteVeinII"), "Intermediate", Address: 2),
            new(cells[4].Rect, anchor, DelveNodeIcon.Resolve("Fossil"), "Target", selected, Address: 4),
            new(cells[5].Rect, anchor, DelveNodeIcon.Resolve("Fossil"), "Side branch", DelveRouteTarget.HumidFissure, Address: 5)
        ];

        lines.RouteEncounterAddresses.Should().BeEquivalentTo(new long[] { 1, 2, 4 });
        var enabled = DelveRevealOverlay.BuildMarkers(markers, chart, projection, false, selected, lines.TargetCounts,
            lines.RouteEncounterAddresses);
        enabled.Select(node => (node.HoverText, node.RemainingNodeCount)).Should().Equal(("Intermediate", null), ("Target", "2"));

        var disabled = DelveRevealOverlay.BuildMarkers(markers, chart, projection, false, selected, lines.TargetCounts);
        disabled.Should().ContainSingle().Which.HoverText.Should().Be("Target");

        var panned = new RectangleF(36, -5, 60, 65);
        DelveRevealOverlay.BuildMarkers(markers, panned, projection, false, selected, lines.TargetCounts, lines.RouteEncounterAddresses)
            .Should().ContainSingle().Which.HoverText.Should().Be("Target");
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void BuildMarkers_AttachesCountsToVisibleTargetsWithoutRedrawingNativeIcons()
    {
        var chart = new RectangleF(0, 0, 120, 100);
        var projection = new DelveRevealOverlay.ChartProjection(chart, chart);
        DelveNodeIcon icon = DelveNodeIcon.Resolve("Fossil");
        DelveRevealOverlay.ChartMarker[] markers =
        [
            new(new RectangleF(10, 10, 20, 20), new NumVector2(.5f, .5f), icon,
                "Molten Cavity", DelveRouteTarget.MoltenCavity, Address: 1),
            new(new RectangleF(40, 40, 20, 20), new NumVector2(.5f, .5f), icon,
                Target: DelveRouteTarget.MoltenCavity, Address: 2, DrawIcon: false),
            new(new RectangleF(70, 40, 20, 20), new NumVector2(.5f, .5f), icon,
                Target: DelveRouteTarget.MoltenCavity, Address: 3, DrawIcon: false),
            new(new RectangleF(150, 40, 20, 20), new NumVector2(.5f, .5f), icon,
                Target: DelveRouteTarget.MoltenCavity, Address: 4, DrawIcon: false)
        ];
        Dictionary<long, string> counts = new() { [1] = "3", [2] = "6", [4] = "7" };

        var nodes = DelveRevealOverlay.BuildMarkers(markers, chart, projection, false, DelveRouteTarget.MoltenCavity, counts);
        nodes.Select(node => (node.DrawIcon, node.RemainingNodeCount)).Should().Equal((true, "3"), (false, "6"));
        DelveRevealOverlay.BuildMarkers(markers, chart, projection, false, DelveRouteTarget.MoltenCavity)
            .Should().ContainSingle().Which.RemainingNodeCount.Should().BeNull();
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void BuildRouteLines_SeparatesFracturedWallLinksFromOrdinaryHiddenLinks()
    {
        Dictionary<long, DelveRevealOverlay.ChartCell> cells = new()
        {
            [1] = new(new RectangleF(0, 0, 20, 20), [2], Hidden: false, Explored: true),
            [2] = new(new RectangleF(20, 0, 20, 20), [3], Hidden: false, BehindWall: true,
                WallSide: DelveRevealOverlay.WallApproach.West),
            [3] = new(new RectangleF(40, 0, 20, 20), [], Target: DelveRouteTarget.Aul),
            [4] = new(new RectangleF(0, 20, 20, 20), [5]),
            [5] = new(new RectangleF(20, 20, 20, 20), [])
        };

        var red = DelveRevealOverlay.BuildRouteLines(cells, DelveRouteTarget.None);
        red.FracturedWalls.Should().Equal(
            new ScreenLine(new NumVector2(10, 10), new NumVector2(30, 10)),
            new ScreenLine(new NumVector2(30, 10), new NumVector2(50, 10)));
        red.Revealed.Should().Equal(new ScreenLine(new NumVector2(10, 30), new NumVector2(30, 30)));

        var green = DelveRevealOverlay.BuildRouteLines(cells, DelveRouteTarget.Aul);
        green.Target.Should().Equal(red.FracturedWalls);
        green.Revealed.Should().Equal(red.Revealed);
        green.FracturedWalls.Should().BeEmpty();
    }

    [TestMethod]
    [TestCategory("Regression")]
    [DataRow(true)]
    [DataRow(false)]
    public void BuildRouteLines_DoesNotRevealAWallFromTheWrongSide(bool wallCellVisible)
    {
        Dictionary<long, DelveRevealOverlay.ChartCell> cells = new()
        {
            [1] = new(new RectangleF(0, 0, 20, 20), [2]),
            [2] = new(new RectangleF(20, 0, 20, 20), [3], Hidden: !wallCellVisible, BehindWall: true,
                WallSide: DelveRevealOverlay.WallApproach.West),
            [3] = new(new RectangleF(40, 0, 20, 20), [], Hidden: false, Explored: true)
        };
        var lines = DelveRevealOverlay.BuildRouteLines(cells, DelveRouteTarget.None);

        lines.FracturedWalls.Should().BeEmpty();
        lines.Revealed.Should().BeEmpty();
        lines.Target.Should().BeEmpty();
    }

    [TestMethod]
    [TestCategory("Regression")]
    [DataRow(false)]
    [DataRow(true)]
    public void BuildRouteLines_TargetRouteCanReachAFracturedWallViaDarkness(bool showFracturedWalls)
    {
        var settings = new ClickItSettings();
        settings.RevealDelveFracturedWalls.Value = showFracturedWalls;
        Dictionary<long, DelveRevealOverlay.ChartCell> cells = new()
        {
            [1] = new(new RectangleF(0, 0, 20, 20), [2], Hidden: false, Explored: true),
            [2] = new(new RectangleF(20, 0, 20, 20), [3]),
            [3] = new(new RectangleF(40, 0, 20, 20), [4], BehindWall: true,
                WallSide: DelveRevealOverlay.WallApproach.West),
            [4] = new(new RectangleF(60, 0, 20, 20), [], BehindWall: true, Target: DelveRouteTarget.MoltenCavity)
        };

        var lines = DelveRevealOverlay.BuildRouteLines(cells, DelveTargetRoutes.GetSelectedTargets(settings));

        lines.Target.Should().Equal(
            new ScreenLine(new NumVector2(10, 10), new NumVector2(30, 10)),
            new ScreenLine(new NumVector2(30, 10), new NumVector2(50, 10)),
            new ScreenLine(new NumVector2(50, 10), new NumVector2(70, 10)));
        lines.FracturedWalls.Should().BeEmpty();
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void BuildRouteLines_RevealedApproachDoesNotExposeASecondWallInDarkness()
    {
        Dictionary<long, DelveRevealOverlay.ChartCell> cells = new()
        {
            [1] = new(new RectangleF(0, 0, 20, 20), [2], Hidden: false),
            [2] = new(new RectangleF(20, 0, 20, 20), [3], BehindWall: true,
                WallSide: DelveRevealOverlay.WallApproach.West),
            [3] = new(new RectangleF(40, 0, 20, 20), [4], BehindWall: true),
            [4] = new(new RectangleF(60, 0, 20, 20), [5], BehindWall: true,
                WallSide: DelveRevealOverlay.WallApproach.West),
            [5] = new(new RectangleF(80, 0, 20, 20), [], BehindWall: true)
        };

        DelveRevealOverlay.BuildRouteLines(cells, DelveRouteTarget.None).FracturedWalls.Should().Equal(
            new ScreenLine(new NumVector2(10, 10), new NumVector2(30, 10)),
            new ScreenLine(new NumVector2(30, 10), new NumVector2(50, 10)));
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void BuildMarkers_ShrinksIconsWithNativeChartZoom()
    {
        var chart = new RectangleF(0, 0, 200, 200);
        DelveRevealOverlay.ChartMarker[] markers =
        [
            new(new RectangleF(80, 80, 40, 40), new NumVector2(.5f), DelveNodeIcon.Resolve("Fossil"))
        ];
        var fullZoom = new DelveRevealOverlay.ChartProjection(chart, chart);
        var quarterZoom = new DelveRevealOverlay.ChartProjection(chart, new RectangleF(0, 0, 50, 50), Zoom: .25f);

        var fullMarker = DelveRevealOverlay.BuildMarkers(markers, chart, fullZoom).Should().ContainSingle().Which;
        var smallMarker = DelveRevealOverlay.BuildMarkers(markers, chart, quarterZoom).Should().ContainSingle().Which;
        fullMarker.Radius.Should().BeApproximately(16.8f, .001f);
        smallMarker.Radius.Should().BeApproximately(2.97f, .01f);
    }

    [TestMethod]
    [TestCategory("Regression")]
    [DataRow("Crystal Spire", "Fossil", (int)DelveRouteTarget.CrystalSpire)]
    [DataRow("Molten Cavity", "Fossil", (int)DelveRouteTarget.MoltenCavity)]
    [DataRow("Humid Fissure", "Fossil", (int)DelveRouteTarget.HumidFissure)]
    [DataRow("Smuggler's Stash", "Fossil", (int)DelveRouteTarget.SmugglersStash)]
    [DataRow("Smuggler's Stash", "Maps", (int)DelveRouteTarget.None)]
    [DataRow("The Grand Architect's Temple", "BossVaalCity", (int)DelveRouteTarget.Architect)]
    [DataRow("Lich's Tomb", "BossAbyss", (int)DelveRouteTarget.Lich)]
    [DataRow("Aul's Throne", "BossProtoVaal", (int)DelveRouteTarget.Aul)]
    [DataRow("Grim Copse", "Minion", (int)DelveRouteTarget.MinionAuraItems)]
    [DataRow("Azurite Cavity", "AzuriteVein", (int)DelveRouteTarget.Azurite1)]
    [DataRow("Azurite Vault", "AzuriteVeinII", (int)DelveRouteTarget.Azurite2)]
    [DataRow("Azurite Fissure", "AzuriteVeinIII", (int)DelveRouteTarget.Azurite3)]
    [DataRow("Time-Lost Cavern", "Fossil", (int)DelveRouteTarget.TimeLostCavern)]
    [DataRow("Stonewood Hollow", "Fossil", (int)DelveRouteTarget.StonewoodHollow)]
    [DataRow("Haunted Tomb", "Fossil", (int)DelveRouteTarget.HauntedTomb)]
    public void TargetRoutes_MatchesEncounterIdentity(string name, string asset, int target)
        => DelveTargetRoutes.Classify(name, asset).Should().Be((DelveRouteTarget)target);

    [TestMethod]
    [TestCategory("Regression")]
    public void BuildMarkers_KeepsHiddenNodesHoverableAndIconDrawnSoTheOutlinedCountAppliesInDarkness()
    {
        // The count outline must be drawn for nodes in darkness too, so a hidden target keeps DrawIcon = true
        // (it still gets its own count text) instead of being reduced to a bare count-only node.
        var chart = new RectangleF(0, 0, 120, 100);
        var projection = new DelveRevealOverlay.ChartProjection(chart, chart);
        DelveRevealOverlay.ChartMarker[] markers =
        [
            new(new RectangleF(10, 10, 20, 20), new NumVector2(.5f, .5f), DelveNodeIcon.Resolve("Fossil"),
                "Molten Cavity", DelveRouteTarget.MoltenCavity, Address: 1, DrawIcon: true),
            new(new RectangleF(40, 10, 20, 20), new NumVector2(.5f, .5f), DelveNodeIcon.Resolve("Fossil"),
                Target: DelveRouteTarget.MoltenCavity, Address: 2, DrawIcon: false)
        ];
        Dictionary<long, string> counts = new() { [1] = "2", [2] = "1" };

        var nodes = DelveRevealOverlay.BuildMarkers(markers, chart, projection, false, DelveRouteTarget.MoltenCavity, counts);

        nodes.Select(node => (node.DrawIcon, node.HoverText, node.RemainingNodeCount))
            .Should().Equal((true, "Molten Cavity", "2"), (false, null, "1"));
        nodes.Should().HaveCount(2, "the hidden target still shows its outlined count");
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void BuildMarkers_TargetOnlyModeShowsOnlyTheSelectedEncounters()
    {
        var chart = new RectangleF(0, 0, 200, 200);
        var projection = new DelveRevealOverlay.ChartProjection(chart, chart);
        var fossil = DelveNodeIcon.Resolve("Fossil");
        DelveRevealOverlay.ChartMarker[] markers =
        [
            new(new RectangleF(20, 20, 30, 30), new NumVector2(.5f), fossil, "Crystal Spire", DelveRouteTarget.CrystalSpire),
            new(new RectangleF(75, 20, 30, 30), new NumVector2(.5f), fossil, "Haunted Tomb", DelveRouteTarget.HauntedTomb)
        ];

        DelveRevealOverlay.BuildMarkers(markers, chart, projection, revealFullMap: false, DelveRouteTarget.CrystalSpire)
            .Should().ContainSingle().Which.HoverText.Should().Be("Crystal Spire");
        DelveRevealOverlay.BuildMarkers(markers, chart, projection).Should().HaveCount(2);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void TargetRoutes_UsesSamePositionForAnEqualLengthRouteAfterAddressesChange()
    {
        Dictionary<long, DelveRevealOverlay.ChartCell> first = new()
        {
            [90] = new(new RectangleF(0, 0, 20, 20), [10], Hidden: false, Explored: true),
            [10] = new(new RectangleF(20, 0, 20, 20), [20], Target: DelveRouteTarget.Aul),
            [20] = new(new RectangleF(40, 0, 20, 20), [], Hidden: false, Explored: true)
        };
        Dictionary<long, DelveRevealOverlay.ChartCell> second = new()
        {
            [80] = first[20],
            [15] = first[10] with { Links = [80, 10] },
            [10] = first[90] with { Links = [] }
        };

        DelveRevealOverlay.BuildRouteLines(first, DelveRouteTarget.Aul).Target.Should().Equal(
            new ScreenLine(new NumVector2(10f, 10f), new NumVector2(30f, 10f)));
        DelveRevealOverlay.BuildRouteLines(second, DelveRouteTarget.Aul).Target.Should().Equal(
            DelveRevealOverlay.BuildRouteLines(first, DelveRouteTarget.Aul).Target);
    }

    [TestMethod]
    [TestCategory("Regression")]
    [DataRow(true, 299L, 1000L, false)]
    [DataRow(true, 300L, 50L, false)]
    [DataRow(true, 300L, 90L, true)]
    [DataRow(true, 900L, 5L, true)]
    [DataRow(false, 2999L, 1000L, false)]
    [DataRow(false, 3000L, 1000L, true)]
    public void IsCaptureDue_ReusesStableTopologyAndBoundsRefreshDelay(bool chunksChanged, long ageMs, long stillMs, bool expected)
        => DelveRevealOverlay.IsCaptureDue(chunksChanged, ageMs, stillMs).Should().Be(expected);

    [TestMethod]
    [TestCategory("Regression")]
    [DataRow(true, true, 0L, false)]
    [DataRow(true, true, 60L, true)]
    [DataRow(false, false, 0L, true)]
    [DataRow(false, true, 60L, false)]
    public void ShouldRefreshMarkers_UpdatesDuringPanWithoutWaitingForRouteCapture(
        bool chunksChanged, bool covered, long ageMs, bool expected)
        => DelveRevealOverlay.ShouldRefreshMarkers(chunksChanged, covered, ageMs).Should().Be(expected);

    [TestMethod]
    [TestCategory("Regression")]
    public void ProjectConnections_AvoidsChartSizedAllocationWhenAlmostAllLinesAreOffscreen()
    {
        RectangleF clip = new(0, 0, 100, 100);
        var projection = new DelveRevealOverlay.ChartProjection(clip, clip);
        var lines = new ScreenLine[8192];
        Array.Fill(lines, new ScreenLine(new NumVector2(500, 10), new NumVector2(520, 10)));
        lines[0] = new ScreenLine(new NumVector2(10, 10), new NumVector2(30, 10));
        _ = DelveRevealOverlay.ProjectConnections(lines, clip, projection);

        long allocationStart = GC.GetAllocatedBytesForCurrentThread();
        ScreenLine[] result = DelveRevealOverlay.ProjectConnections(lines, clip, projection);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocationStart;

        result.Should().Equal(lines[0]);
        allocated.Should().BeLessThan(4096);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void ChartProjection_ProjectsSameLocalCellAtNewPanPosition()
    {
        var localGrid = new RectangleF(0, 0, 6912, 117504);
        var grid = new RectangleF(-244.79994f, -21644.396f, 2332.8f, 39657.6f);
        var cell = new RectangleF(2304, 64224, 144, 144);
        var original = new DelveRevealOverlay.ChartProjection(localGrid, grid).Project(cell);
        original.X.Should().BeApproximately(532.80005f, .01f);
        original.Y.Should().BeApproximately(31.20293f, .01f);

        var movedGrid = new RectangleF(grid.X - 80, grid.Y + 35, grid.Width, grid.Height);
        var moved = new DelveRevealOverlay.ChartProjection(localGrid, movedGrid).Project(cell);
        moved.X.Should().BeApproximately(original.X - 80, .01f);
        moved.Y.Should().BeApproximately(original.Y + 35, .01f);
    }

    [TestMethod]
    [TestCategory("Regression")]
    [DataRow(18f, 12f)]
    [DataRow(9f, 12f)]
    [DataRow(5f, 15f)]
    public void PositionRemainingNodeCount_AlignsTextToTheBottomOfTheIcon(float radius, float textHeight)
    {
        var node = new DelveRevealOverlay.Node(new NumVector2(40, 60), radius, DelveNodeIcon.Resolve("BossAbyss"));

        var position = DelveRevealOverlay.PositionRemainingNodeCount(node, textHeight);

        position.X.Should().Be(node.Center.X);
        (position.Y + textHeight).Should().BeApproximately(node.Center.Y + radius - 2f, 0.001f);
    }

    [TestMethod]
    [TestCategory("Regression")]
    [DataRow(40f, 25f, 16f, false)]
    [DataRow(190f, 26f, 30f, true)]
    [DataRow(190f, 150f, 24f, true)]
    public void PositionHoverTooltip_KeepsTheTextAndShadowAboveTheCursor(float x, float y, float textHeight, bool nearRight)
    {
        var cursor = new NumVector2(x, y);
        var chart = new RectangleF(20, 25, 180, 140);

        var (position, align) = DelveRevealOverlay.PositionHoverTooltip(cursor, chart, textHeight);

        (position.Y + textHeight + 1f).Should().BeLessThan(cursor.Y);
        position.X.Should().Be(x + (nearRight ? -12f : 12f));
        align.Should().Be(nearRight ? FontAlign.Right : FontAlign.Left);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void FindHoverText_UsesTheClosestIconAcrossEncounterTypes()
    {
        var fossil = DelveNodeIcon.Resolve("Fossil");
        DelveRevealOverlay.Node[] nodes =
        [
            new(new NumVector2(20, 20), 8f, fossil, "Smuggler's Stash"),
            new(new NumVector2(30, 20), 8f, fossil, "Crystal Spire"),
            new(new NumVector2(40, 20), 8f, DelveNodeIcon.Resolve("AzuriteVeinIII"), "Azurite Tier 3")
        ];

        DelveRevealOverlay.FindHoverText(nodes, new NumVector2(27, 20)).Should().Be("Crystal Spire");
        DelveRevealOverlay.FindHoverText(nodes, new NumVector2(37, 20)).Should().Be("Azurite Tier 3");
        DelveRevealOverlay.FindHoverText(nodes, new NumVector2(10, 20)).Should().BeNull();
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void BuildConnections_CarriesRevealedAndHiddenAnchorsThroughAnUnmarkedCorner()
    {
        Dictionary<long, DelveRevealOverlay.ChartCell> cells = new()
        {
            [1] = new(new RectangleF(0, 0, 20, 20), [2], Hidden: false, Anchor: new NumVector2(.8f, .5f), HasEncounter: true),
            [2] = new(new RectangleF(0, 20, 20, 20), [3], BehindWall: true, Anchor: new NumVector2(.2f, .7f),
                WallSide: DelveRevealOverlay.WallApproach.North),
            [3] = new(new RectangleF(0, 40, 20, 20), [4], BehindWall: true, Anchor: new NumVector2(.4f, .3f)),
            [4] = new(new RectangleF(20, 40, 20, 20), [], Anchor: new NumVector2(.5f, .75f), HasEncounter: true),
            [5] = new(new RectangleF(20, 0, 20, 20), [6]),
            [6] = new(new RectangleF(20, 20, 20, 20), [])
        };

        DelveRevealOverlay.BuildConnections(cells).Should().BeEquivalentTo(new ScreenLine[]
        {
            new(new NumVector2(16, 10), new NumVector2(16, 30)),
            new(new NumVector2(16, 30), new NumVector2(16, 55)),
            new(new NumVector2(16, 55), new NumVector2(30, 55)),
            new(new NumVector2(30, 10), new NumVector2(30, 30))
        });
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void BuildConnections_KeepsUnanchoredRunsStraightAcrossFractionalCellPositions()
    {
        Dictionary<long, DelveRevealOverlay.ChartCell> cells = new()
        {
            [1] = new(new RectangleF(0, 0, 20, 20), [2], Anchor: new NumVector2(.25f, .75f)),
            [2] = new(new RectangleF(20, .01f, 20, 20), [3], Anchor: new NumVector2(.5f, .4f)),
            [3] = new(new RectangleF(40, .02f, 20, 20), [4]),
            [4] = new(new RectangleF(40, 20, 20, 20), [5]),
            [5] = new(new RectangleF(60, 20, 20, 20), [])
        };

        DelveRevealOverlay.BuildConnections(cells).Should().Equal(
            new ScreenLine(new NumVector2(10, 10), new NumVector2(30, 10)),
            new ScreenLine(new NumVector2(30, 10), new NumVector2(50, 10)),
            new ScreenLine(new NumVector2(50, 10), new NumVector2(50, 30)),
            new ScreenLine(new NumVector2(50, 30), new NumVector2(70, 30)));
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void BuildConnections_JoinsConflictingOffsetsAtTheSharedEdge()
    {
        Dictionary<long, DelveRevealOverlay.ChartCell> cells = new()
        {
            [1] = new(new RectangleF(0, 0, 20, 20), [2], Anchor: new NumVector2(.5f, .25f), HasEncounter: true),
            [2] = new(new RectangleF(20, 0, 20, 20), [1], Anchor: new NumVector2(.5f, .75f), HasEncounter: true)
        };

        DelveRevealOverlay.BuildConnections(cells).Should().Equal(
            new ScreenLine(new NumVector2(10, 5), new NumVector2(20, 5)),
            new ScreenLine(new NumVector2(20, 5), new NumVector2(20, 15)),
            new ScreenLine(new NumVector2(20, 15), new NumVector2(30, 15)));
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void BuildConnections_KeepsTheConfirmedWallRouteAcrossRevealedCells()
    {
        Dictionary<long, DelveRevealOverlay.ChartCell> cells = new()
        {
            [1] = new(new RectangleF(0, 0, 20, 20), [2, 4], Hidden: false),
            [2] = new(new RectangleF(20, 0, 20, 20), [3], Hidden: false, BehindWall: true,
                WallSide: DelveRevealOverlay.WallApproach.West),
            [3] = new(new RectangleF(40, 0, 20, 20), [], Hidden: false, BehindWall: true),
            [4] = new(new RectangleF(0, 20, 20, 20), [1], Hidden: false)
        };

        DelveRevealOverlay.BuildConnections(cells).Should().Equal(
            new ScreenLine(new NumVector2(10, 10), new NumVector2(30, 10)),
            new ScreenLine(new NumVector2(30, 10), new NumVector2(50, 10)));
    }

    [TestMethod]
    [TestCategory("Regression")]
    [DataRow(true)]
    [DataRow(false)]
    public void BuildConnections_JoinsTheFirstHiddenNodeToTheRevealedNode(bool linkFromRevealed)
    {
        Dictionary<long, DelveRevealOverlay.ChartCell> cells = new()
        {
            [1] = new(new RectangleF(0, 0, 20, 20), [2], Hidden: false),
            [2] = new(new RectangleF(20, 0, 20, 20), linkFromRevealed ? [1, 3] : [1],
                Hidden: false, Anchor: new NumVector2(.75f, .25f), HasEncounter: true),
            [3] = new(new RectangleF(40, 0, 20, 20), linkFromRevealed ? [] : [2],
                Anchor: new NumVector2(.25f, .25f), HasEncounter: true)
        };

        DelveRevealOverlay.BuildConnections(cells).Should().Equal(
            new ScreenLine(new NumVector2(35, 5), new NumVector2(45, 5)));
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void BuildConnections_UsesSharedEdgesBetweenLinkedHiddenCells()
    {
        Dictionary<long, DelveRevealOverlay.ChartCell> hiddenCells = new()
        {
            [1] = new(new RectangleF(0, 0, 20, 20), [1, 2, 3, 5, 99]),
            [2] = new(new RectangleF(20, 0, 20, 20), [1, 4]),
            [3] = new(new RectangleF(20, 20, 20, 20), []),
            [4] = new(new RectangleF(40, 0, 20, 20), [2]),
            [5] = new(new RectangleF(60, 0, 20, 20), []),
            [6] = new(new RectangleF(20, -20, 20, 20), [2])
        };

        DelveRevealOverlay.BuildConnections(hiddenCells).Should().BeEquivalentTo(new ScreenLine[]
        {
            new(new NumVector2(10, 10), new NumVector2(30, 10)),
            new(new NumVector2(30, 10), new NumVector2(50, 10)),
            new(new NumVector2(30, -10), new NumVector2(30, 10))
        });
    }

    [TestMethod]
    [TestCategory("Boundary")]
    [DataRow(147f, true)]
    [DataRow(149f, false)]
    public void MayAffectCoverage_KeepsTheNeighborCellNeededForLinksCrossingTheBoundary(float left, bool expected)
    {
        var coverage = new RectangleF(0, 0, 100, 100);
        var chunk = new RectangleF(left, 0, 384, 384);
        DelveRevealOverlay.MayAffectCoverage(chunk, coverage).Should().Be(expected);
    }

    [TestMethod]
    [TestCategory("Regression")]
    [DataRow(20f, 100f, true)]
    [DataRow(26f, 100f, false)]
    [DataRow(0f, 80f, true)]
    [DataRow(0f, 79f, false)]
    public void ChartProjection_CoversOnlyPansAndZoomsInsideTheCapturedMargin(float x, float size, bool expected)
    {
        var originalGrid = new RectangleF(0, 0, 100, 100);
        var projection = new DelveRevealOverlay.ChartProjection(originalGrid, new RectangleF(x, 0, size, size));
        projection.Covers(new RectangleF(-25, -25, 150, 150), originalGrid).Should().Be(expected);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void MergeStraightRuns_CombinesSharedEdgesWithoutBridgingGapsOrTurns()
    {
        var segments = new ScreenLine[258];
        for (int i = 0; i < 256; i++)
            segments[255 - i] = new ScreenLine(new NumVector2(i + 1, 10), new NumVector2(i, 10));
        segments[256] = new ScreenLine(new NumVector2(256, 10), new NumVector2(256, 25));
        segments[257] = new ScreenLine(new NumVector2(260, 10), new NumVector2(280, 10));

        DelveRevealOverlay.MergeStraightRuns(segments).Should().BeEquivalentTo(new ScreenLine[]
        {
            new(new NumVector2(0, 10), new NumVector2(256, 10)),
            new(new NumVector2(256, 10), new NumVector2(256, 25)),
            new(new NumVector2(260, 10), new NumVector2(280, 10))
        });
    }

    [TestMethod]
    [TestCategory("Boundary")]
    public void ProjectConnections_ClipsLinkedCellsToTheViewportWhenPanningAndZooming()
    {
        var projection = new DelveRevealOverlay.ChartProjection(new RectangleF(0, 0, 100, 100), new RectangleF(-20, -10, 200, 200));
        ScreenLine[] connections =
        [
            new(new NumVector2(0, 10), new NumVector2(40, 10)),
            new(new NumVector2(15, 0), new NumVector2(15, 40)),
            new(new NumVector2(0, 40), new NumVector2(40, 40))
        ];

        DelveRevealOverlay.ProjectConnections(connections, new RectangleF(0, 0, 40, 30), projection)
            .Should().Equal(
                new ScreenLine(new NumVector2(0, 10), new NumVector2(40, 10)),
                new ScreenLine(new NumVector2(10, 0), new NumVector2(10, 30)));
    }

    [TestMethod]
    [TestCategory("Regression")]
    [DataRow(false, false, true)]
    [DataRow(true, false, false)]
    [DataRow(false, true, false)]
    [DataRow(null, false, false)]
    [DataRow(false, null, false)]
    public void IsHidden_RequiresBothGameVisibilitySignalsToBeFalse(bool? gameDraws, bool? isVisited, bool expected)
    {
        DelveRevealOverlay.IsHidden(gameDraws, isVisited).Should().Be(expected);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void BuildMarkers_PreservesNodeOffsetWhilePanningAndZooming_AndOmitsOffscreenMarkers()
    {
        var icon = DelveNodeIcon.Resolve("Fossil");
        DelveRevealOverlay.ChartMarker[] cells =
        [
            new(new RectangleF(0, 20, 20, 20), new NumVector2(.25f, .75f), icon),
            new(new RectangleF(20, 20, 20, 20), new NumVector2(.5f, .5f), icon)
        ];
        DelveRevealOverlay.TryGetScreenClip(new RectangleF(-20, 0, 180, 140), new RectangleF(200, 100, 80, 90), out RectangleF clip)
            .Should().BeTrue();
        var projection = new DelveRevealOverlay.ChartProjection(new RectangleF(0, 0, 100, 100), new RectangleF(20, -10, 200, 200));

        var marker = DelveRevealOverlay.BuildMarkers(cells, clip, projection).Should().ContainSingle().Which;
        marker.Center.X.Should().Be(30f);
        marker.Center.Y.Should().BeApproximately(55.6f, 0.001f);
        marker.Radius.Should().BeApproximately(16.8f, 0.001f);
        marker.Icon.Should().BeSameAs(icon);
    }

    [TestMethod]
    [TestCategory("Boundary")]
    public void NormalizeAnchor_UsesLocalCellSizeAndIgnoresDefaultPositions()
    {
        DelveRevealOverlay.NormalizeAnchor(new NumVector2(27, 27), 144, 144).Should().BeNull();
        DelveRevealOverlay.NormalizeAnchor(NumVector2.Zero, 144, 144).Should().BeNull();
        DelveRevealOverlay.NormalizeAnchor(new NumVector2(36, 108), 144, 144).Should().Be(new NumVector2(.25f, .75f));
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void Resolve_UsesEncounterAssetNameAndFallsBackToGenericIcon()
    {
        DelveNodeIcon generic = DelveNodeIcon.Resolve(null);
        DelveNodeIcon fossil = DelveNodeIcon.Resolve("Art/2DArt/UIImages/InGame/Delve/MapEncounters/Fossil");
        DelveNodeIcon azurite = DelveNodeIcon.Resolve("AzuriteVein");

        fossil.Should().BeSameAs(DelveNodeIcon.Resolve("fossil.dds"));
        fossil.Should().NotBeSameAs(generic).And.NotBeSameAs(azurite);
        DelveNodeIcon.Resolve("AzuriteVeinIII").Should().NotBeSameAs(azurite);
        DelveNodeIcon.Resolve("Unrecognized").Should().BeSameAs(generic);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void Resolve_EmbedsTheRequestedChartAndItemTextures()
    {
        string[] keys = ["AzuriteVein", "AzuriteVeinII", "AzuriteVeinIII", "Weapons", "Breach", "Fossil",
            "Armour", "Gems", "Trinkets", "Blood", "Minion", "Bestiary", "Beyond", "Strongbox", "Cold",
            "AbyssEncounter", "Fire", "AbyssalCity", "BossAbyss", "Chaos", "Lightning", "VaalCity",
            "ProtoVaalCity", "Currency", "Maps", "BossVaalCity", "BossProtoVaal", "Chest", "Curse", "Legion",
            "WarbandChaos", "WarbandFire", "WarbandLightning"];
        foreach (string key in keys)
        {
            DelveNodeIcon icon = DelveNodeIcon.Resolve("Art/2DArt/UIImages/InGame/Delve/MapEncounters/" + key);
            icon.AssetName.Should().Be(key);

            using Stream? resource = typeof(DelveNodeIcon).Assembly.GetManifestResourceStream($"ClickIt.Delve.Textures.{key}.webp");
            resource.Should().NotBeNull();
            Span<byte> header = new byte[12];
            resource!.ReadExactly(header);
            header[..4].SequenceEqual("RIFF"u8).Should().BeTrue();
            header[8..].SequenceEqual("WEBP"u8).Should().BeTrue();
        }
    }
}
