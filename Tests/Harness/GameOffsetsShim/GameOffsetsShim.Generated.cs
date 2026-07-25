using System;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct StringPtr
{
    public long Ptr;
}

namespace GameOffsets
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct ActionWrapperOffsets
    {
        public long Skill;
        public long Target;
        public global::GameOffsets.Native.Vector2i Destination;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ActorComponentOffsets
    {
        public long AnimationControllerPtr;
        public long ActionPtr;
        public short ActionId;
        public int AnimationId;
        public global::GameOffsets.Native.NativePtrArray ActorSkillsArray;
        public global::GameOffsets.Native.NativePtrArray ActorSkillsCooldownArray;
        public global::GameOffsets.Native.NativePtrArray ActorVaalSkills;
        public global::GameOffsets.Native.StdVector DeployedObjectArray;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ActorDeployedObjectOffsets
    {
        public uint EntityId;
        public ushort SkillId;
        public ushort ObjectType;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ActorSkillCooldownOffsets
    {
        public int SkillSubId;
        public global::GameOffsets.Native.StdVector Cooldowns;
        public int MaxUses;
        public ushort SkillId;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ActorSkillOffsets
    {
        public byte SkillUseStage;
        public byte CastType;
        public global::GameOffsets.SubActorSkillOffsets SubData;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SubActorSkillOffsets
    {
        public ushort Id;
        public ushort Id2;
        public long EffectsPerLevelPtr;
        public byte CanBeUsedWithWeapon;
        public byte CannotBeUsed;
        public int TotalUses;
        public int Cooldown;
        public int SoulsPerUse;
        public int TotalVaalUses;
        public long StatsPtr;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct AncestorFightSelectionWindowOffsets
    {
        public int RewardEntityPtr;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct AncestorShopWindowOffsets
    {
        public long UnitPtr;
        public long ItemPtr;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct AncestorSidePanelOffsets
    {
        public long UnitPtr;
        public long ItemPtr;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct AnimationControllerOffsets
    {
        public global::GameOffsets.Native.NativePtrArray ActiveAnimationsArrayPtr;
        public long ActorAnimationArrayPtr;
        public int AnimationInActorId;
        public float AnimationProgress;
        public int CurrentAnimationStage;
        public float NextAnimationPoint;
        public float AnimationSpeedMultiplier1;
        public float AnimationSpeedMultiplier2;
        public float MaxAnimationProgressOffset;
        public float MaxAnimationProgress;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ActiveAnimationOffsets
    {
        public int AnimationId;
        public float SlowAnimationSpeed;
        public float NormalAnimationSpeed;
        public long SlowAnimationStartStagePtr;
        public long SlowAnimationEndStagePtr;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ActorAnimationListOffsets
    {
        public global::GameOffsets.Native.NativePtrArray AnimationList;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ActorAnimationStageOffsets
    {
        public float StageStart;
        public int ActorAnimationListIndex;
        public global::GameOffsets.Native.NativeUtf8Text StageName;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct AreaLoadingStateOffsets
    {
        public long IsLoading;
        public uint TotalLoadingScreenTimeMs;
        public long AreaName;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ItemInfoOffsets
    {
        public byte ItemCellsSizeX;
        public byte ItemCellsSizeY;
        public global::GameOffsets.Native.NativeUtf16Text Name;
        public long BaseItemType;
        public global::GameOffsets.Native.StdVector Tags;
        public global::GameOffsets.Native.NativeUtf16Text FlavourText;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BaseComponentOffsets
    {
        public long ItemInfo;
        public byte CurrencyItemLevel;
        public byte Influence;
        public byte Corrupted;
        public global::GameOffsets.Native.NativeUtf16Text PublicPrice;
        public int UnspentAbsorbedCorruption;
        public int ScourgedTier;
    }

    public struct Buffer23<T>
    {
        public T _element;
    }

    public struct Buffer14<T>
    {
        public T _element;
    }

    public struct Buffer8<T>
    {
        public T _element;
    }

    public struct Buffer4<T>
    {
        public T _element;
    }

    public struct Buffer3<T>
    {
        public T _element;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct BuffsOffsets
    {
        public global::GameOffsets.Native.NativePtrArray Buffs;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct BuffOffsets
    {
        public long BuffDatPtr;
        public float MaxTime;
        public float Timer;
        public uint SourceEntityId;
        public ushort Charges;
        public ushort FlaskSlot;
        public ushort SourceSkillId;
        public ushort SourceSkillId2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct CameraOffsets
    {
        public global::GameOffsets.CameraOffsetsInner Inner;
        public float ActualZoomLevel;
        public float DesiredZoomLevel;
        public byte IsFixedCamera;
        public byte IsInstantZoom;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct CameraOffsetsInner
    {
        public global::System.Numerics.Matrix4x4 MatrixBytes;
        public global::System.Numerics.Vector3 Position;
        public float ZFar;
        public int Width;
        public int Height;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ChallengePanelOffsets
    {
        public int BestiaryTabCapturedBeastsTabOffset;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ChestComponentOffsets
    {
        public long StrongboxData;
        public byte IsOpened;
        public byte IsLocked;
        public byte quality;
        public byte IsStrongbox;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct StrongboxChestComponentData
    {
        public long ChestsDat;
        public byte DestroyingAfterOpen;
        public byte IsLarge;
        public byte Stompable;
        public byte OpenOnDamage;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ComponentArrayStructure
    {
        public long NamePtr;
        public int Index;
        public int deadcode;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ComponentLookUpStruct
    {
        public global::GameOffsets.Native.StdVector ComponentPrototypeArray;
        public global::GameOffsets.Native.StdVector ComponentArray;
        public long Capacity;
        public long Count;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ComponentNameAndIndexStruct
    {
        public long NamePtr;
        public int Index;
        public int deadcode;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct ExpeditionAreaDataOffsets
    {
        public global::GameOffsets.Native.NativePtrArray ModsData;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct ExpeditionSagaOffsets
    {
        public byte AreaLevel;
        public global::GameOffsets.Native.NativePtrArray AreasData;
    }

    internal struct CurrencyExchangePanelOffsets
    {
        public long WantedItemCountInputPtr;
        public long WantedItemTypePtr;
        public long OfferedItemCountInputPtr;
        public long OfferedItemTypePtr;
        public uint Stock1TypeHash;
        public uint Stock2TypeHash;
        public global::GameOffsets.Native.StdVector Stock1;
        public global::GameOffsets.Native.StdVector Stock2;
        public short MarketRateGet;
        public short MarketRateGive;
        public long RatioElementPtr;
        public long CurrencyPickerPtr;
        public long OrderListContainerPtr;
        public global::GameOffsets.Native.StdVector OrderList;
    }

    internal struct CurrencyExchangeCurrencyPickerElementOffsets
    {
        public byte PickedCurrencyType;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct CursorOffsets
    {
        public int vTable;
        public int Clicks;
        public long ItemTypePtr;
        public long ItemTypePtrGuard;
        public global::GameOffsets.Native.NativeUtf16Text ActionString;
        public byte Action;
    }

    public struct DatArrayStruct
    {
        public int Count;
        public long ItemArrayPtr;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DiagnosticElementArrayOffsets
    {
        public float CurrValue;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DiagnosticElementOffsets
    {
        public long DiagnosticArray;
        public int X;
        public int Y;
        public int Width;
        public int Height;
    }

    public enum ElementFlags : ulong
    {
        IsScrollable = 0,
        IsVisibleLocal = 0,
        IsActive = 0,
        IsSaturated = 0,
        Flag0 = 0,
        Flag1 = 0,
        Flag2 = 0,
        Flag3 = 0,
        Flag4 = 0,
        Flag5 = 0,
        Flag6 = 0,
        Flag7 = 0,
        Flag8 = 0,
        Flag9 = 0,
        Flag12 = 0,
        Flag14 = 0,
        Flag15 = 0,
        Flag16 = 0,
        Flag17 = 0,
        Flag18 = 0,
        Flag19 = 0,
        Flag20 = 0,
        Flag21 = 0,
        Flag22 = 0,
        Flag23 = 0,
        Flag24 = 0,
        Flag25 = 0,
        Flag26 = 0,
        Flag27 = 0,
        Flag28 = 0,
        Flag29 = 0,
        Flag30 = 0,
        Flag31 = 0,
        Flag32 = 0,
        Flag33 = 0,
        Flag34 = 0,
        Flag35 = 0,
        Flag36 = 0,
        Flag37 = 0,
        Flag38 = 0,
        Flag39 = 0,
        Flag40 = 0,
        Flag41 = 0,
        Flag42 = 0,
        Flag43 = 0,
        Flag44 = 0,
        Flag46 = 0,
        Flag47 = 0,
        Flag48 = 0,
        Flag49 = 0,
        Flag50 = 0,
        Flag51 = 0,
        Flag52 = 0,
        Flag53 = 0,
        Flag54 = 0,
        Flag55 = 0,
        Flag56 = 0,
        Flag57 = 0,
        Flag58 = 0,
        Flag59 = 0,
        Flag60 = 0,
        Flag61 = 0,
        Flag62 = 0,
        Flag63 = 0,
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ElementOffsets
    {
        public int OffsetBuffers;
        public int ElementColorsOffset;
        public int CursorInfoOffset2;
        public long SelfPointer;
        public long ChildStart;
        public global::GameOffsets.Native.NativePtrArray Childs;
        public long ChildEnd;
        public global::System.Numerics.Vector2 ScrollOffset;
        public global::System.Numerics.Vector2 Position;
        public long CursorInfo;
        public long Root;
        public byte LabelTextSize;
        public float Scale;
        public ushort Type;
        public long Parent;
        public global::GameOffsets.ElementFlags Flags;
        public long Tooltip;
        public global::System.Numerics.Vector2 Size;
        public global::SharpDX.ColorBGRA LabelBackgroundColor;
        public global::SharpDX.ColorBGRA LabelTextColor;
        public global::SharpDX.ColorBGRA LabelBorderColor;
        public byte ShinyHighlightState;
        public global::GameOffsets.Native.NativeUtf16Text Text;
        public long TextureNamePtr;
        public global::GameOffsets.Native.NativeUtf16Text TextNoTags;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct EntityLabelMapOffsets
    {
        public long EntityLabelMap;
        public int LabelPtrOffset;
        public int LabelOffset;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct EntityListOffsets
    {
        public long FirstAddr;
        public long SecondAddr;
        public byte IsEmpty;
        public long Entity;
    }

    public enum EntityFlags : byte
    {
        Valid = 0,
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct EntityOffsets
    {
        [FieldOffset(0)]
        public global::GameOffsets.ObjectHeaderOffsets Head;
        [FieldOffset(24)]
        public long EntityDetailsPtr;
        [FieldOffset(32)]
        public global::GameOffsets.Native.StdVector ComponentList;
        [FieldOffset(56)]
        public uint Id;
        [FieldOffset(60)]
        public global::GameOffsets.EntityFlags Flags;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct EnvironmentDataOffsets
    {
        public int Type1Count;
        public int Type1Size;
        public int Type1Offset;
        public int Type2Count;
        public int Type2Size;
        public int Type2Offset;
        public int Type3Count;
        public int Type3Size;
        public int Type3Offset;
        public int Type4Count;
        public int Type4Size;
        public int Type4Offset;
        public int Type5Count;
        public int Type5Size;
        public int Type5Offset;
        public int Type6Count;
        public int Type6Size;
        public int Type6Offset;
        public int Type7Count;
        public int Type7Size;
        public int Type7Offset;
        public int Type8Count;
        public int Type8Size;
        public int Type8Offset;
        public int Type9Count;
        public int Type9Size;
        public int Type9Offset;
        public int Type10Count;
        public int Type10Size;
        public int Type10Offset;
        public global::GameOffsets.Native.StdVector DefaultSettingsList;
        public global::GameOffsets.Native.StdVector ActiveEnvironmentList;
        public global::GameOffsets.Native.StdVector FootstepAudioList;
        public int FirstInlineValueListOffset;
        public int SecondInlineValueListOffset;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DefaultEnvironmentSettingsOffsets
    {
        public global::GameOffsets.Native.NativeUtf16Text Category;
        public global::GameOffsets.Native.NativeUtf16Text Name;
        public int IndexInGroup;
        public global::System.Numerics.Vector3 Value;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Type1EnvironmentSettingsOffsets
    {
        public float Value;
        public byte Override;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Type2EnvironmentSettingsOffsets
    {
        public float Value;
        public byte Override;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Type3EnvironmentSettingsOffsets
    {
        public global::System.Numerics.Vector3 Value;
        public byte Override;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Type4EnvironmentSettingsOffsets
    {
        public byte Value;
        public byte Override;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Type5EnvironmentSettingsOffsets
    {
        public byte Value;
        public byte Override;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Type6EnvironmentSettingsOffsets
    {
        public global::GameOffsets.Native.NativeUtf16Text Name;
        public global::GameOffsets.Native.NativeUtf16Text Category;
        public float Value1;
        public float Value2;
        public byte Override;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Type7PlusEnvironmentSettingsOffsets
    {
        public global::System.Numerics.Vector4 Value;
        public global::System.Numerics.Vector4 Value2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct EnvironmentOffsets
    {
        public ushort Key;
        public ushort Value0;
        public float Value1;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct EscapeStateOffsets
    {
        public byte WasEverActive;
        public long UIRootPtr;
        public long HoveredElementPtr;
        public uint TotalActiveTimeMs;
        public byte IsUnpaused;
    }

    internal struct ExpeditionDetonatorInfoOffsets
    {
        public int ExpeditionDetonatorInfoOffset;
        public long PlacementMarkerPtr;
        public global::GameOffsets.Native.StdVector PlacedExplosives;
        public global::GameOffsets.Native.Vector2i DetonatorGridPosition;
        public global::GameOffsets.Native.Vector2i PlacementIndicatorGridPosition;
        public byte TotalExplosiveCount;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct FileInfo
    {
        public global::GameOffsets.Native.NativeUtf16Text Name;
        public long Records;
        public int AreaChangeCount;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct FileInfoPadded
    {
        public global::GameOffsets.FileInfo FileInfo;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct GameConfigOffsets
    {
        public global::GameOffsets.Native.StdMap ConfigMap;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct GameConfigSectionOffsets
    {
        public global::GameOffsets.Native.NativeUtf16Text SectionKey;
        public global::GameOffsets.Native.UnorderedMap SectionMap;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct GameConfigKeyValueOffsets
    {
        public global::GameOffsets.Native.NativeUtf16Text Key;
        public global::GameOffsets.Native.NativeUtf16Text Value;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct HarvestWorldObjectComponentOffsets
    {
        public global::GameOffsets.Native.StdVector Seeds;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct HeistBlueprintComponentOffsets
    {
        public int WingRecordSize;
        public int JobRecordSize;
        public int RoomRecordSize;
        public int NpcRecordSize;
        public long Owner;
        public byte AreaLevel;
        public byte IsConfirmed;
        public global::GameOffsets.Native.NativePtrArray Wings;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct HeistContractComponentOffsets
    {
        public long Owner;
        public long ObjectiveKey;
        public global::GameOffsets.Native.NativePtrArray Requirements;
        public global::GameOffsets.Native.NativePtrArray Crew;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct HeistContractObjectiveOffsets
    {
        public long TargetKey;
        public long ClientKey;
        public long Unknown1Key;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct HeistContractRequirementOffsets
    {
        public long JobKey;
        public byte JobLevel;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct HeistEquipmentComponentOffsets
    {
        public long OwnerKey;
        public long DataKey;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct HeistEquipmentComponentDataOffsets
    {
        public long HeistEquipmentKey;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct HeistEquipmentOffsets
    {
        public long BaseItemKey;
        public long RequiredJobKey;
        public int RequiredJobMinimumLevel;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct IngameDataOffsets
    {
        public long LabDataPtr;
        public long CurrentArea;
        public byte CurrentAreaLevel;
        public uint CurrentAreaHash;
        public global::GameOffsets.Native.NativePtrArray MapStats;
        public global::GameOffsets.Native.NativePtrArray MapStatsVisible;
        public long IngameStatePtr;
        public long IngameStatePtr2;
        public global::GameOffsets.Native.StdVector EffectEnvironments;
        public long ServerData;
        public long LocalPlayer;
        public long EntityList;
        public long EntitiesCount;
        public global::GameOffsets.TerrainData Terrain;
        public global::GameOffsets.Native.NativePtrArray TgtArray;
        public int MillisecondsSpentInMapBeforeZoneIn;
        public long ZoneInQPC;
        public long EnvironmentDataPtr;
        public global::GameOffsets.Native.StdVector AzmeriDataArray1;
        public global::GameOffsets.Native.StdVector AzmeriDataArray2;
        public global::GameOffsets.Native.Vector2i AzmeriZoneTileDimensions;
        public global::System.Numerics.Vector2 AzmeriZoneMinBorder;
        public global::System.Numerics.Vector2 AzmeriZoneMaxBorder;
        public byte IsInsideAzmeriZone;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct IngameStateOffsets
    {
        public int MouseDataSecondOffset;
        public long Data;
        public long MouseDataFirstPtr;
        public long Camera;
        public long EntityLabelMap;
        public long UIRoot;
        public long FocusedInputElementPtr;
        public long UIHoverElement;
        public global::System.Numerics.Vector2 CurentUIElementPos;
        public long UIHover;
        public global::GameOffsets.Native.Vector2i MouseGlobal;
        public global::System.Numerics.Vector2 UIHoverPos;
        public global::System.Numerics.Vector2 MouseInGame;
        public float TimeInGameF;
        public long IngameUi;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct IngameUIElementsOffsets
    {
        public int CHAT_TITLE_OFFSET;
        public int CHAT_INPUT_OFFSET;
        public int CHAT_BOX_OFFSET_1;
        public int CHAT_BOX_OFFSET_2;
        public long GetQuests;
        public long GameUI;
        public long Mouse;
        public long SkillBar;
        public long HiddenSkillBar;
        public long PartyElement;
        public long BanditDialog;
        public long ChatBox;
        public long MapSideUI;
        public long QuestTracker;
        public long OpenLeftPanel;
        public long OpenRightPanel;
        public long MicrotransactionShopWindow;
        public long InventoryPanel;
        public long StashElement;
        public long GuildStashElement;
        public long OfflineMerchantPanel;
        public long SocialPanel;
        public long UltimatumWorldPanel;
        public long TreePanel;
        public long AtlasPanel;
        public long AtlasSkillPanel;
        public long SettingsPanel;
        public long ChallengePanel;
        public long WorldMap;
        public long HelpWindow;
        public long Map;
        public long itemsOnGroundLabelRoot;
        public long NpcDialog;
        public long ExpeditionNpcDialog;
        public long QuestRewardWindow;
        public long PurchaseWindow;
        public long HaggleWindow;
        public long PurchaseWindowHideout;
        public long SellWindow;
        public long SellWindowHideout;
        public long TradeWindow;
        public long LabyrinthDivineFontPanel;
        public long TrialPlaquePanel;
        public long AscendancySelectPanel;
        public long MapReceptacleWindow;
        public long LabyrinthSelectPanel;
        public long LabyrinthMapPanel;
        public long CardTradeWindow;
        public long IncursionWindow;
        public long DelveWindow;
        public long ZanaMissionChoice;
        public long BetrayalWindow;
        public long CraftBenchWindow;
        public long UnveilWindow;
        public long TrappedStashWindow;
        public long AnointingWindow;
        public long HorticraftingStationWindow;
        public long HeistWindow;
        public long BlueprintWindow;
        public long AllyEquipmentWindow;
        public long GrandHeistWindow;
        public long HeistLockerElement;
        public long MetamorphWindow;
        public long HeistContractWindow;
        public long HeistRevealWindow;
        public long HeistAllyEquipmentWindow;
        public long HeistBlueprintWindow;
        public long HeistLockerWindow;
        public long RitualWindow;
        public long UltimatumPanel;
        public long ExpeditionWindow;
        public long ExpeditionWindowEmpty;
        public long ExpeditionLockerElement;
        public long SanctumFloorWindow;
        public long SanctumRewardWindow;
        public long NecropolisMonsterPanel;
        public long VillageRecruitmentPanel;
        public long VillageRewardWindow;
        public long VillageShipmentScreen;
        public long VillageWorkerManagementPanel;
        public long VillageScreen;
        public long GenesisTreeWindow;
        public long CurrencyExchangePanel;
        public long ItemRightClickPriceMenu;
        public long CurrencyShiftClickMenu;
        public long AsyncItemRightClickPriceMenu;
        public long PopUpWindow;
        public long DestroyConfirmationWindow;
        public long MercenaryEncounterWindow;
        public long AreaInstanceUi;
        public long InstanceManagerPanel;
        public long MirageWishesPanel;
        public long ResurrectPanel;
        public long LeagueMechanicButtons;
        public long ExpeditionDetonatorElement;
        public long InvitesPanel;
        public long GemLvlUpPanel;
        public long BlightEncounterUi;
        public long ItemOnGroundTooltip;
        public long RitualFavourWindow;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct InitObjectOffsets
    {
        public long vTable;
        public long ParentObjectPtr;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct InventoryOffsets
    {
        public int DefaultServerInventoryOffset;
        public int ComplexStashFirstLevelServerInventoryOffset;
        public int ComplexStashSecondLevelServerInventoryOffset;
        public int DivinationServerInventoryOffset;
        public int BlightServerInventoryOffset;
        public int ItemHoverState;
        public long HoverItem;
        public global::GameOffsets.Native.Vector2i FakePos;
        public global::GameOffsets.Native.Vector2i RealPos;
        public int CursorInInventory;
        public long ItemCount;
        public int ServerInventoryId;
        public global::GameOffsets.Native.Vector2i InventorySize;
        public long CursorPtr;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ItemsOnGroundLabelElementOffsets
    {
        public int SecondConfigOffset;
        public int ThirdConfigOffset;
        public int FourthConfigOffset;
        public long ConfigPtr;
        public global::GameOffsets.Native.StdVector VisibleItemLabels;
        public long LabelOnHoverPtr;
        public long ItemOnHoverPtr;
        public long LabelsOnGroundListPtr;
        public long LabelCount;
        public long LabelCount2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct VisibleItemLabelOffsets
    {
        public long ElementPtr;
        public global::System.Numerics.Vector2 PositionOffset;
        public uint EntityId;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct VisibleItemLabelGroupOffsets
    {
        public global::GameOffsets.Native.StdVector Labels;
        public global::System.Numerics.Vector2 GroupPosition;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct LifeComponentOffsets
    {
        public long Owner;
        public global::GameOffsets.VitalStruct Mana;
        public global::GameOffsets.VitalStruct EnergyShield;
        public global::GameOffsets.VitalStruct Health;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct VitalStruct
    {
        public float Regen;
        public int Max;
        public int Current;
        public int ReservedFlat;
        public int ReservedFraction;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct LocalStatsComponentOffsets
    {
        public global::GameOffsets.Components.ComponentHeader Header;
        public global::GameOffsets.Native.StdVector StatsPtr;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct LoginStateOffsets
    {
        public long UIRootPtr;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MapComponentBase
    {
        public long Base;
        public byte Tier;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MapComponentInner
    {
        public long Area;
        public int MapSeries;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct MapDeviceWindowOffsets
    {
        public int IsSelectedOffset;
        public int OptionOffset;
        public int IsCraftingOptionActiveOffset;
        public int CraftingOptionIndexOffset;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MapElement
    {
        public int LargeMapOffset;
        public int SmallMapOffset;
        public int MapPropertiesOffset;
        public int OrangeWordsOffset;
        public int BlueWordsOffset;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MapSubElement
    {
        public int MapShift;
        public int MapShiftX;
        public int MapShiftY;
        public int DefaultMapShift;
        public int MapZoom;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MinimapIconOffsets
    {
        public long NamePtr;
        public byte IsVisible;
        public byte IsHide;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ModsRecordOffsets
    {
        public long KeyPtr;
        public short Hash;
        public long TypePtr;
        public int MinLevel;
        public long Stat1Ptr;
        public long Stat2Ptr;
        public long Stat3Ptr;
        public long Stat4Ptr;
        public int Domain;
        public long UserFriendlyName;
        public int AffixType;
        public global::System.ValueTuple<long, long> Group;
        public long Something;
        public global::GameOffsets.Native.Vector2i Stat1Range;
        public global::GameOffsets.Native.Vector2i Stat2Range;
        public global::GameOffsets.Native.Vector2i Stat3Range;
        public global::GameOffsets.Native.Vector2i Stat4Range;
        public long Tags;
        public long ta;
        public int TagChances;
        public long tc;
        public long BuffDefinitionsPtr;
        public long BuffDefinitions;
        public int BuffValue;
        public long tgcCount;
        public long tgcPtr;
        public global::GameOffsets.Native.Vector2i Stat5Range;
        public long Stat5Ptr;
        public global::GameOffsets.Native.Vector2i Stat6Range;
        public long Stat6Ptr;
        public byte IsEssence;
        public global::GameOffsets.Native.Vector2i Stat7Range;
        public long Tier;
        public long Stat7Ptr;
        public long Stat8Ptr;
        public global::GameOffsets.Native.Vector2i Stat8Range;
        public int InfluenceTypes;
        public uint Hash32;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ModsComponentStatsOffsets
    {
        public global::GameOffsets.Native.StdVector ImplicitStatsArray;
        public global::GameOffsets.Native.StdVector EnchantedStatsArray;
        public global::GameOffsets.Native.StdVector CrucibleStatsArray;
        public global::GameOffsets.Native.StdVector ExplicitStatsArray;
        public global::GameOffsets.Native.StdVector CraftedStatsArray;
        public global::GameOffsets.Native.StdVector FracturedStatsArray;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ModsComponentOffsets
    {
        public int HumanStats;
        public int ItemModRecordSize;
        public int NameRecordSize;
        public int NameOffset;
        public int StatRecordSize;
        public global::GameOffsets.Native.NativePtrArray UniqueName;
        public bool Identified;
        public byte FracturedModsCount;
        public int ItemRarity;
        public global::GameOffsets.Native.NativePtrArray implicitMods;
        public global::GameOffsets.Native.NativePtrArray explicitMods;
        public global::GameOffsets.Native.NativePtrArray enchantMods;
        public global::GameOffsets.Native.NativePtrArray ScourgeMods;
        public global::GameOffsets.Native.NativePtrArray crucibleMods;
        public long AlternativeQualityTypePtr;
        public int AlternateQualityValue;
        public long ModsComponentStatsPtr;
        public global::GameOffsets.Native.NativePtrArray GetSynthesizedStats;
        public int ItemLevel;
        public int RequiredLevel;
        public long IncubatorPtr;
        public ushort IncubatorKills;
        public byte IsMirrored;
        public byte MemoryStrands;
        public byte IsSplit;
        public byte IsUsable;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct NormalInventoryItemOffsets
    {
        public long Item;
        public int Width;
        public int Height;
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct ObjectHeaderOffsets
    {
        [FieldOffset(0)]
        public long MainObject;
        [FieldOffset(8)]
        public long Name;
        [FieldOffset(16)]
        public long ComponentLookUpPtr;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ObjectMagicPropertiesOffsets
    {
        public int Rarity;
        public global::GameOffsets.Native.NativePtrArray Mods;
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct PathEntityOffsets
    {
        [FieldOffset(0)]
        public global::StringPtr Path;
        [FieldOffset(8)]
        public long Length;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PathfindingComponentOffsets
    {
        public int PathNodeStart;
        public int DestinationNodes;
        public global::GameOffsets.Native.Vector2i WantMoveToPosition;
        public float StayTime;
    }

    public struct PlayerComponentOffsets
    {
        public global::GameOffsets.Native.NativeUtf16Text PlayerName;
        public uint Xp;
        public global::GameOffsets.Buffer3<int> Attributes;
        public byte Level;
        public byte PantheonMinor;
        public byte PantheonMajor;
        public long HideoutPtr;
        public global::GameOffsets.Native.StdVector Flags;
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct PositionedComponentOffsets
    {
        [FieldOffset(0)]
        public long OwnerAddress;
        [FieldOffset(8)]
        public byte Reaction;
        [FieldOffset(9)]
        public int Size;
        [FieldOffset(13)]
        public global::GameOffsets.Native.Vector2i RawVelocity;
        [FieldOffset(21)]
        public float SpeedReverseFactor;
        [FieldOffset(25)]
        public global::System.Numerics.Vector2 PrevPosition;
        [FieldOffset(33)]
        public global::System.Numerics.Vector2 TravelStart;
        [FieldOffset(41)]
        public global::System.Numerics.Vector2 TravelOffset;
        [FieldOffset(49)]
        public float TravelProgress;
        [FieldOffset(53)]
        public global::GameOffsets.Native.Vector2i GridPosition;
        [FieldOffset(61)]
        public float Rotation;
        [FieldOffset(65)]
        public float Scale;
        [FieldOffset(69)]
        public global::System.Numerics.Vector2 WorldPosition;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PurchaseWindowOffsets
    {
        public long StashTabContainerPtr;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct QuestStateOffsets
    {
        public long QuestAddress;
        public long Base;
        public byte QuestStateId;
        public long QuestStateTextAddress;
        public long QuestProgressTextAddress;
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct RenderComponentOffsets
    {
        [FieldOffset(0)]
        public global::System.Numerics.Vector3 Pos;
        [FieldOffset(12)]
        public global::System.Numerics.Vector3 Bounds;
        [FieldOffset(24)]
        public global::GameOffsets.Native.NativeUtf16Text Name;
        [FieldOffset(56)]
        public global::System.Numerics.Vector3 Rotation;
        [FieldOffset(68)]
        public float Height;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SanctumRewardWindowOffsets
    {
        public long RewardArrayContainer;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SanctumFloorWindowOffsets
    {
        public long InSanctumDataPtr;
        public long OutOfSanctumDataPtr;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SanctumFloorWindowDataOffsets
    {
        public long OutOfSanctumFloorDataOffset;
        public long InSanctumFloorDataType1Offset;
        public long InSanctumFloorDataType2Offset;
        public long InspirationOffset;
        public long MaxResolveOffset;
        public long CurrentResolveOffset;
        public long GoldOffset;
        public long RoomChoiceHistoryOffset;
        public long RewardArrayOffset;
        public int Gold;
        public int CurrentResolve;
        public int MaxResolve;
        public int Inspiration;
        public bool Flag1;
        public bool Flag2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SanctumRoomOffsets
    {
        public int SanctumRoomDataInElementOffset;
        public int FightRoomOffset;
        public int RewardRoomOffset;
        public int RoomEffectOffset;
        public int RewardSize;
        public int Reward1Offset;
        public int Reward2Offset;
        public int Reward3Offset;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ServerDataMinimapIconOffsets
    {
        public global::GameOffsets.Native.Vector2i GridPosition;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SkillBarIdsStruct
    {
        public ushort SkillBar1;
        public ushort SkillBar2;
        public ushort SkillBar3;
        public ushort SkillBar4;
        public ushort SkillBar5;
        public ushort SkillBar6;
        public ushort SkillBar7;
        public ushort SkillBar8;
        public ushort SkillBar9;
        public ushort SkillBar10;
        public ushort SkillBar11;
        public ushort SkillBar12;
        public ushort SkillBar13;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ServerPlayerDataOffsets
    {
        public global::GameOffsets.Native.NativePtrArray PassiveSkillIds;
        public global::GameOffsets.Native.NativePtrArray PassiveJewelSocketIds;
        public byte PlayerClass;
        public int CharacterLevel;
        public int PassiveRefundPointsLeft;
        public int QuestPassiveSkillPoints;
        public int FreePassiveSkillPointsLeft;
        public int TotalAscendencyPoints;
        public int SpentAscendencyPoints;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ServerDataOffsets
    {
        public int Skip;
        public int ATLAS_REGION_UPGRADES;
        public int WaypointsUnlockStateOffset;
        public int BetrayalDataOffset2;
        public int AtlasPassivesListOffset;
        public int QuestFlagsOffset;
        public long MasterAreas;
        public global::GameOffsets.Native.StdVector AncestorFightInfoList;
        public long IngameDataPtr;
        public int InstanceId;
        public long PlayerRelatedData;
        public global::GameOffsets.Buffer3<global::System.ValueTuple<long, long>> AtlasTreeContainerPtrs;
        public byte ActiveAtlasTreeContainerIndex;
        public byte NetworkState;
        public global::GameOffsets.Native.NativeUtf16Text League;
        public int TimeInGame;
        public int TimeInGame2;
        public int Latency;
        public global::GameOffsets.Native.StdVector PlayerStashTabs;
        public global::GameOffsets.Native.StdVector GuildStashTabs;
        public global::GameOffsets.Native.NativeUtf16Text PartyLeaderName;
        public byte PartyStatusType;
        public byte PartyAllocationType;
        public bool PartyDownscaleDisabled;
        public byte EaterOfWorldsCounter;
        public byte SearingExarchCounter;
        public int Gold;
        public global::GameOffsets.Native.StdVector CurrentParty;
        public long KnownPlayers;
        public long GuildName;
        public global::GameOffsets.SkillBarIdsStruct SkillBarIds;
        public global::System.Numerics.Vector2 WorldMousePosition;
        public long QuestFlagsPtr;
        public global::GameOffsets.Native.NativePtrArray NearestPlayers;
        public global::GameOffsets.Native.StdVector MinimapIcons;
        public global::GameOffsets.Native.NativePtrArray EntityEffects;
        public global::GameOffsets.Native.NativePtrArray PlayerInventories;
        public global::GameOffsets.Native.NativePtrArray NPCInventories;
        public global::GameOffsets.Native.NativePtrArray GuildInventories;
        public ushort TradeChatChannel;
        public ushort GlobalChatChannel;
        public ushort LastActionId;
        public int CompletedMapsCount;
        public global::GameOffsets.Native.StdVector MechanicHandlers;
        public global::GameOffsets.Native.StdMapWithVector MavenWitnessedAreas;
        public global::GameOffsets.Native.StdMapWithVector CompletedAreas;
        public global::GameOffsets.Native.StdMapWithVector BonusCompletedAreas;
        public global::GameOffsets.Native.StdVector PlacedCurrencyOrders;
        public global::GameOffsets.Buffer14<global::GameOffsets.ServerDataBetrayalMember> ActiveBetrayalMembers;
        public global::GameOffsets.Buffer23<byte> BetrayalMemberRelationships;
        public int DialogDepth;
        public byte MonsterLevel;
        public byte MonstersRemaining;
        public int CurrentAzuriteAmount;
        public ushort CurrentSulphiteAmount;
        public ushort CurrentWildWisps;
        public ushort CurrentVividWisps;
        public ushort CurrentPrimalWisps;
        public global::GameOffsets.Native.NativePtrArray BlightLanes;
        public global::GameOffsets.ServerDataArtifacts Artifacts;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ServerDataArtifacts
    {
        public ushort LesserBrokenCircleArtifacts;
        public ushort GreaterBrokenCircleArtifacts;
        public ushort GrandBrokenCircleArtifacts;
        public ushort ExceptionalBrokenCircleArtifacts;
        public ushort LesserBlackScytheArtifacts;
        public ushort GreaterBlackScytheArtifacts;
        public ushort GrandBlackScytheArtifacts;
        public ushort ExceptionalBlackScytheArtifacts;
        public ushort LesserOrderArtifacts;
        public ushort GreaterOrderArtifacts;
        public ushort GrandOrderArtifacts;
        public ushort ExceptionalOrderArtifacts;
        public ushort LesserSunArtifacts;
        public ushort GreaterSunArtifacts;
        public ushort GrandSunArtifacts;
        public ushort ExceptionalSunArtifacts;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ServerDataBetrayalMember
    {
        public byte Id;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ServerInventoryOffsets
    {
        public byte InventType;
        public byte InventSlot;
        public byte IsRequested;
        public int Columns;
        public int Rows;
        public global::GameOffsets.Native.StdVector InventoryItems;
        public long InventorySlotItemsPtr;
        public long ItemCount;
        public int ServerRequestCounter;
        public long Hash;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ServerStashTabOffsets
    {
        public int StructSize;
        public global::GameOffsets.Native.NativeUtf16Text Name;
        public uint Color;
        public uint OfficerFlags;
        public uint TabType;
        public ushort DisplayIndex;
        public uint MemberFlags;
        public byte Flags;
    }

    public enum ShortcutModifier : int
    {
        None = 0,
        Shift = 0,
        Ctrl = 0,
        Alt = 0,
    }

    public enum ShortcutUsage : int
    {
        Flask1 = 0,
        Flask2 = 0,
        Flask3 = 0,
        Flask4 = 0,
        Flask5 = 0,
        TempSkill1 = 0,
        TempSkill2 = 0,
        Skill1 = 0,
        Skill2 = 0,
        Skill3 = 0,
        Skill4 = 0,
        Skill5 = 0,
        Skill6 = 0,
        Skill7 = 0,
        Skill8 = 0,
        Skill9 = 0,
        Skill10 = 0,
        Skill11 = 0,
        Skill12 = 0,
        Skill13 = 0,
        OptionsPanel = 0,
        CharacterPanel = 0,
        SocialPanel = 0,
        InventoryPanel = 0,
        SkillTree = 0,
        Atlas = 0,
        AtlasTree = 0,
        LeagueInterface = 0,
        LeaguePanel = 0,
        ToggleDebug = 0,
        ItemPickup = 0,
        StalkerSentinel = 0,
        PandemoniumSentinel = 0,
        ApexSentinel = 0,
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Shortcut
    {
        public global::System.ConsoleKey MainKey;
        public global::GameOffsets.ShortcutModifier Modifier;
        public global::GameOffsets.ShortcutUsage Usage;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SkillGemOffsets
    {
        public global::GameOffsets.InitObjectOffsets Head;
        public long AdvanceInformation;
        public uint TotalExpGained;
        public uint Level;
        public uint ExperiencePrevLevel;
        public uint ExperienceMaxLevel;
        public uint QualityType;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct GemInformation
    {
        public int SocketColor;
        public long SkillGemPtr;
        public int MaxLevel;
        public int LimitLevel;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SocketsComponentOffsets
    {
        public global::GameOffsets.SocketColorList Sockets;
        public global::GameOffsets.SocketedGemList SocketedGems;
        public global::GameOffsets.Native.StdVector LinkSizes;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SocketColorList
    {
        public int Socket1Color;
        public int Socket2Color;
        public int Socket3Color;
        public int Socket4Color;
        public int Socket5Color;
        public int Socket6Color;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SocketedGemList
    {
        public long Socket1GemPtr;
        public long Socket2GemPtr;
        public long Socket3GemPtr;
        public long Socket4GemPtr;
        public long Socket5GemPtr;
        public long Socket6GemPtr;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct StashElementOffsets
    {
        public int StashTabContainerOffset2;
        public long StashTitlePanelPtr;
        public long ExitButtonPtr;
        public long StashTabContainerPtr1;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct StashTabContainerOffsets
    {
        public long TabSwitchBarPtr;
        public long ViewAllStashesButtonPtr;
        public long PinStashTabListButtonPtr;
        public global::GameOffsets.Native.StdVector Stashes;
        public int VisibleStashIndex;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct StashTabContainerInventoryOffsets
    {
        public global::GameOffsets.Native.NativeUtf16Text Name;
        public long InventoryPtr;
        public long StashButtonPtr;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct StateMachineComponentOffsets
    {
        public long StatesPtr;
        public global::GameOffsets.Native.NativePtrArray StatesValues;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct StatsComponentOffsets
    {
        public long Owner;
        public long SubStatsPtr;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SubStatsComponentOffsets
    {
        public global::GameOffsets.Native.StdVector Stats;
    }

    public enum Offsets : int
    {
        RenderComponentOffsetsPos = 0,
        RenderComponentOffsetsBounds = 0,
        RenderComponentOffsetsName = 0,
        RenderComponentOffsetsRotation = 0,
        RenderComponentOffsetsHeight = 0,
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SubTileStructure
    {
        public global::GameOffsets.Native.StdVector SubTileHeight;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TargetableComponentOffsets
    {
        public bool isTargetable;
        public bool isHighlightable;
        public bool isTargeted;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TerrainData
    {
        public ushort NumCols;
        public ushort NumRows;
        public global::GameOffsets.Native.NativePtrArray TgtArray;
        public int NumTileIndexCols;
        public int NumTileIndexRows;
        public global::GameOffsets.Native.StdVector TileIndexes;
        public global::GameOffsets.Native.StdVector TileDescriptions;
        public global::GameOffsets.Native.NativePtrArray LayerMelee;
        public global::GameOffsets.Native.NativePtrArray LayerRanged;
        public int BytesPerRow;
        public short TileHeightMultiplier;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TgtDetailStruct
    {
        public global::GameOffsets.Native.NativeUtf16Text name;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TgtTileStruct
    {
        public global::GameOffsets.Native.NativeUtf16Text TgtPath;
        public long TgtDetailPtr;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TileStructure
    {
        public long SubTileDetailsPtr;
        public long TgtFilePtr;
        public global::GameOffsets.Native.StdVector EntitiesList;
        public short TileHeight;
        public byte RotationSelector;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TooltipItemFrameElementOffsets
    {
        public long CopyTextPtr;
        public bool IsAdvancedTooltipText;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TreePassiveElementOffsets
    {
        public long PassiveSkillPtr;
        public byte IsAllocatedForPlan;
        public byte CanAllocate;
        public byte CanDeallocate;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct UltimatumPanelOffsets
    {
        public int VotesOffset;
        public int LockedVotesOffset;
        public int IsSelectedOffset;
        public global::GameOffsets.Native.StdVector OfferedModifiers;
        public int SelectedModifierIndex;
    }

    public struct VillageInfoOffsets
    {
        public int InitialResources;
        public global::GameOffsets.Native.StdVector Workers;
        public global::GameOffsets.Native.StdVector WorkersForSale;
        public global::GameOffsets.Native.StdVector Stats;
        public int ShipInfo;
        public int PortRequest;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct WorldDataOffsets
    {
        public global::GameOffsets.CameraOffsetsInner Camera;
    }

}

namespace GameOffsets.Components
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct AreaTransitionComponentOffsets
    {
        public ushort AreaId;
        public byte TransitionType;
        public long WorldAreaInfoPtr;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct ComponentHeader
    {
        public long StaticPtr;
        public long EntityPtr;
    }

}

namespace GameOffsets.Native
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MinMaxStruct
    {
        public int Min;
        public int Max;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct NativeHashNode
    {
        public long Previous;
        public long Root;
        public long Next;
        public byte IsNull;
        public int Key;
        public long Value1;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct NativeListNode
    {
        public long Next;
        public long Prev;
        public long Ptr1_Unused;
        public long Ptr2_Key;
        public int Value;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct NativeListNodeComponent
    {
        public long Next;
        public long Prev;
        public long String;
        public int ComponentList;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct NativePtrArray
    {
        public long First;
        public long Last;
        public long End;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct NativeUtf8Text
    {
        public long Buffer;
        public long Reserved8Bytes;
        public int Length;
        public int LengthWithNullTerminator;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct NativeUtf16Text
    {
        public long Buffer;
        public long Reserved8Bytes;
        public long Length;
        public long LengthWithNullTerminator;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct StdListNode
    {
        public nint Next;
        public nint Previous;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct StdListNode<TValue>
    {
        public nint Next;
        public nint Previous;
        public TValue Data;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct StdList
    {
        public nint Head;
        public ulong Size;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct StdMap
    {
        public long RootNodePtr;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct StdMapWithVector
    {
        public long RootNodePtr;
        public int ItemCount;
        public global::GameOffsets.Native.StdVector ItemsByHash;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct StdMapNode
    {
        public long LeftSubNodePtr;
        public long ParentNodePtr;
        public long RightSubNodePtr;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct StdVector
    {
        public long First;
        public long Last;
        public long End;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct UnorderedMap
    {
        public long InvalidBucketPtr;
        public global::GameOffsets.Native.StdVector Buckets;
        public ulong BucketIndexMask;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct UnorderedMapBucket
    {
        public long LastNodePtr;
        public long FirstNodePtr;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct UnorderedMapNode<TKey, TValue>
    {
        public long PreviousNodePtr;
        public long NextNodePtr;
        public TKey Key;
        public TValue Value;
    }

    public struct Vector2i
    {
        public int X;
        public int Y;
    }

}

namespace GameOffsets.Objects
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct GameStateOffsets
    {
        public global::GameOffsets.Native.StdVector CurrentStatePtr;
        public long State0;
        public long State1;
        public long State2;
        public long State3;
        public long State4;
        public long State5;
        public long State6;
        public long State7;
        public long State8;
        public long State9;
        public long State10;
        public long State11;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct StateInternalStructure
    {
        public byte StateEnumToName;
        public nint StatePtr;
    }

}

