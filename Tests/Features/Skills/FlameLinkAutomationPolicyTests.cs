namespace ClickIt.Tests.Features.Skills;

[TestClass]
public sealed class FlameLinkAutomationPolicyTests
{
    [TestMethod]
    [TestCategory("Settings")]
    public void FlameLinkSettings_AreDisabledByDefault()
    {
        ClickItSettings settings = new();

        settings.CastFlameLinkOnMercenaries.Value.Should().BeFalse();
        settings.CastFlameLinkOnAnimateGuardian.Value.Should().BeFalse();
    }

    [TestMethod]
    [TestCategory("Boundary")]
    public void ShouldRunInArea_RejectsHideoutAndUnknownArea()
    {
        FlameLinkAutomationPolicy.ShouldRunInArea(isHideout: false).Should().BeTrue();
        FlameLinkAutomationPolicy.ShouldRunInArea(isHideout: true).Should().BeFalse();
        FlameLinkAutomationPolicy.ShouldRunInArea(isHideout: null).Should().BeFalse();
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void IsTargetBuffName_MatchesOnlyTargetBuffCaseInsensitively()
    {
        FlameLinkAutomationPolicy.IsTargetBuffName("flame_link_target").Should().BeTrue();
        FlameLinkAutomationPolicy.IsTargetBuffName("FLAME_LINK_TARGET").Should().BeTrue();
        FlameLinkAutomationPolicy.IsTargetBuffName("flame_link_source").Should().BeFalse();
        FlameLinkAutomationPolicy.IsTargetBuffName(null).Should().BeFalse();
    }

    [TestMethod]
    [TestCategory("Boundary")]
    public void TryResolveAbsoluteScreenPoint_ConvertsClientCoordinatesAndRejectsOffscreenPoints()
    {
        RectangleF gameWindow = new(100, 200, 800, 600);

        bool resolved = FlameLinkAutomationPolicy.TryResolveAbsoluteScreenPoint(new Vector2(250, 300), gameWindow, out Vector2 absolutePoint);
        bool offscreenResolved = FlameLinkAutomationPolicy.TryResolveAbsoluteScreenPoint(new Vector2(801, 300), gameWindow, out _);

        resolved.Should().BeTrue();
        absolutePoint.Should().Be(new Vector2(350, 500));
        offscreenResolved.Should().BeFalse();
    }

    [TestMethod]
    [TestCategory("Scenario")]
    public void SelectNextMissingTarget_KeepsRetryingOwnedMercenaryUntilBuffAppears()
    {
        Entity mercenary = EntityProbeFactory.Create(path: "mercenary");
        Entity animateGuardian = EntityProbeFactory.Create(path: "guardian");
        Entity[] mercenaries = [mercenary];
        Entity[] guardians = [animateGuardian];
        FlameLinkTargetBuffState mercenaryState = FlameLinkTargetBuffState.Missing;

        Entity? first = FlameLinkAutomationPolicy.SelectNextMissingTarget(
            mercenaries,
            guardians,
            true,
            true,
            target => ReferenceEquals(target, mercenary) ? mercenaryState : FlameLinkTargetBuffState.Missing,
            _ => true);
        Entity? retry = FlameLinkAutomationPolicy.SelectNextMissingTarget(
            mercenaries,
            guardians,
            true,
            true,
            target => ReferenceEquals(target, mercenary) ? mercenaryState : FlameLinkTargetBuffState.Missing,
            _ => true);
        mercenaryState = FlameLinkTargetBuffState.Present;
        Entity? second = FlameLinkAutomationPolicy.SelectNextMissingTarget(
            mercenaries,
            guardians,
            true,
            true,
            target => ReferenceEquals(target, mercenary) ? mercenaryState : FlameLinkTargetBuffState.Missing,
            _ => true);

        ReferenceEquals(first, mercenary).Should().BeTrue();
        ReferenceEquals(retry, mercenary).Should().BeTrue();
        ReferenceEquals(second, animateGuardian).Should().BeTrue();
    }

    [TestMethod]
    [TestCategory("Boundary")]
    public void SelectNextMissingTarget_SkipsUnknownBuffStateAndHonorsIndependentSettings()
    {
        Entity mercenary = EntityProbeFactory.Create(path: "mercenary");
        Entity animateGuardian = EntityProbeFactory.Create(path: "guardian");
        Entity[] mercenaries = [mercenary];
        Entity[] guardians = [animateGuardian];

        Entity? guardianOnly = FlameLinkAutomationPolicy.SelectNextMissingTarget(
            mercenaries,
            guardians,
            castOnMercenaries: false,
            castOnAnimateGuardian: true,
            target => ReferenceEquals(target, mercenary) ? FlameLinkTargetBuffState.Unknown : FlameLinkTargetBuffState.Missing,
            _ => true);
        Entity? disabled = FlameLinkAutomationPolicy.SelectNextMissingTarget(
            mercenaries,
            guardians,
            castOnMercenaries: false,
            castOnAnimateGuardian: false,
            _ => FlameLinkTargetBuffState.Missing,
            _ => true);

        ReferenceEquals(guardianOnly, animateGuardian).Should().BeTrue();
        disabled.Should().BeNull();
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void SelectNextMissingTarget_SkipsCompanionsNotOwnedByLocalPlayer()
    {
        Entity otherPlayersMercenary = EntityProbeFactory.Create(path: "other-players-mercenary");
        Entity localMercenary = EntityProbeFactory.Create(path: "local-mercenary");
        Entity[] mercenaries = [otherPlayersMercenary, localMercenary];

        Entity? target = FlameLinkAutomationPolicy.SelectNextMissingTarget(
            mercenaries,
            [],
            castOnMercenaries: true,
            castOnAnimateGuardian: false,
            _ => FlameLinkTargetBuffState.Missing,
            candidate => ReferenceEquals(candidate, localMercenary));

        ReferenceEquals(target, localMercenary).Should().BeTrue();
    }
}
