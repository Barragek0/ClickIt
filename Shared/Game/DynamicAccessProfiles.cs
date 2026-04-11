namespace ClickIt.Shared.Game
{
    internal static class DynamicAccessProfiles
    {
        internal static readonly IDynamicMemberReaderProfile CurrentAreaHash = new DynamicMemberReaderProfile(static source => source.CurrentAreaHash);
        internal static readonly IDynamicMemberReaderProfile Address = new DynamicMemberReaderProfile(static source => source.Address);
        internal static readonly IDynamicMemberReaderProfile Camera = new DynamicMemberReaderProfile(static source => source.Camera);
        internal static readonly IDynamicMemberReaderProfile ClientRect = new DynamicMemberReaderProfile(static source => source.GetClientRect());
        internal static readonly IDynamicMemberReaderProfile Data = new DynamicMemberReaderProfile(static source => source.Data);
        internal static readonly IDynamicMemberReaderProfile DistancePlayer = new DynamicMemberReaderProfile(static source => source.DistancePlayer);
        internal static readonly IDynamicMemberReaderProfile Entity = new DynamicMemberReaderProfile(static source => source.Entity);
        internal static readonly IDynamicMemberReaderProfile FirstChild = new DynamicMemberReaderProfile(static source => source.GetChildAtIndex(0));
        internal static readonly IDynamicMemberReaderProfile Game = new DynamicMemberReaderProfile(static source => source.Game);
        internal static readonly IDynamicMemberReaderProfile GridPosNum = new DynamicMemberReaderProfile(static source => source.GridPosNum);
        internal static readonly IDynamicMemberReaderProfile Info = new DynamicMemberReaderProfile(static source => source.Info);
        internal static readonly IDynamicMemberReaderProfile IngameState = new DynamicMemberReaderProfile(static source => source.IngameState);
        internal static readonly IDynamicMemberReaderProfile IsEscapeState = new DynamicMemberReaderProfile(static source => source.IsEscapeState);
        internal static readonly IDynamicMemberReaderProfile IsHidden = new DynamicMemberReaderProfile(static source => source.IsHidden);
        internal static readonly IDynamicMemberReaderProfile IsHide = new DynamicMemberReaderProfile(static source => source.IsHide);
        internal static readonly IDynamicMemberReaderProfile IsLocked = new DynamicMemberReaderProfile(static source => source.IsLocked);
        internal static readonly IDynamicMemberReaderProfile IsOpened = new DynamicMemberReaderProfile(static source => source.IsOpened);
        internal static readonly IDynamicMemberReaderProfile IsTargetable = new DynamicMemberReaderProfile(static source => source.IsTargetable);
        internal static readonly IDynamicMemberReaderProfile IsValid = new DynamicMemberReaderProfile(static source => source.IsValid);
        internal static readonly IDynamicMemberReaderProfile IsVisible = new DynamicMemberReaderProfile(static source => source.IsVisible);
        internal static readonly IDynamicMemberReaderProfile ItemOnGround = new DynamicMemberReaderProfile(static source => source.ItemOnGround);
        internal static readonly IDynamicMemberReaderProfile ItemCellsSizeX = new DynamicMemberReaderProfile(static source => source.ItemCellsSizeX);
        internal static readonly IDynamicMemberReaderProfile ItemCellsSizeY = new DynamicMemberReaderProfile(static source => source.ItemCellsSizeY);
        internal static readonly IDynamicMemberReaderProfile ItemEntity = new DynamicMemberReaderProfile(static source => source.ItemEntity);
        internal static readonly IDynamicMemberReaderProfile Label = new DynamicMemberReaderProfile(static source => source.Label);
        internal static readonly IDynamicMemberReaderProfile Metadata = new DynamicMemberReaderProfile(static source => source.Metadata);
        internal static readonly IDynamicMemberReaderProfile OpenOnDamage = new DynamicMemberReaderProfile(static source => source.OpenOnDamage);
        internal static readonly IDynamicMemberReaderProfile Path = new DynamicMemberReaderProfile(static source => source.Path);
        internal static readonly IDynamicMemberReaderProfile PosNum = new DynamicMemberReaderProfile(static source => source.PosNum);
        internal static readonly IDynamicMemberReaderProfile Rarity = new DynamicMemberReaderProfile(static source => source.Rarity);
        internal static readonly IDynamicMemberReaderProfile RenderName = new DynamicMemberReaderProfile(static source => source.RenderName);
        internal static readonly IDynamicMemberReaderProfile Type = new DynamicMemberReaderProfile(static source => source.Type);
        internal static readonly IDynamicMemberReaderProfile WindowIsForeground = new DynamicMemberReaderProfile(static source => source.IsForeground());
        internal static readonly IDynamicMemberReaderProfile X = new DynamicMemberReaderProfile(static source => source.X);
        internal static readonly IDynamicMemberReaderProfile Y = new DynamicMemberReaderProfile(static source => source.Y);

        internal static readonly Func<dynamic, object?> CurrentAreaHashAccessor = CurrentAreaHash.Read;

        internal static readonly IDynamicMemberReaderProfile GameControllerIngameState = new DynamicMemberReaderProfile(static controller => controller.IngameState);
        internal static readonly IDynamicMemberReaderProfile IngameStateIngameUi = new DynamicMemberReaderProfile(static state => state.IngameUi);
        internal static readonly IDynamicMemberReaderProfile IngameUiUltimatumPanel = new DynamicMemberReaderProfile(static ui => ui.UltimatumPanel);

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
        internal static readonly Func<dynamic, object?> GameControllerIngameStateAccessor = GameControllerIngameState.Read;
        internal static readonly Func<dynamic, object?> IngameStateIngameUiAccessor = IngameStateIngameUi.Read;
        internal static readonly Func<dynamic, object?> IngameUiUltimatumPanelAccessor = IngameUiUltimatumPanel.Read;

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