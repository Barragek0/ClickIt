namespace ClickIt.Tests.Features.Click;

[TestClass]
public class BlightChestTransitionSuppressionTests
{
    private const string BlightChestPath = "Metadata/Chests/BlightChestObject";

    [TestMethod]
    public void ShouldSuppressBlightChestClick_ReturnsFalse_ForNullLabel()
    {
        var suppression = new BlightChestTransitionSuppression();

        suppression.ShouldSuppressBlightChestClick(null, now: 1_000).Should().BeFalse();
    }

    [TestMethod]
    public void ShouldSuppressBlightChestClick_ReturnsFalse_ForLabelWithoutItem()
    {
        var suppression = new BlightChestTransitionSuppression();
        LabelOnGround label = (LabelOnGround)RuntimeHelpers.GetUninitializedObject(typeof(LabelOnGround));

        suppression.ShouldSuppressBlightChestClick(label, now: 1_000).Should().BeFalse();
    }

    [TestMethod]
    public void ShouldSuppressBlightChestClick_ReturnsFalse_ForNonBlightChest_EvenWhenTransitioned()
    {
        var suppression = new BlightChestTransitionSuppression();
        EntityProbe item = CreateItem(path: "Metadata/Chests/OtherChest", address: 0x100, isTransitioned: true);
        LabelOnGround label = OffscreenStickyTargetGraphShaper.CreateVisibleLabel(item);

        suppression.ShouldSuppressBlightChestClick(label, now: 1_000).Should().BeFalse();
        suppression.ShouldSuppressBlightChestClick(label, now: 2_000).Should().BeFalse();
    }

    [TestMethod]
    public void ShouldSuppressBlightChestClick_ReturnsFalse_ForBlightChest_NotYetTransitioned()
    {
        var suppression = new BlightChestTransitionSuppression();
        EntityProbe item = CreateItem(BlightChestPath, 0x100, isTransitioned: false);
        LabelOnGround label = OffscreenStickyTargetGraphShaper.CreateVisibleLabel(item);

        suppression.ShouldSuppressBlightChestClick(label, now: 1_000).Should().BeFalse();
        suppression.ShouldSuppressBlightChestClick(label, now: 2_000).Should().BeFalse();
    }

    [TestMethod]
    public void ShouldSuppressBlightChestClick_ReleasesAfterTwoSeconds_EvenWhileTransitioned()
    {
        var suppression = new BlightChestTransitionSuppression();
        EntityProbe item = CreateItem(BlightChestPath, 0x100, isTransitioned: false);
        LabelOnGround label = OffscreenStickyTargetGraphShaper.CreateVisibleLabel(item);

        suppression.ShouldSuppressBlightChestClick(label, now: 1_000).Should().BeFalse();

        item.IsTransitioned = true;

        suppression.ShouldSuppressBlightChestClick(label, now: 2_000).Should().BeTrue();
        suppression.ShouldSuppressBlightChestClick(label, now: 3_000).Should().BeTrue();
        suppression.ShouldSuppressBlightChestClick(label, now: 3_999).Should().BeTrue();
        suppression.ShouldSuppressBlightChestClick(label, now: 4_500).Should().BeFalse(
            "a chest that stays transitioned is released once the 2-second window elapses");
    }

    [TestMethod]
    public void ShouldSuppressBlightChestClick_ReleasesTwoSecondsAfterTransitionClears()
    {
        var suppression = new BlightChestTransitionSuppression();
        EntityProbe item = CreateItem(BlightChestPath, 0x100, isTransitioned: false);
        LabelOnGround label = OffscreenStickyTargetGraphShaper.CreateVisibleLabel(item);

        suppression.ShouldSuppressBlightChestClick(label, now: 1_000).Should().BeFalse();

        item.IsTransitioned = true;
        suppression.ShouldSuppressBlightChestClick(label, now: 2_000).Should().BeTrue();

        item.IsTransitioned = false;
        suppression.ShouldSuppressBlightChestClick(label, now: 3_000).Should().BeTrue(
            "the 2-second floor still applies after the flag clears");
        suppression.ShouldSuppressBlightChestClick(label, now: 4_500).Should().BeFalse(
            "the chest becomes clickable again once the window has elapsed");
    }

