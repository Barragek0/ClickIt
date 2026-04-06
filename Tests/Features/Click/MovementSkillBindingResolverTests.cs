namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class MovementSkillBindingResolverTests
    {
        [TestCleanup]
        public void TestCleanup()
            => MovementSkillMath.ClearThreadSkillBarEntriesBuffer();

        [TestMethod]
        public void TryFindReadyMovementSkillBinding_UsesChildPathFallback_ForKeyResolution()
        {
            var skillBar = new FakeSkillBar
            {
                Skills = new object?[]
                {
                    new FakeSkillEntry
                    {
                        Skill = new FakeSkill
                        {
                            InternalName = "spark"
                        },
                        KeyText = "W"
                    },
                    new FakeSkillEntry
                    {
                        Skill = new FakeSkill
                        {
                            InternalName = "shield_charge"
                        },
                        Children = BuildKeyPath("Q")
                    }
                }
            };

            bool resolved = MovementSkillBindingResolver.TryFindReadyMovementSkillBinding(skillBar, out MovementSkillBinding binding, out string diagnostic);

            resolved.Should().BeTrue();
            binding.BoundKey.Should().Be(Keys.Q);
            binding.InternalName.Should().Be("shield_charge");
            binding.Entry.Should().NotBeNull();
            diagnostic.Should().Contain("shield_charge").And.Contain("Q");
        }

        [TestMethod]
        public void TryFindReadyMovementSkillBinding_ReturnsDiagnosticSummary_WhenNoEntryQualifies()
        {
            var skillBar = new FakeSkillBar
            {
                Skills = new object?[]
                {
                    null,
                    new FakeSkillEntry
                    {
                        Skill = new FakeSkill
                        {
                            InternalName = "spark"
                        },
                        KeyText = "W"
                    },
                    new FakeSkillEntry
                    {
                        Skill = new FakeSkill
                        {
                            InternalName = "leap_slam",
                            IsOnCooldown = true
                        },
                        KeyText = "Q"
                    },
                    new FakeSkillEntry
                    {
                        Skill = new FakeSkill
                        {
                            InternalName = "shield_charge"
                        },
                        KeyText = "RMB"
                    },
                    new FakeSkillEntry
                    {
                        Skill = new FakeSkill
                        {
                            InternalName = "frostblink"
                        }
                    }
                }
            };

            bool resolved = MovementSkillBindingResolver.TryFindReadyMovementSkillBinding(skillBar, out MovementSkillBinding binding, out string diagnostic);

            resolved.Should().BeFalse();
            binding.BoundKey.Should().Be(Keys.None);
            binding.InternalName.Should().BeNull();
            diagnostic.Should().Contain("entries=5");
            diagnostic.Should().Contain("null=1");
            diagnostic.Should().Contain("nonMovement=1");
            diagnostic.Should().Contain("onCooldown=1");
            diagnostic.Should().Contain("missingKeyText=1");
            diagnostic.Should().Contain("unsupportedOrMouseKey=1");
        }

        [TestMethod]
        public void TryResolveMovementSkillRuntimeState_ReadsNestedSkillObject()
        {
            var entry = new FakeSkillEntry
            {
                Skill = new FakeSkill
                {
                    IsUsing = true,
                    AllowedToCast = false,
                    CanBeUsed = false
                }
            };

            bool resolved = MovementSkillBindingResolver.TryResolveMovementSkillRuntimeState(entry, out bool isUsing, out bool? allowedToCast, out bool? canBeUsed);

            resolved.Should().BeTrue();
            isUsing.Should().BeTrue();
            allowedToCast.Should().BeFalse();
            canBeUsed.Should().BeFalse();
        }

        private static IList BuildKeyPath(string keyText)
        {
            var level3 = new FakeNode
            {
                Children = new ArrayList
                {
                    new FakeNode(),
                    new FakeNode
                    {
                        Text = keyText
                    }
                }
            };
            var level2 = new FakeNode
            {
                Children = new ArrayList { level3 }
            };
            var level1 = new FakeNode
            {
                Children = new ArrayList { level2 }
            };

            return new ArrayList { level1 };
        }

        public sealed class FakeSkillBar
        {
            public object? Skills { get; init; }
        }

        public sealed class FakeSkillEntry
        {
            public FakeSkill? Skill { get; init; }
            public string? KeyText { get; init; }
            public IList Children { get; init; } = new ArrayList();
        }

        public sealed class FakeSkill
        {
            public string? InternalName { get; init; }
            public bool IsOnCooldown { get; init; }
            public bool IsUsing { get; init; }
            public bool AllowedToCast { get; init; }
            public bool CanBeUsed { get; init; }
        }

        public sealed class FakeNode
        {
            public string? Text { get; init; }
            public IList Children { get; init; } = new ArrayList();
        }
    }
}