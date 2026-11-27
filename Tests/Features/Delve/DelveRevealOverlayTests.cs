namespace ClickIt.Tests.Features.Delve;

[TestClass]
public sealed class DelveRevealOverlayTests
{
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
    [DataRow(299L, 1000L, false)]
    [DataRow(300L, 50L, false)]
    [DataRow(300L, 90L, true)]
    [DataRow(900L, 5L, true)]
    public void IsCaptureDue_DefersRoutineTopologyRefreshWhilePanning(long ageMs, long stillMs, bool expected)
        => DelveRevealOverlay.IsCaptureDue(ageMs, stillMs).Should().Be(expected);

    [TestMethod]
    [TestCategory("Regression")]
    [DataRow(true, true, 0L, 1000L, true)]
    [DataRow(false, false, 0L, 10L, true)]
    [DataRow(false, true, 59L, 10L, false)]
    [DataRow(false, true, 60L, 10L, true)]
    [DataRow(false, true, 60L, 90L, false)]
    public void ShouldRefreshMarkers_UpdatesDuringPanWithoutWaitingForRouteCapture(
        bool chunksChanged, bool covered, long ageMs, long stillMs, bool expected)
        => DelveRevealOverlay.ShouldRefreshMarkers(chunksChanged, covered, ageMs, stillMs).Should().Be(expected);

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
