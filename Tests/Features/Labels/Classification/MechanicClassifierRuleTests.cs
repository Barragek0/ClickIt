namespace ClickIt.Tests.Features.Labels.Classification
{
    [TestClass]
    public class MechanicClassifierRuleTests
    {
        [TestMethod]
        public void ShouldClickEssence_ReturnsFalse_WhenEssenceClickingDisabled()
        {
            LabelOnGround label = ExileCoreOpaqueFactory.CreateOpaqueLabel();

            bool result = MechanicClassifier.ShouldClickEssence(clickEssences: false, label);

            result.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickEssence_ReturnsTrue_WhenEssenceTextExists()
        {
            LabelOnGround label = CreateLabelWithText("The monster is imprisoned by powerful Essences.");

            bool result = MechanicClassifier.ShouldClickEssence(clickEssences: true, label);

            result.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldClickEssence_ReturnsFalse_WhenEssenceTextIsMissing()
        {
            LabelOnGround label = CreateLabelWithText("Different text");

            bool result = MechanicClassifier.ShouldClickEssence(clickEssences: true, label);

            result.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickEssence_ReturnsFalse_WhenLabelAdapterIsMissing()
        {
            LabelOnGround label = new LabelProbe();

            bool result = MechanicClassifier.ShouldClickEssence(clickEssences: true, label);

            result.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickEssence_ReturnsFalse_WhenLabelPayloadIsUnsupported()
        {
            LabelOnGround label = new LabelProbe { Label = new object() };

            bool result = MechanicClassifier.ShouldClickEssence(clickEssences: true, label);

            result.Should().BeFalse();
        }

        [TestMethod]
        public void GetRitualMechanicId_ReturnsNull_WhenPathIsNotRitual()
        {
            LabelOnGround label = ExileCoreOpaqueFactory.CreateOpaqueLabel();

            string? mechanicId = MechanicClassifier.GetRitualMechanicId(
                clickRitualInitiate: true,
                clickRitualCompleted: true,
                path: "Metadata/Terrain/Chests/SomeOtherChest",
                label);

            mechanicId.Should().BeNull();
        }

        [TestMethod]
        public void GetRitualMechanicId_ReturnsNull_WhenPathIsEmpty()
        {
            LabelOnGround label = ExileCoreOpaqueFactory.CreateOpaqueLabel();

            string? mechanicId = MechanicClassifier.GetRitualMechanicId(
                clickRitualInitiate: true,
                clickRitualCompleted: true,
                path: string.Empty,
                label);

            mechanicId.Should().BeNull();
        }

        [TestMethod]
        public void GetRitualMechanicId_ReturnsInitiate_WhenFavoursTextIsMissing()
        {
            LabelOnGround label = CreateLabelWithText("Begin the Ritual");

            string? mechanicId = MechanicClassifier.GetRitualMechanicId(
                clickRitualInitiate: true,
                clickRitualCompleted: true,
                path: "Metadata/Leagues/Ritual/Objects/RitualRuneInteractable",
                label);

            mechanicId.Should().Be(MechanicIds.RitualInitiate);
        }

        [TestMethod]
        public void GetRitualMechanicId_ReturnsCompleted_WhenFavoursTextExists()
        {
            LabelOnGround label = CreateLabelWithText("Interact to view Favours");

            string? mechanicId = MechanicClassifier.GetRitualMechanicId(
                clickRitualInitiate: true,
                clickRitualCompleted: true,
                path: "Metadata/Leagues/Ritual/Objects/RitualRuneInteractable",
                label);

            mechanicId.Should().Be(MechanicIds.RitualCompleted);
        }

        [TestMethod]
        public void GetRitualMechanicId_ReturnsNull_WhenInitiateDisabled_AndFavoursTextMissing()
        {
            LabelOnGround label = CreateLabelWithText("Begin the Ritual");

            string? mechanicId = MechanicClassifier.GetRitualMechanicId(
                clickRitualInitiate: false,
                clickRitualCompleted: true,
                path: "Metadata/Leagues/Ritual/Objects/RitualRuneInteractable",
                label);

            mechanicId.Should().BeNull();
        }

        [TestMethod]
        public void GetRitualMechanicId_ReturnsNull_WhenCompletedDisabled_AndFavoursTextExists()
        {
            LabelOnGround label = CreateLabelWithText("Interact to view Favours");

            string? mechanicId = MechanicClassifier.GetRitualMechanicId(
                clickRitualInitiate: true,
                clickRitualCompleted: false,
                path: "Metadata/Leagues/Ritual/Objects/RitualRuneInteractable",
                label);

            mechanicId.Should().BeNull();
        }

        [TestMethod]
        public void ShouldClickStrongbox_ReturnsFalse_WhenPathIsMissing()
        {
            var settings = new ClickSettings
            {
                StrongboxClickMetadata = ["special:strongbox-unique"]
            };

            bool result = MechanicClassifier.ShouldClickStrongbox(settings, string.Empty, label: null!);

            result.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickStrongbox_ReturnsFalse_WhenLabelHasNoItem()
        {
            var settings = new ClickSettings
            {
                StrongboxClickMetadata = ["special:strongbox-unique"]
            };

            bool result = MechanicClassifier.ShouldClickStrongbox(settings, "Metadata/Chests/StrongBoxes/Arcanist", label: null!);

            result.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickStrongbox_ReturnsTrue_ForUniqueStrongbox_WhenClickListContainsUniqueIdentifier()
        {
            var settings = new ClickSettings
            {
                StrongboxClickMetadata = ["special:strongbox-unique"],
                StrongboxDontClickMetadata = []
            };
            LabelOnGround label = CreateStrongboxLabel(new StrongboxItemProbe
            {
                ChestComponent = new ChestProbe { IsLocked = false },
                Rarity = MonsterRarity.Unique,
                RenderName = "Unique Strongbox"
            });

            bool result = MechanicClassifier.ShouldClickStrongbox(settings, "Metadata/Chests/StrongBoxes/Arcanist", label);

            result.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldClickStrongbox_ReturnsFalse_ForUniqueStrongbox_WhenDontClickListContainsUniqueIdentifier()
        {
            var settings = new ClickSettings
            {
                StrongboxClickMetadata = ["special:strongbox-unique"],
                StrongboxDontClickMetadata = ["special:strongbox-unique"]
            };
            LabelOnGround label = CreateStrongboxLabel(new StrongboxItemProbe
            {
                ChestComponent = new ChestProbe { IsLocked = false },
                Rarity = MonsterRarity.Unique,
                RenderName = "Unique Strongbox"
            });

            bool result = MechanicClassifier.ShouldClickStrongbox(settings, "Metadata/Chests/StrongBoxes/Arcanist", label);

            result.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickStrongbox_ReturnsFalse_WhenChestIsLocked()
        {
            var settings = new ClickSettings
            {
                StrongboxClickMetadata = ["Metadata/Chests/StrongBoxes/Arcanist"],
                StrongboxDontClickMetadata = []
            };
            LabelOnGround label = CreateStrongboxLabel(new StrongboxItemProbe
            {
                ChestComponent = new ChestProbe { IsLocked = true },
                Rarity = MonsterRarity.White,
                RenderName = "Arcanist's Strongbox"
            });

            bool result = MechanicClassifier.ShouldClickStrongbox(settings, "Metadata/Chests/StrongBoxes/Arcanist", label);

            result.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickStrongbox_ReturnsFalse_WhenClickMetadataIsMissing()
        {
            var settings = new ClickSettings
            {
                StrongboxClickMetadata = null!,
                StrongboxDontClickMetadata = null!
            };
            LabelOnGround label = CreateStrongboxLabel(new StrongboxItemProbe
            {
                ChestComponent = new ChestProbe { IsLocked = false },
                Rarity = MonsterRarity.White,
                RenderName = "Arcanist's Strongbox"
            });

            bool result = MechanicClassifier.ShouldClickStrongbox(settings, "Metadata/Chests/StrongBoxes/Arcanist", label);

            result.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickStrongbox_ReturnsTrue_WhenChestComponentIsMissing()
        {
            var settings = new ClickSettings
            {
                StrongboxClickMetadata = ["Metadata/Chests/StrongBoxes/Arcanist"],
                StrongboxDontClickMetadata = []
            };
            LabelOnGround label = CreateStrongboxLabel(new StrongboxItemProbe
            {
                ChestComponent = null,
                Rarity = MonsterRarity.White,
                RenderName = "Arcanist's Strongbox"
            });

            bool result = MechanicClassifier.ShouldClickStrongbox(settings, "Metadata/Chests/StrongBoxes/Arcanist", label);

            result.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldClickStrongbox_ReturnsTrue_WhenItemHasNoGetComponentMember()
        {
            var settings = new ClickSettings
            {
                StrongboxClickMetadata = ["Metadata/Chests/StrongBoxes/Arcanist"],
                StrongboxDontClickMetadata = []
            };
            LabelOnGround label = CreateStrongboxLabel(new StrongboxItemWithoutComponentAccessorProbe
            {
                Rarity = MonsterRarity.White,
                RenderName = "Arcanist's Strongbox"
            });

            bool result = MechanicClassifier.ShouldClickStrongbox(settings, "Metadata/Chests/StrongBoxes/Arcanist", label);

            result.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldClickStrongbox_ReturnsFalse_WhenDontClickMetadataMatches()
        {
            var settings = new ClickSettings
            {
                StrongboxClickMetadata = ["Metadata/Chests/StrongBoxes/Arcanist"],
                StrongboxDontClickMetadata = ["Arcanist"]
            };
            LabelOnGround label = CreateStrongboxLabel(new StrongboxItemProbe
            {
                ChestComponent = new ChestProbe { IsLocked = false },
                Rarity = MonsterRarity.White,
                RenderName = "Arcanist's Strongbox"
            });

            bool result = MechanicClassifier.ShouldClickStrongbox(settings, "Metadata/Chests/StrongBoxes/Arcanist", label);

            result.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickStrongbox_ReturnsTrue_WhenClickMetadataMatchesUnlockedNonUniqueStrongbox()
        {
            var settings = new ClickSettings
            {
                StrongboxClickMetadata = ["Metadata/Chests/StrongBoxes/Arcanist"],
                StrongboxDontClickMetadata = []
            };
            LabelOnGround label = CreateStrongboxLabel(new StrongboxItemProbe
            {
                ChestComponent = new ChestProbe { IsLocked = false },
                Rarity = MonsterRarity.White,
                RenderName = "Arcanist's Strongbox"
            });

            bool result = MechanicClassifier.ShouldClickStrongbox(settings, "Metadata/Chests/StrongBoxes/Arcanist", label);

            result.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldClickStrongbox_ReturnsTrue_WhenRenderNameCannotBeRead_ButPathMatches()
        {
            var settings = new ClickSettings
            {
                StrongboxClickMetadata = ["Metadata/Chests/StrongBoxes/Arcanist"],
                StrongboxDontClickMetadata = []
            };
            LabelOnGround label = CreateStrongboxLabel(new StrongboxItemWithoutRenderNameProbe
            {
                ChestComponent = new ChestProbe { IsLocked = false },
                Rarity = MonsterRarity.White
            });

            bool result = MechanicClassifier.ShouldClickStrongbox(settings, "Metadata/Chests/StrongBoxes/Arcanist", label);

            result.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldClickStrongbox_ReturnsTrue_WhenUniqueRarityIsExposedAsInteger()
        {
            var settings = new ClickSettings
            {
                StrongboxClickMetadata = ["special:strongbox-unique"],
                StrongboxDontClickMetadata = []
            };
            LabelOnGround label = CreateStrongboxLabel(new StrongboxItemWithIntegerRarityProbe
            {
                ChestComponent = new ChestProbe { IsLocked = false },
                Rarity = (int)MonsterRarity.Unique,
                RenderName = "Integer Unique Strongbox"
            });

            bool result = MechanicClassifier.ShouldClickStrongbox(settings, "Metadata/Chests/StrongBoxes/Arcanist", label);

            result.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldClickStrongbox_ReturnsTrue_WhenRarityIsMissing_AndPathMatches()
        {
            var settings = new ClickSettings
            {
                StrongboxClickMetadata = ["Metadata/Chests/StrongBoxes/Arcanist"],
                StrongboxDontClickMetadata = []
            };
            LabelOnGround label = CreateStrongboxLabel(new StrongboxItemWithoutRarityProbe
            {
                ChestComponent = new ChestProbe { IsLocked = false },
                RenderName = "Arcanist's Strongbox"
            });

            bool result = MechanicClassifier.ShouldClickStrongbox(settings, "Metadata/Chests/StrongBoxes/Arcanist", label);

            result.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldClickStrongbox_ReturnsTrue_WhenRarityTypeIsUnsupported_AndPathMatches()
        {
            var settings = new ClickSettings
            {
                StrongboxClickMetadata = ["Metadata/Chests/StrongBoxes/Arcanist"],
                StrongboxDontClickMetadata = []
            };
            LabelOnGround label = CreateStrongboxLabel(new StrongboxItemWithStringRarityProbe
            {
                ChestComponent = new ChestProbe { IsLocked = false },
                Rarity = "Unique",
                RenderName = "Arcanist's Strongbox"
            });

            bool result = MechanicClassifier.ShouldClickStrongbox(settings, "Metadata/Chests/StrongBoxes/Arcanist", label);

            result.Should().BeTrue();
        }

        [DataTestMethod]
        [DataRow(null, false)]
        [DataRow(new string[] { "special:strongbox-unique" }, true)]
        [DataRow(new string[] { "SPECIAL:STRONGBOX-UNIQUE" }, true)]
        [DataRow(new string[] { "Metadata/Chests/StrongBoxes/Arcanist" }, false)]
        [DataRow(new string[0], false)]
        public void ContainsStrongboxUniqueIdentifier_ReturnsExpectedValue(string[]? identifiers, bool expected)
        {
            MethodInfo method = typeof(MechanicClassifier).GetMethod("ContainsStrongboxUniqueIdentifier", BindingFlags.Static | BindingFlags.NonPublic)!;

            bool result = (bool)method.Invoke(null, [identifiers])!;

            result.Should().Be(expected);
        }

        [TestMethod]
        public void TryGetLabelAdapter_ReturnsTrue_WhenLabelPayloadIsElement()
        {
            LabelOnGround label = new LabelProbe { Label = ExileCoreOpaqueFactory.CreateOpaque<Element>() };
            object?[] args = [label, null];

            bool result = (bool)typeof(MechanicClassifier)
                .GetMethod("TryGetLabelAdapter", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, args)!;

            result.Should().BeTrue();
            args[1].Should().BeOfType<ElementAdapter>();
        }

        [TestMethod]
        public void TryGetLabelAdapter_ReturnsFalse_WhenConcreteExileCoreLabelReadFails()
        {
            LabelOnGround label = ExileCoreOpaqueFactory.CreateOpaqueLabel();
            object?[] args = [label, null];

            bool result = (bool)typeof(MechanicClassifier)
                .GetMethod("TryGetLabelAdapter", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, args)!;

            result.Should().BeFalse();
            args[1].Should().BeNull();
        }

        private static LabelOnGround CreateLabelWithText(string text)
            => new LabelProbe { Label = new ElementAdapterStub(text) };

        private static LabelOnGround CreateStrongboxLabel(object item)
            => new LabelProbe { ItemOnGround = item };

        public sealed class StrongboxItemWithoutRenderNameProbe
        {
            public object? ChestComponent { get; set; }

            public MonsterRarity Rarity { get; set; }

            public object? GetComponent<T>()
                => ChestComponent;
        }

        public sealed class StrongboxItemWithIntegerRarityProbe
        {
            public object? ChestComponent { get; set; }

            public int Rarity { get; set; }

            public string RenderName { get; set; } = string.Empty;

            public object? GetComponent<T>()
                => ChestComponent;
        }

        public sealed class StrongboxItemWithoutRarityProbe
        {
            public object? ChestComponent { get; set; }

            public string RenderName { get; set; } = string.Empty;

            public object? GetComponent<T>()
                => ChestComponent;
        }

        public sealed class StrongboxItemWithStringRarityProbe
        {
            public object? ChestComponent { get; set; }

            public string Rarity { get; set; } = string.Empty;

            public string RenderName { get; set; } = string.Empty;

            public object? GetComponent<T>()
                => ChestComponent;
        }

        public sealed class StrongboxItemWithoutComponentAccessorProbe
        {
            public MonsterRarity Rarity { get; set; }

            public string RenderName { get; set; } = string.Empty;
        }

    }
}