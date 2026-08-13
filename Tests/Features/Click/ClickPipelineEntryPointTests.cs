namespace ClickIt.Tests.Features.Click;

// Guards the click pipeline rewrite: every runtime entry point the rewrite phases may touch must stay on the port (or an equivalent, test-updated surface).  A phase that deletes/merges a wrapper must NOT drop a member the runtime still calls — this test fails if an entry point disappears. Entry points are asserted by name AND declaring type, so a silent move behind another wrapper (the exact pass-through pattern this rewrite removes) also fails.
[TestClass]
public class ClickPipelineEntryPointTests
{
    private static readonly Type PortType = typeof(global::ClickIt.Features.Click.ClickAutomationPort);

    [TestMethod]
    public void Port_ExposesRuntimeClickEntryPoints()
    {
        AssertMethodExists("ProcessRegularClick");
    }

    [TestMethod]
    public void Port_ExposesRuntimePathingAndSettlementEntryPoints()
    {
        AssertMethodExists("TryClickManualUiHoverLabel");
        AssertMethodExists("CancelOffscreenPathingState");
        AssertMethodExists("CancelPostChestLootSettlementState");
    }

    [TestMethod]
    public void Port_ExposesUltimatumEntryPoints()
    {
        AssertMethodExists("TryGetUltimatumOptionPreview");
        AssertMethodExists("RefreshUltimatumPreview");
    }

    [TestMethod]
    public void Port_ExposesBlightSafetyAndWiringEntryPoints()
    {
        // Fail-closed menu-click gate + pathfinding icon-box guard (safety-critical).
        AssertMethodExists("IsBlightTowerUiAt");
        AssertMethodExists("IsBlightBuildOrUpgradeIconAt");
        AssertPropertyExists("PointIsInClickableArea");
        AssertPropertyExists("ForceRefreshPointIsInClickableArea");

        // Delegate wiring points consumed by the bootstrapper.
        AssertPropertyExists("GetHarvestLabelToClick");
        AssertPropertyExists("TryProgressBlightBuilding");
        AssertPropertyExists("GetBlightPathfindTarget");
        AssertPropertyExists("IsBlightEncounterActive");
        AssertPropertyExists("BlightChestDebug");
    }

    private static void AssertMethodExists(string name)
    {
        MethodInfo? method = PortType.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull($"click pipeline entry method '{name}' must survive on {PortType.Name}");
        method!.DeclaringType.Should().Be(
            PortType,
            $"'{name}' must be declared on {PortType.Name} itself — a pass-through wrapper is not an acceptable surface");
    }

    private static void AssertPropertyExists(string name)
    {
        PropertyInfo? property = PortType.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        property.Should().NotBeNull($"click pipeline entry property '{name}' must survive on {PortType.Name}");
        property!.DeclaringType.Should().Be(
            PortType,
            $"'{name}' must be declared on {PortType.Name} itself — a pass-through wrapper is not an acceptable surface");
    }
}
