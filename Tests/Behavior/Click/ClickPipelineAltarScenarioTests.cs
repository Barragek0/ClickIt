namespace ClickIt.Tests.Behavior.Click
{
    // NOTE: the altar-owns-tick E2E scenario (clickable altar on screen) cannot be driven through the REAL clickability gate: the gate requires Element.IsVisible, and the obfuscated base getter reads game memory that returns false on probe elements (verified: false via both the typed read and DynamicAccess, with or without an address set). The altar clickability gate is therefore covered by AltarAutomationServiceTests unit tests (all rejection paths), and the branch itself is a direct if(HasClickableAltars()) in ClickRuntimeEngine.Run(). If a full altar-on-screen scenario is required, a narrowly-scoped, user-approved visibility seam on the AltarAutomationService would be needed.
    [TestClass]
    public class ClickPipelineAltarScenarioTests
    {
        [TestMethod]
        public void AltarOnScreen_RequiresSimulatableElementVisibility_WhichObfuscatedElementCannotProvide()
        {
            // Documented limitation: Element.IsVisible (obfuscated, reads game memory) returns false on probe elements regardless of the element address, so no probe can pass the real altar clickability gate. This test pins that fact so the limitation is visible.
            Element probe = ClickPipelineScenarioFactory.CreateLabelElement(new RectangleF(0f, 0f, 10f, 10f));
            ClickPipelineScenarioFactory.SetElementAddress(probe, 0x100);
            bool visible;
            try { visible = probe.IsVisible; } catch { visible = false; }
            visible.Should().BeFalse("Element.IsVisible reads obfuscated game memory that probes cannot simulate");
        }
    }
}
