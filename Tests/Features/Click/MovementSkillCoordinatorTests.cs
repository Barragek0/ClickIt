namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class MovementSkillCoordinatorTests
    {
        [TestMethod]
        public void TryGetMovementSkillPostCastBlockState_ReturnsTrue_WhenTimingWindowIsStillActive()
        {
            var runtimeState = new ClickRuntimeState
            {
                MovementSkillPostCastClickBlockUntilTimestampMs = 1_250
            };
            var coordinator = CreateCoordinator(runtimeState);

            bool blocked = coordinator.TryGetMovementSkillPostCastBlockState(now: 1_000, out string reason);

            blocked.Should().BeTrue();
            reason.Should().Contain("timing window active");
        }

        [TestMethod]
        public void TryGetMovementSkillPostCastBlockState_ReturnsTrue_WhenTrackedSkillIsStillUsing()
        {
            var runtimeState = new ClickRuntimeState
            {
                MovementSkillStatusPollUntilTimestampMs = 2_000,
                LastUsedMovementSkillEntry = new FakeSkillEntry
                {
                    Skill = new FakeSkill
                    {
                        IsUsing = true,
                        AllowedToCast = true,
                        CanBeUsed = true
                    }
                }
            };
            var coordinator = CreateCoordinator(runtimeState);

            bool blocked = coordinator.TryGetMovementSkillPostCastBlockState(now: 1_500, out string reason);

            blocked.Should().BeTrue();
            reason.Should().Be("Skill.IsUsing=true");
        }

        [TestMethod]
        public void TryGetMovementSkillPostCastBlockState_ReturnsTrue_WhenTrackedSkillStillCannotCast()
        {
            var runtimeState = new ClickRuntimeState
            {
                MovementSkillStatusPollUntilTimestampMs = 2_000,
                LastUsedMovementSkillEntry = new FakeSkillEntry
                {
                    Skill = new FakeSkill
                    {
                        IsUsing = false,
                        AllowedToCast = false,
                        CanBeUsed = true
                    }
                }
            };
            var coordinator = CreateCoordinator(runtimeState);

            bool blocked = coordinator.TryGetMovementSkillPostCastBlockState(now: 1_500, out string reason);

            blocked.Should().BeTrue();
            reason.Should().Be("Skill.AllowedToCast=false");
        }

        [TestMethod]
        public void TryGetMovementSkillPostCastBlockState_ReturnsTrue_WhenTrackedSkillStillCannotBeUsed()
        {
            var runtimeState = new ClickRuntimeState
            {
                MovementSkillStatusPollUntilTimestampMs = 2_000,
                LastUsedMovementSkillEntry = new FakeSkillEntry
                {
                    Skill = new FakeSkill
                    {
                        IsUsing = false,
                        AllowedToCast = true,
                        CanBeUsed = false
                    }
                }
            };
            var coordinator = CreateCoordinator(runtimeState);

            bool blocked = coordinator.TryGetMovementSkillPostCastBlockState(now: 1_500, out string reason);

            blocked.Should().BeTrue();
            reason.Should().Be("Skill.CanBeUsed=false");
        }

        [TestMethod]
        public void TryGetMovementSkillPostCastBlockState_ReturnsFalse_AndClearsPollState_WhenWindowExpired()
        {
            var runtimeState = new ClickRuntimeState
            {
                MovementSkillStatusPollUntilTimestampMs = 900,
                LastUsedMovementSkillEntry = new FakeSkillEntry
                {
                    Skill = new FakeSkill
                    {
                        IsUsing = false,
                        AllowedToCast = true,
                        CanBeUsed = true
                    }
                }
            };
            var coordinator = CreateCoordinator(runtimeState);

            bool blocked = coordinator.TryGetMovementSkillPostCastBlockState(now: 1_000, out string reason);

            blocked.Should().BeFalse();
            reason.Should().BeEmpty();
            runtimeState.MovementSkillStatusPollUntilTimestampMs.Should().Be(0);
            runtimeState.LastUsedMovementSkillEntry.Should().BeNull();
        }

        [TestMethod]
        public void TryGetMovementSkillPostCastBlockState_ReturnsFalse_AndClearsPollState_WhenTrackedSkillBecomesUsableAgain()
        {
            var runtimeState = new ClickRuntimeState
            {
                MovementSkillStatusPollUntilTimestampMs = 2_000,
                LastUsedMovementSkillEntry = new FakeSkillEntry
                {
                    Skill = new FakeSkill
                    {
                        IsUsing = false,
                        AllowedToCast = true,
                        CanBeUsed = true
                    }
                }
            };
            var coordinator = CreateCoordinator(runtimeState);

            bool blocked = coordinator.TryGetMovementSkillPostCastBlockState(now: 1_500, out string reason);

            blocked.Should().BeFalse();
            reason.Should().BeEmpty();
            runtimeState.MovementSkillStatusPollUntilTimestampMs.Should().Be(0);
            runtimeState.LastUsedMovementSkillEntry.Should().BeNull();
        }

        [TestMethod]
        public void ResolveMovementSkillPostCastClickBlockMsForCast_UsesShieldChargeOverride_WhenConfigured()
        {
            var settings = new ClickItSettings();
            settings.OffscreenShieldChargePostCastClickDelayMs.Value = 345;
            var coordinator = CreateCoordinator(new ClickRuntimeState(), settings);

            int delay = coordinator.ResolveMovementSkillPostCastClickBlockMsForCast("shield_charge");

            delay.Should().Be(345);
        }

        [TestMethod]
        public void ResolveMovementSkillPostCastClickBlockMsForCast_UsesResolvedDefault_ForNonShieldChargeSkills()
        {
            var settings = new ClickItSettings();
            settings.OffscreenShieldChargePostCastClickDelayMs.Value = 345;
            var coordinator = CreateCoordinator(new ClickRuntimeState(), settings);

            int delay = coordinator.ResolveMovementSkillPostCastClickBlockMsForCast("frostblink");

            delay.Should().Be(MovementSkillMath.ResolveMovementSkillPostCastClickBlockMs("frostblink"));
        }

        [TestMethod]
        public void TryUseMovementSkillForOffscreenPathing_ReturnsFalse_WhenSettingDisabled()
        {
            var settings = new ClickItSettings();
            settings.UseMovementSkillsForOffscreenPathfinding.Value = false;
            var coordinator = CreateCoordinator(new ClickRuntimeState(), settings, remainingNodes: 12);

            bool used = coordinator.TryUseMovementSkillForOffscreenPathing("Metadata/TestTarget", new Vector2(100, 100), builtPath: true, out Vector2 castPoint, out string debugReason);

            used.Should().BeFalse();
            castPoint.Should().Be(Vector2.Zero);
            debugReason.Should().Contain("setting disabled");
        }

        [TestMethod]
        public void TryUseMovementSkillForOffscreenPathing_ReturnsFalse_WhenPathWasNotBuilt()
        {
            var settings = new ClickItSettings();
            settings.UseMovementSkillsForOffscreenPathfinding.Value = true;
            var coordinator = CreateCoordinator(new ClickRuntimeState(), settings, remainingNodes: 12);

            bool used = coordinator.TryUseMovementSkillForOffscreenPathing("Metadata/TestTarget", new Vector2(100, 100), builtPath: false, out Vector2 castPoint, out string debugReason);

            used.Should().BeFalse();
            castPoint.Should().Be(Vector2.Zero);
            debugReason.Should().Contain("no fresh path available");
        }

        [TestMethod]
        public void TryUseMovementSkillForOffscreenPathing_ReturnsFalse_WhenRemainingNodesBelowConfiguredMinimum()
        {
            var settings = new ClickItSettings();
            settings.UseMovementSkillsForOffscreenPathfinding.Value = true;
            settings.OffscreenMovementSkillMinPathSubsectionLength.Value = 6;
            var coordinator = CreateCoordinator(new ClickRuntimeState(), settings, remainingNodes: 5);

            bool used = coordinator.TryUseMovementSkillForOffscreenPathing("Metadata/TestTarget", new Vector2(100, 100), builtPath: true, out _, out string debugReason);

            used.Should().BeFalse();
            debugReason.Should().Contain("remaining path nodes 5 below minimum 6");
        }

        [TestMethod]
        public void TryUseMovementSkillForOffscreenPathing_ReturnsFalse_WhenLocalRecastDelayIsActive()
        {
            var settings = new ClickItSettings();
            settings.UseMovementSkillsForOffscreenPathfinding.Value = true;
            settings.OffscreenMovementSkillMinPathSubsectionLength.Value = 2;
            long now = Environment.TickCount64;
            var runtimeState = new ClickRuntimeState
            {
                LastMovementSkillUseTimestampMs = now
            };
            var coordinator = CreateCoordinator(runtimeState, settings, remainingNodes: 8);

            bool used = coordinator.TryUseMovementSkillForOffscreenPathing("Metadata/TestTarget", new Vector2(100, 100), builtPath: true, out _, out string debugReason);

            used.Should().BeFalse();
            debugReason.Should().Contain("local recast delay active");
        }

        [TestMethod]
        public void TryUseMovementSkillForOffscreenPathing_ReturnsFalse_WhenCastDelayIsActive()
        {
            // Spec 13/20: the skill may not be cast until ~200ms have elapsed since pathfinding to the current target started. Within the window the cast is refused (walk-clicks flow).
            var settings = new ClickItSettings();
            settings.UseMovementSkillsForOffscreenPathfinding.Value = true;
            settings.OffscreenMovementSkillMinPathSubsectionLength.Value = 2;
            long now = 10_000;
            var coordinator = CreateCoordinator(new ClickRuntimeState(), settings, remainingNodes: 8, getTimestampMs: () => now);

            bool first = coordinator.TryUseMovementSkillForOffscreenPathing("Metadata/TestTarget", new Vector2(100, 100), builtPath: true, out _, out string firstReason);

            now += 100; // still inside the 200ms cast delay
            bool second = coordinator.TryUseMovementSkillForOffscreenPathing("Metadata/TestTarget", new Vector2(100, 100), builtPath: true, out _, out string secondReason);

            first.Should().BeFalse();
            firstReason.Should().Contain("cast delay");
            second.Should().BeFalse();
            secondReason.Should().Contain("cast delay");
        }

        [TestMethod]
        public void TryUseMovementSkillForOffscreenPathing_ProceedsPastCastDelay_WhenDelayElapsed()
        {
            // Once the 200ms cast delay has elapsed the cast is allowed to proceed - it advances to the binding gate (which fails here only because no skill bar is available).
            var settings = new ClickItSettings();
            settings.UseMovementSkillsForOffscreenPathfinding.Value = true;
            settings.OffscreenMovementSkillMinPathSubsectionLength.Value = 2;
            long now = 10_000;
            var coordinator = CreateCoordinator(
                new ClickRuntimeState(),
                settings,
                remainingNodes: 8,
                gameController: ExileCoreVisibleObjectBuilder.CreateGameControllerWithWindow(new RectangleF(0f, 0f, 1920f, 1080f)),
                getTimestampMs: () => now);

            coordinator.TryUseMovementSkillForOffscreenPathing("Metadata/TestTarget", new Vector2(100, 100), builtPath: true, out _, out _);

            now += MovementSkillMath.CastDelayMs + 50;
            bool afterDelay = coordinator.TryUseMovementSkillForOffscreenPathing("Metadata/TestTarget", new Vector2(100, 100), builtPath: true, out _, out string afterReason);

            afterDelay.Should().BeFalse();
            afterReason.Should().NotContain("cast delay", "once the cast delay has elapsed the cast proceeds to the next gate");
            afterReason.Should().Contain("unable to resolve safe/clickable movement-skill cast point", "with no readable window rect the cast fails closed at cast-point resolution");
        }

        [TestMethod]
        public void TryUseMovementSkillForOffscreenPathing_RestartsCastDelay_WhenTargetChanges()
        {
            var settings = new ClickItSettings();
            settings.UseMovementSkillsForOffscreenPathfinding.Value = true;
            settings.OffscreenMovementSkillMinPathSubsectionLength.Value = 2;
            long now = 10_000;
            var coordinator = CreateCoordinator(new ClickRuntimeState(), settings, remainingNodes: 8, getTimestampMs: () => now);

            coordinator.TryUseMovementSkillForOffscreenPathing("Metadata/TargetA", new Vector2(100, 100), builtPath: true, out _, out _);

            now += MovementSkillMath.CastDelayMs + 50;
            bool newTarget = coordinator.TryUseMovementSkillForOffscreenPathing("Metadata/TargetB", new Vector2(100, 100), builtPath: true, out _, out string newReason);

            newTarget.Should().BeFalse();
            newReason.Should().Contain("cast delay", "a new target restarts the 200ms cast delay");
        }

        [TestMethod]
        public void TryGetMovementSkillPostCastBlockState_ReturnsFalse_WhenTrackedRuntimeEntryCannotBeRead()
        {
            var runtimeState = new ClickRuntimeState
            {
                MovementSkillStatusPollUntilTimestampMs = 2_000,
                LastUsedMovementSkillEntry = new object()
            };
            var coordinator = CreateCoordinator(runtimeState);

            bool blocked = coordinator.TryGetMovementSkillPostCastBlockState(now: 1_500, out string reason);

            blocked.Should().BeFalse();
            reason.Should().BeEmpty();
            runtimeState.MovementSkillStatusPollUntilTimestampMs.Should().Be(2_000);
            runtimeState.LastUsedMovementSkillEntry.Should().NotBeNull();
        }

        [TestMethod]
        public void ResolveMovementSkillPostCastClickBlockMsForCast_ClampsNegativeShieldChargeOverrideToZero()
        {
            var settings = new ClickItSettings();
            settings.OffscreenShieldChargePostCastClickDelayMs.Value = -50;
            var coordinator = CreateCoordinator(new ClickRuntimeState(), settings);

            int delay = coordinator.ResolveMovementSkillPostCastClickBlockMsForCast("shield_charge");

            delay.Should().Be(0);
        }

        private static MovementSkillCoordinator CreateCoordinator(
            ClickRuntimeState runtimeState,
            ClickItSettings? settings = null,
            int remainingNodes = 0,
            GameController? gameController = null,
            Func<Vector2, string, bool>? pointIsInClickableArea = null,
            Func<string, bool>? ensureCursorInsideGameWindowForClick = null,
            Func<long>? getTimestampMs = null)
        {
            settings ??= new ClickItSettings();

            return new MovementSkillCoordinator(new MovementSkillCoordinatorDependencies(
                Settings: settings,
                GameController: gameController!,
                RuntimeState: runtimeState,
                PerformanceMonitor: new PerformanceMonitor(settings),
                GetRemainingOffscreenPathNodeCount: () => remainingNodes,
                EnsureCursorInsideGameWindowForClick: ensureCursorInsideGameWindowForClick ?? (static _ => true),
                PointIsInClickableArea: pointIsInClickableArea ?? (static (_, _) => true),
                DebugLog: static _ => { },
                GetTimestampMs: getTimestampMs));
        }

        public sealed class FakeSkillEntry
        {
            public FakeSkill? Skill { get; init; }
        }

        public sealed class FakeSkill
        {
            public bool IsUsing { get; init; }
            public bool AllowedToCast { get; init; }
            public bool CanBeUsed { get; init; }
        }
    }
}
