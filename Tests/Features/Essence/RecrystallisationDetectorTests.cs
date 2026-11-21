namespace ClickIt.Tests.Features.Essence;

[TestClass]
public sealed class RecrystallisationDetectorTests
{
    public sealed class ServerData
    {
        public object[] AtlasPassiveSkillIds { get; set; } = [];
    }

    public sealed class IngameData
    {
        public ServerData? ServerData { get; set; }
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void IsActive_DetectsPassive11021AndCachesBriefly()
    {
        long now = 1000;
        RecrystallisationDetector detector = new(() => now);
        IngameData active = new() { ServerData = new() { AtlasPassiveSkillIds = [11021] } };
        IngameData inactive = new() { ServerData = new() { AtlasPassiveSkillIds = [9882] } };

        detector.IsActive(active).Should().BeTrue();
        now += 50;
        detector.IsActive(inactive).Should().BeTrue();
        now += 101;
        detector.IsActive(inactive).Should().BeFalse();
        new RecrystallisationDetector(() => now).IsActive(new IngameData { ServerData = null }).Should().BeFalse();
    }
}
