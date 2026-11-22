namespace ClickIt.Tests.Features.Click.Application;

[TestClass]
public sealed class AutoAuguryFatesClickServiceTests
{
    private static readonly RectangleF CardRect = new(50, 60, 100, 120);
    private static readonly RectangleF ConfirmRect = new(400, 500, 100, 40);

    [TestMethod]
    [TestCategory("Regression")]
    public void OpeningAndHover_RequireASettledPanelAndReadableTarget()
    {
        AutoAuguryFatesClickService.IsWithinOpeningSettle(1000, 1099).Should().BeTrue();
        AutoAuguryFatesClickService.IsWithinOpeningSettle(1000, 1100).Should().BeFalse();
        AutoAuguryFatesClickService.IsHoverOnElement(null, 0).Should().BeFalse();
        AutoAuguryFatesClickService.IsHoverOnElement(null, 123).Should().BeFalse();
    }

    [TestMethod]
    [TestCategory("Scenario")]
    public void Cauldron_ChoosesUnselectedCardThenConfirmsSelectedCard()
    {
        var card = new AutoAuguryFatesClickService.Card("Fate", CardRect, false);
        var advice = new AutoAuguryFatesClickService.Advice(true, false, "Fate", CardRect, ConfirmRect);
        AutoAuguryFatesClickService.TryChooseAction(advice, [card], out var target, out bool confirm).Should().BeTrue();
        target.Should().Be(card);
        confirm.Should().BeFalse();

        advice = advice with { Fate = "", CardRect = default };
        AutoAuguryFatesClickService.TryChooseAction(advice, [card with { Selected = true }], out _, out confirm).Should().BeTrue();
        confirm.Should().BeTrue();
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void Boss_RespectsOrderAndWaitsForSelectionBeforeProceeding()
    {
        RectangleF second = new(200, 60, 100, 120);
        var first = new AutoAuguryFatesClickService.Card("Fate", CardRect, false);
        var next = new AutoAuguryFatesClickService.Card("Fate", second, false);
        var advice = new AutoAuguryFatesClickService.Advice(true, true, "Fate", CardRect, ConfirmRect);
        AutoAuguryFatesClickService.TryChooseAction(advice, [first, next], out var target, out _).Should().BeTrue();
        target.Rect.Should().Be(CardRect);
        var pending = new AutoAuguryFatesClickService.PendingClick(true, false, "Fate", CardRect, 0);
        AutoAuguryFatesClickService.HasPlacementAdvanced(pending, advice, [first, next]).Should().BeFalse();
        advice = advice with { CardRect = second };
        AutoAuguryFatesClickService.HasPlacementAdvanced(pending, advice, [first with { Selected = true }, next]).Should().BeTrue();
        AutoAuguryFatesClickService.TryChooseAction(advice, [first with { Selected = true }, next], out target, out _).Should().BeTrue();
        target.Rect.Should().Be(second);
    }

    [TestMethod]
    [TestCategory("Boundary")]
    public void BridgeAdvice_DisagreesOrIsNotReady_DoesNotAuthorizeClick()
    {
        var card = new AutoAuguryFatesClickService.Card("Fate", CardRect, false);
        var advice = new AutoAuguryFatesClickService.Advice(true, true, "Different", CardRect, ConfirmRect);
        AutoAuguryFatesClickService.TryChooseAction(advice, [card], out _, out _).Should().BeFalse();
        AutoAuguryFatesClickService.TryChooseAction(advice with { Ready = false }, [card], out _, out _).Should().BeFalse();
        AutoAuguryFatesClickService.TryChooseAction(advice with { Fate = "", CardRect = default }, [], out _, out bool confirm).Should().BeTrue();
        confirm.Should().BeTrue();
        var pendingConfirm = new AutoAuguryFatesClickService.PendingClick(true, true, "", ConfirmRect, 0);
        AutoAuguryFatesClickService.HasPlacementAdvanced(pendingConfirm, advice, [card with { Selected = true }]).Should().BeFalse();
    }
}
