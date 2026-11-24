namespace ClickIt.Tests.Shared.Game;

[TestClass]
public sealed class AreaClassificationTests
{
    [DataTestMethod]
    [DataRow(true, false, "Other", true)]
    [DataRow(false, true, "Other", true)]
    [DataRow(false, false, "Azurite Mine (34)", true)]
    [DataRow(false, false, "Azurite Mine (35)", false)]
    [DataRow(false, false, "Other", false)]
    [DataRow(false, false, null, false)]
    public void IsSafeArea_RecognizesTownHideoutAndMineEncampment(bool town, bool hideout, string? name, bool expected)
        => AreaClassification.IsSafeArea(town, hideout, name).Should().Be(expected);

    [TestMethod]
    public void IsSafeArea_ReturnsUnknownWhenControllerIsUnavailable()
        => AreaClassification.IsSafeArea(null).Should().BeNull();
}