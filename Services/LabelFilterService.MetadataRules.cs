using ClickIt.Definitions;
using ExileCore;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;

namespace ClickIt.Services
{
    public partial class LabelFilterService
    {
        private static bool ShouldAllowWorldItemByMetadata(ClickSettings settings, Entity item, GameController? gameController, LabelOnGround? label)
        {
            string metadata = GetWorldItemMetadataPath(item);
            string itemName = GetWorldItemBaseName(item);
            string labelText = GetWorldItemLabelText(label);

            IReadOnlyList<string> whitelist = settings.ItemTypeWhitelistMetadata ?? [];
            IReadOnlyList<string> blacklist = settings.ItemTypeBlacklistMetadata ?? [];

            bool whitelistPass = whitelist.Count == 0 || ContainsAnyMetadataIdentifier(metadata, itemName, item, labelText, whitelist);
            if (!whitelistPass)
                return false;

            bool blacklistMatch = blacklist.Count > 0 && ContainsAnyMetadataIdentifier(metadata, itemName, item, labelText, blacklist);
            if (blacklistMatch)
                return false;

            return ShouldAllowWorldItemWhenInventoryFull(item, gameController);
        }

        private static string GetWorldItemLabelText(LabelOnGround? label)
        {
            try
            {
                return label?.Label?.GetText(512) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
