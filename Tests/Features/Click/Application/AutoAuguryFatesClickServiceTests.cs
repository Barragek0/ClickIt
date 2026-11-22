namespace ClickIt.Tests.Features.Click.Application
{
    [TestClass]
    public sealed class AutoAuguryFatesClickServiceTests
    {
        [TestMethod]
        [TestCategory("Scenario")]
        public void TryReadPanelForPlugin_Choosing_UsesAutoAuguryBestOfResult()
        {
            global::AutoAugury.AutoAugury plugin = new(
                new global::AutoAugury.FatePanelState(ChoosingFate: true, InvokingFates: false, RectangleF.Empty, []),
                [
                    new global::AutoAugury.FateScore("Fate A", new RectangleF(10, 20, 30, 40), 2),
                    new global::AutoAugury.FateScore("Fate B", new RectangleF(50, 60, 30, 40), 9),
                    new global::AutoAugury.FateScore("Fate C", new RectangleF(90, 100, 30, 40), 5)
                ]);
            AutoAuguryFatesClickService service = CreateService();

            service.TryReadPanelForPlugin(plugin, out AutoAuguryFatesPanelRead read).Should().BeTrue();

            read.ChoosingFate.Should().BeTrue();
            read.InvokingFates.Should().BeFalse();
            read.Panel.Should().BeNull();
            read.ConfirmRect.Should().Be(RectangleF.Empty);
            read.Recommendations.Should().ContainSingle()
                .Which.Should().Be(new AutoAuguryRecommendedFate("Fate B", new RectangleF(50, 60, 30, 40), 9));
            read.NextInvocation.Should().BeNull();
            read.GuideComputed.Should().BeFalse();
        }

        [TestMethod]
        [TestCategory("Boundary")]
        public void TryReadPanelForPlugin_ChoosingTie_KeepsEveryBestCardInPanelOrder()
        {
            global::AutoAugury.AutoAugury plugin = new(
                new global::AutoAugury.FatePanelState(ChoosingFate: true, InvokingFates: false, RectangleF.Empty, []),
                [
                    new global::AutoAugury.FateScore("Fate A", new RectangleF(10, 20, 30, 40), 0),
                    new global::AutoAugury.FateScore("Fate B", new RectangleF(50, 60, 30, 40), 0),
                    new global::AutoAugury.FateScore("Fate C", new RectangleF(90, 100, 30, 40), 0),
                    new global::AutoAugury.FateScore("Fate D", new RectangleF(130, 140, 30, 40), 0)
                ]);
            AutoAuguryFatesClickService service = CreateService();

            service.TryReadPanelForPlugin(plugin, out AutoAuguryFatesPanelRead read).Should().BeTrue();

            read.Recommendations.Select(static fate => fate.Name)
                .Should().Equal("Fate A", "Fate B", "Fate C", "Fate D");
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void SelectChoice_FromATie_TakesOnlyTheFirstOption()
        {
            AutoAuguryRecommendedFate[] tied =
            [
                new("Fate A", new RectangleF(10, 20, 30, 40)),
                new("Fate B", new RectangleF(50, 60, 30, 40)),
                new("Fate C", new RectangleF(90, 100, 30, 40)),
                new("Fate D", new RectangleF(130, 140, 30, 40))
            ];

            AutoAuguryFatesClickService.SelectChoice(tied)
                .Should().BeSameAs(tied[0]);
            AutoAuguryFatesClickService.SelectChoice([])
                .Should().BeNull();
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void SelectChoice_PreservesFirstRecommendation()
        {
            AutoAuguryRecommendedFate[] rated =
            [
                new("Weak", new RectangleF(10, 20, 30, 40), 12),
                new("Flat +50", new RectangleF(50, 60, 30, 40), 50),
                new("Flat +100", new RectangleF(90, 100, 30, 40), 100),
                new("Worthless", new RectangleF(130, 140, 30, 40), 0)
            ];

            AutoAuguryFatesClickService.SelectChoice(rated).Should().BeSameAs(rated[0]);
            rated[0].Score.Should().Be(12);
        }

        [TestMethod]
        [TestCategory("Boundary")]
        public void SelectChoice_DoesNotOverrideAutoAuguryWithRawScores()
        {
            AutoAuguryRecommendedFate[] rated =
            [
                new("Negative", new RectangleF(10, 20, 30, 40), -40),
                new("Better", new RectangleF(50, 60, 30, 40), 7)
            ];

            AutoAuguryFatesClickService.SelectChoice(rated).Should().BeSameAs(rated[0]);
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void TryReadPanelForPlugin_Choosing_CarriesEachCardsScore()
        {
            global::AutoAugury.AutoAugury plugin = new(
                new global::AutoAugury.FatePanelState(ChoosingFate: true, InvokingFates: false, RectangleF.Empty, []),
                [
                    new global::AutoAugury.FateScore("Fate A", new RectangleF(10, 20, 30, 40), 3),
                    new global::AutoAugury.FateScore("Fate B", new RectangleF(50, 60, 30, 40), 90),
                    new global::AutoAugury.FateScore("Fate C", new RectangleF(90, 100, 30, 40), 40)
                ]);
            AutoAuguryFatesClickService service = CreateService();

            service.TryReadPanelForPlugin(plugin, out AutoAuguryFatesPanelRead read).Should().BeTrue();

            read.Recommendations.Should().ContainSingle()
                .Which.Score.Should().Be(90);
            AutoAuguryFatesClickService.SelectChoice(read.Recommendations)!.Name.Should().Be("Fate B");
        }

        [TestMethod]
        [TestCategory("Scenario")]
        public void TryReadPanelForPlugin_Invoking_UsesAutoAuguryNextCard()
        {
            global::AutoAugury.FateCard card = new("Fate B", new RectangleF(50, 60, 30, 40));
            global::AutoAugury.AutoAugury plugin = new(
                new global::AutoAugury.FatePanelState(
                    ChoosingFate: false,
                    InvokingFates: true,
                    RectangleF.Empty,
                    [new global::AutoAugury.FateCard("Fate A", new RectangleF(10, 20, 30, 40))]),
                [],
                new global::AutoAugury.InvocationGuide(
                    new global::AutoAugury.InvocationAdvice([new global::AutoAugury.InvocationPick(card, 1)], 15, 3),
                    new global::AutoAugury.InvocationPick(card, 1)));
            AutoAuguryFatesClickService service = CreateService();

            service.TryReadPanelForPlugin(plugin, out AutoAuguryFatesPanelRead read).Should().BeTrue();

            read.ChoosingFate.Should().BeFalse();
            read.InvokingFates.Should().BeTrue();
            read.TopRowCount.Should().Be(1);
            read.NextInvocation.Should().Be(new AutoAuguryRecommendedFate("Fate B", new RectangleF(50, 60, 30, 40)));
            read.GuideComputed.Should().BeTrue();
            read.Recommendations.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory("Boundary")]
        public void TryReadPanelForPlugin_InvokingWithEveryPickPlaced_HasNoNextCard()
        {
            global::AutoAugury.AutoAugury plugin = new(
                new global::AutoAugury.FatePanelState(
                    ChoosingFate: false,
                    InvokingFates: true,
                    RectangleF.Empty,
                    [
                        new global::AutoAugury.FateCard("Fate A", new RectangleF(10, 20, 30, 40)),
                        new global::AutoAugury.FateCard("Fate B", new RectangleF(50, 60, 30, 40))
                    ]),
                [],
                new global::AutoAugury.InvocationGuide(
                    new global::AutoAugury.InvocationAdvice([], 30, 3), null));
            AutoAuguryFatesClickService service = CreateService();

            service.TryReadPanelForPlugin(plugin, out AutoAuguryFatesPanelRead read).Should().BeTrue();

            read.TopRowCount.Should().Be(2);
            read.NextInvocation.Should().BeNull();
            read.GuideComputed.Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("EdgeCase")]
        public void TryReadPanelForPlugin_InvokingWithNothingWorthASlot_IsStillAComputedGuide()
        {
            global::AutoAugury.AutoAugury plugin = new(
                new global::AutoAugury.FatePanelState(
                    ChoosingFate: false,
                    InvokingFates: true,
                    RectangleF.Empty,
                    [new global::AutoAugury.FateCard("Fate A", new RectangleF(10, 20, 30, 40))]),
                [],
                new global::AutoAugury.InvocationGuide(new global::AutoAugury.InvocationAdvice([], 0, 3), null));
            AutoAuguryFatesClickService service = CreateService();

            service.TryReadPanelForPlugin(plugin, out AutoAuguryFatesPanelRead read).Should().BeTrue();

            read.TopRowCount.Should().Be(1);
            read.NextInvocation.Should().BeNull();
            read.GuideComputed.Should().BeTrue("an empty advice over a computed guide still lets ClickIt confirm");
        }

        [TestMethod]
        [TestCategory("EdgeCase")]
        public void TryReadPanelForPlugin_InvokingBeforeTheGuideIsComputed_ReportsUncomputedGuide()
        {
            global::AutoAugury.AutoAugury plugin = new(
                new global::AutoAugury.FatePanelState(ChoosingFate: false, InvokingFates: true, RectangleF.Empty, []),
                [],
                global::AutoAugury.InvocationGuide.None);
            AutoAuguryFatesClickService service = CreateService();

            service.TryReadPanelForPlugin(plugin, out AutoAuguryFatesPanelRead read).Should().BeTrue();

            read.InvokingFates.Should().BeTrue();
            read.NextInvocation.Should().BeNull();
            read.GuideComputed.Should().BeFalse();
        }

        [TestMethod]
        [TestCategory("Boundary")]
        public void TryReadPanelForPlugin_PanelClosed_ReportsNoView()
        {
            global::AutoAugury.AutoAugury plugin = new(
                new global::AutoAugury.FatePanelState(ChoosingFate: false, InvokingFates: false, RectangleF.Empty, []),
                []);
            AutoAuguryFatesClickService service = CreateService();

            service.TryReadPanelForPlugin(plugin, out AutoAuguryFatesPanelRead read).Should().BeTrue();

            read.ChoosingFate.Should().BeFalse();
            read.InvokingFates.Should().BeFalse();
            read.Panel.Should().BeNull();
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void BoundsMatch_AllowsOnlySmallUiRefreshDrift()
        {
            RectangleF expected = new(10, 20, 30, 40);

            AutoAuguryFatesClickService.BoundsMatch(expected, new RectangleF(12, 18, 32, 38)).Should().BeTrue();
            AutoAuguryFatesClickService.BoundsMatch(expected, new RectangleF(13, 20, 30, 40)).Should().BeFalse();
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void IsPublishedBoundsUsable_AllowsAnUnpublishedRectangle()
        {
            AutoAuguryFatesClickService
                .IsPublishedBoundsUsable(RectangleF.Empty, new RectangleF(850, 500, 221, 52))
                .Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void IsPublishedBoundsUsable_StillRejectsAStaleRectangle()
        {
            RectangleF published = new(850, 500, 221, 52);

            AutoAuguryFatesClickService.IsPublishedBoundsUsable(published, new RectangleF(850, 520, 221, 52))
                .Should().BeFalse("a card that moved must not be clicked at its old position");
            AutoAuguryFatesClickService.IsPublishedBoundsUsable(published, new RectangleF(851, 501, 220, 51))
                .Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void IsPlacementInProgress_StopsASecondClickOnTheSameCard()
        {
            AutoAuguryRecommendedFate deathbringer =
                new("Deathbringer", new RectangleF(572, 791, 259, 48));

            AutoAuguryFatesClickService.IsPlacementInProgress(1, deathbringer.Bounds, deathbringer)
                .Should().BeTrue("the card was clicked and the guide has not moved on yet");
        }

        [TestMethod]
        [TestCategory("Boundary")]
        public void IsPlacementInProgress_AllowsTheNextCard()
        {
            RectangleF clicked = new(572, 791, 259, 48);
            AutoAuguryRecommendedFate sanguine = new("Sanguine", new RectangleF(830, 791, 259, 48));

            AutoAuguryFatesClickService.IsPlacementInProgress(1, clicked, sanguine).Should().BeFalse();
            AutoAuguryFatesClickService.IsPlacementInProgress(0, RectangleF.Empty, sanguine).Should().BeFalse();
            AutoAuguryFatesClickService.IsPlacementInProgress(1, clicked, null).Should().BeFalse();
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void ResolvePlacement_DropsASkippedCardSoThePanelCanBeConfirmed()
        {
            AutoAuguryRecommendedFate deathbringer =
                new("Deathbringer", new RectangleF(572, 791, 259, 48));
            RectangleF[] skipped = [deathbringer.Bounds];

            AutoAuguryFatesClickService.ResolvePlacement(deathbringer, skipped)
                .Should().BeNull("a card whose placement never registers must not block confirm forever");
        }

        [TestMethod]
        [TestCategory("Boundary")]
        public void ResolvePlacement_ReturnsAnUnskippedCard()
        {
            AutoAuguryRecommendedFate sanguine = new("Sanguine", new RectangleF(830, 791, 259, 48));
            RectangleF[] skipped = [new RectangleF(572, 791, 259, 48)];

            AutoAuguryFatesClickService.ResolvePlacement(sanguine, skipped).Should().Be(sanguine);
            AutoAuguryFatesClickService.ResolvePlacement(sanguine, []).Should().Be(sanguine);
            AutoAuguryFatesClickService.ResolvePlacement(null, skipped).Should().BeNull();
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void ShouldHoldAbsentPanel_EndsTheAttemptAtOnce_WhenConfirmWasSentAndThePanelIsGone()
        {
            AutoAuguryFatesClickService.ShouldHoldAbsentPanel(
                confirmSent: true, panelElementGone: true, absentSince: 1_000, now: 1_001).Should().BeFalse();
        }

        [TestMethod]
        [TestCategory("Boundary")]
        public void ShouldHoldAbsentPanel_StillHoldsWhenConfirmWasSentButThePanelIsVisible()
        {
            AutoAuguryFatesClickService.ShouldHoldAbsentPanel(
                confirmSent: true, panelElementGone: false, absentSince: 1_000, now: 1_001).Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void ShouldHoldAbsentPanel_StillProtectsAnUncommittedSelection()
        {
            AutoAuguryFatesClickService.ShouldHoldAbsentPanel(
                confirmSent: false, panelElementGone: true, absentSince: 1_000, now: 1_001).Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("Boundary")]
        public void ShouldHoldAbsentPanel_ExpiresTheGraceEvenAfterConfirm()
        {
            AutoAuguryFatesClickService.ShouldHoldAbsentPanel(
                confirmSent: false, panelElementGone: true, absentSince: 1_000, now: 9_000).Should().BeFalse();
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void IsWithinPanelAbsenceGrace_HoldsAnAttemptThroughOneFlickeringRead()
        {
            // A transient closed read must not clear selection and cause the reported double click.
            AutoAuguryFatesClickService.IsWithinPanelAbsenceGrace(absentSince: 1_000, now: 1_100).Should().BeTrue();
            AutoAuguryFatesClickService.IsWithinPanelAbsenceGrace(absentSince: 1_000, now: 1_499).Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("Boundary")]
        public void IsWithinPanelAbsenceGrace_EndsTheAttemptOnceThePanelIsReallyGone()
        {
            AutoAuguryFatesClickService.IsWithinPanelAbsenceGrace(absentSince: 1_000, now: 1_500).Should().BeFalse();
            AutoAuguryFatesClickService.IsWithinPanelAbsenceGrace(absentSince: 1_000, now: 9_000).Should().BeFalse();

            AutoAuguryFatesClickService.IsWithinPanelAbsenceGrace(null, 9_000).Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void IsWithinOpeningSettle_HoldsTheFirstClickWhileThePanelOpens()
        {
            AutoAuguryFatesClickService.IsWithinOpeningSettle(openedAt: 1_000, now: 1_050).Should().BeTrue();
            AutoAuguryFatesClickService.IsWithinOpeningSettle(openedAt: 1_000, now: 1_099).Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("Boundary")]
        public void IsWithinOpeningSettle_AllowsClicksOnceThePanelHasOpened()
        {
            AutoAuguryFatesClickService.IsWithinOpeningSettle(openedAt: 1_000, now: 1_100).Should().BeFalse();
            AutoAuguryFatesClickService.IsWithinOpeningSettle(openedAt: 1_000, now: 5_000).Should().BeFalse();

            AutoAuguryFatesClickService.IsWithinOpeningSettle(null, 5_000).Should().BeFalse();
        }

        [TestMethod]
        [TestCategory("Boundary")]
        public void IsHoverOnElement_RejectsAZeroAddress()
        {
            AutoAuguryFatesClickService.IsHoverOnElement(null, 0).Should().BeFalse();
        }

        private static AutoAuguryFatesClickService CreateService()
            => new(null!, null, null!, static (_, _) => true, static (_, _, _) => true,
                static () => { }, static _ => { });
    }
}

namespace AutoAugury
{
    internal sealed class AutoAugury
    {
        private readonly IReadOnlyList<FateScore> _scores;
        private readonly FatePanelState _panelState;
        private readonly FatesPanel _fatesPanel = new();
        private readonly InvocationGuide _guide;

        internal AutoAugury(
            FatePanelState panelState,
            IReadOnlyList<FateScore> scores,
            InvocationGuide? guide = null)
        {
            _panelState = panelState;
            _scores = scores;
            _guide = guide ?? InvocationGuide.None;
        }
    }

    internal sealed record FatePanelState(
        bool ChoosingFate,
        bool InvokingFates,
        RectangleF ConfirmButton,
        IReadOnlyList<FateCard> Offered)
    {
        internal static readonly FatePanelState Closed = new(false, false, RectangleF.Empty, []);
    }

    internal sealed class FatesPanel
    {
        private Element? _panel;

        internal Element? Panel
        {
            get => _panel;
            set => _panel = value;
        }
    }

    internal sealed record FateCard(string Name, RectangleF Rect);

    internal sealed record FateScore(string Name, FateCard Card, double Value)
    {
        internal FateScore(string name, RectangleF bounds, double value) : this(name, new FateCard(name, bounds), value)
        {
        }
    }

    internal sealed record InvocationPick(FateCard Card, int Order);

    internal sealed record InvocationAdvice(IReadOnlyList<InvocationPick> Picks, int Total, int Slots)
    {
        internal static readonly InvocationAdvice None = new([], 0, 0);
    }

    internal sealed record InvocationGuide(InvocationAdvice Advice, InvocationPick? Next)
    {
        internal static readonly InvocationGuide None = new(InvocationAdvice.None, null);
    }

    internal static class FateScoring
    {
        internal static List<FateScore> BestOf(IReadOnlyList<FateScore> scores)
        {
            if (scores.Count == 0)
                return [];

            double highest = scores.Max(static score => score.Value);
            return scores.Where(score => score.Value == highest).ToList();
        }
    }
}
