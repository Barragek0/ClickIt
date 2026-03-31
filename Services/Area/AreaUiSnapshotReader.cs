using ClickIt.Utils;
using ExileCore;

namespace ClickIt.Services.Area
{
    internal static class AreaUiSnapshotReader
    {
        internal static object? TryGetIngameUiProperty(GameController? gameController, string propertyName)
        {
            if (gameController?.IngameState?.IngameUi == null || string.IsNullOrWhiteSpace(propertyName))
                return null;

            return propertyName switch
            {
                "QuestTracker" => TryGetIngameUiMember(gameController.IngameState.IngameUi, DynamicAccessProfiles.IngameUiQuestTracker),
                "ChatPanel" => TryGetIngameUiMember(gameController.IngameState.IngameUi, DynamicAccessProfiles.IngameUiChatPanel),
                "Map" => TryGetIngameUiMember(gameController.IngameState.IngameUi, DynamicAccessProfiles.IngameUiMap),
                "GameUI" => TryGetIngameUiMember(gameController.IngameState.IngameUi, DynamicAccessProfiles.IngameUiGameUi),
                "Root" => TryGetIngameUiMember(gameController.IngameState.IngameUi, DynamicAccessProfiles.IngameUiRoot),
                _ => null,
            };
        }

        internal static bool TryReadCurrentAreaHash(GameController? gameController, out long areaHash)
        {
            areaHash = long.MinValue;
            object? game = gameController?.Game;
            if (!DynamicAccess.TryGetDynamicValue(game, DynamicAccessProfiles.CurrentAreaHash, out object? rawAreaHash)
                || rawAreaHash == null)
            {
                return false;
            }

            try
            {
                areaHash = Convert.ToInt64(rawAreaHash);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static object? TryGetIngameUiMember(object? ingameUi, IDynamicMemberReaderProfile profile)
        {
            return DynamicAccess.TryGetDynamicValue(ingameUi, profile, out object? value)
                ? value
                : null;
        }
    }
}