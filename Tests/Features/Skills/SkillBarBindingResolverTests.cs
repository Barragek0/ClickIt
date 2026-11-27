namespace ClickIt.Tests.Features.Skills
{
    [TestClass]
    public class SkillBarBindingResolverTests
    {
        [TestCleanup]
        public void TestCleanup()
            => SkillBarBindingResolver.ClearThreadSkillBarEntriesBuffer();

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

            bool resolved = SkillBarBindingResolver.TryFindReadyMovementSkillBinding(skillBar, out SkillBinding binding, out string diagnostic);

            resolved.Should().BeTrue();
            binding.BoundKey.Should().Be(Keys.Q);
            binding.InternalName.Should().Be("shield_charge");
            binding.Entry.Should().NotBeNull();
            diagnostic.Should().Contain("shield_charge").And.Contain("Q");
            SkillBarBindingResolver.GetThreadSkillBarEntriesBuffer(16).Should().BeEmpty();
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

            bool resolved = SkillBarBindingResolver.TryFindReadyMovementSkillBinding(skillBar, out SkillBinding binding, out string diagnostic);

            resolved.Should().BeFalse();
            binding.BoundKey.Should().Be(Keys.None);
            binding.InternalName.Should().BeNull();
            diagnostic.Should().Contain("entries=5");
            diagnostic.Should().Contain("null=1");
            diagnostic.Should().Contain("nonMatching=1");
            diagnostic.Should().Contain("onCooldown=1");
            diagnostic.Should().Contain("missingKeyText=1");
            diagnostic.Should().Contain("unsupportedOrMouseKey=1");
            SkillBarBindingResolver.GetThreadSkillBarEntriesBuffer(16).Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory("Scenario")]
        public void TryFindReadySkillBinding_MatchesFlameLinkAndIgnoresOtherSkills()
        {
            var skillBar = new FakeSkillBar
            {
                Skills = new object?[]
                {
                    new FakeSkillEntry { Skill = new FakeSkill { InternalName = "spark" }, KeyText = "Q" },
                    new FakeSkillEntry { Skill = new FakeSkill { InternalName = "flame_link" }, KeyText = "E" }
                }
            };

            bool resolved = SkillBarBindingResolver.TryFindReadySkillBinding(skillBar, "flame_link", out SkillBinding binding, out string diagnostic);

            resolved.Should().BeTrue();
            binding.BoundKey.Should().Be(Keys.E);
            binding.InternalName.Should().Be("flame_link");
            diagnostic.Should().Contain("flame_link");
        }

        [TestMethod]
        [TestCategory("Boundary")]
        public void TryMapKeyTextToKeys_ParsesKeyboardBinds_AndRejectsMouseOnlyBinds()
        {
            SkillBarBindingResolver.TryMapKeyTextToKeys("Q", out Keys q).Should().BeTrue();
            q.Should().Be(Keys.Q);

            SkillBarBindingResolver.TryMapKeyTextToKeys("F5", out Keys f5).Should().BeTrue();
            f5.Should().Be(Keys.F5);

            SkillBarBindingResolver.TryMapKeyTextToKeys("RMB", out _).Should().BeFalse();
        }

        [TestMethod]
        public void TryResolveSkillRuntimeState_ReadsNestedSkillObject()
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

            bool resolved = SkillBarBindingResolver.TryResolveSkillRuntimeState(entry, out bool isUsing, out bool? allowedToCast, out bool? canBeUsed);

            resolved.Should().BeTrue();
            isUsing.Should().BeTrue();
            allowedToCast.Should().BeFalse();
            canBeUsed.Should().BeFalse();
        }

        private static ArrayList BuildKeyPath(string keyText)
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
