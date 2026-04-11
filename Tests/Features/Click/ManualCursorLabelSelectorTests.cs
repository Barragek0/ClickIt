namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class ManualCursorLabelSelectorTests
    {
        [TestMethod]
        public void TryResolveCandidate_ReturnsFalse_WhenLabelsMissing()
        {
            var selector = CreateSelector();

            bool resolved = selector.TryResolveCandidate(null, Vector2.Zero, Vector2.Zero, out LabelOnGround? selectedLabel, out string? mechanicId);

            resolved.Should().BeFalse();
            selectedLabel.Should().BeNull();
            mechanicId.Should().BeNull();
        }

        [TestMethod]
        public void TryResolveCandidate_ReturnsFalse_WhenCandidateEntryIsNull()
        {
            var selector = CreateSelector();

            bool resolved = selector.TryResolveCandidate([null!], Vector2.Zero, Vector2.Zero, out LabelOnGround? selectedLabel, out string? mechanicId);

            resolved.Should().BeFalse();
            selectedLabel.Should().BeNull();
            mechanicId.Should().BeNull();
        }

        [TestMethod]
        public void TryResolveCandidate_ReturnsTrue_WhenCursorInsideLabelRect_AndMechanicIdResolves()
        {
            Entity item = EntityProbeFactory.Create(
                path: "Metadata/Monsters/TestManualCursor",
                isValid: true,
                type: EntityType.Monster);
            LabelOnGround label = CreateLabelWithRect(item, new RectangleF(10f, 20f, 40f, 20f));
            var selector = CreateSelector(getMechanicIdForLabel: candidate => ReferenceEquals(candidate, label) ? "manual-cursor" : null);

            bool resolved = selector.TryResolveCandidate(
                [label],
                cursorAbsolute: new Vector2(30f, 30f),
                windowTopLeft: Vector2.Zero,
                out LabelOnGround? selectedLabel,
                out string? mechanicId);

            resolved.Should().BeTrue();
            selectedLabel.Should().BeSameAs(label);
            mechanicId.Should().Be("manual-cursor");
        }

        [TestMethod]
        public void TryResolveCandidate_ReturnsFalse_WhenHoveredUltimatumCandidateIsInactive()
        {
            Entity ultimatumItem = EntityProbeFactory.Create(
                path: Constants.UltimatumChallengeInteractablePath,
                isValid: true,
                type: EntityType.Monster);
            LabelOnGround label = CreateLabelWithRect(ultimatumItem, new RectangleF(10f, 20f, 40f, 20f));
            var selector = CreateSelector(getMechanicIdForLabel: _ => MechanicIds.UltimatumInitialOverlay);

            bool resolved = selector.TryResolveCandidate(
                [label],
                cursorAbsolute: new Vector2(30f, 30f),
                windowTopLeft: Vector2.Zero,
                out LabelOnGround? selectedLabel,
                out string? mechanicId);

            resolved.Should().BeFalse();
            selectedLabel.Should().BeNull();
            mechanicId.Should().BeNull();
        }

        [TestMethod]
        public void TryResolveCandidate_ReturnsFalse_WhenMechanicIdIsBlank()
        {
            Entity item = EntityProbeFactory.Create(
                path: "Metadata/Monsters/BlankMechanic",
                isValid: true,
                type: EntityType.Monster);
            LabelOnGround label = CreateLabelWithRect(item, new RectangleF(10f, 20f, 40f, 20f));
            var selector = CreateSelector(getMechanicIdForLabel: _ => "   ");

            bool resolved = selector.TryResolveCandidate(
                [label],
                cursorAbsolute: new Vector2(30f, 30f),
                windowTopLeft: Vector2.Zero,
                out LabelOnGround? selectedLabel,
                out string? mechanicId);

            resolved.Should().BeFalse();
            selectedLabel.Should().BeNull();
            mechanicId.Should().BeNull();
        }

        [TestMethod]
        public void TryResolveCandidate_ReturnsTrue_WhenCursorNearGroundProjection_AndLabelRectMisses()
        {
            GameController gameController = CreateProjectionGameController(new FakeProjectedPoint { X = 32f, Y = 32f });
            Entity item = EntityProbeFactory.Create(
                path: "Metadata/Monsters/Projected",
                isValid: true,
                type: EntityType.Monster,
                posX: 1f,
                posY: 2f,
                posZ: 3f);
            LabelOnGround label = CreateLabelWithRect(item, new RectangleF(200f, 200f, 40f, 20f));
            var selector = CreateSelector(
                gameController: gameController,
                getMechanicIdForLabel: candidate => ReferenceEquals(candidate, label) ? "projected" : null);

            bool resolved = selector.TryResolveCandidate(
                [label],
                cursorAbsolute: new Vector2(30f, 30f),
                windowTopLeft: Vector2.Zero,
                out LabelOnGround? selectedLabel,
                out string? mechanicId);

            resolved.Should().BeTrue();
            selectedLabel.Should().BeSameAs(label);
            mechanicId.Should().Be("projected");
        }

        [TestMethod]
        public void TryResolveCandidate_ReturnsFalse_WhenGroundProjectionIsDisabledForWorldItem()
        {
            GameController gameController = CreateProjectionGameController(new FakeProjectedPoint { X = 32f, Y = 32f });
            Entity item = EntityProbeFactory.Create(
                path: "Metadata/Items/ProjectedWorldItem",
                isValid: true,
                type: EntityType.WorldItem,
                posX: 1f,
                posY: 2f,
                posZ: 3f);
            LabelOnGround label = CreateLabelWithRect(item, new RectangleF(200f, 200f, 40f, 20f));
            var selector = CreateSelector(
                gameController: gameController,
                getMechanicIdForLabel: _ => "world-item");

            bool resolved = selector.TryResolveCandidate(
                [label],
                cursorAbsolute: new Vector2(30f, 30f),
                windowTopLeft: Vector2.Zero,
                out LabelOnGround? selectedLabel,
                out string? mechanicId);

            resolved.Should().BeFalse();
            selectedLabel.Should().BeNull();
            mechanicId.Should().BeNull();
        }

        [TestMethod]
        public void TryResolveCandidate_ReturnsFalse_WhenGroundProjectionIsTooFarFromCursor()
        {
            GameController gameController = CreateProjectionGameController(new FakeProjectedPoint { X = 400f, Y = 400f });
            Entity item = EntityProbeFactory.Create(
                path: "Metadata/Monsters/FarProjection",
                isValid: true,
                type: EntityType.Monster,
                posX: 1f,
                posY: 2f,
                posZ: 3f);
            LabelOnGround label = CreateLabelWithRect(item, new RectangleF(200f, 200f, 40f, 20f));
            var selector = CreateSelector(
                gameController: gameController,
                getMechanicIdForLabel: _ => "far-projection");

            bool resolved = selector.TryResolveCandidate(
                [label],
                cursorAbsolute: new Vector2(30f, 30f),
                windowTopLeft: Vector2.Zero,
                out LabelOnGround? selectedLabel,
                out string? mechanicId);

            resolved.Should().BeFalse();
            selectedLabel.Should().BeNull();
            mechanicId.Should().BeNull();
        }

        [TestMethod]
        public void TryResolveCandidate_PrefersCloserLabelRect_WhenMultipleHoveredCandidatesMatch()
        {
            Entity fartherItem = EntityProbeFactory.Create(
                path: "Metadata/Monsters/Farther",
                isValid: true,
                type: EntityType.Monster);
            Entity closerItem = EntityProbeFactory.Create(
                path: "Metadata/Monsters/Closer",
                isValid: true,
                type: EntityType.Monster);
            LabelOnGround farther = CreateLabelWithRect(fartherItem, new RectangleF(10f, 20f, 60f, 30f));
            LabelOnGround closer = CreateLabelWithRect(closerItem, new RectangleF(20f, 24f, 20f, 12f));
            var selector = CreateSelector(getMechanicIdForLabel: candidate =>
            {
                if (ReferenceEquals(candidate, farther))
                    return "farther";
                if (ReferenceEquals(candidate, closer))
                    return "closer";

                return null;
            });

            bool resolved = selector.TryResolveCandidate(
                [farther, closer],
                cursorAbsolute: new Vector2(30f, 30f),
                windowTopLeft: Vector2.Zero,
                out LabelOnGround? selectedLabel,
                out string? mechanicId);

            resolved.Should().BeTrue();
            selectedLabel.Should().BeSameAs(closer);
            mechanicId.Should().Be("closer");
        }

        [TestMethod]
        public void TryResolveEvaluatedCandidates_ReturnsFalse_WhenCandidatesMissing()
        {
            bool resolved = ManualCursorLabelSelector.TryResolveEvaluatedCandidates(null, out LabelOnGround? selectedLabel, out string? mechanicId);

            resolved.Should().BeFalse();
            selectedLabel.Should().BeNull();
            mechanicId.Should().BeNull();
        }

        [TestMethod]
        public void TryResolveEvaluatedCandidates_PicksLowerLabelRectScore_WhenMultipleHoveredLabelsMatch()
        {
            LabelOnGround farther = ExileCoreOpaqueFactory.CreateOpaqueLabel();
            LabelOnGround closer = ExileCoreOpaqueFactory.CreateOpaqueLabel();

            bool resolved = ManualCursorLabelSelector.TryResolveEvaluatedCandidates(
                [
                    new ManualCursorEvaluatedCandidate(farther, "far", IsSuppressed: false, CursorInsideLabelRect: true, LabelRectScore: 64f, CursorNearGroundProjection: false, GroundProjectionScore: float.MaxValue),
                    new ManualCursorEvaluatedCandidate(closer, "close", IsSuppressed: false, CursorInsideLabelRect: true, LabelRectScore: 9f, CursorNearGroundProjection: false, GroundProjectionScore: float.MaxValue)
                ],
                out LabelOnGround? selectedLabel,
                out string? mechanicId);

            resolved.Should().BeTrue();
            selectedLabel.Should().BeSameAs(closer);
            mechanicId.Should().Be("close");
        }

        [TestMethod]
        public void TryResolveEvaluatedCandidates_UsesGroundProjectionFallback_WhenCursorMissesLabelRect()
        {
            LabelOnGround label = ExileCoreOpaqueFactory.CreateOpaqueLabel();

            bool resolved = ManualCursorLabelSelector.TryResolveEvaluatedCandidates(
                [
                    new ManualCursorEvaluatedCandidate(label, "projected", IsSuppressed: false, CursorInsideLabelRect: false, LabelRectScore: float.MaxValue, CursorNearGroundProjection: true, GroundProjectionScore: 16f)
                ],
                out LabelOnGround? selectedLabel,
                out string? mechanicId);

            resolved.Should().BeTrue();
            selectedLabel.Should().BeSameAs(label);
            mechanicId.Should().Be("projected");
        }

        [TestMethod]
        public void TryResolveEvaluatedCandidates_PrefersProjectionScore_WhenItBeatsRectScore()
        {
            LabelOnGround projectedWinner = ExileCoreOpaqueFactory.CreateOpaqueLabel();
            LabelOnGround rectOnlyCandidate = ExileCoreOpaqueFactory.CreateOpaqueLabel();

            bool resolved = ManualCursorLabelSelector.TryResolveEvaluatedCandidates(
                [
                    new ManualCursorEvaluatedCandidate(projectedWinner, "projection", IsSuppressed: false, CursorInsideLabelRect: true, LabelRectScore: 49f, CursorNearGroundProjection: true, GroundProjectionScore: 4f),
                    new ManualCursorEvaluatedCandidate(rectOnlyCandidate, "rect", IsSuppressed: false, CursorInsideLabelRect: true, LabelRectScore: 9f, CursorNearGroundProjection: false, GroundProjectionScore: float.MaxValue)
                ],
                out LabelOnGround? selectedLabel,
                out string? mechanicId);

            resolved.Should().BeTrue();
            selectedLabel.Should().BeSameAs(projectedWinner);
            mechanicId.Should().Be("projection");
        }

        [TestMethod]
        public void TryResolveEvaluatedCandidates_SkipsSuppressedBlankAndNonHoveredCandidates()
        {
            LabelOnGround valid = ExileCoreOpaqueFactory.CreateOpaqueLabel();

            bool resolved = ManualCursorLabelSelector.TryResolveEvaluatedCandidates(
                [
                    new ManualCursorEvaluatedCandidate(ExileCoreOpaqueFactory.CreateOpaqueLabel(), "suppressed", IsSuppressed: true, CursorInsideLabelRect: true, LabelRectScore: 1f, CursorNearGroundProjection: false, GroundProjectionScore: float.MaxValue),
                    new ManualCursorEvaluatedCandidate(ExileCoreOpaqueFactory.CreateOpaqueLabel(), "   ", IsSuppressed: false, CursorInsideLabelRect: true, LabelRectScore: 1f, CursorNearGroundProjection: false, GroundProjectionScore: float.MaxValue),
                    new ManualCursorEvaluatedCandidate(ExileCoreOpaqueFactory.CreateOpaqueLabel(), "not-hovered", IsSuppressed: false, CursorInsideLabelRect: false, LabelRectScore: float.MaxValue, CursorNearGroundProjection: false, GroundProjectionScore: float.MaxValue),
                    new ManualCursorEvaluatedCandidate(valid, "valid", IsSuppressed: false, CursorInsideLabelRect: true, LabelRectScore: 25f, CursorNearGroundProjection: false, GroundProjectionScore: float.MaxValue)
                ],
                out LabelOnGround? selectedLabel,
                out string? mechanicId);

            resolved.Should().BeTrue();
            selectedLabel.Should().BeSameAs(valid);
            mechanicId.Should().Be("valid");
        }

        [TestMethod]
        public void TryResolveEvaluatedCandidates_KeepsFirstCandidate_WhenScoresTie()
        {
            LabelOnGround first = ExileCoreOpaqueFactory.CreateOpaqueLabel();
            LabelOnGround second = ExileCoreOpaqueFactory.CreateOpaqueLabel();

            bool resolved = ManualCursorLabelSelector.TryResolveEvaluatedCandidates(
                [
                    new ManualCursorEvaluatedCandidate(first, "first", IsSuppressed: false, CursorInsideLabelRect: true, LabelRectScore: 16f, CursorNearGroundProjection: false, GroundProjectionScore: float.MaxValue),
                    new ManualCursorEvaluatedCandidate(second, "second", IsSuppressed: false, CursorInsideLabelRect: true, LabelRectScore: 16f, CursorNearGroundProjection: false, GroundProjectionScore: float.MaxValue)
                ],
                out LabelOnGround? selectedLabel,
                out string? mechanicId);

            resolved.Should().BeTrue();
            selectedLabel.Should().BeSameAs(first);
            mechanicId.Should().Be("first");
        }

        [TestMethod]
        public void TryResolveEvaluatedCandidates_PrefersLowerProjectionScore_WhenOnlyProjectionMatches()
        {
            LabelOnGround farther = ExileCoreOpaqueFactory.CreateOpaqueLabel();
            LabelOnGround closer = ExileCoreOpaqueFactory.CreateOpaqueLabel();

            bool resolved = ManualCursorLabelSelector.TryResolveEvaluatedCandidates(
                [
                    new ManualCursorEvaluatedCandidate(farther, "far", IsSuppressed: false, CursorInsideLabelRect: false, LabelRectScore: float.MaxValue, CursorNearGroundProjection: true, GroundProjectionScore: 36f),
                    new ManualCursorEvaluatedCandidate(closer, "close", IsSuppressed: false, CursorInsideLabelRect: false, LabelRectScore: float.MaxValue, CursorNearGroundProjection: true, GroundProjectionScore: 4f)
                ],
                out LabelOnGround? selectedLabel,
                out string? mechanicId);

            resolved.Should().BeTrue();
            selectedLabel.Should().BeSameAs(closer);
            mechanicId.Should().Be("close");
        }

        [TestMethod]
        public void TryResolveEvaluatedCandidates_KeepsFirstCandidate_WhenProjectionScoresTie()
        {
            LabelOnGround first = ExileCoreOpaqueFactory.CreateOpaqueLabel();
            LabelOnGround second = ExileCoreOpaqueFactory.CreateOpaqueLabel();

            bool resolved = ManualCursorLabelSelector.TryResolveEvaluatedCandidates(
                [
                    new ManualCursorEvaluatedCandidate(first, "first", IsSuppressed: false, CursorInsideLabelRect: false, LabelRectScore: float.MaxValue, CursorNearGroundProjection: true, GroundProjectionScore: 9f),
                    new ManualCursorEvaluatedCandidate(second, "second", IsSuppressed: false, CursorInsideLabelRect: false, LabelRectScore: float.MaxValue, CursorNearGroundProjection: true, GroundProjectionScore: 9f)
                ],
                out LabelOnGround? selectedLabel,
                out string? mechanicId);

            resolved.Should().BeTrue();
            selectedLabel.Should().BeSameAs(first);
            mechanicId.Should().Be("first");
        }

        [TestMethod]
        public void TryResolveEvaluatedCandidates_ReturnsFalse_WhenEveryCandidateIsRejected()
        {
            bool resolved = ManualCursorLabelSelector.TryResolveEvaluatedCandidates(
                [
                    new ManualCursorEvaluatedCandidate(null, "null-label", IsSuppressed: false, CursorInsideLabelRect: true, LabelRectScore: 1f, CursorNearGroundProjection: false, GroundProjectionScore: float.MaxValue),
                    new ManualCursorEvaluatedCandidate(ExileCoreOpaqueFactory.CreateOpaqueLabel(), null, IsSuppressed: false, CursorInsideLabelRect: true, LabelRectScore: 1f, CursorNearGroundProjection: false, GroundProjectionScore: float.MaxValue),
                    new ManualCursorEvaluatedCandidate(ExileCoreOpaqueFactory.CreateOpaqueLabel(), "suppressed", IsSuppressed: true, CursorInsideLabelRect: true, LabelRectScore: 1f, CursorNearGroundProjection: false, GroundProjectionScore: float.MaxValue),
                    new ManualCursorEvaluatedCandidate(ExileCoreOpaqueFactory.CreateOpaqueLabel(), "not-hovered", IsSuppressed: false, CursorInsideLabelRect: false, LabelRectScore: float.MaxValue, CursorNearGroundProjection: false, GroundProjectionScore: float.MaxValue)
                ],
                out LabelOnGround? selectedLabel,
                out string? mechanicId);

            resolved.Should().BeFalse();
            selectedLabel.Should().BeNull();
            mechanicId.Should().BeNull();
        }

        [TestMethod]
        public void TryResolveEvaluatedCandidates_PrefersRectScore_WhenProjectionScoreIsWorse()
        {
            LabelOnGround rectWinner = ExileCoreOpaqueFactory.CreateOpaqueLabel();
            LabelOnGround rectOnlyCandidate = ExileCoreOpaqueFactory.CreateOpaqueLabel();

            bool resolved = ManualCursorLabelSelector.TryResolveEvaluatedCandidates(
                [
                    new ManualCursorEvaluatedCandidate(rectWinner, "rect-wins", IsSuppressed: false, CursorInsideLabelRect: true, LabelRectScore: 4f, CursorNearGroundProjection: true, GroundProjectionScore: 16f),
                    new ManualCursorEvaluatedCandidate(rectOnlyCandidate, "rect-only", IsSuppressed: false, CursorInsideLabelRect: true, LabelRectScore: 9f, CursorNearGroundProjection: false, GroundProjectionScore: float.MaxValue)
                ],
                out LabelOnGround? selectedLabel,
                out string? mechanicId);

            resolved.Should().BeTrue();
            selectedLabel.Should().BeSameAs(rectWinner);
            mechanicId.Should().Be("rect-wins");
        }

        [DataTestMethod]
        [DataRow(false, false, false)]
        [DataRow(true, false, true)]
        [DataRow(false, true, true)]
        [DataRow(true, true, true)]
        public void ManualCursorCandidateHoverState_IsHovered_ReturnsExpected(
            bool cursorInsideLabelRect,
            bool cursorNearGroundProjection,
            bool expected)
        {
            var hoverState = new ManualCursorCandidateHoverState(
                cursorInsideLabelRect,
                LabelRectScore: cursorInsideLabelRect ? 4f : float.MaxValue,
                cursorNearGroundProjection,
                GroundProjectionScore: cursorNearGroundProjection ? 9f : float.MaxValue);

            hoverState.IsHovered.Should().Be(expected);
        }

        [TestMethod]
        public void TryGetGroundProjectionPoint_ReturnsFalse_WhenEntityIsInvalid()
        {
            var selector = CreateSelector();
            Entity item = EntityProbeFactory.Create(isValid: false, posX: 1f, posY: 2f, posZ: 3f);

            bool resolved = InvokeTryGetGroundProjectionPoint(selector, item, Vector2.Zero, out Vector2 projectedPoint);

            resolved.Should().BeFalse();
            projectedPoint.Should().Be(Vector2.Zero);
        }

        [TestMethod]
        public void TryGetGroundProjectionPoint_ReturnsFalse_WhenEntityIsMissing()
        {
            var selector = CreateSelector();

            bool resolved = InvokeTryGetGroundProjectionPoint(selector, null, Vector2.Zero, out Vector2 projectedPoint);

            resolved.Should().BeFalse();
            projectedPoint.Should().Be(Vector2.Zero);
        }

        [TestMethod]
        public void TryGetGroundProjectionPoint_ReturnsFalse_WhenProjectionThrows()
        {
            GameController gameController = CreateProjectionGameController(projection: null, throwOnProject: true);
            var selector = CreateSelector(gameController: gameController);
            Entity item = EntityProbeFactory.Create(isValid: true, posX: 1f, posY: 2f, posZ: 3f);

            bool resolved = InvokeTryGetGroundProjectionPoint(selector, item, Vector2.Zero, out Vector2 projectedPoint);

            resolved.Should().BeFalse();
            projectedPoint.Should().Be(Vector2.Zero);
        }

        [TestMethod]
        public void TryGetGroundProjectionPoint_ReturnsFalse_WhenGameGraphIsMissing()
        {
            var controller = (FakeGameControllerShim)RuntimeHelpers.GetUninitializedObject(typeof(FakeGameControllerShim));
            controller.Game = null;
            var selector = CreateSelector(gameController: controller);
            Entity item = EntityProbeFactory.Create(isValid: true, posX: 1f, posY: 2f, posZ: 3f);

            bool resolved = InvokeTryGetGroundProjectionPoint(selector, item, Vector2.Zero, out Vector2 projectedPoint);

            resolved.Should().BeFalse();
            projectedPoint.Should().Be(Vector2.Zero);
        }

        [TestMethod]
        public void TryGetGroundProjectionPoint_ReturnsFalse_WhenCameraGraphIsMissing()
        {
            GameController gameController = CreateProjectionGameControllerWithCamera(null);
            var selector = CreateSelector(gameController: gameController);
            Entity item = EntityProbeFactory.Create(isValid: true, posX: 1f, posY: 2f, posZ: 3f);

            bool resolved = InvokeTryGetGroundProjectionPoint(selector, item, Vector2.Zero, out Vector2 projectedPoint);

            resolved.Should().BeFalse();
            projectedPoint.Should().Be(Vector2.Zero);
        }

        [TestMethod]
        public void TryGetGroundProjectionPoint_ReturnsFalse_WhenProjectionResultIsMissing()
        {
            GameController gameController = CreateProjectionGameController(projection: null);
            var selector = CreateSelector(gameController: gameController);
            Entity item = EntityProbeFactory.Create(isValid: true, posX: 1f, posY: 2f, posZ: 3f);

            bool resolved = InvokeTryGetGroundProjectionPoint(selector, item, Vector2.Zero, out Vector2 projectedPoint);

            resolved.Should().BeFalse();
            projectedPoint.Should().Be(Vector2.Zero);
        }

        [TestMethod]
        public void TryGetGroundProjectionPoint_ReturnsFalse_WhenProjectionIsNotFinite()
        {
            GameController gameController = CreateProjectionGameController(new FakeProjectedPoint { X = float.NaN, Y = 15f });
            var selector = CreateSelector(gameController: gameController);
            Entity item = EntityProbeFactory.Create(isValid: true, posX: 1f, posY: 2f, posZ: 3f);

            bool resolved = InvokeTryGetGroundProjectionPoint(selector, item, Vector2.Zero, out Vector2 projectedPoint);

            resolved.Should().BeFalse();
            projectedPoint.X.Should().Be(float.NaN);
            float.IsFinite(projectedPoint.X).Should().BeFalse();
        }

        [TestMethod]
        public void TryResolveCursorInsideLabelRect_ReturnsFalse_WhenLabelRectIsMissing()
        {
            var selector = CreateSelector();
            LabelOnGround label = new ManualCursorProbeLabel
            {
                ItemOnGround = EntityProbeFactory.Create(isValid: true, type: EntityType.Monster)
            };

            bool resolved = InvokeTryResolveCursorInsideLabelRect(selector, label, Vector2.Zero, Vector2.Zero, out float labelRectScore);

            resolved.Should().BeFalse();
            labelRectScore.Should().Be(float.MaxValue);
        }

        [TestMethod]
        public void TryResolveCursorNearGroundProjection_ReturnsFalse_WhenEntityTypeIsWorldItemInt()
        {
            var selector = CreateSelector();
            Entity item = new IntTypeEntityProbe
            {
                IsValid = true,
                Type = (int)EntityType.WorldItem,
                PosNum = new System.Numerics.Vector3(1f, 2f, 3f)
            };

            bool resolved = InvokeTryResolveCursorNearGroundProjection(selector, item, new Vector2(30f, 30f), Vector2.Zero, out float groundProjectionScore);

            resolved.Should().BeFalse();
            groundProjectionScore.Should().Be(float.MaxValue);
        }

        [TestMethod]
        public void TryResolveCursorNearGroundProjection_ReturnsTrue_WhenEntityTypeFallsBackToNonWorldItemObject()
        {
            GameController gameController = CreateProjectionGameController(new FakeProjectedPoint { X = 32f, Y = 32f });
            var selector = CreateSelector(gameController: gameController);
            Entity item = new ObjectTypeEntityProbe
            {
                IsValid = true,
                Type = "monster",
                PosNum = new System.Numerics.Vector3(1f, 2f, 3f)
            };

            bool resolved = InvokeTryResolveCursorNearGroundProjection(selector, item, new Vector2(30f, 30f), Vector2.Zero, out float groundProjectionScore);

            resolved.Should().BeTrue();
            groundProjectionScore.Should().BeGreaterThanOrEqualTo(0f);
        }

        private static ManualCursorLabelSelector CreateSelector(
            GameController? gameController = null,
            Func<LabelOnGround?, string?>? getMechanicIdForLabel = null)
        {
            gameController ??= (GameController)RuntimeHelpers.GetUninitializedObject(typeof(GameController));
            var settings = new ClickItSettings();
            settings.AvoidOverlappingLabelClickPoints.Value = false;
            var runtimeState = new ClickRuntimeState();
            var pathfindingLabelSuppression = new PathfindingLabelSuppressionEvaluator(new PathfindingLabelSuppressionEvaluatorDependencies(
                settings,
                runtimeState));
            var labelClickPointResolver = new LabelClickPointResolver(settings);

            ILabelInteractionPort port = new StubLabelInteractionPort(getMechanicIdForLabel ?? (_ => null));

            return new ManualCursorLabelSelector(new ManualCursorLabelSelectorDependencies(
                gameController,
                port,
                pathfindingLabelSuppression,
                labelClickPointResolver));
        }

        private static bool InvokeTryGetGroundProjectionPoint(
            ManualCursorLabelSelector selector,
            Entity? item,
            Vector2 windowTopLeft,
            out Vector2 projectedPoint)
        {
            object?[] args = [item, windowTopLeft, null];
            MethodInfo method = typeof(ManualCursorLabelSelector).GetMethod("TryGetGroundProjectionPoint", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("TryGetGroundProjectionPoint was not found.");

            bool resolved = (bool)method.Invoke(selector, args)!;
            projectedPoint = args[2] is Vector2 point ? point : default;
            return resolved;
        }

        private static bool InvokeTryResolveCursorInsideLabelRect(
            ManualCursorLabelSelector selector,
            LabelOnGround candidate,
            Vector2 cursorAbsolute,
            Vector2 windowTopLeft,
            out float labelRectScore)
        {
            object?[] args = [candidate, cursorAbsolute, windowTopLeft, null];
            MethodInfo method = typeof(ManualCursorLabelSelector).GetMethod("TryResolveCursorInsideLabelRect", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("TryResolveCursorInsideLabelRect was not found.");

            bool resolved = (bool)method.Invoke(null, args)!;
            labelRectScore = args[3] is float score ? score : default;
            return resolved;
        }

        private static bool InvokeTryResolveCursorNearGroundProjection(
            ManualCursorLabelSelector selector,
            Entity? item,
            Vector2 cursorAbsolute,
            Vector2 windowTopLeft,
            out float groundProjectionScore)
        {
            object?[] args = [item, cursorAbsolute, windowTopLeft, null];
            MethodInfo method = typeof(ManualCursorLabelSelector).GetMethod("TryResolveCursorNearGroundProjection", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("TryResolveCursorNearGroundProjection was not found.");

            bool resolved = (bool)method.Invoke(selector, args)!;
            groundProjectionScore = args[3] is float score ? score : default;
            return resolved;
        }

        private static GameController CreateProjectionGameController(object? projection, bool throwOnProject = false)
        {
            var controller = (FakeGameControllerShim)RuntimeHelpers.GetUninitializedObject(typeof(FakeGameControllerShim));
            controller.Game = new FakeGameShim
            {
                IngameState = new FakeIngameStateShim
                {
                    Camera = new FakeCameraShim
                    {
                        Projection = projection,
                        ThrowOnProject = throwOnProject
                    }
                }
            };
            return controller;
        }

        private static GameController CreateProjectionGameControllerWithCamera(object? camera)
        {
            var controller = (FakeGameControllerShim)RuntimeHelpers.GetUninitializedObject(typeof(FakeGameControllerShim));
            controller.Game = new FakeGameShim
            {
                IngameState = new FakeIngameStateShim
                {
                    Camera = camera
                }
            };
            return controller;
        }

        private static LabelOnGround CreateLabelWithRect(Entity itemOnGround, RectangleF rect)
        {
            var label = new ManualCursorProbeLabel
            {
                ItemOnGround = itemOnGround,
                Label = new TestLabelElement(rect)
            };
            return label;
        }

        private sealed class StubLabelInteractionPort(Func<LabelOnGround?, string?> getMechanicIdForLabel) : ILabelInteractionPort
        {
            public SelectionDebugSummary GetSelectionDebugSummary(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
                => default;

            public void LogSelectionDiagnostics(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
            {
            }

            public string? GetMechanicIdForLabel(LabelOnGround? label)
                => getMechanicIdForLabel(label);

            public LabelOnGround? GetNextLabelToClick(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
                => null;

            public bool ShouldCorruptEssence(LabelOnGround label)
                => false;
        }

        public sealed class TestLabelElement(RectangleF clientRect) : Element
        {
            public new bool IsValid { get; set; } = true;

            public override RectangleF GetClientRect() => clientRect;
        }

        public sealed class ManualCursorProbeLabel : LabelOnGround
        {
            public new object? ItemOnGround { get; set; }

            public new object? Label { get; set; }
        }

        public sealed class IntTypeEntityProbe : Entity
        {
            public new bool IsValid { get; set; }

            public new object? Type { get; set; }

            public new System.Numerics.Vector3 PosNum { get; set; }
        }

        public sealed class ObjectTypeEntityProbe : Entity
        {
            public new bool IsValid { get; set; }

            public new object? Type { get; set; }

            public new System.Numerics.Vector3 PosNum { get; set; }
        }

        public sealed class FakeGameControllerShim : GameController
        {
            public FakeGameControllerShim()
                : base(null!, null!, null!, null!)
            {
            }

            public new object? Game { get; set; }
        }

        public sealed class FakeGameShim
        {
            public object? IngameState { get; set; }
        }

        public sealed class FakeIngameStateShim
        {
            public object? Camera { get; set; }
        }

        public sealed class FakeCameraShim
        {
            public object? Projection { get; set; }

            public bool ThrowOnProject { get; set; }

            public object? WorldToScreen(System.Numerics.Vector3 position)
            {
                if (ThrowOnProject)
                    throw new InvalidOperationException("Projection failed.");

                return Projection;
            }
        }

        public sealed class FakeProjectedPoint
        {
            public float X { get; set; }

            public float Y { get; set; }
        }
    }
}