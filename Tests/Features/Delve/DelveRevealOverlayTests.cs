namespace ClickIt.Tests.Features.Delve;

[TestClass]
public sealed class DelveRevealOverlayTests
{
    [TestMethod]
    [TestCategory("Regression")]
    public void BuildConnections_CarriesRevealedAndHiddenAnchorsThroughAnUnmarkedCorner()
    {
        Dictionary<long, DelveRevealOverlay.ChartCell> cells = new()
        {
            [1] = new(new RectangleF(0, 0, 20, 20), [2], Hidden: false, Anchor: new NumVector2(.8f, .5f)),
            [2] = new(new RectangleF(0, 20, 20, 20), [3], BehindWall: true),
            [3] = new(new RectangleF(0, 40, 20, 20), [4], BehindWall: true),
            [4] = new(new RectangleF(20, 40, 20, 20), [], Anchor: new NumVector2(.5f, .75f)),
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
    public void BuildConnections_JoinsConflictingOffsetsAtTheSharedEdge()
    {
        Dictionary<long, DelveRevealOverlay.ChartCell> cells = new()
        {
            [1] = new(new RectangleF(0, 0, 20, 20), [2], Anchor: new NumVector2(.5f, .25f)),
            [2] = new(new RectangleF(20, 0, 20, 20), [1], Anchor: new NumVector2(.5f, .75f))
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
            [2] = new(new RectangleF(20, 0, 20, 20), [3], Hidden: false, BehindWall: true),
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
                Hidden: false, Anchor: new NumVector2(.75f, .25f)),
            [3] = new(new RectangleF(40, 0, 20, 20), linkFromRevealed ? [] : [2],
                Anchor: new NumVector2(.25f, .25f))
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