    [TestMethod]
    public void ShouldSuppressBlightChestClick_ReArms_OnFreshFalseToTrueTransition_AfterRelease()
    {
        var suppression = new BlightChestTransitionSuppression();
        EntityProbe item = CreateItem(BlightChestPath, 0x100, isTransitioned: false);
        LabelOnGround label = OffscreenStickyTargetGraphShaper.CreateVisibleLabel(item);

        suppression.ShouldSuppressBlightChestClick(label, now: 1_000).Should().BeFalse();

        item.IsTransitioned = true;
        suppression.ShouldSuppressBlightChestClick(label, now: 2_000).Should().BeTrue();
        suppression.ShouldSuppressBlightChestClick(label, now: 5_500).Should().BeFalse();

        item.IsTransitioned = false;
        suppression.ShouldSuppressBlightChestClick(label, now: 6_000).Should().BeFalse();

        item.IsTransitioned = true;
        suppression.ShouldSuppressBlightChestClick(label, now: 6_500).Should().BeTrue(
            "a fresh transition re-arms suppression after the chest was released");
    }

    [TestMethod]
    public void ShouldSuppressBlightChestClick_OnlySuppressesTransitioningChest_OthersRemainClickable()
    {
        var suppression = new BlightChestTransitionSuppression();
        EntityProbe transitioning = CreateItem(BlightChestPath, 0x100, isTransitioned: false);
        EntityProbe other = CreateItem(BlightChestPath, 0x200, isTransitioned: false);
        LabelOnGround transitioningLabel = OffscreenStickyTargetGraphShaper.CreateVisibleLabel(transitioning);
        LabelOnGround otherLabel = OffscreenStickyTargetGraphShaper.CreateVisibleLabel(other);

        suppression.ShouldSuppressBlightChestClick(transitioningLabel, now: 1_000).Should().BeFalse();
        suppression.ShouldSuppressBlightChestClick(otherLabel, now: 1_000).Should().BeFalse();

        transitioning.IsTransitioned = true;

        suppression.ShouldSuppressBlightChestClick(transitioningLabel, now: 2_000).Should().BeTrue();
        suppression.ShouldSuppressBlightChestClick(otherLabel, now: 2_000).Should().BeFalse();
        suppression.ShouldSuppressBlightChestClick(transitioningLabel, now: 5_500).Should().BeFalse(
            "the transitioning chest is released once its window elapses");
        suppression.ShouldSuppressBlightChestClick(otherLabel, now: 5_500).Should().BeFalse();
    }

    [TestMethod]
    public void ShouldSuppressBlightChestClick_StaysSuppressed_WhileTransitionRemainsTrue()
    {
        var suppression = new BlightChestTransitionSuppression();
        EntityProbe item = CreateItem(BlightChestPath, 0x100, isTransitioned: true);
        LabelOnGround label = OffscreenStickyTargetGraphShaper.CreateVisibleLabel(item);

        suppression.ShouldSuppressBlightChestClick(label, now: 1_000).Should().BeTrue();
        suppression.ShouldSuppressBlightChestClick(label, now: 1_500).Should().BeTrue();
        suppression.ShouldSuppressBlightChestClick(label, now: 5_000).Should().BeFalse(
            "a chest first observed already transitioned is released once the 3-second window elapses");

        item.IsTransitioned = false;
        suppression.ShouldSuppressBlightChestClick(label, now: 6_000).Should().BeFalse();
        suppression.ShouldSuppressBlightChestClick(label, now: 8_500).Should().BeFalse();
    }

    [TestMethod]
    public void ShouldSuppressBlightChestClick_ReArms_AfterRelease_WhileFlagStaysTrue()
    {
        var suppression = new BlightChestTransitionSuppression();
        EntityProbe item = CreateItem(BlightChestPath, 0x100, isTransitioned: false);
        LabelOnGround label = OffscreenStickyTargetGraphShaper.CreateVisibleLabel(item);

        suppression.ShouldSuppressBlightChestClick(label, now: 1_000).Should().BeFalse();

        item.IsTransitioned = true;
        suppression.ShouldSuppressBlightChestClick(label, now: 2_000).Should().BeTrue();
        suppression.ShouldSuppressBlightChestClick(label, now: 5_500).Should().BeFalse(
            "the chest is released for one click attempt once the window elapses");
        suppression.ShouldSuppressBlightChestClick(label, now: 5_600).Should().BeTrue(
            "re-observation while the flag is still set re-arms a fresh window");
        suppression.ShouldSuppressBlightChestClick(label, now: 8_700).Should().BeFalse(
            "the fresh window also elapses, throttling to one click attempt per window");
    }

    private static EntityProbe CreateItem(string path, long address, bool isTransitioned)
    {
        EntityProbe item = (EntityProbe)EntityProbeFactory.Create(path: path, address: address);
        item.IsTransitioned = isTransitioned;
        return item;
    }
}
