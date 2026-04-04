namespace ClickIt.Tests.UI
{
    [TestClass]
    public class LabelDebugOverlaySectionTests
    {
        [TestMethod]
        public void RenderAltarAndHoveredSections_UseProjectedTelemetry_WhenLiveServicesAreUnavailable()
        {
            var deferredTextQueue = new DeferredTextQueue();
            var section = new LabelDebugOverlaySection(new DebugOverlayRenderContext(
                (BaseSettingsPlugin<ClickItSettings>)RuntimeHelpers.GetUninitializedObject(typeof(ClickIt)),
                altarService: null,
                areaService: null,
                weightCalculator: null,
                deferredTextQueue,
                new DeferredFrameQueue(),
                new FakeDebugTelemetrySource(DebugTelemetrySnapshot.Empty with
                {
                    Altar = new AltarTelemetrySnapshot(
                        ServiceAvailable: true,
                        ComponentCount: 1,
                        Components:
                        [
                            new AltarComponentTelemetrySnapshot(
                                Top: new AltarModSectionTelemetrySnapshot(
                                    SectionName: "Top",
                                    UpsideCount: 1,
                                    DownsideCount: 1,
                                    Upsides: [new AltarWeightedModTelemetrySnapshot("Top Upside", 5m)],
                                    Downsides: [new AltarWeightedModTelemetrySnapshot("Top Downside", null)]),
                                Bottom: new AltarModSectionTelemetrySnapshot(
                                    SectionName: "Bottom",
                                    UpsideCount: 1,
                                    DownsideCount: 0,
                                    Upsides: [new AltarWeightedModTelemetrySnapshot("Bottom Upside", null)],
                                    Downsides: Array.Empty<AltarWeightedModTelemetrySnapshot>()))
                        ],
                        ServiceDebug: new AltarServiceDebugTelemetrySnapshot(
                            LastScanExarchLabels: 3,
                            LastScanEaterLabels: 4,
                            ElementsFound: 5,
                            ComponentsProcessed: 6,
                            ComponentsAdded: 7,
                            ComponentsDuplicated: 8,
                            ModsMatched: 9,
                            ModsUnmatched: 10,
                            LastProcessedAltarType: "EaterOfWorlds",
                            LastError: "none",
                            LastScanTime: new DateTime(2026, 4, 4, 12, 34, 56))),
                    HoveredItem = new HoveredItemMetadataTelemetrySnapshot(
                        LabelsAvailable: true,
                        CursorInsideWindow: true,
                        HasHoveredItem: true,
                        GroundItemName: "Chaos Orb",
                        EntityPath: "Metadata/Items/Currency/CurrencyRerollRare",
                        MetadataPath: "Metadata/Items/Currency/CurrencyRerollRare")
                })));

            int yPos = section.RenderAltarDebug(10, 120, 18);
            yPos = section.RenderAltarServiceDebug(10, yPos, 18);
            section.RenderHoveredItemMetadataDebug(10, yPos, 18);

            string output = string.Join("\n", deferredTextQueue.GetPendingTextSnapshot());
            output.Should().Contain("Altar Components: 1");
            output.Should().Contain("1: Top Upside (5)");
            output.Should().Contain("Last Scan Exarch: 3");
            output.Should().Contain("Name: Chaos Orb");
            output.Should().Contain("Item Metadata: Metadata/Items/Currency/CurrencyRerollRare");
        }

        private sealed class FakeDebugTelemetrySource(DebugTelemetrySnapshot snapshot) : IDebugTelemetrySource
        {
            private readonly DebugTelemetrySnapshot _snapshot = snapshot;

            public DebugTelemetrySnapshot GetSnapshot()
                => _snapshot;

            public bool TryGetFreezeState(out long remainingMs, out string reason)
            {
                remainingMs = 0;
                reason = string.Empty;
                return false;
            }
        }
    }
}