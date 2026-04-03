using ClickIt.Features.Click.Runtime;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows.Forms;

namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class MovementSkillMathTests
    {
        [TestMethod]
        public void ShouldAttemptMovementSkill_RespectsPathLengthAndRecastDelay()
        {
            MovementSkillMath.ShouldAttemptMovementSkill(true, true, remainingPathNodes: 10, minPathNodes: 8, now: 1000, lastSkillUseTimestampMs: 0, recastDelayMs: 450)
                .Should()
                .BeTrue();

            MovementSkillMath.ShouldAttemptMovementSkill(true, true, remainingPathNodes: 6, minPathNodes: 8, now: 1000, lastSkillUseTimestampMs: 0, recastDelayMs: 450)
                .Should()
                .BeFalse();

            MovementSkillMath.ShouldAttemptMovementSkill(true, true, remainingPathNodes: 10, minPathNodes: 8, now: 1000, lastSkillUseTimestampMs: 800, recastDelayMs: 450)
                .Should()
                .BeFalse();
        }

        [TestMethod]
        public void ResolveMovementSkillPostCastClickBlockMs_ReturnsExpectedProfiles()
        {
            MovementSkillMath.ResolveMovementSkillPostCastClickBlockMs("Frostblink").Should().Be(0);
            MovementSkillMath.ResolveMovementSkillPostCastClickBlockMs("shield_charge").Should().Be(MovementSkillMath.ShieldChargePostCastClickBlockMs);
            MovementSkillMath.ResolveMovementSkillPostCastClickBlockMs("leap_slam").Should().BeGreaterThan(0);
        }

        [TestMethod]
        public void ResolveMovementSkillStatusPollWindowMs_DisablesForInstantSkills()
        {
            MovementSkillMath.ResolveMovementSkillStatusPollWindowMs(120, "Frostblink").Should().Be(0);
            MovementSkillMath.ResolveMovementSkillStatusPollWindowMs(120, "leap_slam").Should().BeGreaterThan(120);
        }

        [TestMethod]
        public void TryMapKeyTextToKeys_ParsesKeyboardBinds_AndRejectsMouseOnlyBinds()
        {
            MovementSkillMath.TryMapKeyTextToKeys("Q", out Keys q).Should().BeTrue();
            q.Should().Be(Keys.Q);

            MovementSkillMath.TryMapKeyTextToKeys("F5", out Keys f5).Should().BeTrue();
            f5.Should().Be(Keys.F5);

            MovementSkillMath.TryMapKeyTextToKeys("RMB", out _).Should().BeFalse();
        }

        [TestMethod]
        public void IsMovementSkillPostCastClickBlocked_ComputesRemainingWindow()
        {
            MovementSkillMath.IsMovementSkillPostCastClickBlocked(now: 1000, blockUntilTimestampMs: 1200, out long remaining)
                .Should()
                .BeTrue();
            remaining.Should().Be(200);

            MovementSkillMath.IsMovementSkillPostCastClickBlocked(now: 1200, blockUntilTimestampMs: 1200, out long expiredRemaining)
                .Should()
                .BeFalse();
            expiredRemaining.Should().Be(0);
        }
    }
}