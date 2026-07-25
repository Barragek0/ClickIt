namespace ClickIt.Tests.Behavior.Click
{
    [TestClass]
    public class LabelClickPointResolutionPolicyTests
    {
        [DataTestMethod]
        [DataRow(true, false, true)]
        [DataRow(false, false, false)]
        [DataRow(true, true, false)]
        [DataRow(false, true, false)]
        public void ShouldAllowSettlersRelaxedFallback_ReturnsExpected_ForAllInputCombinations(bool hasBackingEntity, bool worldProjectionInWindow, bool expected)
        {
            bool result = LabelClickPointResolutionPolicy.ShouldAllowSettlersRelaxedFallback(hasBackingEntity, worldProjectionInWindow);

            result.Should().Be(expected);
        }
    }
}
