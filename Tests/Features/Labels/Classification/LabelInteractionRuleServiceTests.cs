namespace ClickIt.Tests.Features.Labels.Classification
{
    [TestClass]
    public class LabelInteractionRuleServiceTests
    {
        [TestMethod]
        public void ShouldAllowWorldItemByMetadata_DelegatesThroughMetadataPolicy()
        {
            Entity item = ExileCoreOpaqueFactory.CreateOpaqueEntity();
            GameController controller = ExileCoreOpaqueFactory.CreateOpaqueGameController();
            LabelOnGround label = CreateLabel();
            var settings = new ClickSettings();
            Entity? delegatedItem = null;
            GameController? delegatedController = null;

            LabelInteractionRuleService service = CreateService(
                shouldAllowWorldItemByMetadata: (_, passedItem, passedController, passedLabel, shouldAllowWhenInventoryFull) =>
                {
                    delegatedItem = passedItem;
                    delegatedController = passedController;
                    passedLabel.Should().BeSameAs(label);
                    shouldAllowWhenInventoryFull.Should().NotBeNull();
                    return true;
                });

            bool result = service.ShouldAllowWorldItemByMetadata(settings, item, controller, label);

            result.Should().BeTrue();
            delegatedItem.Should().BeSameAs(item);
            delegatedController.Should().BeSameAs(controller);
        }

        [TestMethod]
        public void ShouldAllowClosedDoorPastMechanic_DelegatesToInventoryInteractionPolicy()
        {
            GameController controller = ExileCoreOpaqueFactory.CreateOpaqueGameController();
            InventorySnapshot snapshot = default(InventorySnapshot) with
            {
                HasPrimaryInventory = true,
                FullProbe = InventoryFullProbe.Empty with
                {
                    HasPrimaryInventory = true,
                    Notes = "Inventory layout unreliable from inventory slots (raw:5 parsed:0)"
                }
            };
            LabelInteractionRuleService service = CreateService(inventorySnapshot: snapshot);

            bool result = service.ShouldAllowClosedDoorPastMechanic(controller);

            result.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldClickEssence_ReturnsFalse_WhenEssenceClickingDisabled()
        {
            LabelOnGround label = ExileCoreOpaqueFactory.CreateOpaqueLabel();

            bool result = LabelInteractionRuleService.ShouldClickEssence(clickEssences: false, label);

            result.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickEssence_ReturnsTrue_WhenEssenceTextExists()
        {
            LabelOnGround label = CreateLabelWithText("The monster is imprisoned by powerful Essences.");

            bool result = LabelInteractionRuleService.ShouldClickEssence(clickEssences: true, label);

            result.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldClickEssence_ReturnsFalse_WhenEssenceTextIsMissing()
        {
            LabelOnGround label = CreateLabelWithText("Different text");

            bool result = LabelInteractionRuleService.ShouldClickEssence(clickEssences: true, label);

            result.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickEssence_ReturnsFalse_WhenLabelAdapterIsMissing()
        {
            LabelOnGround label = new LabelProbe();

            bool result = LabelInteractionRuleService.ShouldClickEssence(clickEssences: true, label);

            result.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickEssence_ReturnsFalse_WhenLabelPayloadIsUnsupported()
        {
            LabelOnGround label = new LabelProbe { Label = new object() };

            bool result = LabelInteractionRuleService.ShouldClickEssence(clickEssences: true, label);

            result.Should().BeFalse();
        }

        [TestMethod]
        public void GetRitualMechanicId_ReturnsNull_WhenPathIsNotRitual()
        {
            LabelOnGround label = ExileCoreOpaqueFactory.CreateOpaqueLabel();

            string? mechanicId = LabelInteractionRuleService.GetRitualMechanicId(
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

            string? mechanicId = LabelInteractionRuleService.GetRitualMechanicId(
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

            string? mechanicId = LabelInteractionRuleService.GetRitualMechanicId(
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

            string? mechanicId = LabelInteractionRuleService.GetRitualMechanicId(
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

            string? mechanicId = LabelInteractionRuleService.GetRitualMechanicId(
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

            string? mechanicId = LabelInteractionRuleService.GetRitualMechanicId(
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

            bool result = LabelInteractionRuleService.ShouldClickStrongbox(settings, string.Empty, label: null!);

            result.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickStrongbox_ReturnsFalse_WhenLabelHasNoItem()
        {
            var settings = new ClickSettings
            {
                StrongboxClickMetadata = ["special:strongbox-unique"]
            };

            bool result = LabelInteractionRuleService.ShouldClickStrongbox(settings, "Metadata/Chests/StrongBoxes/Arcanist", label: null!);

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

            bool result = LabelInteractionRuleService.ShouldClickStrongbox(settings, "Metadata/Chests/StrongBoxes/Arcanist", label);

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

            bool result = LabelInteractionRuleService.ShouldClickStrongbox(settings, "Metadata/Chests/StrongBoxes/Arcanist", label);

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

            bool result = LabelInteractionRuleService.ShouldClickStrongbox(settings, "Metadata/Chests/StrongBoxes/Arcanist", label);

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

            bool result = LabelInteractionRuleService.ShouldClickStrongbox(settings, "Metadata/Chests/StrongBoxes/Arcanist", label);

            result.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickStrongbox_ReturnsFalse_WhenChestComponentIsMissing()
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

            bool result = LabelInteractionRuleService.ShouldClickStrongbox(settings, "Metadata/Chests/StrongBoxes/Arcanist", label);

            result.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickStrongbox_ReturnsFalse_WhenItemHasNoGetComponentMember()
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

            bool result = LabelInteractionRuleService.ShouldClickStrongbox(settings, "Metadata/Chests/StrongBoxes/Arcanist", label);

            result.Should().BeFalse();
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

            bool result = LabelInteractionRuleService.ShouldClickStrongbox(settings, "Metadata/Chests/StrongBoxes/Arcanist", label);

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

            bool result = LabelInteractionRuleService.ShouldClickStrongbox(settings, "Metadata/Chests/StrongBoxes/Arcanist", label);

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

            bool result = LabelInteractionRuleService.ShouldClickStrongbox(settings, "Metadata/Chests/StrongBoxes/Arcanist", label);

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

            bool result = LabelInteractionRuleService.ShouldClickStrongbox(settings, "Metadata/Chests/StrongBoxes/Arcanist", label);

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

            bool result = LabelInteractionRuleService.ShouldClickStrongbox(settings, "Metadata/Chests/StrongBoxes/Arcanist", label);

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

            bool result = LabelInteractionRuleService.ShouldClickStrongbox(settings, "Metadata/Chests/StrongBoxes/Arcanist", label);

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
            MethodInfo method = typeof(LabelInteractionRuleService).GetMethod("ContainsStrongboxUniqueIdentifier", BindingFlags.Static | BindingFlags.NonPublic)!;

            bool result = (bool)method.Invoke(null, [identifiers])!;

            result.Should().Be(expected);
        }

        [TestMethod]
        public void TryGetLabelAdapter_ReturnsTrue_WhenLabelPayloadIsElement()
        {
            LabelOnGround label = new LabelProbe { Label = ExileCoreOpaqueFactory.CreateOpaque<Element>() };
            object?[] args = [label, null];

            bool result = (bool)typeof(LabelInteractionRuleService)
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

            bool result = (bool)typeof(LabelInteractionRuleService)
                .GetMethod("TryGetLabelAdapter", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, args)!;

            result.Should().BeFalse();
            args[1].Should().BeNull();
        }

        private static LabelInteractionRuleService CreateService(
            Func<ClickSettings, Entity, GameController?, LabelOnGround?, Func<Entity, GameController?, bool>, bool>? shouldAllowWorldItemByMetadata = null,
            InventorySnapshot inventorySnapshot = default)
        {
            var metadataPolicy = new FakeWorldItemMetadataPolicy
            {
                ShouldAllowWorldItemByMetadataFunc = shouldAllowWorldItemByMetadata
                    ?? ((_, _, _, _, shouldAllowWhenInventoryFull) => shouldAllowWhenInventoryFull(null!, null))
            };

            return new LabelInteractionRuleService(
                metadataPolicy,
                CreateInventoryInteractionPolicy(inventorySnapshot));
        }

        private static InventoryInteractionPolicy CreateInventoryInteractionPolicy(InventorySnapshot snapshot)
        {
            InventoryProbeService probeService = new(new InventoryProbeServiceDependencies(
                CacheWindowMs: 50,
                DebugTrailCapacity: 8,
                TryBuildInventorySnapshot: _ => (true, snapshot),
                LayoutCache: new InventoryLayoutCache(cacheWindowMs: 50)));

            InventoryItemEntityService itemEntityService = new(new InventoryItemEntityServiceDependencies(
                CacheWindowMs: 50,
                TryGetPrimaryServerInventory: _ => (false, null),
                TryGetPrimaryServerInventorySlotItems: _ => (false, null),
                EnumerateObjects: _ => System.Array.Empty<object?>(),
                TryGetInventoryItemEntityFromEntry: _ => null,
                ClassifyInventoryItemEntity: _ => (false, string.Empty)));

            InventoryPickupPolicyEngine pickupPolicy = new(new InventoryPickupPolicyDependencies(
                _ => (false, snapshot.FullProbe with { HasPrimaryInventory = snapshot.HasPrimaryInventory }),
                entity => entity,
                _ => "Test Item",
                _ => false,
                (_, _, _) => (false, 0, 0),
                (_, _) => false,
                InventoryCoreLogic.ShouldAllowPickupWhenPrimaryInventoryMissing,
                InventoryCoreLogic.ShouldAllowPickupWhenGroundItemEntityMissing,
                InventoryCoreLogic.ShouldAllowPickupWhenGroundItemIdentityMissing,
                (_, _, _) => false,
                (_, _, _, _, _, _, _, _, allowPickup) => new InventoryDebugSnapshot(
                    HasData: true,
                    Stage: "Test",
                    InventoryFull: false,
                    InventoryFullSource: string.Empty,
                    HasPrimaryInventory: snapshot.HasPrimaryInventory,
                    UsedFullFlag: false,
                    FullFlagValue: false,
                    UsedCellOccupancy: false,
                    CapacityCells: 0,
                    OccupiedCells: 0,
                    InventoryEntityCount: 0,
                    LayoutEntryCount: 0,
                    GroundItemPath: string.Empty,
                    GroundItemName: string.Empty,
                    IsGroundStackable: false,
                    MatchingPathCount: 0,
                    PartialMatchingStackCount: 0,
                    HasPartialMatchingStack: false,
                    DecisionAllowPickup: allowPickup,
                    Notes: snapshot.FullProbe.Notes,
                    Sequence: 0,
                    TimestampMs: 0),
                _ => { }));

            return new InventoryInteractionPolicy(probeService, itemEntityService, pickupPolicy, "Incursion/IncursionKey");
        }

        private static LabelOnGround CreateLabel(string? text = null)
            => ExileCoreOpaqueFactory.CreateOpaqueLabel();

        private static LabelOnGround CreateLabelWithText(string text)
            => new LabelProbe { Label = new ElementAdapterStub(text) };

        private static LabelOnGround CreateStrongboxLabel(object item)
            => new LabelProbe { ItemOnGround = item };

        private sealed class FakeWorldItemMetadataPolicy : IWorldItemMetadataPolicy
        {
            public Func<ClickSettings, Entity, GameController?, LabelOnGround?, Func<Entity, GameController?, bool>, bool>? ShouldAllowWorldItemByMetadataFunc { get; init; }

            public string GetWorldItemMetadataPath(Entity item) => string.Empty;

            public string GetWorldItemBaseName(Entity item) => string.Empty;

            public bool ShouldAllowWorldItemByMetadata(ClickSettings settings, Entity item, GameController? gameController, LabelOnGround? label, Func<Entity, GameController?, bool> shouldAllowWhenInventoryFull)
                => ShouldAllowWorldItemByMetadataFunc!(settings, item, gameController, label, shouldAllowWhenInventoryFull);
        }

        public sealed class LabelProbe : LabelOnGround
        {
            public new object? Label { get; set; }

            public new object? ItemOnGround { get; set; }
        }

        public sealed class StrongboxItemProbe
        {
            public object? ChestComponent { get; set; }

            public MonsterRarity Rarity { get; set; }

            public string RenderName { get; set; } = string.Empty;

            public object? GetComponent<T>()
                => ChestComponent;
        }

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

        public sealed class ChestProbe
        {
            public bool IsLocked { get; set; }
        }

    }
}