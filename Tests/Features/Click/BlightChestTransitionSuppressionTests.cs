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
            "a chest that stays transitioned becomes clickable again once the 2-second window elapses");
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
    public void ShouldSuppressBlightChestClick_NeverReArms_AfterFirstTransition()
    {
        var suppression = new BlightChestTransitionSuppression();
        EntityProbe item = CreateItem(BlightChestPath, 0x100, isTransitioned: false);
        LabelOnGround label = OffscreenStickyTargetGraphShaper.CreateVisibleLabel(item);

        suppression.ShouldSuppressBlightChestClick(label, now: 1_000).Should().BeFalse();

        item.IsTransitioned = true;
        suppression.ShouldSuppressBlightChestClick(label, now: 2_000).Should().BeTrue("the first false -> true transition arms the blacklist");
        suppression.ShouldSuppressBlightChestClick(label, now: 4_500).Should().BeFalse("the blacklist is released once the window elapses");

        // The blacklist is never re-armed after the first transition, even while the flag stays set or toggles back to true — post-first-click transitions cannot be detected.
        suppression.ShouldSuppressBlightChestClick(label, now: 5_000).Should().BeFalse();
        item.IsTransitioned = false;
        suppression.ShouldSuppressBlightChestClick(label, now: 5_500).Should().BeFalse();
        item.IsTransitioned = true;
        suppression.ShouldSuppressBlightChestClick(label, now: 6_000).Should().BeFalse("a later transition must not re-arm the blacklist");
    }

    [TestMethod]
    public void ShouldSuppressBlightChestClick_NeverReArms_EvenAfterBlacklistPruning()
    {
        var suppression = new BlightChestTransitionSuppression();
        EntityProbe item = CreateItem(BlightChestPath, 0x100, isTransitioned: false);
        LabelOnGround label = OffscreenStickyTargetGraphShaper.CreateVisibleLabel(item);

        suppression.ShouldSuppressBlightChestClick(label, now: 1_000).Should().BeFalse();
        item.IsTransitioned = true;
        suppression.ShouldSuppressBlightChestClick(label, now: 2_000).Should().BeTrue();
        suppression.ShouldSuppressBlightChestClick(label, now: 4_500).Should().BeFalse("released after the window");

        // Enough other chests transitioning to force the blacklist past its cap and trigger pruning.
        for (int i = 0; i < 300; i++)
        {
            EntityProbe other = CreateItem(BlightChestPath, 0x1000 + i, isTransitioned: true);
            LabelOnGround otherLabel = OffscreenStickyTargetGraphShaper.CreateVisibleLabel(other);
            suppression.ShouldSuppressBlightChestClick(otherLabel, now: 5_000 + i);
        }

        // The already-transitioned chest must still never re-arm, even though pruning ran in between.
        suppression.ShouldSuppressBlightChestClick(label, now: 9_000).Should().BeFalse(
            "pruning the blacklist must not erase the never-re-arm guarantee");
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
            "a chest first observed already transitioned is released once the 2-second window elapses");

        item.IsTransitioned = false;
        suppression.ShouldSuppressBlightChestClick(label, now: 6_000).Should().BeFalse();
        suppression.ShouldSuppressBlightChestClick(label, now: 8_500).Should().BeFalse();
    }


    private static EntityProbe CreateItem(string path, long address, bool isTransitioned)
    {
        EntityProbe item = (EntityProbe)EntityProbeFactory.Create(path: path, address: address);
        item.IsTransitioned = isTransitioned;
        return item;
    }
}
