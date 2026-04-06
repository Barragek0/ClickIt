namespace ClickIt.Features.Area
{
    internal static class AreaUiSnapshotReader
    {
        internal static object? TryGetIngameUiProperty(GameController? gameController, string propertyName)
            => TryResolveIngameUiProperty(gameController?.IngameState?.IngameUi, propertyName);

        internal static object? TryResolveIngameUiProperty(object? ingameUi, string propertyName)
        {
            if (ingameUi == null || string.IsNullOrWhiteSpace(propertyName))
                return null;

            return propertyName switch
            {
                "QuestTracker" => TryGetIngameUiMember(ingameUi, DynamicAccessProfiles.IngameUiQuestTracker),
                "ChatPanel" => TryGetIngameUiMember(ingameUi, DynamicAccessProfiles.IngameUiChatPanel),
                "Map" => TryGetIngameUiMember(ingameUi, DynamicAccessProfiles.IngameUiMap),
                "GameUI" => TryGetIngameUiMember(ingameUi, DynamicAccessProfiles.IngameUiGameUi),
                "Root" => TryGetIngameUiMember(ingameUi, DynamicAccessProfiles.IngameUiRoot),
                _ => null,
            };
        }

        internal static bool TryReadCurrentAreaHash(GameController? gameController, out long areaHash)
            => TryReadCurrentAreaHashValue(gameController?.Game, out areaHash);

        internal static bool TryReadCurrentAreaHashValue(object? game, out long areaHash)
        {
            areaHash = long.MinValue;
            if (!DynamicAccess.TryGetDynamicValue(game, DynamicAccessProfiles.CurrentAreaHash, out object? rawAreaHash)
                || rawAreaHash == null)
            {
                return false;
            }

            return TryConvertAreaHash(rawAreaHash, out areaHash);
        }

        internal static bool TryConvertAreaHash(object? rawAreaHash, out long areaHash)
        {
            areaHash = long.MinValue;
            if (rawAreaHash == null)
                return false;

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