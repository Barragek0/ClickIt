namespace ClickIt.Utils
{
    internal static class DynamicAccessProfiles
    {
        internal static readonly IDynamicMemberReaderProfile CurrentAreaHash = new DynamicMemberReaderProfile(static source => source.CurrentAreaHash);
        internal static readonly Func<dynamic, object?> CurrentAreaHashAccessor = CurrentAreaHash.Read;

        internal static readonly IDynamicMemberReaderProfile IngameUiQuestTracker = new DynamicMemberReaderProfile(static ui => ui.QuestTracker);
        internal static readonly IDynamicMemberReaderProfile IngameUiChatPanel = new DynamicMemberReaderProfile(static ui => ui.ChatPanel);
        internal static readonly IDynamicMemberReaderProfile IngameUiMap = new DynamicMemberReaderProfile(static ui => ui.Map);
        internal static readonly IDynamicMemberReaderProfile IngameUiGameUi = new DynamicMemberReaderProfile(static ui => ui.GameUI);
        internal static readonly IDynamicMemberReaderProfile IngameUiRoot = new DynamicMemberReaderProfile(static ui => ui.Root);

        internal static readonly Func<dynamic, object?> IngameUiQuestTrackerAccessor = IngameUiQuestTracker.Read;
        internal static readonly Func<dynamic, object?> IngameUiChatPanelAccessor = IngameUiChatPanel.Read;
        internal static readonly Func<dynamic, object?> IngameUiMapAccessor = IngameUiMap.Read;
        internal static readonly Func<dynamic, object?> IngameUiGameUiAccessor = IngameUiGameUi.Read;
        internal static readonly Func<dynamic, object?> IngameUiRootAccessor = IngameUiRoot.Read;

        internal static readonly IDynamicMemberReaderProfile ServerData = new DynamicMemberReaderProfile(static source => source.ServerData);
        internal static readonly IDynamicMemberReaderProfile PlayerInventories = new DynamicMemberReaderProfile(static source => source.PlayerInventories);
        internal static readonly IDynamicMemberReaderProfile Inventory = new DynamicMemberReaderProfile(static source => source.Inventory);
        internal static readonly IDynamicMemberReaderProfile InventorySlotItems = new DynamicMemberReaderProfile(static source => source.InventorySlotItems);

        internal static readonly Func<dynamic, object?> ServerDataAccessor = ServerData.Read;
        internal static readonly Func<dynamic, object?> PlayerInventoriesAccessor = PlayerInventories.Read;
        internal static readonly Func<dynamic, object?> InventoryAccessor = Inventory.Read;
        internal static readonly Func<dynamic, object?> InventorySlotItemsAccessor = InventorySlotItems.Read;
    }
}