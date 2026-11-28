namespace ClickIt.Tests.Features.Skills;

[TestClass]
public sealed class LinkAutomationPolicyTests
{
    [TestMethod]
    [TestCategory("Settings")]
    public void FlameLinkSettings_AreDisabledByDefault()
    {
        ClickItSettings settings = new();

        settings.CastFlameLinkOnMercenaries.Value.Should().BeFalse();
        settings.CastFlameLinkOnAnimateGuardian.Value.Should().BeFalse();
        settings.CastFlameLinkOnSpectres.Value.Should().BeFalse();
        settings.CastDestructiveLinkOnMercenaries.Value.Should().BeFalse();
        settings.CastDestructiveLinkOnAnimateGuardian.Value.Should().BeFalse();
        settings.CastDestructiveLinkOnSpectres.Value.Should().BeFalse();
    }

    [TestMethod]
    [TestCategory("Boundary")]
    public void ShouldRunInArea_RejectsHideoutAndUnknownArea()
    {
        LinkAutomationPolicy.ShouldRunInArea(isHideout: false).Should().BeTrue();
        LinkAutomationPolicy.ShouldRunInArea(isHideout: true).Should().BeFalse();
        LinkAutomationPolicy.ShouldRunInArea(isHideout: null).Should().BeFalse();
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void IsTargetBuffName_MatchesOnlyTargetBuffCaseInsensitively()
    {
        LinkAutomationPolicy.IsTargetBuffName("flame_link_target").Should().BeTrue();
        LinkAutomationPolicy.IsTargetBuffName("FLAME_LINK_TARGET").Should().BeTrue();
        LinkAutomationPolicy.IsTargetBuffName("flame_link_source").Should().BeFalse();
        LinkAutomationPolicy.IsTargetBuffName(null).Should().BeFalse();
        LinkAutomationPolicy.IsTargetBuffName("critical_link_target", true).Should().BeTrue();
        LinkAutomationPolicy.IsTargetBuffName("flame_link_target", true).Should().BeFalse();
        LinkAutomationPolicy.IsTargetBuffName("critical_link_target").Should().BeFalse();
    }

    [TestMethod]
    [TestCategory("Boundary")]
    public void TryResolveAbsoluteScreenPoint_ConvertsClientCoordinatesAndRejectsOffscreenPoints()
    {
        RectangleF gameWindow = new(100, 200, 800, 600);

        bool resolved = LinkAutomationPolicy.TryResolveAbsoluteScreenPoint(new Vector2(250, 300), gameWindow, out Vector2 absolutePoint);
        bool offscreenResolved = LinkAutomationPolicy.TryResolveAbsoluteScreenPoint(new Vector2(801, 300), gameWindow, out _);

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
        LinkTargetBuffState mercenaryState = LinkTargetBuffState.Missing;

        Entity? first = LinkAutomationPolicy.SelectNextMissingTarget(
            mercenaries,
            guardians,
            true,
            true,
            target => ReferenceEquals(target, mercenary) ? mercenaryState : LinkTargetBuffState.Missing,
            _ => true);
        Entity? retry = LinkAutomationPolicy.SelectNextMissingTarget(
            mercenaries,
            guardians,
            true,
            true,
            target => ReferenceEquals(target, mercenary) ? mercenaryState : LinkTargetBuffState.Missing,
            _ => true);
        mercenaryState = LinkTargetBuffState.Present;
        Entity? second = LinkAutomationPolicy.SelectNextMissingTarget(
            mercenaries,
            guardians,
            true,
            true,
            target => ReferenceEquals(target, mercenary) ? mercenaryState : LinkTargetBuffState.Missing,
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

        Entity? guardianOnly = LinkAutomationPolicy.SelectNextMissingTarget(
            mercenaries,
            guardians,
            castOnMercenaries: false,
            castOnAnimateGuardian: true,
            target => ReferenceEquals(target, mercenary) ? LinkTargetBuffState.Unknown : LinkTargetBuffState.Missing,
            _ => true);
        Entity? disabled = LinkAutomationPolicy.SelectNextMissingTarget(
            mercenaries,
            guardians,
            castOnMercenaries: false,
            castOnAnimateGuardian: false,
            _ => LinkTargetBuffState.Missing,
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

        Entity? target = LinkAutomationPolicy.SelectNextMissingTarget(
            mercenaries,
            [],
            castOnMercenaries: true,
            castOnAnimateGuardian: false,
            _ => LinkTargetBuffState.Missing,
            candidate => ReferenceEquals(candidate, localMercenary));

        ReferenceEquals(target, localMercenary).Should().BeTrue();
    }

    [TestMethod]
    [TestCategory("Regression")]
    [DataRow(true)]
    [DataRow(false)]
    public void SelectNextMissingTarget_SkipsPlayerComponentsAndUnreadableComponentState(bool includeEligibleCompanion)
    {
        Entity playerWithCompanionMetadata = EntityProbeFactory.Create(path: "mercenary");
        EntityProbeFactory.WithComponent(playerWithCompanionMetadata,
            (PoEMemoryComponents.Player)RuntimeHelpers.GetUninitializedObject(typeof(PoEMemoryComponents.Player)));
        var unreadableCompanion = (EntityProbe)EntityProbeFactory.Create(path: "guardian");
        unreadableCompanion.Components = null!;
        Entity spectre = EntityProbeFactory.Create(path: "spectre");
        var buffReads = new List<Entity>();

        Entity? target = LinkAutomationPolicy.SelectNextMissingTarget(
            [playerWithCompanionMetadata],
            [unreadableCompanion],
            castOnMercenaries: true,
            castOnAnimateGuardian: true,
            candidate =>
            {
                buffReads.Add(candidate);
                return LinkTargetBuffState.Missing;
            },
            _ => true,
            includeEligibleCompanion ? [spectre] : [],
            castOnSpectres: true);

        LinkAutomationPolicy.IsNonPlayerTarget(playerWithCompanionMetadata).Should().BeFalse();
        LinkAutomationPolicy.IsNonPlayerTarget(unreadableCompanion).Should().BeFalse();
        LinkAutomationPolicy.IsNonPlayerTarget(spectre).Should().BeTrue();
        ReferenceEquals(target, includeEligibleCompanion ? spectre : null).Should().BeTrue();
        buffReads.Count.Should().Be(includeEligibleCompanion ? 1 : 0);
    }

    [TestMethod]
    [TestCategory("Scenario")]
    public void SelectNextMissingTarget_ReturnsSpectre_WhenOnlySpectresAreEnabled()
    {
        Entity spectre = EntityProbeFactory.Create(path: "spectre");

        Entity? target = LinkAutomationPolicy.SelectNextMissingTarget(
            [],
            [],
            castOnMercenaries: false,
            castOnAnimateGuardian: false,
            _ => LinkTargetBuffState.Missing,
            _ => true,
            [spectre],
            castOnSpectres: true);

        ReferenceEquals(target, spectre).Should().BeTrue();
    }

    [TestMethod]
    [TestCategory("Boundary")]
    public void SelectNextMissingTarget_IgnoresSpectres_WhenTheSpectreOptionIsOff()
    {
        Entity spectre = EntityProbeFactory.Create(path: "spectre");

        Entity? target = LinkAutomationPolicy.SelectNextMissingTarget(
            [],
            [],
            castOnMercenaries: false,
            castOnAnimateGuardian: false,
            _ => LinkTargetBuffState.Missing,
            _ => true,
            [spectre],
            castOnSpectres: false);

        target.Should().BeNull();
    }

    [TestMethod]
    [TestCategory("Boundary")]
    public void SelectNextMissingTarget_PrefersMercenariesAndGuardians_OverSpectres()
    {
        Entity mercenary = EntityProbeFactory.Create(path: "mercenary");
        Entity spectre = EntityProbeFactory.Create(path: "spectre");

        Entity? target = LinkAutomationPolicy.SelectNextMissingTarget(
            [mercenary],
            [],
            castOnMercenaries: true,
            castOnAnimateGuardian: false,
            _ => LinkTargetBuffState.Missing,
            _ => true,
            [spectre],
            castOnSpectres: true);

        ReferenceEquals(target, mercenary).Should().BeTrue();
    }
}
