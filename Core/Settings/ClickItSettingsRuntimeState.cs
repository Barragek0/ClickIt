using ExileCore.Shared.Nodes;

namespace ClickIt
{
    public partial class ClickItSettings
    {
        private string[] _ultimatumPrioritySnapshot = [];
        private string[] _mechanicPrioritySnapshot = [];
        private string[] _mechanicIgnoreDistanceSnapshot = [];
        private KeyValuePair<string, int>[] _mechanicIgnoreDistanceWithinSnapshot = [];
        private IReadOnlyDictionary<string, int> _mechanicIgnoreDistanceWithinMapSnapshot = new Dictionary<string, int>(PriorityComparer);

        private MechanicToggleTableEntry[]? _mechanicTableEntriesCache;
        private Dictionary<string, ToggleNode>? _mechanicToggleNodeByIdCache;
        private int _itemTypeMetadataSnapshotSignature = int.MinValue;
        private string[] _itemTypeWhitelistMetadataSnapshot = [];
        private string[] _itemTypeBlacklistMetadataSnapshot = [];
        private int _strongboxMetadataSnapshotSignature = int.MinValue;
        private string[] _strongboxClickMetadataSnapshot = [];
        private string[] _strongboxDontClickMetadataSnapshot = [];
    }
}